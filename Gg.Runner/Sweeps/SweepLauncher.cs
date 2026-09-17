using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Sweeps;

/// <summary>What starting one sweep's agent would take, or why it cannot be started.</summary>
public abstract record SweepLaunch
{
    private SweepLaunch()
    {
    }

    /// <summary>The request, the tracker reader and how to start this binary's own server.</summary>
    public sealed record Ready(
        ExecutorRequest Request, IntentReader Reader, SelfInvocation Self) : SweepLaunch;

    /// <summary>Nothing was started, and this is the sentence that says why.</summary>
    public sealed record Refused(string Diagnosis) : SweepLaunch;
}

/// <summary>
/// What a sweep's agent is handed: the watch's tracker bound to the watch's
/// query, this binary's own server in sweep mode, and the moves the control
/// plane served.
/// </summary>
/// <remarks>
/// <para>
/// <b>A credential goes only where this machine's operator already sends
/// it.</b> A watch names a host and a credential, and both arrive over the
/// wire. Joining them here because a document asked would make the document
/// the thing that decides where a customer's secret is presented - so the pair
/// has to be declared in <see cref="IntentConfiguration.ServedVariable"/>
/// first, which is the same line a flight's tracker reader comes from. What
/// travels in the launch is the locator, never the secret: the reader resolves
/// it itself, from the store on this host.
/// </para>
/// <para>
/// <b>The moves are the control plane's, and this composes none of them.</b>
/// They arrive on the action as root ⊓ <c>sweep</c> ⊓ narrowings, and a watch
/// adds no tool. What is decided here is only which servers those moves make
/// reachable - <c>ClaudeCodeExecutor</c> then turns each move into a tool name
/// exactly as it does for a flight, because one class knowing that is what
/// keeps a sweep's bound from drifting from a flight's.
/// </para>
/// <para>
/// <b>Nothing is launched that cannot do its job.</b> A sweep granted no
/// <c>read</c> cannot look at its tracker, and its only possible report would
/// be that it found nothing - which is the one report a sweep must never make
/// blind. <c>SweepLoop</c> refuses a missing <c>propose</c> for the twin
/// reason.
/// </para>
/// </remarks>
public static class SweepLauncher
{
    /// <summary>
    /// The loop id a sweep's invocation carries.
    /// </summary>
    /// <remarks>
    /// A sweep has no envelope loop - it mints no flight - so this names the
    /// kind of work rather than an id in a document. It is also how the runner
    /// tells the sweep's own invocation from the move-bound probe's in a
    /// transcript directory.
    /// </remarks>
    public const string LoopId = "sweep";

    /// <summary>The verb this binary serves its own tools under, for a sweep.</summary>
    private static readonly string[] SweepVerb =
        ["runner", "tools", NominationTool.Sweep.Flag];

