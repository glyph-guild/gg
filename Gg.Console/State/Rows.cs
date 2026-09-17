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
    string FlightId, string Number, string State, string Kind, string Loop, string Age, string Work);

/// <summary>
/// One registered repository: what it is, whether this console is flying
/// against it, and whether this machine could.
/// </summary>
/// <remarks>
/// <b>The last three columns are the ones that refuse a flight.</b> A path and
/// a name say which repository this is; the credential says whether work on it
/// can start here, the ref says whether a flight naming none can start at all,
/// and the narrowings directory says whether every file in it is policy. None
/// of the three was visible in this console before.
/// </remarks>
public sealed record RepositoryRow(
    string Chosen,
    string Path,
    string Name,
    string Provider,
    string Credential,
    string Ref,
    string Narrowings);

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
/// line. <b>Carried rather than drawn:</b> no column renders it, because a
/// table cell is not a place to read prose. It is what the pane beneath the
/// log renders for the entry under the cursor, and what the linear rendering
/// behind <c>PaneText.Modal</c> reads.
/// </param>
public sealed record LogRow(
    int Entry,
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

    /// <summary>
    /// The machine's own name, as the control plane holds it.
    /// </summary>
    /// <remarks>
    /// <b>Beside <c>Runner</c> rather than parsed back out of it.</b> That column
    /// is a display - a short id, two spaces, then this - and recovering the
    /// label by splitting it would be a string format standing in for a value.
    /// It is here because a suggested command needs the name to ssh to, and a
    /// suffix on it says how the runner was started.
    /// </remarks>
    string Label,

    /// <summary>Why it was withheld, or empty when nobody did.</summary>
    string ParkedBecause,
    string Here,
    string Runner,
    string State,
    string Work,
    string Labels,
    string Heard,

    /// <summary>
    /// The runner that warmed this one, when this row is a pool member, and
    /// empty when it is a machine in its own right.
    /// </summary>
    /// <remarks>
    /// <b>A fact, not a position.</b> Where a row is drawn is
    /// <see cref="Under"/>; this is only what the control plane said.
    /// </remarks>
    string HostRunnerId = "",

    /// <summary>The machine this runner runs on, or empty when nobody said.</summary>
    /// <remarks>
    /// <b>Not <see cref="Machine"/>, which is a bool</b> saying whether this row
    /// is the runner registered on the machine running this console.
    /// </remarks>
    string MachineName = "",

    /// <summary>
    /// The row this one is drawn beneath, or empty when it sits flush.
    /// </summary>
    /// <remarks>
    /// <b>Decided once, by <c>Rows.Runners</c>, and read by every surface.</b>
    /// Whether a row is nested depends on the WHOLE list - whether its machine
    /// has a resident here, whether its host is here at all - so no single row
    /// can answer it, and a renderer that tried would disagree with the order.
    /// Empty rather than null, like every other absent string on this record.
    /// </remarks>
    string Under = "");

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
    /// <remarks>
    /// <b><c>kind</c> is beside <c>state</c> rather than beside <c>work</c>,
    /// and the two are not the same question.</b> <c>work</c> is what somebody
    /// CALLED this flight; <c>kind</c> is what governs it. Two flights with
    /// one name can be bound by different envelopes, so the column that tells
    /// them apart belongs with the other facts about the flight rather than
    /// tucked beside the prose.
    /// </remarks>
    public static IReadOnlyList<string> FlightColumns { get; } =
        ["flight", "state", "kind", "loop", "age", "work"];

    /// <summary>
    /// What a person reads down each column of the work list.
    /// </summary>
    /// <remarks>
    /// <b>Where it is filed is a column because it is what a filter narrows
    /// on.</b> A person who narrows to a team and is shown the same
    /// undifferentiated list cannot tell a filter that took from one that did
    /// not - and with no filter at all it is the fastest way to see which part
    /// of a project the backlog is actually in.
    /// </remarks>
    public static IReadOnlyList<string> BrowseColumns { get; } =
        ["item", "state", "where", "title"];

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
        ["time", "attempt", "event"];

    /// <summary>
    /// The runners' columns, the first of which has no name.
    /// </summary>
    /// <remarks>
    /// It holds the mark against this machine's own runner, for the reason the
    /// repositories' first column has none: a heading over a column of marks is
    /// a word explaining a symbol that already explains itself.
    /// </remarks>
    /// <summary>
    /// The runner cell with a member pushed in under the machine that warmed
    /// it, which is how every surface draws the fleet's shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Composed here rather than stored on the row.</b> <see cref="RunnerRow"/>
    /// keeps what is true - which host warmed this one - and this is where
    /// that becomes an indent. The record is written to disk under
    /// <c>GG_STATE_DUMP</c> and read back by things that are not a renderer,
    /// so spaces baked into it are presentation in the wrong place: a spike
    /// feeding those cells to a <c>TreeView</c> had to strip them off again
    /// before drawing, one renderer undoing another's decision.
    /// </para>
    /// <para>
    /// <b>A function rather than a column style.</b> Terminal.Gui's
    /// <c>RepresentationGetter</c> would be the idiomatic home and cannot do
    /// it: it is handed the CELL VALUE and nothing else, so it cannot tell a
    /// member from a machine. The table's cell projection has the row and so
    /// does the pane, so both call this and neither invents its own amount.
    /// </para>
    /// </remarks>
    public static string Nested(RunnerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (row.Under.Length > 0 ? "  " : "") + row.Runner;
    }

    /// <summary>
    /// What a person reads down each column of the board.
    /// </summary>
    /// <remarks>
    /// <b>The first column says which kind of row this is, because the board
    /// holds two.</b> A nomination is work somebody could open; a sweep is the
    /// watch that goes looking for it. They share a pane because they are one
    /// story - a watch finds an item, the item stands as a nomination, a person
    /// opens it - and a reader who cannot tell them apart at a glance has a
    /// list of two things pretending to be one.
    /// </remarks>
    public static IReadOnlyList<string> BoardColumns { get; } =
        ["", "subject", "state", "kind", "when", "why"];

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
    public static IReadOnlyList<string> RepositoryColumns { get; } =
        ["", "path", "name", "provider", "credential", "ref", "narrowings"];

    /// <summary>
    /// One filter tab's columns: the mark, and the value.
    /// </summary>
    /// <remarks>
    /// <b>The mark column has no heading</b>, for the reason the runners' and
    /// the repositories' first columns have none: a word explaining a symbol
    /// that already explains itself. What it marks is what is in the filter.
    /// </remarks>
    public static IReadOnlyList<string> FilterColumns(BrowseFacet view) =>
        ["", FilterViews.Column(view)];

    /// <summary>
    /// The work item history's columns.
    /// </summary>
    /// <remarks>
    /// <b>Three, because a change has three parts</b> - and it arrived as one
    /// rendered line until now, which is a table flattened at the last point
    /// anybody could still see it was one.
    /// </remarks>
    public static IReadOnlyList<string> WorkItemColumns { get; } = ["when", "who", "what"];

    /// <summary>
    /// The work kind question's columns.
    /// </summary>
    /// <remarks>
    /// <b>Two, because a name alone is a question only its author can
    /// answer.</b> The kinds are a tenant's own words and somebody opening this
    /// for the first time is picking between strings they have never seen.
    /// </remarks>
    public static IReadOnlyList<string> WorkKindColumns { get; } = ["kind", "what it is for"];

    /// <summary>The mark against a value that is in the filter.</summary>
    /// <remarks>
    /// The same arrow the repositories table puts against the one this console
    /// is flying against, and for the same reason: it points at the row rather
    /// than decorating it, so a column of them reads as a list of picks.
    /// </remarks>
    public const string Picked = "→";

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
                    // NOT DEFAULTED TO `implement`, which is what the control
                    // plane substitutes at CREATION. Filling an absent value in
                    // here would be right by coincidence for new flights and a
                    // fabrication for every summary from a control plane that
                    // does not send this yet - on the field that says which
                    // envelope governs.
                    f.WorkKind is { Length: > 0 } kind ? kind : "(not said)",
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
    /// <summary>
    /// The board: every nomination, and every watch that makes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nominations first, watches under them</b>, because the nominations
    /// are what somebody can act on and the watches are why they are there. A
    /// person opening this pane is usually answering a row rather than auditing
    /// a schedule.
    /// </para>
    /// <para>
    /// <b>A watch that has gone quiet reads as a state rather than a
    /// timestamp.</b> `quiet` is the word the control plane uses for it and
    /// the row a person acts on; making them subtract two clocks to find that
    /// out is how a board comes to look healthy while nothing is sweeping.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<BoardRow> Board(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = new List<BoardRow>();

        foreach (var nomination in (state.Board?.Nominations ?? [])
            .OrderByDescending(n => n.MadeAt))
        {
            rows.Add(new BoardRow(
                nomination.NominationId.ToString(),
                BoardRow.Nomination,
                nomination.Subject,
                // WHAT IT IS NOW: the ending if it has one, the mode if it is
                // still standing. A row reading `standing` that has in fact
                // been refused is the one thing this column must never say.
                nomination.Ending is { Length: > 0 } ended ? ended : nomination.Mode,
                nomination.WorkKind,
                PaneText.AgeOf(nomination.MadeAt),
                // THE SENTENCE THAT ENDED IT, or the one that gated it. A
                // standing row with neither says nothing here rather than
                // borrowing a word from somewhere else.
                nomination.Because ?? ""));
        }

        foreach (var watch in (state.Watches?.Standings ?? [])
            .OrderBy(w => w.Name, StringComparer.Ordinal))
        {
            rows.Add(new BoardRow(
                watch.Name,
                BoardRow.Sweep,
                watch.Name,
                watch.QuietSince is not null
                    ? "quiet"
                    : watch.Outcome is { Length: > 0 } outcome ? outcome : "never swept",
                watch.Executor ?? "",
                watch.LastHeardAt is { } heard ? PaneText.AgeOf(heard) : "-",
                Spent(watch)));
        }

        return rows;
    }

    /// <summary>
    /// What a watch has cost, with the scale beside it.
    /// </summary>
    /// <remarks>
    /// <b>`3` says nothing and `3 of 5 in 24h` says whether the next one will
    /// stand.</b> A watch with no budget prints the count and no bound, because
    /// unbounded is a state rather than a bound of zero - and the sentence is
    /// the runner's own when it could not sweep, because that is the only thing
    /// anybody can act on.
    /// </remarks>
    /// <summary>
    /// The nomination under the board's cursor that can still be answered, or
    /// null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Through the rows rather than into the list, because the cursor
    /// indexes what is on the screen.</b> <see cref="Board"/> orders
    /// nominations newest first and then puts the watches underneath, so the
    /// nth nomination and the nth row are different things - and reading the
    /// second as the first is how a key answers a row somebody is not looking
    /// at.
    /// </para>
    /// <para>
    /// <b>Null for a watch, and null for a row already ended.</b> A watch is
    /// the machinery that made the rows above it and has nothing on it to
    /// answer; an ended row is a 409 at the door, and a key advertised where it
    /// will be refused is worse than no key at all.
    /// </para>
    /// </remarks>
    public static NominationSummary? StandingUnder(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = Board(state);

        if (state.BoardSelected < 0 || state.BoardSelected >= rows.Count)
        {
            return null;
        }

        var row = rows[state.BoardSelected];

        if (!string.Equals(row.What, BoardRow.Nomination, StringComparison.Ordinal))
        {
            return null;
        }

        var one = (state.Board?.Nominations ?? [])
            .FirstOrDefault(n => n.NominationId.ToString() == row.Key);

        // STANDING IS WHAT THE ENDING SAYS, and it is the row's own word rather
        // than `State`: the board sends both, and the one every other reader
        // here already trusts is the ending.
        return one is not null && one.Ending is not { Length: > 0 } ? one : null;
    }

    private static string Spent(Gg.Contracts.WatchStanding watch) =>
        watch.Diagnosis is { Length: > 0 } why
            ? why
            : watch.Budgeted is { } bound
                ? $"{watch.Opened} of {bound} in {watch.Window}"
                : $"{watch.Opened} in {watch.Window}";

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

        rows = UnderTheirHosts(rows);

        if (rows.Any(r => r.Mine) is false && mine is { Length: > 0 })
        {
            // REGISTERED AND NEVER HEARD FROM, which is what offline means.
            // Inventing a fourth word for it here would be a second vocabulary
            // for the same fact, and RunnerStates is the one the control plane
            // derives.
            rows.Insert(0, new RunnerRow(
                Mine: true,
                Id: Short(mine),
                // Nothing about this row came from the control plane; it is
                // invented from a file this machine wrote.
                ParkedBecause: "",

                // EMPTY, NOT THIS MACHINE'S NAME. The label is what the control
                // plane holds, and this row is one the control plane has never
                // heard of - so there is nothing to ssh to and no suffix saying
                // how it was started. A suggestion composed from a guess is the
                // thing step 2 exists not to offer.
                Label: "",
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

    /// <summary>
    /// Arranges the fleet as machines: each row that sits flush, followed by
    /// everything drawn beneath it, with <see cref="RunnerRow.Under"/> set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The resident is the anchor.</b> On a host, the runner whose label IS
    /// the machine name is the one a person thinks of as the machine, so every
    /// other runner on that host - the pool maintainer, an attended runner, the
    /// members - is drawn beneath it, as peers. Nesting members under the
    /// maintainer instead is what put two healthy members beneath a
    /// registration that never beats and reads as offline.
    /// </para>
    /// <para>
    /// <b>Then the host, as a fallback.</b> A control plane that has not
    /// learned machines sends none, and a host running only a maintainer has
    /// no resident to sit under. In both cases a member is drawn beneath the
    /// runner that warmed it, which is what the fleet showed before machines
    /// existed - so nothing flattens while the two repositories catch up.
    /// </para>
    /// <para>
    /// <b>A member with no machine is on its host's.</b> The mint writes a
    /// member's machine from its maintainer's row, so a member minted before
    /// the maintainer stated one carries none. Grouping it by its host's
    /// machine is the same fact read one step later - and without it the
    /// member points at a maintainer that is itself nested, and lands flush.
    /// </para>
    /// <para>
    /// <b>An orphan keeps its place.</b> A row whose anchor and host are both
    /// absent sits flush where it was, because a revoked host is exactly when
    /// somebody needs to see the machine asking for help.
    /// </para>
    /// <para>
    /// <b>Stable.</b> Rows keep the order they arrived in among their peers;
    /// this only decides where each group sits.
    /// </para>
    /// </remarks>
    private static List<RunnerRow> UnderTheirHosts(List<RunnerRow> rows)
    {
        var present = new HashSet<string>(
            rows.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);

        // THE RESIDENT OF EACH MACHINE: the row whose label is the machine's
        // own name. First wins, so a host registered twice anchors once.
        var residents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.MachineName.Length > 0
                && string.Equals(row.Label, row.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                residents.TryAdd(row.MachineName, row.Id);
            }
        }

        var machines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            machines.TryAdd(row.Id, row.MachineName);
        }

        string Parent(RunnerRow row)
        {
            var machine = row.MachineName.Length > 0
                ? row.MachineName
                : machines.GetValueOrDefault(row.HostRunnerId, "");

            if (machine.Length > 0
                && residents.TryGetValue(machine, out var resident)
                && !string.Equals(resident, row.Id, StringComparison.OrdinalIgnoreCase))
            {
                return resident;
            }

            return row.HostRunnerId.Length > 0 && present.Contains(row.HostRunnerId)
                ? row.HostRunnerId
                : "";
        }

        var placed = rows.Select(r => r with { Under = Parent(r) }).ToList();

        if (placed.All(r => r.Under.Length == 0))
        {
            return placed;
        }

        // ONE LEVEL, deliberately. A row whose parent is itself drawn beneath
        // something would need a tree; this list has two depths, and a row that
        // would have been a grandchild is listed flush instead.
        var roots = placed.Where(r => r.Under.Length == 0).ToList();
        var rootIds = new HashSet<string>(
            roots.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);

        var ordered = new List<RunnerRow>(placed.Count);
        foreach (var root in roots)
        {
            ordered.Add(root);
            ordered.AddRange(placed.Where(c => string.Equals(
                c.Under, root.Id, StringComparison.OrdinalIgnoreCase)));
        }

        // Anything whose parent was not a root is still listed, flush, rather
        // than dropped.
        ordered.AddRange(placed
            .Where(c => c.Under.Length > 0 && !rootIds.Contains(c.Under))
            .Select(c => c with { Under = "" }));

        return ordered;
    }

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

        // Cleaned at ingress like every other string a control plane composes:
        // this record is written to disk under GG_STATE_DUMP and read back by
        // things that are not PaneText.
        ParkedBecause: ControlText.Strip(runner.ParkedBecause),
        Label: ControlText.Strip(runner.Label),
        Here: mine ? Ours : yours ? Owned : machine ? Alongside : " ",
        Runner: Short(runner.RunnerId) + (runner.Label is { Length: > 0 } label
            ? "  " + label
            : ""),
        // BOTH FACTS OR NEITHER. Parking sits beside the state on the wire
        // because a runner can be parked AND busy - draining, which is the
        // reason to park anything. A column that printed only State would show
        // a machine somebody deliberately withheld as `idle`, which is exactly
        // the pair the claim path refuses to collapse.
        State: runner.ParkedAt is null
            ? runner.State
            : $"{runner.State} · parked",
        Work: runner.CurrentFlightNumber ?? "",
        Labels: string.Join(", ", runner.Labels.Select(Advertised)),
        Heard: runner.LastHeartbeatAt is { } at ? at.ToString("u") : "never",

        // Stripped like every other string a control plane composes, even
        // though this one is an id: the doorway cleans, and an exception here
        // would be an exception nobody told the next reader about.
        HostRunnerId: ControlText.Strip(runner.HostRunnerId ?? ""),
        MachineName: ControlText.Strip(runner.Machine ?? ""));

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

    /// <summary>One row of the compose modal's repositories tab.</summary>
    public sealed record FlyingWithRow
    {
        /// <summary>Whether this flight names it.</summary>
        public string Mark { get; init; } = "";

        /// <summary>The path the control plane knows it by.</summary>
        public string Path { get; init; } = "";

        /// <summary>What a person calls it.</summary>
        public string Name { get; init; } = "";

        /// <summary>Whether the credential it needs is here.</summary>
        public string Credential { get; init; } = "";
    }

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
                state.ChosenRepositories.Contains(r.Path, StringComparer.Ordinal) ? "→" : " ",
                r.Path,
                r.Name,
                r.Provider,
                Gg.Client.RepositoryCredentials.StandingOf(state.RepositoryCredentials, r.Path),
                // AN ABSENCE IS RENDERED, not blanked. "Null is different from
                // any ref": a ticket flight against a repository with no
                // default ref is refused, and an empty cell would read as a
                // column that failed to load.
                r.Ref is { Length: > 0 } pinned ? pinned : "(none)",
                r.Narrowings is { Length: > 0 } governed ? governed : "(off)")),
        ];
    }

    /// <summary>
    /// What this tenant may fly against, marked with what THIS flight names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same list as <see cref="Repositories"/> and a different mark.</b>
    /// That one shows what every new flight starts with; this shows what the
    /// flight being composed will actually name. Same rows, two ranges, and a
    /// person who has changed one for this flight can see that they have.
    /// </para>
    /// <para>
    /// <b>The credential standing comes with it</b>, because that is the thing
    /// that refuses a flight after it is opened. A person choosing what to fly
    /// against is deciding, and deciding without it means finding out from a
    /// grounded flight.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<FlyingWithRow> FlyingWith(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Repositories is not { } listed)
        {
            return [];
        }

        return
        [
            .. listed.Repositories.Select(r => new FlyingWithRow
            {
                // A BOX RATHER THAN AN ARROW, because several can be true at
                // once and an arrow reads as "this one". The empty box is drawn
                // rather than left blank: a column that is sometimes absent
                // reads as a column that failed.
                Mark = state.Against.Contains(r.Path, StringComparer.Ordinal) ? "[x]" : "[ ]",
                Path = r.Path,
                Name = r.Name,
                Credential = Gg.Client.RepositoryCredentials.StandingOf(
                    state.RepositoryCredentials, r.Path),
            }),
        ];
    }

    /// <summary>The columns the compose modal's repositories tab declares.</summary>
    public static IReadOnlyList<string> FlyingWithColumns { get; } =
        ["", "repository", "name", "credential"];

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
    /// Where to scroll the log so the cursor stays on the line it is on.
    /// </summary>
    /// <param name="was">The row the cursor was on, in the rows being replaced.</param>
    /// <param name="offset">What the view was scrolled to, in those same rows.</param>
    /// <param name="now">The row the cursor is on, in the rows replacing them.</param>
    /// <remarks>
    /// <para>
    /// <b>Every cursor move rewrites the row set, and a refill says nothing
    /// about the scroll.</b> The entry being left closes and the one being
    /// arrived at opens, so the rows between them move by however many lines
    /// those two details are worth - and the view stays scrolled to a number
    /// that meant something in the old set. Measured: reading to the end of a
    /// thirty-five-line detail and pressing down once more put the cursor three
    /// lines ABOVE the top of the viewport.
    /// </para>
    /// <para>
    /// <b>The cursor keeps its line, and the log moves around it.</b> The
    /// alternative is to anchor whatever entry is at the top of the viewport,
    /// which holds the text still and lets the highlight drift instead - and a
    /// drifting highlight is the symptom rather than the fix. What a person
    /// follows is the highlight.
    /// </para>
    /// <para>
    /// <b>Here rather than in the view.</b> A <c>TableView</c> cannot be
    /// constructed without a terminal, so arithmetic left in the view is
    /// arithmetic no test can reach.
    /// </para>
    /// </remarks>
    public static int KeepingTheCursorsLine(int was, int offset, int now)
    {
        // NEVER NEGATIVE. A cursor above the top of the viewport is the thing
        // this exists to prevent, so it is not an input to be honoured back out
        // again - which is what the widget hands over after it has clamped an
        // offset against a table that just got shorter.
        var line = Math.Max(0, was - offset);

        // AND NEVER PAST THE CURSOR. There is no room above the first row to
        // put the missing lines in, so near the top the cursor ends up closer to
        // it than it was. That is what actually happened: what was being read
        // closed up.
        return Math.Max(0, now - line);
    }
    /// <summary>
    /// One line broken into several, none wider than the room given.
    /// </summary>
    /// <remarks>
    /// <b>On words, and on characters when a word will not fit.</b> A path, a
    /// commit hash or a url has no spaces in it and is exactly the thing
    /// somebody opened the log to read - so a wrapper that could only break on
    /// spaces would drop the one line that mattered.
    /// <para>
    /// <b>Shared with the runner's log</b>, which wants the identical thing for
    /// the identical reason: a stack trace is mostly paths and a runner's own
    /// output is where they appear.
    /// </para>
    /// </remarks>
    internal static List<string> Wrapped(string text, int width)
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

/// <summary>
/// One row of the board: a nomination standing (or ended), or a watch and how
/// its sweeping is going.
/// </summary>
/// <remarks>
/// <b>One record for two shapes, because they share a pane and a cursor.</b>
/// The alternative - two tables stacked - gives a person two cursors on one
/// screen, which this console has already met once and wrote down: a pane and
/// its title answering one question from different places.
/// </remarks>
public sealed record BoardRow(
    string Key, string What, string Subject, string State, string Kind, string When, string Why)
{
    /// <summary>What a nomination's row says it is.</summary>
    /// <remarks>
    /// <b>A constant since a key started dispatching on it.</b> While this was
    /// only drawn, the word was a literal in one place and that was honest; now
    /// the answer key asks the column which kind of row it is before it offers
    /// anything, and two spellings of one word would be a key that silently
    /// stopped being offered.
    /// </remarks>
    public const string Nomination = "nomination";

    /// <summary>What a watch's row says it is.</summary>
    public const string Sweep = "sweep";
}
