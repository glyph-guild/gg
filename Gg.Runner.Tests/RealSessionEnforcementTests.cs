using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A real flight's fact set carries per-tool enforcement measured inside that
/// flight's own session.
/// </summary>
/// <remarks>
/// A genuine agent loop, and it says so: a real executor, a real tree, the
/// session's probe in front of the invocation, and the environment.identity
/// fact that ships afterwards carrying what THAT probe held and when it
/// measured - not a capability constant, not a startup run's leftovers.
/// `real agent, one host`, per slice seven's vocabulary.
/// </remarks>
public class RealSessionEnforcementTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    private static string Binary =>
        Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
        ?? throw new InvalidOperationException(
            "GG_EXECUTOR_BINARY is not set. This proves the fact carries the session's own "
          + "measurement, which only a real agent session can produce.");

    private static LeaseGranted ALease(GitFixture fixture) => new()
    {
        LeaseId = "lease-real",
        Generation = 1,
        FlightId = "flight-real",
        FlightNumber = FlightRef.Format(1077),
        Repos =
        [
            new LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(30),
        RenewWithinSeconds = 5,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 120,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    [Test]
    [Category("RealAgent")]
    public async Task The_fact_set_carries_enforcement_measured_inside_the_session()
    {
        var started = DateTimeOffset.UtcNow;

        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture)));

        var observer = new RecordingObserver();
        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    // BOTH clocks move: the movable one advances so renewals
                    // and the hold progress, and a small real delay paces the
                    // loop so it does not hot-spin while a real agent spends
                    // real seconds.
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.Delay(TimeSpan.FromMilliseconds(100), token);
                },
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                executor: new ClaudeCodeExecutor(Binary))
            {
                HoldFor = TimeSpan.FromSeconds(1),
            }
            .RunAsync("runner-real", ["linux"], stopping.Token);

        var identity = protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Single(f => f.Kind == FactKinds.EnvironmentIdentity)
            .Environment!;

        await Assert.That(identity.MoveEnforcement).IsEqualTo(MoveEnforcements.PerTool)
            .Because("the value on the wire is what this session's probe proved, and a "
                   + "real probe against a real binary proves per-tool.");
        await Assert.That(identity.MovesProbed).IsEquivalentTo((string[])["Edit", "Write"])
            .Because("each denied tool held against its own artifact, in this session.");
        await Assert.That(identity.ProbedAt!.Value).IsGreaterThanOrEqualTo(started)
            .Because("probedAt sits inside this flight's own window - a measurement of "
                   + "THIS session, auditable rather than asserted.");
        await Assert.That(identity.ProbedAt!.Value)
            .IsLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }
}
