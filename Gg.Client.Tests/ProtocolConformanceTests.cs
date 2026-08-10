using System.Net;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// gg's half of the conformance check: everything this client does must be
/// declared in <see cref="ProtocolSurface"/>.
/// </summary>
/// <remarks>
/// <para>
/// The control plane's tests assert the mirror image against the same
/// declaration. Neither repository can reference the other, so this is how the
/// two are held together: not by an end-to-end test, which is impossible
/// across the split, but by both sides conforming to one artifact that ships
/// in the package.
/// </para>
/// <para>
/// What this catches that the stub could not: the stub was written in this
/// repository, so it agreed with the client by construction. A client calling
/// a path the control plane does not serve, omitting a header it requires, or
/// serializing a field the server spells differently would all have passed.
/// </para>
/// </remarks>
public class ProtocolConformanceTests
{
    private static Endpoint Declared(string method, string path) =>
        ProtocolSurface.Endpoints.Single(e => e.Method == method && e.Path == path);

    /// <summary>Drives the client and reports what it actually put on the wire.</summary>
    private static async Task<(List<string> Paths, List<Dictionary<string, string>> Headers)>
        ObserveAsync(Func<ControlPlaneClient, Task> exercise)
    {
        await using var stub = new StubControlPlane();
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        await exercise(new ControlPlaneClient(http));
        return (stub.ObservedPaths, stub.ObservedHeaders);
    }

    [Test]
    public async Task Every_path_the_client_calls_is_a_declared_endpoint()
    {
        // The whole login and identity surface in one pass.
        var (paths, _) = await ObserveAsync(async client =>
        {
            var started = await client.StartDeviceAuthorizationAsync("laptop");
            await client.PollDeviceAuthorizationAsync(started.DeviceCode);
            await client.WhoAmIAsync(StubControlPlane.IssuedSessionToken);
            await client.RevokeSessionAsync(StubControlPlane.IssuedSessionToken);
        });

        var declared = ProtocolSurface.Endpoints.Select(e => e.Path).ToHashSet();
        var undeclared = paths.Where(p => !declared.Contains(p)).Distinct().ToList();

        await Assert.That(paths).IsNotEmpty()
            .Because("with no observed traffic this test would pass without checking anything.");
        await Assert.That(undeclared).IsEmpty()
            .Because($"the control plane serves only what is declared. Found: {string.Join(", ", undeclared)}");
    }

    [Test]
    public async Task Every_request_carries_all_three_version_headers()
    {
        var (_, headers) = await ObserveAsync(async client =>
        {
            var started = await client.StartDeviceAuthorizationAsync("laptop");
            await client.PollDeviceAuthorizationAsync(started.DeviceCode);
            await client.WhoAmIAsync(StubControlPlane.IssuedSessionToken);
            await client.RevokeSessionAsync(StubControlPlane.IssuedSessionToken);
        });

        foreach (var required in ProtocolSurface.VersionHeaders)
        {
            var missing = headers.Count(h => !h.ContainsKey(required));
            await Assert.That(missing).IsEqualTo(0)
                .Because($"{required} is declared on every governed request; {missing} lacked it.");
        }
    }

    [Test]
    public async Task Authenticated_calls_send_the_declared_credential_header()
    {
        var (paths, headers) = await ObserveAsync(async client =>
        {
            await client.WhoAmIAsync(StubControlPlane.IssuedSessionToken);
            await client.RevokeSessionAsync(StubControlPlane.IssuedSessionToken);
        });

        for (var i = 0; i < paths.Count; i++)
        {
            foreach (var required in Declared(paths[i] == "/v1/auth/whoami" ? "GET" : "POST", paths[i]).RequiredHeaders)
            {
                await Assert.That(headers[i].ContainsKey(required)).IsTrue()
                    .Because($"{paths[i]} declares {required}.");
            }
        }
    }

