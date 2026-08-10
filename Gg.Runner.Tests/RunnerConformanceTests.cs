using System.Net;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner half of the cross-repo conformance check.
/// </summary>
/// <remarks>
/// The control plane asserts the mirror image against the same declaration.
/// These paths carry real ids, so they are matched against the declared
/// templates through <see cref="ProtocolSurface.Find"/> - the matching rule
/// lives in the contract rather than being written once per repo and drifting.
/// </remarks>
public class RunnerConformanceTests
{
    private static async Task<StubRunnerSurface> ExerciseAsync(
        Func<RunnerProtocolClient, Task> use, Action<StubRunnerSurface>? configure = null)
    {
        var stub = new StubRunnerSurface();
        configure?.Invoke(stub);
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        await use(new RunnerProtocolClient(http, "runner-token"));
        return stub;
    }

    private static Task<StubRunnerSurface> ExerciseAllAsync() =>
        ExerciseAsync(async client =>
        {
            await client.HeartbeatAsync("runner-1", ["linux"]);
            await client.ClaimAsync("runner-1", ["linux"], RunnerLoop.ClaimWaitSeconds);
            await client.RenewAsync("lease-9", 3);
            await client.ReleaseAsync("lease-9", 3, RunnerDisposition.Completed);
        });

    [Test]
    public async Task Every_request_the_runner_makes_is_a_declared_endpoint()
    {
        await using var stub = await ExerciseAllAsync();

        await Assert.That(stub.Observed).IsNotEmpty()
            .Because("with no observed traffic this would pass without checking anything.");

        var undeclared = stub.Observed
            .Where(o => ProtocolSurface.Find(o.Method, o.Path) is null)
            .Select(o => $"{o.Method} {o.Path}")
            .ToList();

        await Assert.That(undeclared).IsEmpty()
            .Because($"the control plane serves only what is declared. Found: {string.Join(", ", undeclared)}");
    }

    [Test]
    public async Task Every_request_is_on_the_runner_surface_and_carries_the_runner_credential()
    {
        await using var stub = await ExerciseAllAsync();

        for (var i = 0; i < stub.Observed.Count; i++)
        {
            var declared = ProtocolSurface.Find(stub.Observed[i].Method, stub.Observed[i].Path)!;

            await Assert.That(declared.Audience).IsEqualTo(Audience.Runner);
            await Assert.That(stub.Headers[i].ContainsKey(ProtocolSurface.RunnerHeader)).IsTrue();
            await Assert.That(stub.Headers[i].ContainsKey(ProtocolSurface.SessionHeader)).IsFalse()
                .Because("a runner has no session and must never present one.");
        }
    }

    [Test]
    public async Task Every_request_carries_all_three_version_headers()
    {
        await using var stub = await ExerciseAllAsync();

        foreach (var required in ProtocolSurface.VersionHeaders)
        {
            await Assert.That(stub.Headers.Count(h => !h.ContainsKey(required))).IsEqualTo(0)
                .Because($"{required} is declared on every governed request.");
        }
    }

    [Test]
    public async Task The_runner_serializes_to_the_declared_member_names()
    {
        // Declared, never derived. Two serializers agreeing with themselves is
        // exactly how a casing split survives two green suites.
        var runnerTypes = ProtocolSurface.JsonMembers
            .Where(pair => RunnerJsonContext.Default.GetTypeInfo(pair.Key) is not null)
            .ToList();

        await Assert.That(runnerTypes.Count).IsGreaterThanOrEqualTo(8)
            .Because("resolving none would make the comparison below vacuous.");

        foreach (var (type, expected) in runnerTypes)
        {
            var actual = RunnerJsonContext.Default.GetTypeInfo(type)!.Properties
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            await Assert.That(actual).IsEquivalentTo(expected.OrderBy(n => n, StringComparer.Ordinal).ToList())
                .Because($"{type.Name} must serialize to the declared members. Got: {string.Join(", ", actual)}");
        }
    }

    [Test]
    public async Task Nothing_to_claim_is_204_and_is_not_an_error()
    {
        // The normal answer for an idle fleet. A client that treated it as a
        // failure would make an idle runner look broken in every dashboard.
        await using var stub = await ExerciseAsync(
            async client =>
            {
                var result = await client.ClaimAsync("runner-1", [], 30);
                await Assert.That(result).IsTypeOf<ClaimResult.Nothing>();
            },
            s => s.ClaimStatus = HttpStatusCode.NoContent);

        await Assert.That(ProtocolSurface.Find("POST", "/v1/leases:claim")!.Statuses).Contains(204);
    }

    [Test]
    public async Task A_409_on_renew_is_read_as_the_fence_and_not_as_a_transport_failure()
    {
        await using var stub = await ExerciseAsync(
            async client =>
            {
                var result = await client.RenewAsync("lease-9", 1);
                await Assert.That(result).IsTypeOf<RenewResult.Fenced>();
            },
            s => s.RenewStatus = HttpStatusCode.Conflict);

        await Assert.That(ProtocolSurface.Find("POST", "/v1/leases/lease-9/renew")!.Statuses).Contains(409);
    }

    [Test]
    public async Task A_409_on_release_is_read_as_the_fence_too()
    {
        await using var stub = await ExerciseAsync(
            async client =>
            {
                var result = await client.ReleaseAsync("lease-9", 1, RunnerDisposition.Completed);
                await Assert.That(result).IsTypeOf<ReleaseResult.Fenced>();
            },
            s => s.ReleaseStatus = HttpStatusCode.Conflict);

        await Assert.That(ProtocolSurface.Find("POST", "/v1/leases/lease-9/release")!.Statuses).Contains(409);
    }

    [Test]
    public async Task The_template_matcher_does_not_match_a_missing_id()
    {
        // /v1/leases//renew is a client that forgot to substitute, not a
        // request for lease "". Matching it would hide that bug behind a 404.
        await Assert.That(ProtocolSurface.Find("POST", "/v1/leases//renew")).IsNull();
        await Assert.That(ProtocolSurface.Find("POST", "/v1/leases/abc/renew")).IsNotNull();
        await Assert.That(ProtocolSurface.Find("POST", "/v1/leases/abc/extra/renew")).IsNull();
        await Assert.That(ProtocolSurface.Find("GET", "/v1/leases/abc/renew")).IsNull()
            .Because("the method is part of the identity of an endpoint.");
    }

    [Test]
    public async Task The_runner_assembly_cannot_reach_the_developer_client()
    {
        // Structural, not a convention: if Gg.Runner could see Gg.Client it
        // could hold a session, and "the runner never holds a developer
        // credential" would be a promise rather than a fact.
        var referenced = typeof(RunnerProtocolClient).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        await Assert.That(referenced).DoesNotContain("Gg.Client");
        await Assert.That(referenced).Contains("Gg.Contracts")
            .Because("without this the assertion above would pass for an assembly referencing nothing.");
    }
}
