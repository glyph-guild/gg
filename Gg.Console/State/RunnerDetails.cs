using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What the runner modal shows, in the shapes it shows them in.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="FlightDetails"/>'s argument, one modal over.</b> Everything
/// here was a preformatted string in a <c>Label</c>, so nothing could be
/// selected and nothing copied — and the runner id is the value most often
/// wanted out of this modal, because it is what <c>gg runner</c> takes and the
/// grid deliberately shows eight characters of it.
/// </para>
/// <para>
/// <b>Two sources, and only one of them is the fleet's.</b> State, labels,
/// last-heard and who registered it come from the control plane about
/// anybody's runner. A pid and a log path are facts about a process THIS
/// console started, and belong only to the row that process is. Keeping that
/// distinction here is what stops the modal saying a build host's runner is
/// running on this laptop.
/// </para>
/// <para>
/// <b>The log is text and not a table</b>, which is where this parts from the
/// flight modal: a flight's log has a time, an attempt and an event, and a
/// runner's log is whatever a child wrote to its own stdout.
/// </para>
/// </remarks>
public static class RunnerDetails
{
    /// <summary>What the frame over the log says.</summary>
    public const string LogTitle = "Log";

    /// <summary>
    /// The runner this modal is about, named the way the grid names it.
    /// </summary>
    /// <remarks>
    /// <b>The same short id and label the row carries</b>, so the modal and the
    /// row a person opened it from agree about what they are looking at. It was
    /// a fixed title, true of whichever runner was open — and once enter began
    /// opening any row in the fleet, true of none of them in particular.
    /// </remarks>
    public static string Title(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Rows.Selected(state) is { } row)
        {
            return row.Runner;
        }

