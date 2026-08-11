using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

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

    /// <summary>
    /// A repository is on disk: which commit, and how much of it.
    /// </summary>
    /// <remarks>
    /// The commit and the byte count, never a path inside the tree and never a
    /// byte of what is in it. This line goes to stdout, and stdout is what a
    /// customer pastes into a ticket.
    /// </remarks>
    void Materialized(string slug, string headCommit, long bytes);

    /// <summary>The workspace could not be prepared, and this is why.</summary>
    void WorkspaceFailed(string diagnosis);

    /// <summary>Facts left the machine. How many, never which.</summary>
    void FactsShipped(int count);

    /// <summary>
    /// A credential the lease named could not be read here.
    /// </summary>
    /// <remarks>
    /// The failure carries the REFERENCE and a sentence, never anything that
    /// came of resolving it. This is narrated to stdout in a real runner, and
    /// stdout is what a customer pastes into a ticket.
    /// </remarks>
    void CredentialUnresolved(CredentialResolutionFailure failure);
}

/// <summary>Ignores everything, for tests where the narration is not the subject.</summary>
public sealed class SilentObserver : IRunnerObserver
{
    public void Claimed(LeaseGranted lease) { }
    public void Renewed(string leaseId, DateTimeOffset expiresAt) { }
    public void Fenced(string leaseId) { }
    public void Released(string leaseId, string disposition) { }
    public void Idle() { }
    public void CredentialUnresolved(CredentialResolutionFailure failure) { }
    public void Materialized(string slug, string headCommit, long bytes) { }
    public void WorkspaceFailed(string diagnosis) { }
    public void FactsShipped(int count) { }
}