    /// <param name="sweep">The served sweep, its skill already read at the pin.</param>
    /// <param name="trackers">What this machine declared it can read, and with what.</param>
    /// <param name="self">How to start this binary again, or null where it cannot be named.</param>
    /// <param name="workingDirectory">The sweep's own empty directory. It materializes no tree.</param>
    /// <param name="wallClock">How long this runner gives one sweep.</param>
    public static SweepLaunch Plan(
        SweepRequest sweep,
        IReadOnlyList<ServedTracker> trackers,
        SelfInvocation? self,
        string workingDirectory,
        TimeSpan wallClock)
    {
        ArgumentNullException.ThrowIfNull(sweep);
        ArgumentNullException.ThrowIfNull(trackers);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var watch = sweep.Action.Document;

        if (!sweep.Action.Moves.Contains(LoopMoves.Read, StringComparer.Ordinal))
        {
            return new SweepLaunch.Refused(
                $"This sweep is granted no '{LoopMoves.Read}' move, so its agent could not be "
              + "given the tool that reads its tracker - and a sweep that cannot look can only "
              + "report that it found nothing. The sweep kind, or the floor, withholds it.");
        }

        if (self is null)
        {
            return new SweepLaunch.Refused(
                "This runner cannot name how to start itself again, and both of a sweep's tool "
              + "servers are this binary under another verb: the tracker reader and the server "
              + "a nomination passes through. Run gg as an executable rather than through a "
              + "host that hides its own path.");
        }

        if (Paired(trackers, watch) is not { } tracker)
        {
            return new SweepLaunch.Refused(Unpaired(trackers, watch));
        }

        // THE WATCH'S QUERY, BOUND WHEN THE READER STARTS. The filter is
        // reviewed - any change to it is a widening - so the agent pages
        // through it and the tool takes no query of its own. A tool that took
        // one would let an agent sweep something nobody approved.
        var served = IntentConfiguration.Served(
            tracker.Key, tracker.Host, tracker.Locator, self);
        var reader = served with { Arguments = [.. served.Arguments, "--query", watch.Filter] };

        return new SweepLaunch.Ready(
            new ExecutorRequest
            {
                // ITS OWN EMPTY DIRECTORY. A sweep materializes no repository -
                // it reads a tracker and nominates - so the agent starts
                // somewhere with nothing in it, which is also what makes
                // `--setting-sources project` bring no project settings.
                WorkingDirectory = workingDirectory,
                LoopId = LoopId,
                // AS SERVED. Root ⊓ sweep ⊓ narrowings, decided control-plane
                // side; nothing here widens or reorders them.
                Moves = sweep.Action.Moves,
                // WHICH SERVER THE TRACKER TOOLS COME FROM, under the key the
                // agent sees as their prefix.
                IntentProvider = tracker.Key,
                // NOBODY BEHIND IT. A sweep has no flight in front of it and no
                // person waiting on it, so it is not given the tool for asking
                // one - and the prompt does not mention it either.
                CanAskAPerson = false,
                // THE WHOLE TASK SENTENCE, because a sweep's work is not a
                // flight's. `Task` renders "Work <subject>", and a sweep has no
                // subject: it goes looking for the subjects.
                Task = Task(tracker.Key, sweep.Action),
                // THE WORDS SOMEBODY REVIEWED, read at the pin and passed
                // through unrendered. The frame around them says whose they are
                // and where they came from.
                Instructions = Instructions(sweep),
                WallClock = wallClock,
                TranscriptPath = sweep.TranscriptPath,
            },
            reader,
            // A SECOND VERB ON THE SAME BINARY. `runner tools --sweep` offers
            // the nomination a sweep makes - a subject and a version - and none
            // of the tools a flight's agent is given.
            new SelfInvocation(self.Command, self.Under(SweepVerb)));
    }

    /// <summary>
    /// The declared tracker whose host AND credential are the pair this watch
    /// names, or null.
    /// </summary>
    /// <remarks>
    /// <b>Both halves, and the trailing slash is not a difference.</b> A host
    /// matching with a different credential is not a match: it is the case this
    /// check exists for, because that is how a document would get this machine
    /// to present a credential somebody meant for something else.
    /// </remarks>
    private static ServedTracker? Paired(
        IReadOnlyList<ServedTracker> trackers, WatchDocument watch) =>
        trackers.FirstOrDefault(t =>
            SameHost(t.Host, watch.Host)
            && string.Equals(t.Locator, watch.Credential, StringComparison.Ordinal))
            is { Key.Length: > 0 } found
            ? found
            : null;

