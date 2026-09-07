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
/// One line of the log: an entry, or the continuation of one.
/// </summary>
/// <param name="Entry">
/// Which of the story's entries this line belongs to. <b>The cursor is kept as
/// one of these and never as a row number</b>, because an entry that unwraps
/// becomes several rows - a row number would point at a different thing the
/// moment anything expanded, and the view maps both ways through this.
/// </param>
/// <param name="Mark">
/// Whether there is prose under this entry, and whether it is showing. Empty
/// on an entry with nothing written against it, and empty on a continuation -
/// a mark repeated down one entry reads as several entries.
/// </param>
/// <param name="Time">
/// By the clock of whatever recorded it. Empty on a continuation: a timestamp
/// repeated down the left of one entry reads as several things happening at
/// once.
/// </param>
/// <param name="Attempt">
/// Which pass this belongs to, or empty. Empty and never <c>0</c>: an entry
/// from a record that never carried an attempt is absent rather than first,
/// and a cell reading zero would be this console inventing one.
/// </param>
/// <param name="Event">
/// <b>What this LINE says, which is why it is the only column with room.</b>
/// On the row that carries an entry it is the contract's own sentence for the
/// kind and its params - never the kind; a loop that ended blocked read as
/// <c>loop-ended</c> for as long as this was a column of enum members. On the
/// rows that continue it, one line of what somebody wrote. Both are an account
/// of the same event, and it is the last column, so it is the one that expands.
/// </param>
/// <param name="Detail">
/// The whole of what somebody wrote - <c>StoryEntry.Said</c> - flattened to one
/// line. <b>Carried rather than drawn:</b> no column renders it, because 29
/// characters is not a place to read prose. It is what
/// <see cref="Unwrapped"/> breaks into continuations, and what the linear
/// rendering behind <c>PaneText.Modal</c> reads.
/// </param>
public sealed record LogRow(
    int Entry,
    string Mark,
    string Time,
    string Attempt,
    string Event,
    string Detail);

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
    string RegisteredBy,

    /// <summary>The whole id, where <c>Runner</c> carries the short one.</summary>
    /// <remarks>
    /// <b>The grid shows eight characters and `gg runner` takes all of it.</b>
    /// Fifteen rows of full uuid is a column of noise, so the table shortens it
    /// - and the modal has to be able to show the whole thing, which means the
    /// row has to still have it. Recovering it by matching a short id back
    /// against the fleet would be a prefix comparison standing in for an
    /// identity.
    /// </remarks>
    string Id,
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
        ["", "time", "attempt", "event"];

    /// <summary>
    /// How wide the mark column is: one character, always.
    /// </summary>
    /// <remarks>
    /// Both marks are one character and the heading is blank, so this is a
    /// fact about the column rather than a measurement of what happens to be
    /// in it today.
    /// </remarks>
    private const int MarkWidth = 1;

    /// <summary>An entry with prose under it, showing.</summary>
    public const string Open = "▾";

    /// <summary>An entry with prose under it, not showing.</summary>
    public const string Closed = "▸";

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
                Id: Short(mine),
                // NOBODY, because nothing about this row came from the control
                // plane - it is invented from a file this machine wrote.
                RegisteredBy: "",

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

    /// <summary>
    /// The runner the cursor is on, or null when it is on none.
    /// </summary>
    /// <remarks>
    /// <b>One answer, because two disagreed.</b> The table drew its cursor from
    /// <c>RunnerSelected</c> and the modal drew its subject from "whichever row
    /// is this machine's", so enter on any row but the first opened the wrong
    /// runner - with the right shape, which is why nobody noticed.
    /// <para>
    /// <b>Null rather than a fallback.</b> The cursor is an index and the fleet
    /// shrinks under a refresh when a runner is revoked, so an index past the
    /// end is reachable. Answering "the local one" there is the same defect
    /// one race later.
    /// </para>
    /// </remarks>
    public static RunnerRow? Selected(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = Runners(state);

        return state.RunnerSelected >= 0 && state.RunnerSelected < rows.Count
            ? rows[state.RunnerSelected]
            : null;
    }

    /// <summary>The mark against the runner this console can act on.</summary>
    private const string Ours = "→";

    /// <summary>The mark against a runner this person registered.</summary>
    /// <remarks>
    /// <b>Not the arrow, deliberately.</b> Stop, restart and the log all act on
    /// the runner this console holds a pidfile for; this one is a process on
    /// another host, or a registration long gone. What it tells you is that you
    /// brought it up and know where to go, which is why it says something
    /// rather than nothing.
    /// <para>
    /// <b>And not the dot either, which is the correction.</b> One glyph
    /// covered both for a while, on the argument that a person can do exactly
    /// as much about either - nothing. That weighed what a row can be ACTED on
    /// and ignored what it TELLS you, and it was written while ownership was an
    /// inference from a machine label rather than something the control plane
    /// recorded.
    /// </para>
    /// </remarks>
    private const string Owned = "*";

    /// <summary>
    /// The mark against this machine's runners that nobody is recorded as
    /// having registered.
    /// </summary>
    /// <remarks>
    /// Every runner registered before the control plane began recording a
    /// principal is permanently in this state, so this is not a transitional
    /// glyph. A star would claim what the control plane declined to say; the
    /// row is marked at all because it is on the machine somebody is sitting
    /// at, which is the weaker claim the label can still support.
    /// </remarks>
    private const string Alongside = "·";

    private static RunnerRow Row(RunnerSummary runner, bool mine, bool yours, bool machine) => new(
        Mine: mine,
        Yours: yours,
        Machine: machine || mine,
        Id: ControlText.Strip(runner.RunnerId),

        // TEXT SOMEBODY ELSE CHOSE, and this is its doorway. Cleaned before
        // STORAGE rather than at render, which is the console's rule: this
        // record is written to disk under GG_STATE_DUMP and read back by things
        // that are not PaneText.
        RegisteredBy: ControlText.Strip(runner.RegisteredBy),
        Here: mine ? Ours : yours ? Owned : machine ? Alongside : " ",
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
            .. story.Entries.Select((entry, at) => new LogRow(
                at,

                // WHICH MARK IS A QUESTION ABOUT THE CURSOR, and the cursor is
                // not this method's business. Unwrapped puts it on.
                "",
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
    /// The same rows, with the entry under the cursor unwrapped.
    /// </summary>
    /// <param name="rows">What <see cref="Log"/> answered.</param>
    /// <param name="selected">Which ENTRY the cursor is on, not which row.</param>
    /// <param name="width">
    /// How wide the detail column is, from <see cref="DetailWidth"/>. Zero
    /// before anything has been laid out, and zero means wrap nothing - a wrap
    /// to no width is one row per character.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Terminal.Gui has no variable row heights, so an entry that unwraps
    /// becomes several rows.</b> The first carries the entry; the rest carry
    /// the remainder of its detail with the other three columns empty, so every
    /// row is still one line and the table still reads as a table.
    /// </para>
    /// <para>
    /// <b>Only the columns that size themselves are left empty, and that is the
    /// point.</b> A continuation contributes nothing to the width of time,
    /// attempt or event - so expanding one cannot move where the detail column
    /// starts, and the table does not shift sideways as the cursor travels.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<LogRow> Unwrapped(
        IReadOnlyList<LogRow> rows, int selected, int width)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var shown = new List<LogRow>(rows.Count);

        foreach (var row in rows)
        {
            var has = row.Detail.Length > 0;
            var open = has && row.Entry == selected && width > 0;

            shown.Add(row with { Mark = !has ? "" : open ? Open : Closed });

            if (!open)
            {
                continue;
            }

            // UNDER THE ENTRY, IN THE COLUMN THAT EXPANDS, and blank in the
            // three that size themselves - which is what keeps the table from
            // shifting sideways as the cursor travels, since a continuation
            // contributes nothing to any width.
            shown.AddRange(Wrapped(row.Detail, width)
                .Select(line => new LogRow(row.Entry, "", "", "", line, "")));
        }

        return shown;
    }

    /// <summary>
    /// How wide a continuation may be: what the wide column expands into.
    /// </summary>
    /// <param name="rows">The rows the table is holding.</param>
    /// <param name="available">The table's own width, which only it knows.</param>
    /// <remarks>
    /// <para>
    /// <b>The widget's rule, restated where a test can read it.</b> A column is
    /// as wide as the widest of its heading and its cells, with one column of
    /// separator after it; the last expands into whatever is left. Restating it
    /// is a cost, and the alternative was asking a <c>TableView</c> that cannot
    /// be constructed without a terminal - so the arithmetic would have been
    /// beyond the reach of any test, which is the same argument
    /// <see cref="ConsoleTheme"/> and <c>CollectionViews</c> already make.
    /// </para>
    /// <para>
    /// <b>The event column's own content is not subtracted, and that is the
    /// whole change.</b> It is the last column, so it takes what is left
    /// whatever is in it - and what is left, once the mark and the two fixed
    /// columns have had theirs, is a line rather than a quarter of one. The
    /// sentences are what a continuation shares the column with, not what it
    /// competes with.
    /// </para>
    /// </remarks>
    public static int DetailWidth(IReadOnlyList<LogRow> rows, int available)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // THE MARK IS ONE WIDE BY CONSTRUCTION, so it is a constant and not a
        // measurement. Measuring it made the answer depend on whether anything
        // was marked yet - rows straight out of Log carry no marks and rows out
        // of Unwrapped do - so the width moved by one the moment a cursor
        // landed, which is the jitter this whole arithmetic exists to prevent.
        var taken =
            MarkWidth + 1
          + Column(LogColumns[1], rows.Select(r => r.Time))
          + Column(LogColumns[2], rows.Select(r => r.Attempt));

        return Math.Max(0, available - taken);
    }

    /// <summary>One column's width, plus the separator that follows it.</summary>
    private static int Column(string heading, IEnumerable<string> cells) =>
        Math.Max(heading.Length, cells.Select(cell => cell.Length).DefaultIfEmpty(0).Max()) + 1;

    /// <summary>
    /// One line broken into several, none wider than the column.
    /// </summary>
    /// <remarks>
    /// <b>On words, and on characters when a word will not fit.</b> A path, a
    /// commit hash or a url has no spaces in it and is exactly the thing
    /// somebody opened the log to read - so a wrapper that could only break on
    /// spaces would drop the one line that mattered.
    /// </remarks>
    private static List<string> Wrapped(string text, int width)
    {
        var lines = new List<string>();
        var line = new System.Text.StringBuilder(width);

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word_ = word;

            // A WORD WIDER THAN THE COLUMN, cut where the column ends. Nothing
            // is lost; it simply continues on the next line.
            while (word_.Length > width)
            {
                if (line.Length > 0)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }

                lines.Add(word_[..width]);
                word_ = word_[width..];
            }

            if (line.Length > 0 && line.Length + 1 + word_.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word_);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines.Count == 0 ? [""] : lines;
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
