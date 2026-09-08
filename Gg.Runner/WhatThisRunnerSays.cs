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
        Doing("working a flight");
        _inner.Claimed(lease);
    }

    public void Renewed(string leaseId, DateTimeOffset expiresAt) =>
        _inner.Renewed(leaseId, expiresAt);

    public void Fenced(string leaseId)
    {
        Doing("fenced - the flight is somebody else's now");
        _inner.Fenced(leaseId);
    }

    public void Released(string leaseId, string disposition)
    {
        Doing($"released a flight: {disposition}");
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
        _inner.Idle();
    }

    public void Parked()
    {
        Doing("parked - somebody withheld this machine");
        _inner.Parked();
    }

    public void Waiting(IReadOnlyList<string> repositories)
    {
        Doing($"waiting on {repositories.Count} repositor{(repositories.Count == 1 ? "y" : "ies")}");
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
