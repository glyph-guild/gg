using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The seed a lease carries reaches the executor's request, untouched.
/// </summary>
/// <remarks>
/// <para>
/// Slice seven proved the control plane composes <c>resumesFrom</c> and that a
/// second runner's lease carries it across machines. This is the other half of
/// that sentence: the runner has to HAND it to the loop it starts, or the
/// delivery proved nothing. The tree's twin was wired first -
/// <c>continuesFrom</c> is checked out - and a resumed agent working on the
/// prior attempt's tree with no idea what the prior attempt ruled out redoes
/// the afternoon anyway.
/// </para>
/// <para>
/// <b>Verbatim, because the document is already rendered.</b> The contract
/// renders the seed once, control-plane-side; a runner that re-worded it would
/// be a second implementation of a document the contract package already
/// renders. The same disposition as <c>Feedback</c>: passed through, never
/// interpreted.
/// </para>
/// </remarks>
public class ResumptionContextTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private const string Seed =
        "GG-1042 - taking over flight-1\n\nMEASURED (by gg, from the run's own event stream)\n"
      + "read, not changed:\n  src/rounding.py\nchanged:\n  src/orders.py\n";

    private sealed class CapturingExecutor : IExecutorPort
    {
        internal List<ExecutorRequest> Requests { get; } = [];

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<ExecutorRun?>(ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(1),
                movesUsed: [LoopMoves.Read]));
        }
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture, string? resumesFrom) => new()
    {
        LeaseId = "lease-resume",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
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
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
            ResumesFrom = resumesFrom,
        },
    };

    private static async Task<CapturingExecutor> FlyAsync(string? resumesFrom)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, resumesFrom)));
        var observer = new RecordingObserver();
        var executor = new CapturingExecutor();

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

        return executor;
    }

    [Test]
    public async Task A_resuming_lease_hands_the_seed_to_the_executor_verbatim()
    {
        var executor = await FlyAsync(Seed);

        // The session's probe invokes the executor too (slice eleven); the
        // request under test is the WORK's.
        var work = executor.Requests.Where(r => r.LoopId != "gg-move-bound-probe").ToList();
        await Assert.That(work).Count().IsEqualTo(1);
        await Assert.That(work[0].ResumesFrom).IsEqualTo(Seed)
            .Because("the seed is a rendered document; a runner that re-worded or trimmed it "
                   + "would be a second implementation of what the contract renders once.");
    }

    [Test]
    public async Task A_first_attempt_carries_nothing_to_resume_from()
    {
        var executor = await FlyAsync(resumesFrom: null);

        var work = executor.Requests.Where(r => r.LoopId != "gg-move-bound-probe").ToList();
        await Assert.That(work).Count().IsEqualTo(1);
        await Assert.That(work[0].ResumesFrom).IsNull()
            .Because("no prior attempt and a prior attempt are different states, and the "
                   + "ordinary first attempt must not grow a synthesized record.");
    }
}
