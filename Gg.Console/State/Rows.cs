using Gg.Contracts;

namespace Gg.Console;

/// <summary>One row of a table, for the three views that are lists.</summary>
/// <remarks>
/// <b>What goes in a cell is the model's; drawing it is the widget's.</b> These
/// panes formatted their own columns into a string, which meant every column
/// was as wide as the widest value anybody imagined and nothing said what a
/// column was. A record per row keeps the values checkable without a terminal
/// and leaves the alignment to something that can measure the screen.
/// </remarks>
public sealed record FlightRow(
    string FlightId, string Number, string State, string Loop, string Age, string Work);

/// <summary>One registered repository, and whether this console is flying against it.</summary>
public sealed record RepositoryRow(string Chosen, string Path, string Name);

/// <summary>
/// One thing that happened to a flight, as a row of the log.
/// </summary>
/// <param name="When">By the clock of whatever recorded it.</param>
/// <param name="Attempt">
/// Which pass this belongs to, or empty. Empty and never <c>0</c>: an entry
/// from a record that never carried an attempt is absent rather than first,
/// and a cell reading zero would be this console inventing one.
/// </param>
/// <param name="Happened">
/// The contract's own sentence for the kind and its params - never the kind. A
/// loop that ended blocked read as <c>loop-ended</c> for as long as this was a
/// column of enum members.
/// </param>
/// <param name="Said">
/// Prose somebody actually wrote, flattened to one line because a cell is one
/// line. Nothing is dropped: the table scrolls sideways, so a diagnosis wider
/// than the column is a diagnosis a person can still read to the end.
/// </param>
public sealed record LogRow(string When, string Attempt, string Happened, string Said);

/// <summary>
/// One runner in the fleet, and whether it is this machine's.
/// </summary>
/// <param name="Mine">
/// Whether this row is the runner registered on this machine.
/// </param>
/// <param name="Here">
/// The mark that says so, because a bool cannot be a cell.
/// </param>
/// <param name="Work">
/// The flight it holds, or empty. Empty rather than a dash: idle and holding a
/// flight are different answers and one placeholder for both says neither.
/// </param>
/// <param name="Labels">
/// What it advertises, which is the column that answers why a flight will not
/// fly here.
/// </param>
public sealed record RunnerRow(
    bool Mine,
    bool Yours,
    bool Machine,
    string Here,
    string Runner,
    string State,
    string Work,
    string Labels,
    string Heard);

/// <summary>
/// The rows behind the three tables, and the names of their columns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, and the order is the order a cursor indexes.</b> The flights list
/// is newest first because that is what a person opens it to see; a cursor
/// pointing at row two means the second row ON THE SCREEN, so the order has to
/// live here rather than in the view.
/// </para>
/// <para>
/// <b>Empty rather than a header over nothing.</b> A table with no rows says a
/// read succeeded and found nothing, which is one of three things an empty pane
/// can mean - the others being a read that failed and a view nobody has fetched
/// - so each pane keeps its own sentence for those and the table is drawn only
/// when there is something to put in it.
/// </para>
/// </remarks>
public static class Rows
{
    /// <summary>What a person reads down each column of the flights table.</summary>
    public static IReadOnlyList<string> FlightColumns { get; } =
        ["flight", "state", "loop", "age", "work"];

    public static IReadOnlyList<string> BrowseColumns { get; } = ["item", "state", "title"];

    /// <summary>
    /// The log's columns, inside the flight modal rather than on a tab.
    /// </summary>
    /// <remarks>
    /// <b>Here with the tabs' columns, because a reader looking for a table's
    /// cells should find one file.</b> The log is the fifth table this console
    /// draws and the first that is not a tab; where it is drawn is the view's
    /// business, and what is in a cell is this one's.
    /// </remarks>
    public static IReadOnlyList<string> LogColumns { get; } =
        ["when", "#", "what happened", "said"];

    /// <summary>
    /// The runners' columns, the first of which has no name.
    /// </summary>
    /// <remarks>
    /// It holds the mark against this machine's own runner, for the reason the
    /// repositories' first column has none: a heading over a column of marks is
    /// a word explaining a symbol that already explains itself.
    /// </remarks>
    public static IReadOnlyList<string> RunnerColumns { get; } =
        ["", "runner", "state", "working on", "advertises", "last heard"];

    /// <summary>
    /// The repositories' columns, the first of which has no name.
    /// </summary>
    /// <remarks>
    /// It holds the mark against the one this console is flying against, and a
    /// heading over a column of marks would be a word explaining a symbol that
    /// explains itself.
    /// </remarks>
    public static IReadOnlyList<string> RepositoryColumns { get; } = ["", "path", "name"];

    /// <summary>Every flight this tenant has, newest first.</summary>
    public static IReadOnlyList<FlightRow> Flights(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Flights is not { } list)
        {
            return [];
        }