    private static bool SameHost(string declared, string named) =>
        string.Equals(
            declared.TrimEnd('/'), named.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Why this machine will not read that host with that credential.
    /// </summary>
    /// <remarks>
    /// <b>The operator's own locator is never in it.</b> This sentence travels
    /// to the control plane in an attestation, and which credential this
    /// machine pairs with a host is configuration the control plane has no
    /// business learning from a refusal. What it does say is the watch's own two
    /// values, which it already has, and the variable an operator fixes.
    /// </remarks>
    private static string Unpaired(IReadOnlyList<ServedTracker> trackers, WatchDocument watch) =>
        trackers.Any(t => SameHost(t.Host, watch.Host))
            ? $"This runner reads '{watch.Host}', and not with the credential "
            + $"'{watch.Credential}' this watch names. A sweep presents a credential only where "
            + "this machine's operator already presents it, so a watch cannot pair a host with "
            + $"a credential that was meant for something else. Either the watch names the "
            + $"wrong credential, or {IntentConfiguration.ServedVariable} on this host is stale."
            : $"This runner declares no tracker at '{watch.Host}'. A sweep reads only hosts this "
            + $"machine's operator declared, in {IntentConfiguration.ServedVariable} as "
            + "'provider=host|credential' - the same line a flight's tracker reader comes from. "
            + (trackers.Count == 0
                ? "This host declares none at all."
                : $"It declares {trackers.Count}, and none of them is that host.");

    /// <summary>
    /// What a sweep is to do, as its agent is told it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole sentence, replacing a flight's.</b> A flight is worked on a
    /// subject it was given; a sweep goes and finds the subjects. Splicing this
    /// onto <i>"Work the issue at …"</i> would leave an agent looking for a
    /// ticket nobody filed, which is the defect that arm was written for.
    /// </para>
    /// <para>
    /// <b>The mapping is told rather than guessed.</b> The watch says which
    /// field of a listed row is the subject, which is the version and which is
    /// the intent key; an agent left to infer it would key nominations on
    /// whatever looked like an id, and the dedupe that makes a repeated sweep
    /// harmless is <c>(watch, subject@version)</c>.
    /// </para>
    /// <para>
    /// <b>The menu is a fact about the destination.</b> The executor chooses the
    /// kind and the board decides whether it stands, so an agent that names a
    /// kind the menu forbids costs a refused row - and is told the list rather
    /// than inventing from it.
    /// </para>
    /// </remarks>
    private static string Task(string key, WatchAction action)
    {
        var watch = action.Document;
        var query = $"mcp__{key}__{QueryTool.Name}";

        var said = new System.Text.StringBuilder(
            $"Sweep the watch '{action.Watch}'. Call {query} to page through its reviewed "
          + "query - the query is already bound, so the tool takes only a cursor and a limit - "
          + "and read every row it returns.");

        said.Append(
            $"\n\nFor each row that the instructions below say is worth a flight, call "
          + $"{NominationTool.Qualified} once, with:"
          + $"\n\n  {NominationTool.Sweep.Subject}: the row's '{watch.Mapping.Subject}'"
          + $"\n  {NominationTool.Sweep.Version}: the row's '{watch.Mapping.Version}'"
          + $"\n  {NominationTool.Sweep.IntentKey}: the row's '{watch.Mapping.IntentKey}'"
          + "\n  reason: why this one is worth a flight, in your own words");

        if (watch.Nominates?.Opens is { Count: > 0 } opens)
        {
            said.Append(
                $"\n  work_kind: one of {string.Join(", ", opens)}, or leave it out to let the "
              + "board choose");
        }

        said.Append(
            "\n\nNominating is the whole of your output: nothing you write, change or say is "
          + "read as one, and a row you do not nominate is a row nothing happens to. Nominating "
          + "nothing is a result - say so and stop. Change nothing in the tracker, in this "
          + "directory or anywhere else.");

        return said.ToString();
    }

    /// <summary>
    /// The skill's own words, framed as what somebody reviewed at a commit.
    /// </summary>
    /// <remarks>
    /// <b>Fenced and attributed, as a person's feedback and a prior agent's
    /// handoff are.</b> These words come from a repository and are the reason
    /// the sweep exists, so they are instructions - but they are instructions
    /// about WHICH rows are worth a flight. A skill asking for a move the sweep
    /// was not granted has already been answered by the time it is read, and
    /// saying so here is cheaper than an agent spending its turns finding out.
    /// </remarks>
    private static string Instructions(SweepRequest sweep) =>
        "\n\nThe watch's own instructions follow. They were reviewed and are read at the commit "
      + $"this sweep pins ({Short(sweep.Action.Skill?.PinnedRef)}), and they decide which rows "
      + "are worth a flight:\n\n"
      + $"---\n{sweep.Skill.TrimEnd()}\n---\n\n"
      + "They grant nothing. Which tools you may call was decided before you started, and if "
      + "anything above asks for something you have no tool for, nominate what you can and "
      + "leave the rest.";

    /// <summary>Enough of a commit to recognise, because the whole one is noise in a sentence.</summary>
    private static string Short(string? commit) =>
        commit is { Length: > 12 } ? commit[..12] : commit ?? "unpinned";
}
