using System.Security.Cryptography;
using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner says it is alive while it is flying, and takes up an introduction
/// that arrives while the agent is working.
/// </summary>
/// <remarks>
/// <para>
/// <b>The beat used to stop the moment a flight started.</b> It was sent from
/// the idle loop and from inside the claim, and neither runs once work begins:
/// <c>WorkAsync</c> materializes, invokes, ships, waits for a landing and lands,
/// and the only thing it sent the control plane over all of it was a renewal.
/// Measured on the fleet on 2026-09-08 — four beats sixteen seconds apart while
/// idle, then thirty-nine seconds of nothing but one renew.
/// </para>
/// <para>
/// <b>Two things break, and the second is the one that looks like a network
/// problem.</b> Status is derived control-plane-side from heartbeat age, so a
/// busy runner decays to offline — the exact defect <c>_nextBeatDue</c>'s remark
/// records for the IDLE case, still open for the busy one. And introductions
/// ride the beat: a console that reaches a runner mid-flight leaves a sealed
/// offer that nothing collects, then reports that the machine "is not asking".
/// </para>
/// <para>
/// <b>What is asserted here is delivery, not the handshake.</b> Whether two
/// peers can actually reach each other is
/// <c>AConsoleReachesARunnerTests</c>'s subject and needs both ends; this needs
/// only that the offer is SEEN while the agent is still working, which is what
/// was missing. An offer this fake makes is not openable, so the runner says so
/// — and saying so is proof it looked.
/// </para>
/// </remarks>
public class BeatsWhileFlyingTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 8, 23, 0, 0, TimeSpan.Zero);

    /// <summary>An executor that stops inside the flight until it is let go.</summary>
    /// <remarks>
    /// The subject is what the runner does WHILE work is in progress, and every
    /// other fake finishes too fast to have a "while".
    /// </remarks>
    private sealed class BlockingExecutor : IExecutorPort
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Entered => _entered.Task;

        internal void Release() => _release.TrySetResult();

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public async Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // The bound probe runs first and must not block, or the flight never
            // reaches the work this test is about.
            if (string.Equals(request.LoopId, "gg-move-bound-probe", StringComparison.Ordinal))
            {
                return new ExecutorRun
                {
                    LoopId = request.LoopId,
                    Outcome = LoopOutcomes.Completed,
                    Reason = "probed",
                    Attempts = 1,
                    DurationMs = 10,
                    MovesUsed = [],
                };
            }

            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);

            return ExecutorRun.Exhausted(request.LoopId, request.WallClock, [LoopMoves.Read]);
        }
    }

    private static LeaseGranted ALease(GitFixture fixture, bool attended) => new()
    {
        LeaseId = "lease-1",
        FlightId = "01a08346-85a5-7449-b2b0-2325aca756bf",
        FlightNumber = "GG-64",
        Generation = 1,
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
        Attended = attended ? true : null,
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private sealed record Flown(FakeProtocol Protocol, RecordingObserver Observer, int BeatsWhileFlying);

    /// <summary>
    /// Flies one flight, holding the agent inside it long enough to watch what
    /// the runner sends while it is working.
    /// </summary>
    private static async Task<Flown> FlyAsync(
        bool attended,
        Func<string, AttendedSession>? sessions = null,
        Action<FakeProtocol>? arrange = null)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture, attended)));
        arrange?.Invoke(protocol);

        var observer = new RecordingObserver();
        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        var executor = new BlockingExecutor();

        var flying = new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.CompletedTask;
                },
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                executor: executor,
                attendedSessions: sessions,
                // THE BEAT'S OWN PACE, and it is a SECOND knob deliberately.
                // The flight's own waits are logical steps a test moves a clock
                // through; the beat is a background cadence running alongside
                // them. Sharing one delegate makes a test's clock jump by a
                // heartbeat interval every time the pump goes round, which is
                // how a shared knob turns "the runner is alive" into an expired
                // lease. Here: move the clock the pump waited, then yield for a
                // real instant so the flight it is running beside can proceed.
                beatPace: (span, token) =>
                {
                    clock.Advance(span);
                    return Task.Delay(1, token);
                })
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        // Wait for the agent to be inside the flight, then count what the runner
        // sends from there. Bounded, and loud if the flight never gets that far.
        await executor.Entered.WaitAsync(TimeSpan.FromSeconds(30));

        var before = protocol.Calls.Count(c => c == "heartbeat");
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        int beats;

        while ((beats = protocol.Calls.Count(c => c == "heartbeat") - before) < 2
            && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        executor.Release();
        await flying;

        return new Flown(protocol, observer, beats);
    }

    [Test]
    public async Task A_runner_beats_while_the_agent_is_working()
    {
        var flown = await FlyAsync(attended: false);

        await Assert.That(flown.BeatsWhileFlying).IsGreaterThanOrEqualTo(2)
            .Because("status is derived from heartbeat age, so a runner that stops beating "
                   + "when it starts flying reads as offline for the whole flight - and a "
                   + "capability-gated flight is only ever offered to a live runner.");
    }

    [Test]
    public async Task An_introduction_is_taken_up_while_the_agent_is_still_working()
    {
        // WIRED TO BE DRIVEN, which is what a non-null session factory means.
        // The channel and the dispatch are real; only the offer is nonsense, so
        // the runner gets as far as failing to open it - and that failure is the
        // evidence that the beat delivered it.
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        var flown = await FlyAsync(
            attended: true,
            sessions: flightId => new AttendedSession(
                key,
                new RunnerChannel([], TimeSpan.FromSeconds(1)),
                new AskDispatch(new WhatThisRunnerSays(
                    new SilentObserver(),
                    new TheFlightsOwnOutput(Path.Combine(Path.GetTempPath(), $"{flightId}.ndjson")),
                    () => T0)),
                new SilentObserver()),
            arrange: p => p.Introductions.Enqueue(new PendingIntroduction
            {
                IntroductionId = "introduction-1",
                Offer = new RunnerSealedOffer { Sealed = [1, 2, 3, 4] },
            }));

        await Assert.That(flown.Protocol.IntroductionsTaken).IsGreaterThanOrEqualTo(1)
            .Because("an introduction rides the heartbeat, and a runner that does not beat "
                   + "while it flies can never be reached during the one window a person "
                   + "actually wants: while the agent is working.");
    }

    [Test]
    public async Task The_flight_still_finishes()
    {
        // The poison twin. Every assertion above would also pass on a runner
        // that beat forever and never released, which is the failure mode a
        // background pump introduces.
        var flown = await FlyAsync(attended: false);

        await Assert.That(flown.Observer.Events.Any(
            e => e.StartsWith("released:", StringComparison.Ordinal))).IsTrue();
    }
}
