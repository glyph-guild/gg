using Gg.Contracts;

namespace Gg.Runner;

/// <summary>Where a runner's time goes. Written out so a test can assert on it.</summary>
public enum RunnerActivity
{
    /// <summary>Long-polling for a flight.</summary>
    Claiming,

    /// <summary>Holding a lease.</summary>
    Holding,
}

/// <summary>What the loop did, reported as it happens.</summary>
public interface IRunnerObserver
{
    void Claimed(LeaseGranted lease);

    void Renewed(string leaseId, DateTimeOffset expiresAt);

    /// <summary>The fence refused us. The flight is somebody else's now.</summary>
    void Fenced(string leaseId);

    void Released(string leaseId, string disposition);

    void Idle();
}

/// <summary>Ignores everything, for tests where the narration is not the subject.</summary>
public sealed class SilentObserver : IRunnerObserver
{
    public void Claimed(LeaseGranted lease) { }
    public void Renewed(string leaseId, DateTimeOffset expiresAt) { }
    public void Fenced(string leaseId) { }
    public void Released(string leaseId, string disposition) { }
    public void Idle() { }
}

/// <summary>
/// The runner's whole life at this step: claim, hold, heartbeat, release.
/// </summary>
/// <remarks>
/// <para>
/// It does NOTHING with the lease. No materialize, no credential resolution,
/// no facts - those are later steps. This is deliberately a runner that proves
/// the protocol and performs no work, so anything failing here is the protocol
/// failing rather than the work.
/// </para>
/// <para>
/// Heartbeat and renew are kept apart on purpose. The heartbeat says this
/// process is alive; the renewal extends one specific lease. A runner that
/// heartbeats but stops renewing loses its lease and should - collapsing them
/// is the obvious simplification and it is exactly what breaks takeover.
/// </para>
/// <para>
/// Time enters through <see cref="IClock"/> and waiting through a delegate, so
/// every decision here is testable with no real time passing. The one thing
/// that cannot be tested that way - a lease outliving the process holding it -
/// is tested by killing a real process instead.
/// </para>
/// </remarks>
public sealed class RunnerLoop(
    IRunnerProtocol protocol,
    IClock clock,
    Func<TimeSpan, CancellationToken, Task> delay,
    IRunnerObserver observer)
{
    /// <summary>Seconds the control plane may hold a claim open.</summary>
    public const int ClaimWaitSeconds = 30;

    private readonly IRunnerProtocol _protocol = protocol;
    private readonly IClock _clock = clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;
    private readonly IRunnerObserver _observer = observer;

    /// <summary>
    /// How long this no-op runner holds a lease before releasing it.
    /// </summary>
    /// <remarks>
    /// Stands in for the work later steps will do. It exists so heartbeat and
    /// renewal are exercised at all, and so a kill test has a window in which
    /// to kill something.
    /// </remarks>
    public TimeSpan HoldFor { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>What the runner is doing right now.</summary>
    public RunnerActivity Activity { get; private set; } = RunnerActivity.Claiming;

    /// <summary>Runs until cancelled.</summary>
    public async Task RunAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Activity = RunnerActivity.Claiming;

                var claim = await _protocol.ClaimAsync(runnerId, labels, ClaimWaitSeconds, cancellationToken);
                if (claim is not ClaimResult.Granted(var lease))
                {
                    // The control plane already held the request open for up to
                    // ClaimWaitSeconds. Going straight round again is a long
                    // poll, not a busy loop.
                    _observer.Idle();
                    continue;
                }

                _observer.Claimed(lease);
                Activity = RunnerActivity.Holding;

                await HoldAsync(runnerId, labels, lease, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is how this loop ends. It is not a failure, and the
            // lease is deliberately NOT released on the way out: proving that a
            // lease survives its holder and expires on the control plane's
            // clock is the point of the whole step.
        }
    }

    private async Task HoldAsync(
        string runnerId, IReadOnlyList<string> labels, LeaseGranted lease, CancellationToken cancellationToken)
    {
        var expiresAt = lease.ExpiresAt;
        var until = _clock.UtcNow + HoldFor;

        while (_clock.UtcNow < until && !cancellationToken.IsCancellationRequested)
        {
            var beat = await _protocol.HeartbeatAsync(runnerId, labels, cancellationToken);

            // Renewal is decided against the control plane's expiry, never
            // against our own elapsed time. A process that was paused or
            // descheduled must not conclude it still has time in hand.
            if (_clock.UtcNow >= expiresAt - TimeSpan.FromSeconds(lease.RenewWithinSeconds))
            {
                switch (await _protocol.RenewAsync(lease.LeaseId, lease.Generation, cancellationToken))
                {
                    case RenewResult.Renewed renewed:
                        expiresAt = renewed.ExpiresAt;
                        _observer.Renewed(lease.LeaseId, expiresAt);
                        break;

                    default:
                        // Fenced or gone. Stop, and do not release: this flight
                        // belongs to another runner now, and releasing it would
                        // end their work.
                        _observer.Fenced(lease.LeaseId);
                        return;
                }
            }

            await _delay(TimeSpan.FromSeconds(beat.NextHeartbeatSeconds), cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var release = await _protocol.ReleaseAsync(
            lease.LeaseId, lease.Generation, RunnerDisposition.Completed, detail: null, cancellationToken);

        if (release is ReleaseResult.Released)
        {
            _observer.Released(lease.LeaseId, RunnerDisposition.Completed);
        }
        else
        {
            _observer.Fenced(lease.LeaseId);
        }
    }
}