        return
        [
            .. list.Flights
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FlightRow(
                    f.FlightId,
                    f.FlightNumber,
                    f.State,
                    PaneText.LoopEndingOf(f),
                    PaneText.AgeOf(f.CreatedAt),
                    f.Name)),
        ];
    }

    /// <summary>
    /// The tracker's work items, in the order it answered.
    /// </summary>
    /// <remarks>
    /// <b>Already rows, and that is the finding.</b> <c>BrowseRow</c> has held
    /// an id, a title and a state since the pane was written; the renderer took
    /// those three fields and formatted them into one string, which is the step
    /// this whole change removes. Nothing is reordered either: a tracker's own
    /// order is a decision somebody made in the tracker, and a console that
    /// sorted it would be second-guessing a query it did not write.
    /// </remarks>
    public static IReadOnlyList<BrowseRow> Browse(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Browse?.Items ?? [];
    }

    /// <summary>
    /// The fleet, this machine first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ours first, and it is the only row here anybody can act on.</b>
    /// Another tenant's runner being busy is information; this one being absent
    /// means <c>gg runner up</c> was never run or has died, which is a thing to
    /// go and do. The rest keep the order the control plane sent.
    /// </para>
    /// <para>
    /// <b>And a runner registered here that the fleet has never seen is still a
    /// row.</b> That is the case a person is most likely to be in - the machine
    /// is registered and the process is not running, so it has never
    /// heartbeated and the control plane has nothing to list. A tab that showed
    /// only what the fleet knows would be blank in exactly the situation
    /// somebody opened it to diagnose.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<RunnerRow> Runners(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var fleet = state.Runners?.Runners ?? [];
        var mine = state.LocalRunnerId;
        var machine = state.Machine;
        var you = state.PrincipalId;

        // THIS MACHINE'S RUNNERS TOGETHER, AND THE ONE WITH KEYS BEHIND IT
        // FIRST. Registering twice is what running `gg runner up` twice does,
        // and only the registration in this console's own file was being
        // lifted - so the others sat in a fleet ordered by nothing, which on
        // the live one is fifteen rows deep.
        //
        // ORDERED BY, NOT SORTED. OrderBy is stable in .NET, so everything
        // below keeps the order the control plane sent it in: re-ranking the
        // fleet here would be the console inventing an order that the same
        // list read through `gg runner list` does not have.
        var rows = fleet
            .Select(r => Row(
                r,
                string.Equals(r.RunnerId, mine, StringComparison.Ordinal),

                // WHOSE IT IS, WHEN ANYBODY SAID. Both sides must be non-empty:
                // a console with no session has no principal id, and every
                // runner registered before the control plane recorded one has
                // no principal id either - so an empty-matches-empty test would
                // hand a signed-out console the entire fleet as its own.
                you is { Length: > 0 }
                    && string.Equals(r.RegisteredByPrincipalId, you, StringComparison.Ordinal),

                // EQUAL, NOT A PREFIX. `vmlinux001:maintain` is a real label
                // and it is a DIFFERENT runner from `vmlinux001` - a prefix
                // match would put another host's housekeeping process in the
                // group a person reads as theirs.
                machine is { Length: > 0 }
                    && string.Equals(r.Label, machine, StringComparison.Ordinal)))

            // YOURS ABOVE THIS MACHINE'S, because one is a fact the control
            // plane recorded and the other is an inference from a label. The
            // machine rank stays underneath rather than being replaced: a
            // runner registered before the id shipped has none permanently, and
            // dropping those into the fleet would lose rows the machine
            // grouping was already showing - including a person's own, on the
            // machine they are sitting at.
            .OrderBy(r => r.Mine ? 0 : r.Yours ? 1 : r.Machine ? 2 : 3)
            .ToList();

        if (rows.Any(r => r.Mine) is false && mine is { Length: > 0 })
        {
            // REGISTERED AND NEVER HEARD FROM, which is what offline means.
            // Inventing a fourth word for it here would be a second vocabulary
            // for the same fact, and RunnerStates is the one the control plane
            // derives.
            rows.Insert(0, new RunnerRow(
                Mine: true,

                // NOT CLAIMED AS YOURS. This row is invented from a file this
                // machine wrote, so nothing about it came from the control
                // plane - including who registered it.
                Yours: false,
                Machine: true,
                Here: Ours,
                Runner: Short(mine),
                State: RunnerStates.Offline,
                Work: "",

                // NOTHING EITHER WAY. Labels come from what a runner
                // heartbeats, and this one never has - which is a different
                // silence from advertising nothing, and the pane's own sentence
                // is where that difference is stated.
                Labels: "",
                Heard: "never"));
        }

        return rows;
    }

    /// <summary>
    /// Whether this machine has no runner running.
    /// </summary>
    /// <remarks>
    /// <b>Three cases and one predicate, because one command answers all
    /// three.</b> Nothing registered here; registered and never heard from,
    /// which is what <c>gg runner up</c> having been run once and the process
    /// being gone looks like; and registered with a heartbeat that has gone
    /// stale. <c>gg runner up</c> is the remedy for each, so a caller asking
    /// "should I offer the start" is asking one question.
    /// </remarks>
    public static bool NoRunnerHere(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // A CHILD THIS CONSOLE STARTED AND HAS NOT SEEN DIE IS NOT NOTHING.
        // It registers and then heartbeats, so for a few seconds the fleet has
        // no row for it and the honest answer is "coming up" rather than "none
        // here" - and a start key that is live during those seconds is a second
        // runner one press away.
        if (state.Here is { Up: true })
        {
            return false;
        }

        return Runners(state).FirstOrDefault(r => r.Mine) is not { } mine
            || mine.State == RunnerStates.Offline;
    }

    /// <summary>The mark against the runner this console can act on.</summary>
    private const string Ours = "→";

    /// <summary>
    /// The mark against the rest of a person's own group - their runners
    /// elsewhere, and this machine's other registrations.
    /// </summary>
    /// <remarks>
    /// <b>Not the arrow, deliberately.</b> Stop, restart and the log all act on
    /// the runner this console holds a pidfile for; these are other processes,
    /// on other hosts or long gone. They are grouped with it because they are
    /// what a person is looking for, and marked differently because none of the
    /// keys pointed at the arrow will do anything to them.
    /// <para>
    /// <b>One mark for both claims rather than two.</b> A third symbol would
    /// ask a person to learn which of "yours" and "this machine's" a glyph
    /// meant, to distinguish two rows they can do exactly as much about.
    /// </para>
    /// </remarks>
    private const string Alongside = "·";

    private static RunnerRow Row(RunnerSummary runner, bool mine, bool yours, bool machine) => new(
        Mine: mine,
        Yours: yours,
        Machine: machine || mine,
        Here: mine ? Ours : yours || machine ? Alongside : " ",
        Runner: Short(runner.RunnerId) + (runner.Label is { Length: > 0 } label
            ? "  " + label
            : ""),
        State: runner.State,
        Work: runner.CurrentFlightNumber ?? "",
        Labels: string.Join(", ", runner.Labels.Select(Advertised)),
        Heard: runner.LastHeartbeatAt is { } at ? at.ToString("u") : "never");

    /// <summary>
    /// One advertised label, and a word only when it is worth one.
    /// </summary>
    /// <remarks>
    /// <c>measured</c> means the name has a registered meaning - a predicate
    /// evaluated from produced facts - and is the ordinary case, so it costs no
    /// words. <c>stated</c> means somebody claimed it and nothing checks, which
    /// is the half a person reading a fleet wants to notice.
    /// </remarks>
    private static string Advertised(AdvertisedLabel label) =>
        label.Disposition == LabelDispositions.Measured
            ? label.Name
            : $"{label.Name} ({label.Disposition})";

    /// <summary>
    /// Enough of an id to tell two runners apart, and no more.
    /// </summary>
    /// <remarks>
    /// A full uuid in a cell pushes the columns a person is reading off the
    /// screen, and nobody types one of these - the label is how a runner is
    /// recognised and the prefix is how it is disambiguated.
    /// </remarks>
    private static string Short(string runnerId) =>
        runnerId.Length <= 8 ? runnerId : runnerId[..8];

    /// <summary>What this tenant may fly against.</summary>
    public static IReadOnlyList<RepositoryRow> Repositories(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Repositories is not { } listed)
        {
            return [];
        }

        return
        [
            .. listed.Repositories.Select(r => new RepositoryRow(
                string.Equals(r.Path, state.ChosenRepository, StringComparison.Ordinal) ? "→" : " ",
                r.Path,
                r.Name)),
        ];
    }

    /// <summary>
    /// Everything recorded against the flight the modal is open on, oldest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The story's own order, which is the order it happened in.</b> The
    /// other four tables sort - newest flight first, this machine's runner first
    /// - because those answer "what should I look at". A history answers "how
    /// did this get here", and reversing it would be this console rearranging a
    /// sequence of events.
    /// </para>
    /// <para>
    /// <b>Empty when no story was fetched AND when one was fetched with nothing
    /// in it</b>, which are different facts a table cannot tell apart.
    /// <see cref="FlightDetails.LogAbsence"/> keeps the sentence for both.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<LogRow> Log(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight
            || PaneText.StoryOf(state, flight.FlightId) is not { } story)
        {
            return [];
        }

        return
        [
            .. story.Entries.Select(entry => new LogRow(
                $"{entry.At:u}",
                entry.Attempt is { } which
                    ? which.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "",
                ControlText.Strip(
                    Gg.Contracts.FlightStory.Sentence(entry.Kind, entry.Params)),
                OneLine(entry.Said))),
        ];
    }

    /// <summary>
    /// Prose as a cell: every line of it, on one line.
    /// </summary>
    /// <remarks>
    /// <b>Joined rather than truncated.</b> A halt's diagnosis runs to
    /// paragraphs and it is the only thing in the log that says what to do
    /// about the halt, so the second line is not less worth having than the
    /// first. Blank lines go, because a paragraph break rendered as two spaces
    /// is a gap a reader reads as the end.
    /// </remarks>
    private static string OneLine(string? said) =>
        said is not { Length: > 0 }
            ? ""
            : string.Join(
                " ",
                ControlText.Strip(said, allowLineBreaks: true)
                    .ReplaceLineEndings("\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries
                                | StringSplitOptions.TrimEntries));
}