/// <summary>
/// The runner's whole life: claim, resolve, materialize, ship, hold, release.
/// </summary>
/// <remarks>
/// <para>
/// The order is load-bearing and the whole of it is here: <b>lease → resolve
/// credentials → materialize → extract facts → compute digest → apply filter →
/// emit</b>. Everything up to emit is now real. What is still missing is the
/// executor, so nothing runs a customer's tests yet - which is why a flight
/// holds its lease for a fixed window rather than for as long as work takes.
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
    IRunnerObserver observer,
    ICredentialResolver credentials,
    IWorkspace workspace)
{
    /// <summary>Seconds the control plane may hold a claim open.</summary>
    public const int ClaimWaitSeconds = 30;

    private readonly IRunnerProtocol _protocol = protocol;
    private readonly IClock _clock = clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;
    private readonly IRunnerObserver _observer = observer;
    private readonly ICredentialResolver _credentials = credentials;
    private readonly IWorkspace _workspace = workspace;

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

                // Lease, THEN resolve credentials, then everything else. The
                // order is load-bearing and this is the second step of it; a
                // credential that cannot be read stops the flight here, before
                // anything is materialized, rather than halfway through.
                var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
                if (await ResolveAsync(lease, resolved, cancellationToken) is { } failure)
                {
                    await GiveBackAsync(lease, failure, cancellationToken);
                    continue;
                }

                // ...then MATERIALIZE, then extract facts, then digest, then
                // filter, then emit. The rest of the order, and the first part
                // of it that puts a customer's source code on our disk.
                try
                {
                    await WorkAsync(runnerId, labels, lease, resolved, cancellationToken);
                }
                finally
                {
                    // Whatever happened, the tree goes. A SIGKILL defeats this
                    // and that is what the startup sweep is for; everything
                    // short of one is handled here.
                    _workspace.Release(lease.FlightId);
                }
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

    /// <summary>
    /// Resolves every credential the lease named, or reports the first that
    /// could not be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not "resolve what you can". A flight running with half its credentials
    /// produces a partial result nobody can tell from a whole one, which is
    /// exactly the failure Article XI exists for - a silently-absent input is
    /// indistinguishable from one that was there.
    /// </para>
    /// <para>
    /// The resolved values are held in a local dictionary for exactly as long
    /// as the materialize below needs them, and in no field of this class. A
    /// secret on an object that outlives the flight is a secret in a heap dump
    /// for no reason.
    /// </para>
    /// </remarks>
    private async Task<CredentialResolutionFailure?> ResolveAsync(
        LeaseGranted lease,
        Dictionary<string, string> resolvedByLocator,
        CancellationToken cancellationToken)
    {
        foreach (var reference in lease.Credentials)
        {
            var resolution = await _credentials.ResolveAsync(reference, cancellationToken);

            if (resolution is CredentialResolution.Resolved(var secret))
            {
                // Kept only for as long as the materialize below needs it, and
                // keyed by the locator the contract derives - the same
                // derivation gg credential add used, so the two cannot drift.
                resolvedByLocator[reference.Locator] = secret;
            }

            if (resolution is CredentialResolution.Unresolvable(var problem))
            {
                var failure = new CredentialResolutionFailure { Reference = reference, Problem = problem };
                _observer.CredentialUnresolved(failure);
                return failure;
            }
        }

        return null;
    }

    /// <summary>
    /// Hands the lease straight back with the diagnosis.
    /// </summary>
    /// <remarks>
    /// At once, rather than holding it. A runner that kept a lease it cannot
    /// work would block the flight for the lease's whole duration and then
    /// expire, which is the stalled flight ADR-0004 named wearing a timer.
    /// </remarks>
    private async Task GiveBackAsync(
        LeaseGranted lease, CredentialResolutionFailure failure, CancellationToken cancellationToken)
    {
        var release = await _protocol.ReleaseAsync(
            lease.LeaseId, lease.Generation, RunnerDisposition.Failed,
            detail: null, credentialFailure: failure, cancellationToken);

        if (release is ReleaseResult.Released)
        {
            _observer.Released(lease.LeaseId, RunnerDisposition.Failed);
        }
        else
        {
            _observer.Fenced(lease.LeaseId);
        }
    }

    /// <summary>
    /// Materialize, extract, digest, filter, emit - then hold the lease.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The facts are shipped BEFORE the hold rather than after it. A runner
    /// that gathered evidence and then sat on it until the work finished would
    /// lose all of it to the crash that the work caused, which is exactly the
    /// flight somebody needs the evidence for.
    /// </para>
    /// <para>
    /// A workspace that cannot be prepared ends the flight with a diagnosis. A
    /// declared capability gap is answerable - the flight asked for something
    /// this runner cannot serve - and a stalled flight is not.
    /// </para>
    /// </remarks>
    private async Task WorkAsync(
        string runnerId,
        IReadOnlyList<string> labels,
        LeaseGranted lease,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken)
    {
        WorkspaceResult workspace;
        try
        {
            workspace = await _workspace.PrepareAsync(
                lease.FlightId, lease.Repos, secretsByLocator, cancellationToken);
        }
        catch (Exception failure) when (failure is VcsCapabilityException or InvalidOperationException)
        {
            _observer.WorkspaceFailed(failure.Message);
            await ReleaseAsync(lease, RunnerDisposition.Failed, failure.Message, cancellationToken);
            return;
        }

        foreach (var tree in workspace.Trees)
        {
            // The commit and the size. Never a path inside the tree, and never
            // a byte of what is in it.
            _observer.Materialized(tree.Slug, tree.HeadCommit, tree.Bytes);
        }

        await ShipAsync(lease, workspace, cancellationToken);

        await HoldAsync(runnerId, labels, lease, cancellationToken);
    }

    /// <summary>
    /// The three stages, in the only order the types allow.
    /// </summary>
    /// <remarks>
    /// Digest before filter, filter before egress. Written here as three
    /// statements because that is all it can be: <c>Filter</c> takes what only
    /// <c>Digest</c> produces, and what ships takes what only <c>Filter</c>
    /// produces.
    /// </remarks>
    private async Task ShipAsync(
        LeaseGranted lease, WorkspaceResult workspace, CancellationToken cancellationToken)
    {
        var payloads = new List<FactPayload>
        {
            new FactPayload.Environment(EnvironmentSurvey.Observe(
                // The first tree, when there is one: lock files are a property
                // of what was checked out, and with no repository there is
                // nothing to hash and the fact is about the machine alone.
                workspace.Trees.Count > 0 ? workspace.Trees[0].Path : null,
                workspace.Reused ? EnvironmentProvenance.Reused : EnvironmentProvenance.Fresh)),
        };

        foreach (var tree in workspace.Trees)
        {
            // What changed, when the flight named a base to measure from. The
            // tenant's rules classify every path here, on this machine, before
            // the filter decides which of them may cross.
            if (ChangeExtractor.Extract(tree, lease.ClassificationRules) is { } manifest)
            {
                payloads.Add(new FactPayload.Change(manifest));
            }

            payloads.Add(new FactPayload.Source(new SourceProvenance
            {
                Provider = lease.Repos.First(r => r.Slug == tree.Slug).Provider,
                Slug = tree.Slug,
                RequestedRef = tree.RequestedRef,
                ResolvedRef = tree.ResolvedRef,
                HeadCommit = tree.HeadCommit,
                HeadIsFork = tree.HeadIsFork,
                ForkSlug = tree.ForkSlug,
                FileCount = tree.FileCount,
                Bytes = tree.Bytes,
            }));
        }

        var digested = FactPipeline.Digest(new GatheredFacts(payloads), lease.FlightId, _clock.UtcNow);
        var filtered = FactPipeline.Filter(digested, lease.ClassificationCeiling);

        await _protocol.ShipFactsAsync(lease.LeaseId, lease.Generation, filtered, cancellationToken);
        _observer.FactsShipped(filtered.Items.Count);
    }

    /// <summary>Gives the lease back with a disposition, and narrates the outcome.</summary>
    private async Task ReleaseAsync(
        LeaseGranted lease, string disposition, string? detail, CancellationToken cancellationToken)
    {
        var release = await _protocol.ReleaseAsync(
            lease.LeaseId, lease.Generation, disposition, detail, credentialFailure: null, cancellationToken);

        if (release is ReleaseResult.Released)
        {
            _observer.Released(lease.LeaseId, disposition);
        }
        else
        {
            _observer.Fenced(lease.LeaseId);
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
            lease.LeaseId, lease.Generation, RunnerDisposition.Completed,
            detail: null, credentialFailure: null, cancellationToken);

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