        // A CHILD WITH NO FLEET ROW YET, which is the case this modal exists
        // for: `s` starts a runner and it registers a beat later, so for those
        // seconds the fleet knows nothing and the console knows everything.
        return state.Here is { Up: true }
            ? "Starting here"
            : PaneText.ModalTitle(UiMode.Runner);
    }

    /// <summary>
    /// The scalars, each with the name it goes by.
    /// </summary>
    /// <remarks>
    /// <b>A field is omitted rather than emptied.</b> An empty value beside a
    /// label is a fact that is missing; no label at all is a fact that does not
    /// apply. An idle runner is not working on nothing — it is not working.
    /// </remarks>
    public static IReadOnlyList<FlightField> Fields(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Rows.Selected(state) is not { } row)
        {
            // NO FLEET ROW IS NOT NO RUNNER. A runner this console just started
            // has not registered yet; everything the fleet would say is unknown
            // and everything the child knows is available.
            return state.Here is { } starting ? Child(starting) : [];
        }

        var fields = new List<FlightField>
        {
            // THE WHOLE ID, WHICH THE GRID CANNOT GIVE. Fifteen rows of full
            // uuid would be a column of noise, so the table shows eight
            // characters - and `gg runner` takes all of it. This is where it
            // has to be complete, which is why the row carries both.
            new("id", row.Id),
            new("state", row.State),
        };

        if (row.Work is { Length: > 0 } work)
        {
            fields.Add(new FlightField("working on", work));
        }

        if (row.Labels is { Length: > 0 } labels)
        {
            fields.Add(new FlightField("advertises", labels));
        }

        fields.Add(new FlightField("last heard", row.Heard));

        if (row.RegisteredBy is { Length: > 0 } who)
        {
            fields.Add(new FlightField("registered by", who));
        }

        // WHY IT WAS WITHHELD, where there is room for a sentence. The grid has
        // room for a word and says `parked`; without the reason that word sends
        // somebody to ask a person, which is what recording a reason prevents.
        if (row.ParkedBecause is { Length: > 0 } because)
        {
            fields.Add(new FlightField("parked", because));
        }

        // WHAT ONLY THIS CONSOLE KNOWS, and only about its own child. A pid
        // under another host's runner would say a process is running here that
        // is not, which is the same defect the modal's subject had.
        if (row.Mine && state.Here is { } here)
        {
            fields.AddRange(Child(here));
        }

        return fields;
    }

    /// <summary>What this console knows about the child it started.</summary>
    private static List<FlightField> Child(RunnerHere here)
    {
        var fields = new List<FlightField>();

        if (here.Pid is { } pid)
        {
            fields.Add(new FlightField("process", $"{pid}"));
        }

        if (here.Exit is { } exit)
        {
            fields.Add(new FlightField("exited", $"{exit}"));
        }

        if (here.LogPath is { Length: > 0 } path)
        {
            fields.Add(new FlightField("log", path));
        }

        return fields;
    }

    /// <summary>
    /// What the child this console started has said, or nothing.
    /// </summary>
    /// <remarks>
    /// <b>Only ever this console's own child.</b> A log is a local file, so
    /// there is one for exactly one row in any fleet — showing it under another
    /// host's runner would attribute one machine's output to another.
    /// </remarks>
    public static string Log(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // NOT SOMEBODY ELSE'S ROW - and NO row is not somebody else's row. A
        // runner coming up here has no fleet row for a few seconds, and those
        // are exactly the seconds a person opened this to watch.
        return Rows.Selected(state) is not { Mine: false }
            && state.Here is { Log.Count: > 0 } here
            ? string.Join("\n", here.Log)
            : "";
    }

    /// <summary>
    /// The log as lines that fit the room given, ready for a list to show.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Wrapped here rather than truncated by the widget.</b> A runner's
    /// output is mostly paths, urls and stack frames - the longest lines in it
    /// are the ones somebody opened it to read - and a list that cut them at
    /// the frame would drop exactly those. <see cref="Rows.Wrapped"/> breaks on
    /// characters when a word will not fit, which is what makes a path survive.
    /// </para>
    /// <para>
    /// <b>Width is a parameter because the view knows it and this does not.</b>
    /// The same reason the flight log's unwrap takes one: a viewport is a fact
    /// about a terminal somebody may resize, and a producer that guessed it
    /// would be wrong on the first drag.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Lines(AppState state, int width)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Log(state) is not { Length: > 0 } said)
        {
            return [];
        }

        // BEFORE THE LAYOUT HAS HAPPENED the viewport is zero wide, and a
        // render runs before a layout does. Wrapping to nothing would return a
        // line per character; the whole lines are right for that one pass and
        // are replaced the moment the viewport is known.
        var lines = said.Split('\n');

        return width <= 0
            ? lines
            : [.. lines.SelectMany(line => line.Length <= width
                ? (IEnumerable<string>)[line]
                : Rows.Wrapped(line, width))];
    }

    /// <summary>
    /// The whole modal as one string, for the renderings that are not widgets.
    /// </summary>
    /// <remarks>
    /// <b>Composed FROM the producers the view binds, never beside them.</b>
    /// <see cref="FlightDetails.Linear"/>'s reason, one modal over: two
    /// renderings assembled independently are two authors for one screen, and
    /// they drift the first time a field is added to one of them. This one
    /// drifted before it existed - the fields went in and the text rendering
    /// still said what it had always said.
    /// </remarks>
    internal static string Linear(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var text = new System.Text.StringBuilder();

        text.AppendLine($"  {Title(state)}");
        text.AppendLine();

        foreach (var field in Fields(state))
        {
            text.AppendLine($"  {field.Label,-13} {field.Value}");
        }

        text.AppendLine();
        text.AppendLine($"  {LogTitle}");

        if (LogAbsence(state) is { Length: > 0 } absence)
        {
            text.AppendLine($"  {absence}");

            return text.ToString().TrimEnd();
        }

        text.AppendLine(Log(state));

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What stands where the log would be, when there is none.
    /// </summary>
    /// <remarks>
    /// <b>Two silences that mean opposite things.</b> A child of ours that has
    /// not spoken yet will; a runner on another host was never going to send
    /// its output here. "It has said nothing yet" over the second reads as a
    /// runner that has gone quiet, which is the opposite of what is true.
    /// </remarks>
    public static string LogAbsence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Log(state).Length > 0)
        {
            return "";
        }

        if (Rows.Selected(state) is { Mine: false } elsewhere)
        {
            return "This console did not start it, so there is no log here to read. What it "
                 + "says is on the machine it is running on."
                 + Suggestion(elsewhere);
        }

        // NOTHING ANYWHERE, said plainly. A modal that only ever described a
        // runner would describe one it invented when there is none - and the
        // empty fleet with no child is the state a person is most likely to be
        // confused by, because it is what a machine that has never run one
        // looks like.
        return Rows.Selected(state) is null && state.Here is null
            ? "There is no runner here: none registered on this machine, and none started "
            + "from this console."
            : "It has said nothing yet.";
    }

    /// <summary>
    /// Where to look, on a machine a person can reach, and how sure we are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A SUGGESTION, and it says so, because gg is guessing about somebody
    /// else's machine.</b> ADR-0013 Decision 2 is reference-and-fetch: the modal
    /// names where the output is and the person's own access is the channel. What
    /// gg actually knows is a convention it follows itself, not a fact the
    /// control plane reported - <c>XDG_STATE_HOME</c> may be set, the host may be
    /// a container, and the label may not resolve to anything ssh can reach.
    /// </para>
    /// <para>
    /// <b>The suffix decides which convention applies, and this was FOUND rather
    /// than designed.</b> Walking the development fleet showed the pool host's
    /// runner is a systemd unit, so its output is in the journal and
    /// <c>~/.local/state/good-grief/runner.log</c> does not exist there at all.
    /// The file path is written only when a CONSOLE starts a runner and captures
    /// its streams. Offering the path for everything would have been a command
    /// that fails on every properly supervised runner in a fleet.
    /// </para>
    /// <para>
    /// <b>Which gg can tell from the name, because gg chose the names.</b>
    /// <c>&lt;machine&gt;:maintain</c> is run by the unit gg ships in
    /// <c>deploy/pool-host</c>, so naming that unit is a fact about our own
    /// packaging rather than a guess about their host.
    /// <c>&lt;machine&gt;:hand</c> is <see cref="Gg.Client.AttendedRunner"/> - a
    /// person flying by hand, so a console started it and the file is there. A
    /// bare machine name is <c>gg runner up</c>, which either could have started,
    /// and that ambiguity is reported rather than resolved by picking one.
    /// </para>
    /// <para>
    /// <b>No credential of ours is anywhere in this.</b> The person's own access
    /// to their own host is the channel, which is <c>ReaderSessions</c>' shape:
    /// the declaration carries a locator at most, and the child resolves it for
    /// itself.
    /// </para>
    /// </remarks>
    public static string Suggestion(RunnerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.Label is not { Length: > 0 } label)
        {
            // NOTHING TO NAME. A row the control plane never reported has no
            // machine to reach, and a suggestion composed from a guess is what
            // this whole method exists not to offer.
            return "";
        }

        var machine = label.Split(':')[0];

        // WHAT THE FLIGHT IS SAYING, FIRST, when there is a flight. Everything
        // slice thirty-four built was reachable from nowhere: a person opening
        // this pane found an ssh command and no way to use any of it. The two
        // answer different questions and both belong here - `watch` is what the
        // FLIGHT is saying, and the ssh below is what the RUNNER is doing.
        //
        // OFFERED ONLY WHILE SOMETHING IS FLYING, because a channel to a runner
        // exists only while a flight does. Offering it on an idle machine would
        // be a command that always fails, which is the thing this method exists
        // not to do.
        var watch = row.Work is { Length: > 0 } flying
            ? $"\n\n  gg runner watch {row.Id}\n\n"
            + $"What {flying} is saying, as it says it, over a channel this control plane "
            + "relays and cannot read. It answers only for a flight opened to be watched; an "
            + "ordinary flight has no channel, however healthy the machine is. Ctrl-C stops it."
            : "";

        if (label.EndsWith(":maintain", StringComparison.Ordinal))
        {
            return watch + $"\n\n  ssh {machine} sudo journalctl -u gg-runner-maintain -n 200\n\n"
                 + "That is the unit gg ships for a pool runner, so the command is a fact "
                 + "about our packaging. Whether that host answers to this name is not.";
        }

        if (label.EndsWith(":hand", StringComparison.Ordinal))
        {
            return watch + $"\n\n  ssh {machine} tail -n 200 {LogPath}\n\n"
                 + "A console started that one, so gg wrote the file. Suggested rather than "
                 + "reported: nothing here came from the control plane.";
        }

        return watch + $"\n\n  ssh {machine} tail -n 200 {LogPath}\n"
             + $"  ssh {machine} sudo journalctl -u 'gg-runner-*' -n 200\n\n"
             + "One of the two: gg writes the file when a console starts a runner, and a "
             + "service manager keeps the output itself when one starts it instead. Which "
             + "is which is not something the control plane reports.";
    }

    /// <summary>
    /// Where gg puts a runner log, spelled the way a person would type it.
    /// </summary>
    /// <remarks>
    /// <b>Not <see cref="Gg.Local.LocalPaths.StateRoot"/> resolved.</b> That
    /// answers for THIS machine - and on macOS it answers
    /// <c>~/Library/Application Support/good-grief</c>, which is not where the
    /// Linux host in the suggestion keeps anything. The tilde form is the
    /// convention rather than this machine's answer to it.
    /// </remarks>
    private const string LogPath = "~/.local/state/good-grief/runner.log";
}
