using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The bound is proven for the session it governs - before every invocation,
/// not once per process - and a bound that breaks mid-life releases the lease
/// naming the diagnosis, ships nothing, and stops the runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ambient settings act on the session, so a measurement taken before the
/// session is a measurement of something else.</b> Step 0 measured the family
/// live: without <c>--setting-sources ""</c> a withheld Write wrote, and
/// <c>--permission-mode acceptEdits</c> defeats the bound even with sources
/// cleared - either can arrive between a runner's startup and its tenth
/// flight. One executor invocation per lease is the session, so the probe
/// runs under the lease's own renewal, immediately before the work.
/// </para>
/// <para>
/// <b>A broken bound is a property of the machine, not of the lease.</b> The
/// lease goes back failed with the probe's diagnosis - the named halt,
/// Article XI - no facts ship (a fact set for a session that never ran would
/// be evidence of a flight that did not fly), and the runner stops taking
/// work with the same exit its startup refusal uses.
/// </para>
/// </remarks>
public class ProbePerSessionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Answers probes and work apart, and can break the bound on cue.</summary>
    private sealed class SessionExecutor : IExecutorPort
    {
        internal List<string> Sequence { get; } = [];
        internal int Probes;
        internal Func<int, bool> BreakOnProbe { get; init; } = _ => false;

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            if (string.Equals(request.LoopId, "gg-move-bound-probe", StringComparison.Ordinal))
            {
                var number = Interlocked.Increment(ref Probes);
                Sequence.Add("probe");

                if (BreakOnProbe(number))
                {
                    File.WriteAllText(
                        Path.Combine(request.WorkingDirectory, MoveBoundProbe.Canary),
                        "unbound");
                }

                return Task.FromResult(new ExecutorRun
                {
                    LoopId = request.LoopId,
                    Outcome = LoopOutcomes.Completed,
                    Reason = "probed",
                    Attempts = 1,
                    DurationMs = 10,
                    MovesUsed = [],
                });
            }

            Sequence.Add("work");
            return Task.FromResult(ExecutorRun.Exhausted(
                request.LoopId, request.WallClock, [LoopMoves.Read]));
        }
    }

    private static LeaseGranted ALease(GitFixture fixture, int number) => new()
    {
        LeaseId = $"lease-{number}",
        Generation = number,
        FlightId = $"flight-{number}",
        FlightNumber = FlightRef.Format(1000 + number),
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
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private static async Task<(SessionExecutor Executor, FakeProtocol Protocol, int Exit)>
        RunAsync(SessionExecutor executor, int leases, int stopAfterReleases)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        for (var lease = 1; lease <= leases; lease++)
        {
            protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture, lease)));
        }

        var observer = new RecordingObserver();
        using var stopping = new CancellationTokenSource();
        var released = 0;
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal)
                && Interlocked.Increment(ref released) >= stopAfterReleases)
            {
                stopping.Cancel();
            }
        };

        var exit = await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.CompletedTask;
                },
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                executor: executor)
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return (executor, protocol, exit);
    }

    [Test]
    public async Task Every_session_is_probed_before_its_own_invocation()
    {
        var (executor, _, _) = await RunAsync(new SessionExecutor(), leases: 2,
            stopAfterReleases: 2);

        await Assert.That(executor.Sequence)
            .IsEquivalentTo((string[])["probe", "work", "probe", "work"],
                CollectionOrdering.Matching)
            .Because("one startup probe answering for every later session is a measurement "
                   + "of something else - ambient settings act on the session, and the "
                   + "family has five members now (acceptEdits joined at step 0).");
    }

    [Test]
    public async Task A_bound_that_breaks_mid_life_releases_naming_why_ships_nothing_and_stops()
    {
        var (executor, protocol, exit) = await RunAsync(
            new SessionExecutor { BreakOnProbe = n => n == 2 },
            leases: 2, stopAfterReleases: 3);

        await Assert.That(executor.Sequence)
            .IsEquivalentTo((string[])["probe", "work", "probe"], CollectionOrdering.Matching)
            .Because("the second session's work never ran: the probe in front of it broke.");

        await Assert.That(protocol.Calls).Contains("release:2:failed")
            .Because("the lease goes back rather than expiring under a runner that "
                   + "quietly stopped.");
        await Assert.That(protocol.Serialized.Any(s =>
                s.Contains(MoveBoundProbe.Canary, StringComparison.Ordinal))).IsTrue()
            .Because("the release carries the probe's diagnosis - the named halt, never "
                   + "an unqualified anything.");

        await Assert.That(protocol.ShippedFacts.SelectMany(b => b.Items)
                .Count(f => f.Kind == FactKinds.LoopOutcome)).IsEqualTo(1)
            .Because("only the first session shipped: a fact set for a session that never "
                   + "ran would be evidence of a flight that did not fly.");

        await Assert.That(exit).IsEqualTo(69)
            .Because("a broken bound is a property of the machine, not of the lease - the "
                   + "runner stops with the same exit its startup refusal uses.");
    }
}
