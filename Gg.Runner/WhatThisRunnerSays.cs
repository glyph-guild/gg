using Gg.Contracts;

namespace Gg.Runner;

/// <summary>
/// A runner answering about itself, and watching itself to be able to.
/// </summary>
/// <remarks>
/// <para>
/// <b>An observer, because that is where the answers already are.</b>
/// ADR-0013's tier 2c is three free-form diagnosis strings — <c>BoundBroken</c>,
/// <c>WorkspaceFailed</c>, <c>ControlPlaneRefused</c> — that the runner narrates
/// into a log file and nothing else ever sees. They are already reported here;
/// what was missing was anything that remembered the last one.
/// </para>
/// <para>
/// <b>The LAST one rather than all of them.</b> A history would be a second log,
/// held in memory, growing while a runner runs — and the log is what
/// <c>tail-log</c> is for. What <c>status</c> answers is "what is wrong now",
/// which is one string.
/// </para>
/// <para>
/// <b>It wraps rather than replaces.</b> The runner still narrates to whatever
/// it narrated to; this watches on the way past. A version that swallowed the
/// inner observer would make asking a runner about itself change what it writes
/// down, which is the sort of thing nobody expects a read to do.
/// </para>
/// </remarks>
public sealed class WhatThisRunnerSays(
    IRunnerObserver inner, IReadOnlyLog log, Func<DateTimeOffset> now)
    : IRunnerObserver, IAnswersAboutItself
{
    private readonly IRunnerObserver _inner = inner;
    private readonly IReadOnlyLog _log = log;
    private readonly Func<DateTimeOffset> _now = now;
    private readonly Lock _gate = new();

    private string _doing = "starting";
    private string? _diagnosis;

    /// <summary>The flight this runner holds, as a person reads it. Null when none.</summary>
    /// <remarks>
    /// <b>Learned from the lease and nowhere else.</b> Claimed carries it; a
    /// second derivation would be a second answer to what this machine is on.
    /// </remarks>
    private string? _flightNumber;

    /// <summary>When this runner last beat the control plane.</summary>
    private DateTimeOffset? _beatAt;

    public LogTail Tail(int lines)
    {
        var read = _log.Tail(lines);

        return new LogTail
        {
            Lines = read.Lines,
            // TRUNCATED IS A FACT, NOT AN APOLOGY. A tail that stopped at the
            // bound and one genuinely that short read identically otherwise.
            Truncated = read.Truncated,
        };
    }

    public RunnerStatusReport Status()
    {
        lock (_gate)
        {
            return new RunnerStatusReport
            {
                Doing = _doing,
                Diagnosis = _diagnosis,
                At = _now(),
                FlightNumber = _flightNumber,
                BeatAt = _beatAt,
            };
        }
    }

    /// <summary>
    /// Records something that went wrong without changing what this is doing.
    /// </summary>
    /// <remarks>
    /// <b>The two are not the same fact.</b> A handshake that failed is a person
    /// who could not reach this runner; the runner went on doing whatever it was
    /// doing. Writing it through <c>Doing</c> would report the flight as stopped
    /// because somebody else's console timed out.
    /// </remarks>
    private void Diagnosed(string diagnosis)
    {
        lock (_gate)
        {
            _diagnosis = diagnosis;
        }
    }

    private void Doing(string what, string? diagnosis = null)
    {
        lock (_gate)
        {
            _doing = what;

            // A DIAGNOSIS IS CLEARED BY GETTING ON WITH SOMETHING, not by time.
            // A runner that claimed a flight after failing to build a workspace
            // has recovered, and still showing the old failure would send
            // somebody after a problem that is over.
            _diagnosis = diagnosis;
        }
    }

    public void Claimed(LeaseGranted lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        Doing("working a flight");

        lock (_gate)
        {
            _flightNumber = lease.FlightNumber;
        }

        _inner.Claimed(lease);
    }

    /// <summary>
    /// This runner beat the control plane.
    /// </summary>
    /// <remarks>
    /// <b>A SECOND LIVENESS, and it is not this channel's.</b> A watcher can see
    /// that an answer came back; what it cannot see from here is whether the
    /// machine is still reaching the control plane. A runner partitioned from
    /// the control plane answers a peer connection perfectly and is never given
    /// work again, and a person watching it wait deserves to know which of those
    /// two silences they are in.
    /// <para>
    /// <b>It does not touch what this runner is DOING.</b> Beating happens
    /// beside a flight and beside an idle poll alike.
    /// </para>
    /// </remarks>
    public void Beat(DateTimeOffset at)
    {
        lock (_gate)
        {
            _beatAt = at;
        }
    }

    public void Renewed(string leaseId, DateTimeOffset expiresAt) =>
        _inner.Renewed(leaseId, expiresAt);

    public void Fenced(string leaseId)
    {
        Doing("fenced - the flight is somebody else's now");
        FlyingNothing();
        _inner.Fenced(leaseId);
    }

    /// <summary>
    /// Lets go of the flight. Every ending calls it.
    /// </summary>
    /// <remarks>
    /// <b>A number that outlives its flight is worse than none</b>, because the
    /// one thing it is on the wire to report is a CHANGE - so a watcher left
    /// holding a landed flight's number goes on naming work that is over, and
    /// says nothing when the next one starts.
    /// </remarks>
    private void FlyingNothing()
    {
        lock (_gate)
        {
            _flightNumber = null;
        }
    }

    public void Released(string leaseId, string disposition)
    {
        Doing($"released a flight: {disposition}");
        FlyingNothing();
        _inner.Released(leaseId, disposition);
    }

    public void CannotBeFlownByHand(string diagnosis)
    {
        // REMEMBERED AS A DIAGNOSIS, not as what this runner is DOING. It went
        // on doing whatever it was doing; somebody else could not reach it, and
        // overwriting `doing` would report the flight as stopped because a
        // handshake failed.
        Diagnosed(diagnosis);
        _inner.CannotBeFlownByHand(diagnosis);
    }

    public void BoundBroken(string diagnosis)
    {
        Doing("stopped: a bound was broken", diagnosis);
        _inner.BoundBroken(diagnosis);
    }

    public void ControlPlaneRefused(string diagnosis, TimeSpan retryIn)
    {
        Doing($"waiting {retryIn.TotalSeconds:0}s after a refusal", diagnosis);
        _inner.ControlPlaneRefused(diagnosis, retryIn);
    }

    public void WorkspaceFailed(string diagnosis)
    {
        Doing("could not make a workspace", diagnosis);
        _inner.WorkspaceFailed(diagnosis);
    }

    public void Idle()
    {
        Doing("idle");
        FlyingNothing();
        _inner.Idle();
    }

    public void Parked()
    {
        Doing("parked - somebody withheld this machine");
        FlyingNothing();
        _inner.Parked();
    }

    public void AllowanceSpent()
    {
        // NOT "PARKED", and not "idle". A person watching this runner needs to
        // know there is nothing to do about it and nobody to ask.
        Doing("allowance spent - waiting for the window to roll over");
        FlyingNothing();
        _inner.AllowanceSpent();
    }

    public void Waiting(IReadOnlyList<string> repositories)
    {
        Doing($"waiting on {repositories.Count} repositor{(repositories.Count == 1 ? "y" : "ies")}");
        FlyingNothing();
        _inner.Waiting(repositories);
    }

    public void Materialized(string slug, string headCommit, long bytes)
    {
        Doing($"materialized {slug}");
        _inner.Materialized(slug, headCommit, bytes);
    }

    // PASSED STRAIGHT THROUGH, and the split is deliberate rather than lazy.
    // What `status` answers is "what is this machine doing and what is wrong
    // with it". These say what a FLIGHT did - facts shipped, a loop's outcome,
    // where a bundle was held - and a runner that reported them as its own state
    // would be answering a question about the work when it was asked one about
    // the machine.
    //
    // MoveRefused is the near miss on that list: it is a diagnosis string, and
    // it belongs to the agent's attempt rather than to the runner. A person
    // asking why a MOVE was refused wants the flight's log, which is what
    // tail-log is for.
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

/// <summary>The last lines of something, and whether there were more.</summary>
public readonly record struct TailRead(IReadOnlyList<string> Lines, bool Truncated);

/// <summary>
/// Somewhere a runner's own output can be read from.
/// </summary>
/// <remarks>
/// <b>An interface because a runner's output is not always a file.</b> Step 0
/// found the pool host's runner is a systemd unit, so its output is in the
/// journal and the conventional path holds nothing at all. A dispatch bound to
/// <c>File.ReadLines</c> would answer "nothing" on every properly supervised
/// runner in a fleet, which is worse than refusing.
/// </remarks>
public interface IReadOnlyLog
{
    /// <summary>The last <paramref name="lines"/> lines, or fewer.</summary>
    TailRead Tail(int lines);
}
