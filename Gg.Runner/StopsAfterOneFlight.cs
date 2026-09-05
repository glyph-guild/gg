using Gg.Contracts;
using Gg.Runner.Facts;

namespace Gg.Runner;

/// <summary>
/// A runner that goes home when the flight it came for is done.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its lifetime is its availability, which is the whole difference from a
/// fleet runner.</b> A fleet runner is a service: it stays, it beats, and its
/// being there is what makes work claimable. A hand-flight's runner is a
/// person's session — and one that lingered would be a runner nobody knows is
/// there, taking fleet work onto somebody's laptop after they had walked away
/// from it.
/// </para>
/// <para>
/// <b>A decorator rather than a flag on the loop</b>, because the loop's
/// question is "keep going?" and the answer here is not about the loop at all —
/// it is about who started it and why. A flag would put a person's intent
/// inside the thing that serves the fleet.
/// </para>
/// <para>
/// <b>It stops at the RELEASE, not at the landing.</b> A flight can land and the
/// lease still be held — the runner holds the tree for a window afterwards — and
/// stopping early would kill the process while it still owed the control plane a
/// disposition. The release is the runner saying what became of the flight, and
/// after that there is nothing left it is for.
/// </para>
/// <para>
/// <b>Everything is forwarded.</b> A decorator that swallowed the narration
/// would make a hand-flight silent, which is worse than one that never stops:
/// the person is at the terminal, and this is where they learn what happened.
/// </para>
/// </remarks>
public sealed class StopsAfterOneFlight(IRunnerObserver inner, CancellationTokenSource stopping)
    : IRunnerObserver
{
    private readonly IRunnerObserver _inner = inner;
    private readonly CancellationTokenSource _stopping = stopping;

    public void Released(string leaseId, string disposition)
    {
        _inner.Released(leaseId, disposition);

        // AFTER forwarding, so the person reads what happened before the process
        // begins winding down. Cancel is idempotent; a runner that somehow
        // released twice stops once.
        _stopping.Cancel();
    }

    public void Claimed(LeaseGranted lease) => _inner.Claimed(lease);

    public void Renewed(string leaseId, DateTimeOffset expiresAt) =>
        _inner.Renewed(leaseId, expiresAt);

    public void Fenced(string leaseId) => _inner.Fenced(leaseId);

    public void BoundBroken(string diagnosis) => _inner.BoundBroken(diagnosis);

    public void ControlPlaneRefused(string diagnosis, TimeSpan retryIn) =>
        _inner.ControlPlaneRefused(diagnosis, retryIn);

    public void Idle() => _inner.Idle();

    public void Parked() => _inner.Parked();

    public void Waiting(IReadOnlyList<string> repos) => _inner.Waiting(repos);

    public void Materialized(string slug, string headCommit, long bytes) =>
        _inner.Materialized(slug, headCommit, bytes);

    public void WorkspaceFailed(string diagnosis) => _inner.WorkspaceFailed(diagnosis);

    public void FactsShipped(int count) => _inner.FactsShipped(count);

    public void LoopFinished(
        string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed) =>
        _inner.LoopFinished(loopId, outcome, attempts, movesUsed);

    public void MoveRefused(string diagnosis) => _inner.MoveRefused(diagnosis);

    public void Landed(string outcome, string detail) => _inner.Landed(outcome, detail);

    public void Held(string flightNumber, string path, long bytes, bool preserved = false) =>
        _inner.Held(flightNumber, path, bytes, preserved);

    public void CredentialUnresolved(CredentialResolutionFailure failure) =>
        _inner.CredentialUnresolved(failure);
}