    [Test]
    public async Task The_anonymous_endpoints_are_called_without_a_credential()
    {
        // The mirror of the test above, and the one that would catch a client
        // helpfully attaching a session it happens to hold: these two are the
        // control plane's entire anonymous surface, and sending a credential
        // to them would make the deadlock argument for it look wrong.
        var (paths, headers) = await ObserveAsync(async client =>
        {
            var started = await client.StartDeviceAuthorizationAsync("laptop");
            await client.PollDeviceAuthorizationAsync(started.DeviceCode);
        });

        for (var i = 0; i < paths.Count; i++)
        {
            await Assert.That(Declared("POST", paths[i]).Audience).IsEqualTo(Audience.Anonymous);
            await Assert.That(headers[i].ContainsKey(ProtocolSurface.SessionHeader)).IsFalse();
            await Assert.That(headers[i].ContainsKey(ProtocolSurface.RunnerHeader)).IsFalse();
        }
    }

    [Test]
    public async Task The_client_handles_every_status_the_poll_endpoint_may_answer()
    {
        // 200, 202 and 410 are all NORMAL outcomes of a device poll. A client
        // that treats any of them as an error turns a routine wait or a
        // declined login into a crash.
        var declared = Declared("POST", "/v1/auth/device/token").Statuses;
        await Assert.That(declared).Contains(202);
        await Assert.That(declared).Contains(410);

        await using var stub = new StubControlPlane { PendingPolls = 1 };
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var client = new ControlPlaneClient(http);

        var started = await client.StartDeviceAuthorizationAsync("laptop");

        await Assert.That(await client.PollDeviceAuthorizationAsync(started.DeviceCode))
            .IsTypeOf<DevicePollResult.Pending>();
        await Assert.That(await client.PollDeviceAuthorizationAsync(started.DeviceCode))
            .IsTypeOf<DevicePollResult.Complete>();

        stub.Declined = true;
        await Assert.That(await client.PollDeviceAuthorizationAsync(started.DeviceCode))
            .IsTypeOf<DevicePollResult.Declined>();
    }

    [Test]
    public async Task The_client_recognises_the_declared_protocol_refusal()
    {
        var withoutRefusal = ProtocolSurface.Endpoints
            .Where(e => !e.Statuses.Contains(ProtocolSurface.ProtocolTooOld))
            .Select(e => e.Path)
            .ToList();
        await Assert.That(withoutRefusal).IsEmpty()
            .Because("every governed endpoint may refuse a caller below the floor.");

        await using var stub = new StubControlPlane
        {
            ProtocolFloorMessage = "This control plane speaks 1..1.",
        };
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var client = new ControlPlaneClient(http);

        await Assert.That(async () => await client.StartDeviceAuthorizationAsync("laptop"))
            .Throws<ProtocolTooOldException>()
            .Because("426 must arrive as something actionable, not as a bare status code.");
    }

    [Test]
    public async Task The_client_serializes_every_wire_type_to_the_declared_member_names()
    {
        // The bug this exists for: gg spelling a field one way and the control
        // plane another. Both sides compare against the DECLARED names, never
        // against each other's output - two serializers agreeing with
        // themselves is exactly how that survives two green suites.
        foreach (var (type, expected) in ProtocolSurface.JsonMembers)
        {
            var info = ProtocolJsonContext.Default.GetTypeInfo(type);
            if (info is null)
            {
                continue; // Not every wire type is one this client serializes.
            }

            var actual = info.Properties.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            await Assert.That(actual).IsEquivalentTo(expected.OrderBy(n => n, StringComparer.Ordinal).ToList())
                .Because($"{type.Name} must serialize to the declared members. Got: {string.Join(", ", actual)}");
        }
    }

    [Test]
    public async Task The_client_serializes_at_least_the_types_it_sends_and_receives()
    {
        // Guards the test above: if the accessor resolved nothing, every
        // comparison would be skipped and the whole thing would pass empty.
        var resolved = ProtocolSurface.JsonMembers.Keys
            .Count(t => ProtocolJsonContext.Default.GetTypeInfo(t) is not null);

        await Assert.That(resolved).IsGreaterThanOrEqualTo(7)
            .Because("the client sends or receives almost every wire type; resolving none would make the check vacuous.");
    }
}
