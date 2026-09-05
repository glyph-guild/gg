using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The lease is renewed while the agent works, and a fence answered mid-work
/// stops the work rather than the runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by the first real flight ever flown end to end, 2026-08-24.</b>
/// A lease lasts sixty seconds on the control plane and the runner renewed
/// only in <c>HoldAsync</c> - after the work - so any loop longer than the
/// lease died fenced at ship time: the real agent worked for ninety seconds,
/// the lease expired underneath it, and the facts it produced were refused
/// with <c>RunnerFencedException</c>, unhandled, killing the whole runner
/// process. Every prior real-agent proof granted itself a thirty-minute lease
/// through a stub surface, which is exactly how the fake hid it.
/// </para>
/// <para>
/// <b>A fence mid-work means the flight is somebody else's now.</b> The clock
/// expired us and the control plane may already have requeued; burning the
/// rest of the agent's budget on a lease we lost is work nobody will accept,
/// and shipping its facts is the crash above. So the work is cancelled, the
/// batch is not shipped, and the runner goes back to claiming - fenced is a
/// state the loop already narrates, never an unhandled exception.
/// </para>
/// </remarks>
public class RenewWhileWorkingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class PendingExecutor(Func<ExecutorRequest, CancellationToken, Task<ExecutorRun?>> respond)
        : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            respond(request, cancellationToken);
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture, DateTimeOffset expiresAt) => new()
    {
        LeaseId = "lease-working",
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
        // THE REAL CONTROL PLANE'S SHAPE: a short lease a working loop outlives
        // unless somebody renews it.
        ExpiresAt = expiresAt,
        RenewWithinSeconds = 20,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
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
    public async Task The_lease_is_renewed_while_the_agent_works()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, T0.AddSeconds(60))));
        var observer = new RecordingObserver();

        // The agent does not finish until the lease has been renewed under it,
        // bounded: five real seconds of nothing is the red state, reported
        // rather than hung.
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        protocol.OnRenew = () => renewed.TrySetResult();
        var sawRenewal = false;
        var executor = new PendingExecutor(async (request, _) =>
        {
            sawRenewal = await Task.WhenAny(renewed.Task, Task.Delay(TimeSpan.FromSeconds(5)))
                == renewed.Task;
            return ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(90),
                movesUsed: [LoopMoves.Read]);
        });

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await Build(protocol, clock, observer,
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)), executor)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(sawRenewal).IsTrue()
            .Because("the work is the one time the lease actually needs to stay alive - "
                   + "renewing only while holding is how the first real flight died fenced.");
        await Assert.That(protocol.ShippedFacts).IsNotEmpty()
            .Because("the renewed lease is still generation 1, so the facts it produced "
                   + "are accepted rather than refused.");
    }

    [Test]
    public async Task A_fence_answered_mid_work_cancels_the_agent_and_the_runner_survives()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, T0.AddSeconds(60))));
        protocol.Renewals.Enqueue(new RenewResult.Fenced());
        var observer = new RecordingObserver();

        // The agent works until it is told to stop, bounded the same way: on
        // the red side nothing cancels it, it "finishes" after five real
        // seconds, and the assertions below say what went wrong.
        var cancelled = false;
        var executor = new PendingExecutor(async (request, token) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                throw;
            }

            return ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(90),
                movesUsed: [LoopMoves.Read]);
        });

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("fenced:", StringComparison.Ordinal)
                || e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await Build(protocol, clock, observer,
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)), executor)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(cancelled).IsTrue()
            .Because("the flight is somebody else's now, and the rest of the agent's budget "
                   + "spent on a lost lease is work nobody will accept.");
        await Assert.That(protocol.ShippedFacts).IsEmpty()
            .Because("shipping on a dead generation is the RunnerFencedException that killed "
                   + "the whole runner process on the first real flight.");
        await Assert.That(observer.Events.Any(e =>
                e.StartsWith("fenced:", StringComparison.Ordinal))).IsTrue()
            .Because("fenced is a state the loop narrates, never an unhandled exception.");
    }
}
