using System.Net;
using Gg.Contracts;
using Gg.Runner.Execution;
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

    /// <summary>
    /// A session's probe found the bound broken: the machine changed under a
    /// live runner, and it is stopping.
    /// </summary>
    void BoundBroken(string diagnosis);

    /// <summary>
    /// The control plane refused or could not be reached, and the runner is
    /// going to ask again in <paramref name="retryIn"/>.
    /// </summary>
    /// <remarks>
    /// <b>Because surviving quietly is the worse bug.</b> The crash this
    /// replaces at least left a stack trace on somebody's console. A runner
    /// that retried in silence would look idle from both ends - it has nothing
    /// to do, and the control plane has nobody asking - which is the outage
    /// nobody finds.
    /// </remarks>
    void ControlPlaneRefused(string diagnosis, TimeSpan retryIn);

    void Idle();

    /// <summary>
    /// A person has withheld this machine from claiming.
    /// </summary>
    /// <remarks>
    /// <b>Separate from <see cref="Idle"/>, and that separation is the whole
    /// point of the state.</b> An idle fleet and a machine somebody took out of
    /// service both take no work; only one of them is something a person did on
    /// purpose, and only one of them is cleared by a person. Reporting parking
    /// through <see cref="Idle"/> would print "nothing ready" on a withheld
    /// machine — the line an operator reads for a fortnight while wondering why
    /// nothing runs there.
    /// </remarks>
    void Parked();

    /// <summary>
    /// A flight is ready and its lease cannot be completed yet.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Idle"/> because they used to be the same 204,
    /// and the difference is the whole reason the claim became a request: an
    /// idle fleet needs nobody, and a waiting one needs somebody to register a
    /// credential. The repositories by name, because that names the action.
    /// </remarks>
    void Waiting(IReadOnlyList<string> repos);

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
    /// A loop ran, and this is how it ended.
    /// </summary>
    /// <remarks>
    /// The outcome, the attempts and the moves it reached for - never a line of
    /// what the agent produced. This goes to stdout, and stdout is what a
    /// customer pastes into a ticket.
    /// </remarks>
    void LoopFinished(string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed);

    /// <summary>
    /// A loop wanted something its envelope does not declare, and this says what
    /// to add and where.
    /// </summary>
    /// <remarks>
    /// The move and the envelope path, never a line of what the agent was trying
    /// to do with it. This goes to stdout, and stdout is what a customer pastes
    /// into a ticket - which is exactly who needs to know an envelope is one word
    /// short.
    /// </remarks>
    void MoveRefused(string diagnosis);

    /// <summary>
    /// The work landed somewhere, or was refused, and this says which.
    /// </summary>
    /// <remarks>
    /// The branch and the proposal - never a line of what was in them. This
    /// goes to stdout, and stdout is what a customer pastes into a ticket.
    /// </remarks>
    void Landed(string outcome, string detail);

    /// <summary>
    /// A flight's tree was kept for somebody to take over.
    /// </summary>
    /// <remarks>
    /// The size is here because this is the first resource this product spends
    /// in a customer's environment deliberately, and a retention policy nobody
    /// has a number for is a guess.
    /// </remarks>
    /// <param name="preserved">
    /// Whether the work also reached a handoff branch, making this tree a cache
    /// rather than the only copy.
    /// </param>
    void Held(string flightNumber, string path, long bytes, bool preserved = false);

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
    public void BoundBroken(string diagnosis) { }
    public void ControlPlaneRefused(string diagnosis, TimeSpan retryIn) { }

    public void Idle() { }

    public void Parked() { }
    public void Waiting(IReadOnlyList<string> repos) { }
    public void CredentialUnresolved(CredentialResolutionFailure failure) { }
    public void Materialized(string slug, string headCommit, long bytes) { }
    public void WorkspaceFailed(string diagnosis) { }
    public void FactsShipped(int count) { }
    public void LoopFinished(string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed) { }
    public void MoveRefused(string diagnosis) { }
    public void Landed(string outcome, string detail) { }
    public void Held(string flightNumber, string path, long bytes, bool preserved = false) { }
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
    IWorkspace workspace,
    IExecutorPort? executor = null,
    IReadOnlyList<Execution.IntentReader>? readers = null,
    IReadOnlyList<Vcs.HostDeclaration>? hosts = null,
    TranscriptStore? transcripts = null,
    IReadOnlyList<IDestinationAdapter>? destinations = null)
{
    /// <summary>Seconds the control plane may hold a claim open.</summary>
    public const int ClaimWaitSeconds = 30;

    /// <summary>
    /// How long a claimed lease is held before it is released.
    /// </summary>
    /// <remarks>
    /// <b>Read unqualified, inside this type</b>, at <c>_clock.UtcNow + HoldFor</c>.
    /// Slice twenty's unread scan reported it as read by nothing and it was one
    /// edit away from being deleted — the scan matches <c>.Member</c> and cannot
    /// see a member used without a receiver in its own declaring type. That is a
    /// false-positive class, it is asserted in the scan's own tests now, and this
    /// comment is here because the next person to read a finding about this
    /// member deserves to know it was already wrong once.
    /// </remarks>
    public TimeSpan HoldFor { get; init; } = TimeSpan.FromSeconds(10);

    private readonly IRunnerProtocol _protocol = protocol;

    /// <summary>When this runner next owes the control plane a heartbeat.</summary>
    /// <remarks>
    /// <b>Idle is the state that matters.</b> The beat used to be sent only from
    /// <c>HoldAsync</c>, so a runner reported being alive exactly while it was
    /// busy. Status is derived control-plane-side from heartbeat age, so an idle
    /// runner decayed to offline while polling happily - and a capability-gated
    /// flight is only offered to a live runner, which it then never became.
    /// </remarks>
    private DateTimeOffset _nextBeatDue = DateTimeOffset.MinValue;

    /// <summary>How long to wait before asking again. Zero while things are well.</summary>
    private TimeSpan _backoff = TimeSpan.Zero;

    /// <summary>Says this process is alive, no more often than asked.</summary>
    /// <remarks>
    /// The interval is the control plane's, read from its own answer, never a
    /// constant here - it is derived from the staleness threshold and only that
    /// side knows it. Called from the idle loop AND from inside the claim's long
    /// poll, because the poll blocks for longer than the interval: beating only
    /// between polls would drift toward stale on a runner that is working
    /// correctly.
    /// </remarks>
    private async Task BeatIfDueAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        if (_clock.UtcNow < _nextBeatDue)
        {
            return;
        }

        var beat = await _protocol.HeartbeatAsync(runnerId, labels, cancellationToken);
        _nextBeatDue = _clock.UtcNow + TimeSpan.FromSeconds(beat.NextHeartbeatSeconds);
    }
    private readonly IClock _clock = clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay;
    private readonly IRunnerObserver _observer = observer;
    private readonly ICredentialResolver _credentials = credentials;
    private readonly IWorkspace _workspace = workspace;

    /// <summary>
    /// The runner's missing verb, when this runner has one.
    /// </summary>
    /// <remarks>
    /// Optional, and null is a real state rather than a degraded one: a runner
    /// with no executor does exactly what every runner did before this step -
    /// materialize, extract, ship. Most flights in slice one had no envelope at
    /// all, so a loop is something a flight HAS rather than something a runner
    /// requires.
    /// </remarks>
    private readonly IExecutorPort? _executor = executor;

    /// <summary>
    /// Which trackers this runner can read a work item in.
    /// </summary>
    /// <remarks>
    /// Read HERE rather than only inside the executor, because the decision is
    /// whether to invoke at all: an agent handed a work item it has no tool for
    /// spends the loop's whole budget establishing that, and reports it as prose
    /// somebody has to interpret.
    /// </remarks>
    private readonly IReadOnlyList<Execution.IntentReader> _readers = readers ?? [];

    /// <summary>
    /// Which provider key reaches which host, as this runner declares it.
    /// </summary>
    /// <remarks>
    /// <b>The mapping the control plane must not hold.</b> A policy store
    /// containing hosts would make credential destination a policy edit, so the
    /// registry names a provider KEY and this maps it. Two things read it here:
    /// whether a flight's link comes from somewhere this runner serves, and
    /// which tracker can read a link-shaped work item.
    /// </remarks>
    private readonly IReadOnlyList<Vcs.HostDeclaration> _hosts = hosts ?? [];

    private readonly TranscriptStore _transcripts = transcripts ?? new TranscriptStore();

    /// <summary>
    /// Where this runner can land work, when it is admitted to.
    /// </summary>
    /// <remarks>
    /// Empty is the ordinary state and not a degraded one: a runner nobody has
    /// configured to write cannot write, which is the slice-one property
    /// surviving as the default.
    /// </remarks>
    private readonly IReadOnlyList<IDestinationAdapter> _destinations = destinations ?? [];

    /// <summary>
    /// Flights whose work reached a remote, so their tree is finished with.
    /// </summary>
    /// <remarks>
    /// Recorded from what LANDING returned rather than from the admission: a
    /// flight admitted to land and then refused at the credential has nothing on
    /// a remote, and is exactly as takeable as one that was never admitted.
    /// </remarks>
    private readonly HashSet<string> _landed = new(StringComparer.Ordinal);

    /// <summary>Runs until cancelled.</summary>
    public async Task<int> RunAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_boundBroke)
            {

                // BEFORE asking, so a runner that never gets work still reports
                // being alive. This is the call whose absence made an idle
                // runner invisible.
                await BeatIfDueAsync(runnerId, labels, cancellationToken);

                ClaimResult claim;
                try
                {
                    claim = await AskForWorkAsync(runnerId, labels, cancellationToken);
                }
                catch (HttpRequestException refusal) when (TransientFailure.IsTransient(refusal))
                {
                    // THE CONTROL PLANE'S PROBLEM, NOT THIS RUNNER'S. A deploy,
                    // a restart, a cold start or a database blip - all of them
                    // pass, and none of them is a reason to remove this machine
                    // from the fleet until somebody notices.
                    //
                    // Said out loud, then waited out. The backoff is bounded
                    // because a runner that backs off forever is the outage it
                    // was meant to prevent, wearing a longer timer.
                    _backoff = TransientFailure.Next(_backoff);

                    _observer.ControlPlaneRefused(
                        TransientFailure.Diagnose(refusal, _backoff), _backoff);

                    await _delay(_backoff, cancellationToken);
                    continue;
                }

                // A SERVED CLAIM CLEARS IT, so an hour of health does not
                // inherit a bad minute's wait.
                _backoff = TimeSpan.Zero;

                if (claim is not ClaimResult.Granted(var lease))
                {
                    // Every wait this took was one the control plane asked for,
                    // inside AskForWorkAsync. Going round again here makes a new
                    // request rather than re-reading a finished one.
                    // Waiting was already said, once per poll, by the ask
                    // itself. Saying it again here would double every line a
                    // person watching this runner reads.
                    if (claim is ClaimResult.Parked)
                    {
                        _observer.Parked();
                    }
                    else if (claim is not ClaimResult.Waiting)
                    {
                        _observer.Idle();
                    }

                    continue;
                }

                // NAMED BY THE CONTROL PLANE, refused here, and BEFORE the
                // workspace is asked for anything. Cloning what we have no
                // credential for used to go out anonymously - a public
                // repository worked and a private one failed later at git's own
                // words, with nothing pointing at the missing credential.
                if (lease.UnresolvedRepos.Count > 0)
                {
                    _observer.Claimed(lease);
                    var missing =
                        "This flight names repositories the control plane holds no credential "
                      + $"reference for: {string.Join(", ", lease.UnresolvedRepos)}. Register one "
                      + "with `gg credential add` and fly it again. Fetching without one either "
                      + "fails at the forge with nothing pointing at the cause or, worse, quietly "
                      + "succeeds against a public copy of something that was meant to be read "
                      + "with a credential.";

                    _observer.WorkspaceFailed(missing);
                    await ReleaseAsync(lease, RunnerDisposition.Failed, missing, cancellationToken);
                    continue;
                }

                _observer.Claimed(lease);

                // Lease, THEN resolve credentials, then everything else. The
                // order is load-bearing and this is the second step of it; a
                // credential that cannot be read stops the flight here, before
                // anything is materialized, rather than halfway through.
                // AND THE LINK COMES FROM SOMEWHERE THIS RUNNER SERVES, checked
                // in the same place and for the same reason: a link resolved to
                // a registered repository by PATH ALONE may name a repository on
                // a host nobody declared, and the control plane cannot tell -
                // it holds no host, deliberately. This is the only layer that
                // can, and it must answer before any source is fetched.
                if (lease.Repos.Select(r => Vcs.HostDeclaration.Unserved(
                        r.Provider, lease.IntentUri, _hosts))
                    .FirstOrDefault(why => why is not null) is { } unserved)
                {
                    _observer.WorkspaceFailed(unserved);
                    await ReleaseAsync(
                        lease, RunnerDisposition.Failed, unserved, cancellationToken);
                    continue;
                }

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
                    // A flight that LANDED has a branch and a proposal, so its
                    // work is somewhere a person can fetch and the tree is
                    // finished with. One that did not has neither, and the work
                    // exists only here - which is exactly the flight somebody
                    // takes over, and exactly the one that used to have nothing
                    // left to take.
                    //
                    // Held by MOVING it to a root of its own, so the startup
                    // sweep keeps its one good property: every tree under the
                    // working root belongs to a process that is gone, a rule
                    // with no state behind it and therefore no way to be wrong.
                    if (_landed.Contains(lease.FlightId))
                    {
                        _workspace.Release(lease.FlightId);
                    }
                    else if (_workspace.Hold(lease.FlightId) is { } held)
                    {
                        // PRESERVED SAYS THE TREE IS A CACHE. This slice's first
                        // danger is two trees, one branch, and the loser silent:
                        // after the work reaches a handoff branch this machine still
                        // holds a copy, and nothing had told it so. The line a person
                        // reads is where that is said, because there is no reader for
                        // a staleness flag and a value nothing consults is not a
                        // warning.
                        _observer.Held(
                            lease.FlightNumber, held.Path, held.Bytes,
                            _preserved.Contains(lease.FlightId));
                    }

                    _landed.Remove(lease.FlightId);
                    _preserved.Remove(lease.FlightId);
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

        // The startup refusal's own exit, for the same finding mid-life: a
        // broken bound is a property of the machine, not of the lease, and a
        // runner that kept claiming would fly ungoverned flights on it.
        return _boundBroke ? 69 : 0;
    }

    /// <summary>Whether a session's probe found the bound broken.</summary>
    private bool _boundBroke;

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
    /// <summary>
    /// Makes a request and asks about it until it settles or its window passes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rate limiter changed hands here.</b> The claim used to be a long
    /// poll: the control plane held the request open for
    /// <see cref="ClaimWaitSeconds"/>, so a runner going straight round again
    /// was polling rather than spinning. Nothing holds it open now, and this
    /// runner has no backoff of its own - so the interval the control plane
    /// sends is the entire thing standing between an idle fleet and a busy loop,
    /// and it is honoured rather than adjusted.
    /// </para>
    /// <para>
    /// <b>Bounded by the same window the request has.</b> The runner stops
    /// asking when its own patience runs out and makes a fresh request, which is
    /// what keeps a heartbeat flowing and what stops an abandoned request from
    /// being polled forever.
    /// </para>
    /// </remarks>
    private async Task<ClaimResult> AskForWorkAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        var acceptance = await _protocol.RequestClaimAsync(
            runnerId, labels, ClaimWaitSeconds, cancellationToken);

        if (acceptance is ClaimAcceptance.Inline(var answered))
        {
            return answered;
        }

        var accepted = (ClaimAcceptance.Accepted)acceptance;
        var giveUpAt = _clock.UtcNow + TimeSpan.FromSeconds(ClaimWaitSeconds);
        ClaimResult latest = new ClaimResult.Nothing();

        while (!cancellationToken.IsCancellationRequested && _clock.UtcNow < giveUpAt)
        {
            await _delay(accepted.PollAfter, cancellationToken);

            // The long poll runs longer than the heartbeat interval, so a beat
            // owed during the wait is owed here rather than after it.
            await BeatIfDueAsync(runnerId, labels, cancellationToken);

            latest = await _protocol.ReadClaimAsync(accepted.RequestId, cancellationToken);

            // Granted and expired are terminal, and expired especially: polling
            // a request the control plane has finished with is polling something
            // that will never change again.
            // PARKED JOINS THEM, because it will not change inside this
            // window without a person acting: continuing to poll a withheld
            // machine's request spends the whole claim wait learning the same
            // answer. The runner asks again on the next round, exactly as it
            // does for pending.
            if (latest is ClaimResult.Granted or ClaimResult.Expired or ClaimResult.Parked)
            {
                return latest;
            }

            // WAITING IS SAID EVERY TIME IT IS READ, not once. A person watching
            // a runner needs to see that it is still blocked and on what; a
            // single line at the start of a long wait reads as a runner that
            // stopped.
            if (latest is ClaimResult.Waiting(var blocked))
            {
                _observer.Waiting(blocked);
            }
        }

        return latest;
    }

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
            // NAMES THE CAPABILITY, which the exception has carried since it was
            // written and no diagnosis has ever shown. Its own doc says it
            // "names the CAPABILITY as well as the sentence, so a diagnosis
            // points at the declaration rather than at a symptom" - true of the
            // type and, until now, of nothing a person ever read.
            var diagnosis = failure is VcsCapabilityException capability
                ? $"{capability.Capability}: {failure.Message}"
                : failure.Message;

            _observer.WorkspaceFailed(diagnosis);
            await ReleaseAsync(lease, RunnerDisposition.Failed, diagnosis, cancellationToken);
            return;
        }

        foreach (var tree in workspace.Trees)
        {
            // The commit and the size. Never a path inside the tree, and never
            // a byte of what is in it.
            _observer.Materialized(tree.Slug, tree.HeadCommit, tree.Bytes);
        }

        // THE MISSING VERB, here and only here: lease -> resolve -> materialize
        // -> INVOKE -> extract -> digest -> filter -> emit. The order matters
        // for a new reason now - the manifest is extracted AFTER the agent has
        // worked, so what ships is a measurement of the agent's own edits
        // rather than of the tree it was handed.
        //
        // RENEWED WHILE IT WORKS, because the work is the one time the lease
        // actually needs to stay alive. A lease lasts sixty seconds and the
        // agent may hold this await for minutes; renewing only while HOLDING
        // - which is what this loop did - meant the first real flight's facts
        // were refused as fenced, and the unhandled refusal killed the
        // process. A fence answered mid-work means the flight is somebody
        // else's now: the work is cancelled, nothing ships, and the loop goes
        // back to claiming.
        var (probe, run, lost) = await InvokeRenewingAsync(lease, workspace, cancellationToken);
        if (lost)
        {
            _observer.Fenced(lease.LeaseId);
            return;
        }

        // THE SESSION'S OWN PROBE SAID NO. The lease goes back with the
        // diagnosis - the named halt - and NO facts ship: a fact set for a
        // session that never ran would be evidence of a flight that did not
        // fly. The runner then stops taking work, because a broken bound is a
        // property of the machine rather than of the lease, and the next claim
        // would fly ungoverned on the same machine.
        if (probe is { Bound: false })
        {
            _observer.BoundBroken(probe.Diagnosis);
            await ReleaseAsync(lease, RunnerDisposition.Failed, probe.Diagnosis, cancellationToken);
            _boundBroke = true;
            return;
        }

        // THE TREE IS HELD ACROSS THIS ROUND TRIP, and that is new. Until this
        // step the tree died as soon as facts were shipped; the landing
        // decision depends on the facts that just arrived, so the runner has to
        // still be holding what it would push when the answer comes back. Said
        // out loud because it is the mechanism rather than an incidental
        // consequence of where the release happens to sit.
        await ShipAsync(lease, workspace, run, probe, cancellationToken);

        // AND THEN IT ASKS, because shipping is accepted rather than answered.
        // The control plane records the batch and evaluates afterwards, so the
        // decision arrives on a route of its own - and the tree is still held
        // while the runner waits for it.
        var decision = await AwaitLandingAsync(lease, cancellationToken);

        await LandAsync(lease, workspace, decision, secretsByLocator, cancellationToken);

        await HoldAsync(runnerId, labels, lease, cancellationToken);
    }

    /// <summary>
    /// The loop's invocation with the lease kept alive under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Renewal is decided against the control plane's expiry, the same rule
    /// as <see cref="HoldAsync"/> - and an expiry the control plane has put
    /// effectively out of reach means there is nothing left to keep alive, so
    /// the keeper stands down rather than spinning toward it.
    /// </para>
    /// <para>
    /// Returns <c>lost: true</c> when a renewal was fenced: the agent's token
    /// is cancelled, its cancellation is absorbed here, and the caller ships
    /// nothing - a fact batch on a dead generation is a refusal, and it was
    /// an unhandled one once.
    /// </para>
    /// </remarks>
    private async Task<(Execution.ProbeResult? Probe, ExecutorRun? Run, bool Lost)>
        InvokeRenewingAsync(
            LeaseGranted lease, WorkspaceResult workspace, CancellationToken cancellationToken)
    {
        using var working = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var work = ProbeThenInvokeAsync(lease, workspace, working.Token);
        var expiresAt = lease.ExpiresAt;
        var fenced = false;

        while (!work.IsCompleted)
        {
            if (expiresAt - _clock.UtcNow > TimeSpan.FromDays(1))
            {
                break;
            }

            var renewAt = expiresAt - TimeSpan.FromSeconds(lease.RenewWithinSeconds);
            if (_clock.UtcNow >= renewAt)
            {
                switch (await _protocol.RenewAsync(lease.LeaseId, lease.Generation, cancellationToken))
                {
                    case RenewResult.Renewed renewed:
                        expiresAt = renewed.ExpiresAt;
                        _observer.Renewed(lease.LeaseId, expiresAt);
                        continue;

                    case RenewResult.Fenced:
                        fenced = true;
                        await working.CancelAsync();
                        break;
                }

                break;
            }

            // Wake at the renew window or when the work ends, whichever is
            // first. The delay is the injected one, so a test's clock moves
            // and a real one waits.
            await Task.WhenAny(work, _delay(renewAt - _clock.UtcNow, working.Token));
        }

        try
        {
            var (probe, run) = await work;
            return (probe, run, false);
        }
        catch (OperationCanceledException) when (fenced && !cancellationToken.IsCancellationRequested)
        {
            return (null, null, true);
        }
    }

    /// <summary>
    /// The session's probe, then the session - in that order, under the same
    /// renewal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Per session, because ambient settings act on the session.</b> A
    /// measurement taken at startup measures the machine as it was before this
    /// session existed: step 0's re-measurement gave the ambient family its
    /// fifth member (--permission-mode acceptEdits defeats the bound even with
    /// setting sources cleared), and any of them can arrive between a runner's
    /// startup and its tenth flight. The tax is the probe's measured cost -
    /// 15 to 21 seconds and one small request, in front of a session that
    /// spends minutes of agent time - and what it buys is the product's only
    /// claim: that the measurement measures the session it governs.
    /// </para>
    /// <para>
    /// A loop never outlives its invocation today; the day resume changes
    /// that, the probe moves with it - per loop, as the slice doc records.
    /// </para>
    /// </remarks>
    private async Task<(Execution.ProbeResult? Probe, ExecutorRun? Run)> ProbeThenInvokeAsync(
        LeaseGranted lease, WorkspaceResult workspace, CancellationToken cancellationToken)
    {
        if (_executor is null || lease.Loop is null)
        {
            // Nothing to govern: no agent will be invoked, so there is no
            // session for a bound to hold over.
            return (null, await InvokeAsync(lease, workspace, cancellationToken));
        }

        // ARTICLE XI, BEFORE ANYTHING IS SPENT - including the probe, which is
        // itself an agent invocation. A flight about a work item in a tracker
        // this runner cannot read is refused with a reason rather than handed to
        // an agent that will establish the same thing slowly and report it as
        // prose. "A provider nobody configured is a declared capability gap" -
        // the words this repository already has for this.
        if (Execution.IntentConfiguration.Unreadable(lease.IntentProvider, _readers)
            is { } unreadable)
        {
            return (null, ExecutorRun.Failed(
                lease.Loop.LoopId, unreadable, attempts: 1, took: TimeSpan.Zero, movesUsed: []));
        }

        // THE SAME SHAPE, ONE MOVE OVER. A loop that declares `propose` on a
        // runner that cannot name its own executable cannot be served the tool
        // that move grants - so it is refused here rather than handed to an
        // agent that will find the tool missing and report it as prose. The
        // process fact is read once, so this asks the same question the launch
        // will ask and gets the same answer.
        if (Execution.NominationTool.Unservable(
            lease.Loop.Moves, Execution.SelfInvocation.Current) is { } unservable)
        {
            return (null, ExecutorRun.Failed(
                lease.Loop.LoopId, unservable, attempts: 1, took: TimeSpan.Zero, movesUsed: []));
        }

        var probe = await Execution.MoveBoundProbe.RunAsync(_executor, cancellationToken);
        if (!probe.Bound)
        {
            return (probe, null);
        }

        return (probe, await InvokeAsync(lease, workspace, cancellationToken));
    }

    /// <summary>
    /// Runs the flight's loop, when it has one and this runner can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing happens when the flight declares no loop, and nothing happens
    /// when this runner has no executor. Neither is a failure: the first is a
    /// flight nothing governs, and the second is a runner that only observes.
    /// </para>
    /// <para>
    /// The agent works in the FIRST tree. One repository per loop this slice;
    /// a multi-repo flight running one loop has no answer to which tree is
    /// "the" working directory, and inventing one here would be the second
    /// loop arriving early.
    /// </para>
    /// </remarks>
    private async Task<ExecutorRun?> InvokeAsync(
        LeaseGranted lease, WorkspaceResult workspace, CancellationToken cancellationToken)
    {
        // NO LONGER GATED ON HAVING CLONED SOMETHING. A ticket, a text intent
        // and an issue link all resolve to no repository - correctly - and
        // `Trees.Count == 0` here meant all three were leased, claimed, and
        // silently never worked.
        // NAMES WORK, rather than names a URI. A ticket is a provider and an id
        // and carries no uri, so requiring one here meant every work-item flight
        // was leased, cloned, and returned with no agent invoked and nothing
        // said - the same silence the tree check above was fixed for, arriving
        // one condition later.
        if (_executor is null || lease.Loop is not { } loop
            || !Execution.ExecutorRequest.NamesWork(
                lease.IntentUri, lease.IntentProvider, lease.IntentId))
        {
            return null;
        }

        var run = await _executor.ExecuteAsync(
            new ExecutorRequest
            {
                // ALWAYS, because this side cannot know whether anybody is
                // watching: the console is a different process with no channel
                // to here but the filesystem. The field's own remark carries the
                // decision and what it costs.
                Live = new LiveStream(Gg.Local.LocalPaths.LiveView(lease.FlightId)),
                // PASSED THROUGH, never interpreted. The runner does not read the reason
                // and does not derive anything from it: it hands the agent what a person
                // said and lets the envelope keep deciding what may happen.
                Feedback = lease.Feedback,
                // THE SAME DISPOSITION. Already rendered by the contract; the runner
                // hands it over and the prompt says whose words it holds.
                ResumesFrom = loop.ResumesFrom,
                // The first tree when there is one, and the flight's own
                // directory when there is not. See WorkspaceResult.Root.
                WorkingDirectory = workspace.Trees.Count > 0
                    ? workspace.Trees[0].Path
                    : workspace.Root,
                LoopId = loop.LoopId,
                IntentUri = lease.IntentUri,
                // A TICKET SAYS ITS PROVIDER; A LINK DOES NOT, so a link is
                // asked of the host declarations. This is what gives a
                // work-item URL a tracker tool - the reader is keyed on a
                // provider, and without this such a flight reaches the agent
                // with nothing able to read what it is about. It does not change
                // what the flight is RECORDED as, and the prompt still names the
                // link, because the person named a link.
                IntentProvider = lease.IntentProvider
                    ?? Vcs.HostDeclaration.ProviderFor(lease.IntentUri, _hosts),
                IntentId = lease.IntentId,
                Moves = loop.Moves,
                WallClock = TimeSpan.FromSeconds(loop.WallClockSeconds),
                TranscriptPath = _transcripts.For(lease.FlightId, loop.LoopId),
            },
            cancellationToken);

        // THE ENVELOPE'S SENTENCE, appended where the envelope is known. The
        // executor measured the stop; who the flight waits for next is policy,
        // and a constant in the factory once told every reader a person was
        // waiting on a flight the control plane was about to requeue for an
        // agent.
        if (string.Equals(run.Outcome, LoopOutcomes.Exhausted, StringComparison.Ordinal))
        {
            run = run with
            {
                Reason = run.Reason + " " + (string.Equals(
                        loop.OnExhaustion, ExhaustionPolicies.HandoffToAgent, StringComparison.Ordinal)
                    ? "This flight is queued for another agent."
                    : "This flight is waiting for a person."),
            };
        }

        _observer.LoopFinished(run.LoopId, run.Outcome, run.Attempts, run.MovesUsed);

        // WHAT WAS NEEDED AND WHERE TO ADD IT. A refusal that only says no teaches
        // people to want a rejection reason that can widen an envelope, and that
        // was refused on governance grounds - so the way out has to be a sentence
        // somebody can act on through the envelope itself.
        if (run.Digest is { } digest
            && MoveRefusal.Diagnose(digest.RefusedMoves, loop.LoopId) is { } refusal)
        {
            _observer.MoveRefused(refusal);
        }

        return run;
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
        LeaseGranted lease, WorkspaceResult workspace, ExecutorRun? run,
        Execution.ProbeResult? probe, CancellationToken cancellationToken)
    {
        var payloads = new List<FactPayload>
        {
            new FactPayload.Environment(EnvironmentSurvey.Observe(
                // The first tree, when there is one: lock files are a property
                // of what was checked out, and with no repository there is
                // nothing to hash and the fact is about the machine alone.
                workspace.Trees.Count > 0 ? workspace.Trees[0].Path : null,
                workspace.Reused ? EnvironmentProvenance.Reused : EnvironmentProvenance.Fresh,
                probe: probe)),
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

        if (run is not null)
        {
            // What the loop did, and where its transcript is. Two facts rather
            // than one: the outcome is short and decidable and somebody reads
            // it first; the transcript is enormous and customer-adjacent, so
            // what crosses is a hash and a locator.
            payloads.Add(new FactPayload.Loop(run.ToFact(lease.Loop!.Executor)));

            if (run.Transcript is { } transcript)
            {
                payloads.Add(new FactPayload.Transcript(transcript));
            }

            // And what the transcript SAID, extracted. The reference above only
            // resolves on this machine, so without this a person on the other
            // side has a hash and a path they cannot follow.
            if (run.Digest is { } summary)
            {
                payloads.Add(new FactPayload.Digest(summary));
            }

            // AND WHAT IT ASKED FOR, when it asked. Null is the ordinary state:
            // only a classifying loop nominates, and one that could not decide
            // nominates nothing - which is a real answer that admission reads
            // as "open no flight", not a fact that failed to arrive.
            if (run.Question is { } question)
            {
                payloads.Add(new FactPayload.Question(question));
            }

            if (run.Nomination is { } nomination)
            {
                payloads.Add(new FactPayload.Nomination(nomination));
            }
        }

        // STRIPPED BEFORE THE DIGEST, which the types enforce: Digest takes
        // what only Clean produces. The hash is computed over the fact as
        // produced, so cleaning it later would make the stored bytes disagree
        // with the hash that proves what they were.
        var digested = FactPipeline.Digest(
            FactHygiene.Clean(new GatheredFacts(payloads)), lease.FlightId, _clock.UtcNow);
        var filtered = FactPipeline.Filter(digested, lease.ClassificationCeiling);

        await _protocol.ShipFactsAsync(
            lease.LeaseId, lease.Generation, filtered, cancellationToken);
        _observer.FactsShipped(filtered.Items.Count);
    }

    /// <summary>
    /// Waits for the control plane to have an answer about landing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Settled is not the answer, it is whether there is one.</b> Both
    /// permissions are refused by being absent, which is only safe while absence
    /// means the control plane said no. Once the evaluation happens after the
    /// request returns, absence also means it has not looked yet - and a runner
    /// reading those as one would stop pushing work that was going to be
    /// admitted, silently and with nothing to see.
    /// </para>
    /// <para>
    /// <b>Bounded, and refusal on exhaustion.</b> A control plane that never
    /// settles must not strand a runner holding a tree forever; giving up and
    /// treating it as unsettled means nothing is pushed, which is the same
    /// direction every other absence here fails in.
    /// </para>
    /// </remarks>
    private async Task<LandingDecision?> AwaitLandingAsync(
        LeaseGranted lease, CancellationToken cancellationToken)
    {
        var deadline = _clock.UtcNow + LandingPatience;

        while (_clock.UtcNow < deadline)
        {
            var decision = await _protocol.ReadAdmissionAsync(lease.LeaseId, cancellationToken);

            if (decision.Settled)
            {
                return decision;
            }

            await _delay(LandingPoll, cancellationToken);
        }

        _observer.Landed("refused",
            "the control plane did not settle this flight's landing in "
          + $"{LandingPatience.TotalSeconds:0}s, so nothing was pushed");

        return null;
    }

    /// <summary>How long a runner holds its tree waiting for a landing decision.</summary>
    private static readonly TimeSpan LandingPatience = TimeSpan.FromMinutes(5);

    /// <summary>How often it asks while it waits.</summary>
    private static readonly TimeSpan LandingPoll = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Lands the work, if and only if the control plane said to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The decision arrives or nothing is pushed.</b> This runner can see the
    /// facts it produced and could work out an obligation for itself; one that
    /// did would be deciding, and a patched one would decide differently. So it
    /// acts on a decision rather than on the inputs to one - Article IX, at the
    /// only place in this binary where it could be broken profitably.
    /// </para>
    /// <para>
    /// <b>Two controls, and this method needs both.</b> The admission is the
    /// envelope's permission; the credential's write scope is the ability. A
    /// destination admitted against a read-only credential fails at the
    /// credential, with a diagnosis naming it - which is the layering model
    /// reaching across the boundary rather than a check bolted on here.
    /// </para>
    /// </remarks>
    private async Task LandAsync(
        LeaseGranted lease,
        WorkspaceResult workspace,
        LandingDecision? accepted,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken)
    {
        // TWO GATES, READ INDEPENDENTLY. The push is granted when no machine
        // obligation is violated; the proposal when every requirement is satisfied.
        // Neither is derived from the other: a runner that inferred a push from an
        // admission - or a proposal from a push - would be deciding one of them
        // itself, and this is the one place in this binary where that would pay.
        if (accepted?.Push is not { } push)
        {
            // Absent means no, for every reason at once: no destination, a violated
            // obligation, or a control plane too old to answer. Nothing is pushed
            // and nobody is asked, which is deliberate for the case where a machine
            // obligation failed AND a human obligation is pending: presenting a gate
            // on work that already failed a check spends the attention this product
            // exists to protect.
            //
            // EXPIRY CONDITION, and it must not be defended on principle later. That
            // rule is correct only because `when:` reads facts and not verdicts. The
            // canonical gate trigger in the design -
            // `when: obligations.contracts-intact == violated` - describes a gate
            // that exists PRECISELY BECAUSE a machine obligation is violated, and
            // the moment that form ships this inverts: the violation becomes the
            // reason to ask rather than the reason not to.
            return;
        }

        if (workspace.Trees.FirstOrDefault(t => t.Slug == push.Slug) is not { } tree)
        {
            _observer.Landed("refused",
                $"cleared to push {push.Slug} and this flight does not hold it");
            return;
        }

        var admission = accepted.Admission;

        // Matched by LOCATOR, which is what ties a credential to a repository -
        // derived by the contract's own rule, so gg and the control plane cannot
        // disagree about which credential belongs to which repo.
        var wanted = CredentialLocator.ForRepo(push.Slug);
        var reference = lease.Credentials.FirstOrDefault(c =>
            string.Equals(c.Locator, wanted, StringComparison.Ordinal));

        if (reference is null || !CredentialScopes.AllowWrite(reference.Scopes))
        {
            // FAILS AT THE CREDENTIAL, which is the criterion slice one wrote
            // and could never verify - there was nothing that could try.
            _observer.Landed("refused",
                $"the credential registered for {push.Slug} carries "
              + $"{(reference is null ? "no scopes at all" : string.Join(",", reference.Scopes))} "
              + "and pushing needs write. An envelope declares that a flight may land somewhere; "
              + "it cannot grant the ability to.");
            return;
        }

        // THE TWO GRANTS DESCRIBE ONE LANDING, checked before anything is done
        // about either. They arrive from separate decisions and carry the same
        // three values; a disagreement would push one branch and propose
        // another, and the record would show a landing. Compared rather than
        // reconciled, because reconciling is the inference the comment above
        // says this binary must never make.
        if (LandingGrants.Disagreement(push, admission) is { } conflict)
        {
            _observer.Landed("refused", conflict);
            return;
        }

        var adapter = _destinations.FirstOrDefault(d =>
            d.Provider == lease.Repos.First(r => r.Slug == push.Slug).Provider);

        if (adapter is null)
        {
            _observer.Landed("refused", "this runner is not configured to land anywhere");
            return;
        }

        var request = new LandingRequest
        {
            WorkingDirectory = tree.Path,
            Slug = push.Slug,
            Branch = push.Branch,
            BaseRef = push.BaseRef,
            Title = $"{lease.FlightNumber}: {admission?.Reason ?? push.Reason}",
            Secret = secretsByLocator[reference.Locator],
        };

        // THE PUSH FIRST, ALWAYS. A proposal on a branch that is not there yet is a
        // proposal against nothing, and a gate whose work exists only in a tree that
        // is about to be released is a gate nobody can act on.
        var pushed = await adapter.PushAsync(request, cancellationToken);

        var commit = pushed switch
        {
            PushOutcome.Pushed(_, var sha) => sha,
            // ALREADY THERE is the crash-recovery case and still a reference: a
            // runner that pushed, died and came back must not lose it.
            PushOutcome.AlreadyThere(_, var sha) => sha,
            _ => null,
        };

        if (commit is null)
        {
            // THE SAFETY PROPERTY. Nothing is reported and the tree is NOT released,
            // because the only copy of the work is here - entering a pending
            // decision with the work in a doomed tree loses it. `_landed` stays
            // unset, so the finally block holds the tree for a takeover.
            _observer.Landed("refused", pushed switch
            {
                PushOutcome.Refused(var slug, var diagnosis) => $"{slug}: {diagnosis}",
                PushOutcome.NothingToPush(var diagnosis) => diagnosis,
                _ => "the branch was not pushed",
            });
            return;
        }

        // WHICH KIND OF PUSH THIS WAS, from what the control plane told us to push.
        // The branch is its answer - gg/handoff/GG-42 rather than gg/GG-42 - and
        // DestinationBranch.IsHandoff is in the contract so this reads it rather
        // than matching a prefix here. A runner deriving a governance answer from a
        // string is what Article IX is about.
        var preserved = DestinationBranch.IsHandoff(push.Branch);

        if (preserved)
        {
            // Remembered on the loop rather than passed down, for the same reason
            // _landed is: the block that holds the tree runs in a finally after this
            // method has returned, and cannot see its locals.
            _preserved.Add(lease.FlightId);
        }

        // KEYED ON THE PUSH, not on the proposal. The work is on the remote, so the
        // tree is finished with - and a gated flight releases its tree for the same
        // reason a landed one does. Keying this on the proposal would hold every
        // gated flight's tree forever; keying it on "we tried" would release the
        // only copy of work that failed to push.
        //
        // EXCEPT FOR A PRESERVATION, and the exception is about the transcript
        // rather than about the code. The branch is authoritative for what was
        // written; the transcript is runner-local, ArtifactScopes has one value, and
        // the seed can only ever POINT at it. Releasing this tree would destroy the
        // one artifact somebody taking the flight over might walk to another machine
        // for - and it is the flight most likely to need taking over, because it
        // failed a check.
        if (!preserved)
        {
            _landed.Add(lease.FlightId);
        }

        _observer.Landed(
            pushed is PushOutcome.AlreadyThere ? "pushed" : "pushed",
            $"{push.Branch} at {commit[..Math.Min(7, commit.Length)]}"
          + (preserved ? " (preserved for handoff; no proposal follows)" : ""));

        // REPORTED AS A PUSH, always, and before any proposal. Two events, neither
        // overwriting the other: this one says a branch reached the remote at a
        // commit, and it is true whether or not a proposal follows. A gated flight
        // produces only this, and it is what the pending decision is about.
        await ReportPushAsync(lease, push.Slug, push.Branch, commit, preserved, cancellationToken);

        if (admission is null)
        {
            // The second gate was not granted. The branch is on the remote and the
            // proposal waits on a decision, which is the whole shape of a gate.
            return;
        }

        var outcome = await adapter.ProposeAsync(request, cancellationToken);

        switch (outcome)
        {
            case LandingOutcome.Landed(var branch, var uri, var number):
                _observer.Landed("landed", $"{branch} -> {uri}");

                // Recorded because it happened. A landing nobody can trace back
                // to a flight is a branch nobody will ever delete.
                await ReportLandingAsync(
                    lease, branch, admission.DestinationId, uri, number, cancellationToken);
                break;

            case LandingOutcome.BranchExists(var existing):
                _observer.Landed("refused",
                    $"{existing} already exists on the remote and was not overwritten");
                break;

            case LandingOutcome.CredentialRefused(var locator, var diagnosis):
                _observer.Landed("refused", $"{locator}: {diagnosis}");
                break;

            case LandingOutcome.Unsupported(var diagnosis):
                _observer.Landed("refused", diagnosis);
                break;
        }
    }

    /// <summary>
    /// Reports what reached the remote, with or without a proposal.
    /// </summary>
    /// <remarks>
    /// <b>One reporter for both shapes.</b> A pushed branch with no proposal and a
    /// pushed branch with one are the same fact kind with a different disposition,
    /// and two call sites building it would eventually disagree about the commit.
    /// </remarks>
    /// <summary>
    /// Flights whose push was a preservation rather than a landing.
    /// </summary>
    /// <remarks>
    /// Beside <c>_landed</c> and for the same reason: the tree is dealt with in a
    /// finally block, after the method that knows which kind of push happened has
    /// returned. Two sets rather than one tri-state, because the questions are
    /// different - one asks whether to release the tree and the other asks what to
    /// tell a person about the one that stayed.
    /// </remarks>
    private readonly HashSet<string> _preserved = new(StringComparer.Ordinal);

    private Task ReportPushAsync(
        LeaseGranted lease,
        string slug,
        string branch,
        string commit,
        bool preserved,
        CancellationToken cancellationToken) =>
        _protocol.ShipFactsAsync(
            lease.LeaseId, lease.Generation,
            FactPipeline.Filter(
                FactPipeline.Digest(
                    FactHygiene.Clean(new GatheredFacts([new FactPayload.Push(new DestinationPushed
                    {
                        // NO DESTINATION. A push under a pending decision was cleared
                        // by the first gate and admitted nowhere, and naming a
                        // destination would be a record claiming permission nobody
                        // granted.
                        Slug = slug,
                        Branch = branch,
                        Commit = commit,

                        // WHETHER A PROPOSAL FOLLOWS. A gg/ branch with no pull
                        // request is not a proposal, and a reader counting this
                        // platform's branches has to be able to tell work that was
                        // admitted from work that was merely kept - they mean
                        // opposite things about whether anybody is expected to look
                        // at it.
                        //
                        // Null on the ordinary path rather than false, because
                        // absent is what every push before this reported and a
                        // reader of an older fact must not have to guess.
                        Preserved = preserved ? true : null,
                    })])),
                    lease.FlightId, _clock.UtcNow),
                lease.ClassificationCeiling),
            cancellationToken);

    private Task ReportLandingAsync(
        LeaseGranted lease,
        string branch,
        string destinationId,
        string uri,
        int number,
        CancellationToken cancellationToken) =>
        _protocol.ShipFactsAsync(
            lease.LeaseId, lease.Generation,
            FactPipeline.Filter(
                FactPipeline.Digest(
                    FactHygiene.Clean(new GatheredFacts([new FactPayload.Landing(new DestinationLanded
                    {
                        DestinationId = destinationId,
                        Branch = branch,
                        PullRequestUri = uri,
                        PullRequestNumber = number,
                    })])),
                    lease.FlightId, _clock.UtcNow),
                lease.ClassificationCeiling),
            cancellationToken);

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
