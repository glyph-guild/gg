using System.Text.RegularExpressions;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine measures itself against the profile it enrolled under and says
/// what it lacks, again and again, so a bring-up gate closes when the item
/// verifies - gg's half of slice forty-three, rule 25.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the machine can say.</b> Whether a credential reference resolves, or
/// a forge answers, is a fact about this machine's stores and network; the
/// control plane holds the profile and the gate, and this reading is how the
/// two meet. The agent's login stays <c>AgentReading</c>'s - what is measured
/// here is whether the machine even declares the agent its profile names.
/// </para>
/// <para>
/// <b>Nothing a check produced is reported.</b> A credential item names its
/// reference, never what resolving it returned.
/// </para>
/// </remarks>
public class AMachineSaysWhatItLacksTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static FleetProfileState ADevWorker(string? agent = "claude") => new()
    {
        Name = "dev-worker",
        Version = "v3",
        AppliedAt = T0,
        Profile = new FleetProfile
        {
            Roles = [ProfileRoles.Run],
            Environment = "dev",
            Agent = agent,
            Forges = ["acme=git.acme.example"],
            Credentials = ["local:forge-token", "keyvault://vault.example.net/npm"],
        },
    };

    private static Task<string?> Resolves(string reference, CancellationToken _) =>
        Task.FromResult(reference == "local:forge-token" ? null : $"{reference} is not in this machine's credential store.");

    private static Task<string?> Reaches(string host, CancellationToken _) =>
        Task.FromResult<string?>(host == "git.acme.example" ? null : $"{host} did not answer.");

    [Test]
    public async Task Every_item_the_profile_asks_for_is_measured_against_its_version()
    {
        var reading = await ProfileReadiness.MeasureAsync(
            ADevWorker(), declaredAgent: "claude", Resolves, Reaches, T0);

        await Assert.That(reading.Profile).IsEqualTo("dev-worker");
        await Assert.That(reading.Version).IsEqualTo("v3")
            .Because("a reading taken before an apply must not close a gate the new version opened.");
        await Assert.That(reading.Items.Select(i => (i.Kind, i.Subject, i.Met))).IsEquivalentTo(new[]
        {
            (ReadinessKinds.Agent, "claude", true),
            (ReadinessKinds.Credential, "local:forge-token", true),
            (ReadinessKinds.Credential, "keyvault://vault.example.net/npm", false),
            (ReadinessKinds.Forge, "acme", true),
        });
    }

    [Test]
    public async Task A_machine_declaring_no_agent_or_another_says_which()
    {
        var none = await ProfileReadiness.MeasureAsync(ADevWorker(), declaredAgent: null, Resolves, Reaches, T0);
        var other = await ProfileReadiness.MeasureAsync(ADevWorker(), declaredAgent: "codex", Resolves, Reaches, T0);

        var noAgent = none.Items.Single(i => i.Kind == ReadinessKinds.Agent);
        await Assert.That(noAgent.Met).IsFalse();
        await Assert.That(noAgent.Diagnosis!).Contains("executor-binary");
        await Assert.That(other.Items.Single(i => i.Kind == ReadinessKinds.Agent).Diagnosis!).Contains("codex");
    }

    [Test]
    public async Task A_profile_that_names_no_agent_asks_nothing_of_one()
    {
        var reading = await ProfileReadiness.MeasureAsync(ADevWorker(agent: null), null, Resolves, Reaches, T0);

        await Assert.That(reading.Items.Any(i => i.Kind == ReadinessKinds.Agent)).IsFalse();
    }

    [Test]
    public async Task A_forge_unreached_is_named_by_its_key()
    {
        var reading = await ProfileReadiness.MeasureAsync(
            ADevWorker(), "claude", Resolves, (_, _) => Task.FromResult<string?>("no route."), T0);

        var forge = reading.Items.Single(i => i.Kind == ReadinessKinds.Forge);
        await Assert.That(forge.Subject).IsEqualTo("acme");
        await Assert.That(forge.Met).IsFalse();
    }

    [Test]
    public async Task The_doors_are_the_runners_own()
    {
        foreach (var path in new[] { "/v1/runner/profile", "/v1/runner/readiness" })
        {
            await Assert.That(ProtocolSurface.Endpoints.Single(e => e.Path == path).Audience)
                .IsEqualTo(Audience.Runner);
        }
    }

    [Test]
    public async Task An_idle_runner_measures_itself_again_every_few_minutes_and_not_every_turn()
    {
        // A GATE CLEARS WHEN ITS ITEM VERIFIES ON A LATER READING, so the loop
        // must measure again - and not on every turn, which would dial every
        // forge in the profile every few seconds.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        using var stopping = new CancellationTokenSource();
        var measured = 0;
        var turns = 0;

        _ = await new RunnerLoop(new FakeProtocol(), clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(TimeSpan.FromMinutes(2));
                    if (++turns >= 12)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                new RecordingObserver(), new NoCredentialResolver(),
                trees.Workspace(new Vcs.LocalVcsAdapter(fixture.Directory)),
                measureReadiness: _ =>
                {
                    measured++;
                    return Task.CompletedTask;
                })
        {
            HoldFor = TimeSpan.FromSeconds(1),
        }
            .RunAsync("runner-1", ["linux"], stopping.Token)
            .WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(measured).IsGreaterThanOrEqualTo(2)
            .Because("measured at the first idle and again once five minutes had passed.");
        await Assert.That(measured).IsLessThan(turns);
    }

    [Test]
    public async Task Runner_up_measures_against_the_profile_it_enrolled_under()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        var program = await File.ReadAllTextAsync(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
        var hosts = Regex.Matches(
                program,
                @"RunnerHost\.RunAsync\((?<args>(?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))\)",
                RegexOptions.Singleline)
            .Select(m => m.Groups["args"].Value)
            .ToList();

        await Assert.That(hosts.Count(args => args.Contains("readiness:", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("runner up is the enrolled machine; a member's strategy fixes what it is, and "
                   + "a hand-flight is a person's own machine.");
        await Assert.That(program).Contains("ProfileReadiness.MeasureAsync(");
    }
}
