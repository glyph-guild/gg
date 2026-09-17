using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Sweeps;

/// <summary>
/// The instructions executor: an agent, headless, with the watch's tracker and
/// this binary's own server, reporting what it nominated.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0023 § 1: a sweep begins as instructions.</b> The skill's words are
/// the whole of what makes this sweep different from any other, and they arrive
/// read at a pinned commit. What the agent nominated is read from the
/// transcript rather than from anything it said, because a tool call is
/// something it chose to make and a sentence is something it can be told to
/// write.
/// </para>
/// <para>
/// <b>A sweep that never read its tracker did not find nothing.</b> The one
/// report a sweep must never make blind is the empty one, so the calls to the
/// tracker's own tool are counted: a sweep has looked when a read came back.
/// This is the runner's half of rule 12 - what a person can act on, escalated
/// as a decision rather than reported as a quiet backlog.
/// </para>
/// <para>
/// <b>The bound is measured first, exactly as it is for a flight.</b> A sweep
/// is an agent on this machine with <c>read</c> and <c>propose</c>; a machine
/// where withholding <c>edit</c> does not withhold it is not one to start an
/// agent on, whatever the envelope says. The probe is itself an invocation, so
/// it is not spent on a sweep that was never going to run.
/// </para>
/// </remarks>
/// <param name="executorFor">
/// The agent, built with one sweep's tracker reader and its sweep-mode server.
/// Handed in because how this machine's agent is declared and where its
/// credentials live belong to the composition root, which is the only place
/// that can see both.
/// </param>
/// <param name="trackers">What this machine declared it reads, and with which credential.</param>
/// <param name="self">How to start this binary again, or null where it cannot be named.</param>
/// <param name="scratch">Where a sweep's own empty working directory goes.</param>
/// <param name="wallClock">How long this runner gives one sweep.</param>
public sealed class AgentSweepExecutor(
    Func<IntentReader, SelfInvocation, IExecutorPort> executorFor,
    IReadOnlyList<ServedTracker> trackers,
    SelfInvocation? self,
    string scratch,
    TimeSpan? wallClock = null) : ISweepExecutor
{
    private readonly Func<IntentReader, SelfInvocation, IExecutorPort> _executorFor = executorFor;
    private readonly IReadOnlyList<ServedTracker> _trackers = trackers;
    private readonly SelfInvocation? _self = self;
    private readonly string _scratch = scratch;

    /// <summary>
    /// How long one sweep gets.
    /// </summary>
    /// <remarks>
    /// <b>The runner's, because no envelope bounds a sweep's clock.</b> A
    /// flight's wall clock comes from its loop; a sweep has no loop and no
    /// flight, and its work is bounded by its own trigger instead - so the
    /// machine that pays for the tokens decides, and a sweep that cannot page a
    /// backlog inside this is a watch whose filter is too wide.
    /// </remarks>
    public static readonly TimeSpan DefaultWallClock = TimeSpan.FromMinutes(15);

    private readonly TimeSpan _wallClock = wallClock ?? DefaultWallClock;

    public async Task<SweepExecution> RunAsync(
        SweepRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ITS OWN DIRECTORY PER SWEEP, named for the action so two sweeps of one
        // watch cannot share one. Nothing is materialized into it: it exists so
        // the agent starts somewhere that is not this runner's own directory.
        var working = Path.Combine(
            _scratch, LocalPaths.Safe(request.Action.Watch),
            request.Action.ActionId.ToString("N"));

        if (SweepLauncher.Plan(request, _trackers, _self, working, _wallClock) is not
            SweepLaunch.Ready ready)
        {
            // NOTHING IS STARTED, INCLUDING THE PROBE, which is itself an agent
            // invocation: a sweep that was never going to run must not cost one.
            return new SweepExecution.Failed(
                ((SweepLaunch.Refused)SweepLauncher.Plan(
                    request, _trackers, _self, working, _wallClock)).Diagnosis);
        }

        var agent = _executorFor(ready.Reader, ready.Self);

        try
        {
            Directory.CreateDirectory(working);

            if (agent.BoundIsMeasurable)
            {
                var probe = await MoveBoundProbe.RunAsync(agent, cancellationToken);
                if (!probe.Bound)
                {
                    return new SweepExecution.Failed(probe.Diagnosis);
                }
            }

            var ran = await agent.ExecuteAsync(ready.Request, cancellationToken);

            if (ran is null)
            {
                // ONLY THE ATTENDED EXECUTOR ANSWERS THIS, and a sweep has
                // nobody at it - so this is a machine configured to sweep with
                // an executor that cannot sweep, said rather than read as an
                // empty result.
                return new SweepExecution.Failed(
                    "This runner's executor measured nothing, which is what an attended session "
                  + "answers - and a sweep has no person at it to attend one. Declare a "
                  + "headless executor on the machine that sweeps.");
            }

            return Concluded(ready, ran, request);
        }
        finally
        {
            try
            {
                if (Directory.Exists(working))
                {
                    Directory.Delete(working, recursive: true);
                }
            }
            catch (Exception leftBehind) when (leftBehind is IOException
                                                  or UnauthorizedAccessException)
            {
                // A scratch directory that will not delete is not a reason to
                // fail a sweep that ran. The operating system will get it.
            }
        }
    }

    /// <summary>
    /// What the run amounts to: what it nominated, or why this is not a report
    /// of an empty backlog.
    /// </summary>
    /// <remarks>
    /// <b>Read from what it DID.</b> The outcome record says how the turn
    /// ended; the transcript says what was called and what came back, and the
    /// two together are the only evidence here. Nothing is taken from what the
    /// agent said about its work.
    /// </remarks>
    private SweepExecution Concluded(
        SweepLaunch.Ready ready, ExecutorRun ran, SweepRequest request)
    {
        if (string.Equals(ran.Outcome, LoopOutcomes.Failed, StringComparison.Ordinal))
        {
            return new SweepExecution.Failed(
                $"The sweep's agent did not finish: {ran.Reason}");
        }

        if (string.Equals(ran.Outcome, LoopOutcomes.Exhausted, StringComparison.Ordinal))
        {
            // ITS NOMINATIONS GO WITH IT, and that is the honest reading: a
            // sweep stopped mid-backlog cannot say the rest holds nothing, and
            // the dedupe makes the next sweep's repeat of the part it did reach
            // free. A watch that exhausts every time is a filter too wide, and
            // it escalates as a decision rather than looking like a quiet
            // backlog.
            return new SweepExecution.Failed(
                $"The sweep ran out of the {Said(_wallClock)} this runner gives one, so what it "
              + "had not reached is unknown - and a partial pass must not be reported as a "
              + "backlog with nothing in it.");
        }

        var transcript = Read(request.TranscriptPath);
        var queried = TranscriptDigest.CallsTo(
            transcript, $"mcp__{ready.Reader.Key}__{QueryTool.Name}");

        if (queried.Answered == 0)
        {
            return new SweepExecution.Failed(Blind(ready, queried, request));
        }

        return new SweepExecution.Ran(TranscriptDigest.SweepNominations(transcript));
    }

    /// <summary>
    /// Why an empty pass is not a report, in this platform's own words.
    /// </summary>
    /// <remarks>
    /// <b>The tracker's sentence is not in it.</b> A refused read can quote the
    /// page that came back - a sign-in page, an error page, a customer's own
    /// text - and this sentence travels to the control plane. So it carries the
    /// count, the host the watch already names, and the path to the transcript
    /// on this machine, which is where the operator reads the rest.
    /// </remarks>
    private static string Blind(
        SweepLaunch.Ready ready, TranscriptDigest.ToolCalls queried, SweepRequest request)
    {
        var tool = $"mcp__{ready.Reader.Key}__{QueryTool.Name}";

        return queried.Refused > 0
            ? $"{request.Action.Document.Host} refused all {queried.Refused} of this sweep's "
            + $"reads, so nothing was read and nothing can be concluded about the backlog. The "
            + $"tracker's own words are in this runner's transcript at {request.TranscriptPath}; "
            + "the usual cause is the watch's credential missing, expired or lacking work-item "
            + "read on this host."
            : $"This sweep's agent never called {tool}, so it read nothing and its pass says "
            + "nothing about the backlog. The transcript on this runner is at "
            + $"{request.TranscriptPath}.";
    }

    /// <summary>The transcript, or nothing where the run never wrote one.</summary>
    private static string Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch (Exception unreadable) when (unreadable is IOException
                                              or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>The budget as a person says it, because "00:15:00" is not a sentence.</summary>
    private static string Said(TimeSpan budget) =>
        budget.TotalMinutes >= 1
            ? $"{budget.TotalMinutes:0} minutes"
            : $"{budget.TotalSeconds:0} seconds";
}
