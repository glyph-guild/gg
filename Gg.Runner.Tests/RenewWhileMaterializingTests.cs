using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The lease is renewed while the workspace is materialized, and a fence answered then stops
/// the flight before an agent is started.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on the fleet, 2026-10-14 (GG-1123).</b> A lease lasts about sixty seconds and
/// materializing JDNext took a hundred: the runner renewed only once the agent was invoked, so
/// every claim lapsed mid-clone, was fenced, and was claimed again to clone the same tree from
/// nothing - eight times in fifteen minutes, and the flight never started. GG-1099 needed twelve
/// claims when the clone took sixty seconds. <see cref="RenewWhileWorkingTests"/> proved the
/// agent's half and left this one, because its fixture's tree materializes in milliseconds.
/// </para>
/// </remarks>
public class RenewWhileMaterializingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 6, 0, 0, TimeSpan.Zero);

    /// <summary>A workspace that does not finish until it is let go, like a long clone.</summary>
    private sealed class SlowWorkspace(IWorkspace inner, Func<CancellationToken, Task> until) : IWorkspace
    {
        public bool Cancelled { get; private set; }

        public async Task<WorkspaceResult> PrepareAsync(
            string flightId,
            IReadOnlyList<LeaseRepoRef> repos,
            IReadOnlyDictionary<string, string> secretsByLocator,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await until(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled = true;
                throw;
            }

            return await inner.PrepareAsync(flightId, repos, secretsByLocator, cancellationToken);
        }

        public void Release(string flightId) => inner.Release(flightId);

        public HeldTree? Hold(string flightId) => inner.Hold(flightId);

        public int SweepOrphans() => inner.SweepOrphans();
    }

    private sealed class CountingExecutor : IExecutorPort
    {
        public int Invoked { get; private set; }

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Invoked++;
            return Task.FromResult<ExecutorRun?>(ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(1),
                movesUsed: [LoopMoves.Read]));
        }
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture) => new()
    {
        LeaseId = "lease-materializing",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1123),
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
        ExpiresAt = T0.AddSeconds(58),
        RenewWithinSeconds = 20,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "investigate",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer,
        IWorkspace workspace, IExecutorPort executor) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer, new NoCredentialResolver(), workspace, executor: executor)
        {
            HoldFor = TimeSpan.FromSeconds(3),
        };

    [Test]
    public async Task The_lease_is_renewed_while_the_tree_is_materialized()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        var observer = new RecordingObserver();

        // THE CLONE DOES NOT FINISH until the lease has been renewed under it, bounded: five
        // real seconds of nothing is the red state, reported rather than hung.
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        protocol.OnRenew = () => renewed.TrySetResult();
        var sawRenewal = false;
        var workspace = new SlowWorkspace(
            trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
            async _ => sawRenewal =
                await Task.WhenAny(renewed.Task, Task.Delay(TimeSpan.FromSeconds(5))) == renewed.Task);

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal)
                || e.StartsWith("fenced:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await Build(protocol, clock, observer, workspace, new CountingExecutor())
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(sawRenewal).IsTrue()
            .Because("a clone longer than the lease lapses it unless somebody renews it - "
                   + "GG-1123 was fenced mid-clone eight times and never started.");
        await Assert.That(protocol.ShippedFacts).IsNotEmpty()
            .Because("the renewed lease is still generation 1, so the flight goes on to work.");
    }

    [Test]
    public async Task A_fence_answered_while_materializing_stops_the_flight_before_the_agent()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        protocol.Renewals.Enqueue(new RenewResult.Fenced());
        var observer = new RecordingObserver();

        // THE CLONE RUNS UNTIL IT IS TOLD TO STOP, bounded the same way.
        var workspace = new SlowWorkspace(
            trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
            token => Task.Delay(TimeSpan.FromSeconds(5), token));
        var executor = new CountingExecutor();

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("fenced:", StringComparison.Ordinal)
                || e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await Build(protocol, clock, observer, workspace, executor)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(workspace.Cancelled).IsTrue()
            .Because("the flight is somebody else's now, and finishing the clone is work nobody "
                   + "will use.");
        await Assert.That(executor.Invoked).IsEqualTo(0)
            .Because("an agent started on a lease already lost would work for nobody.");
        await Assert.That(protocol.ShippedFacts).IsEmpty();
        await Assert.That(observer.Events.Any(e =>
                e.StartsWith("fenced:", StringComparison.Ordinal))).IsTrue()
            .Because("fenced is a state the loop narrates, never an unhandled exception.");
    }
}
