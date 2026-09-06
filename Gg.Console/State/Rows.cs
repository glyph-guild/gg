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
public sealed record RunnerRow(
    bool Mine, string Here, string Runner, string State, string Work, string Heard);

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
    /// The runners' columns, the first of which has no name.
    /// </summary>
    /// <remarks>
    /// It holds the mark against this machine's own runner, for the reason the
    /// repositories' first column has none: a heading over a column of marks is
    /// a word explaining a symbol that already explains itself.
    /// </remarks>
    public static IReadOnlyList<string> RunnerColumns { get; } =
        ["", "runner", "state", "working on", "last heard"];

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

        var rows = fleet
            .Select(r => Row(r, string.Equals(r.RunnerId, mine, StringComparison.Ordinal)))
            .ToList();

        var ours = rows.FindIndex(r => r.Mine);

        if (ours > 0)
        {
            var row = rows[ours];
            rows.RemoveAt(ours);
            rows.Insert(0, row);
        }
        else if (ours < 0 && mine is { Length: > 0 })
        {
            // REGISTERED AND NEVER HEARD FROM, which is what offline means.
            // Inventing a fourth word for it here would be a second vocabulary
            // for the same fact, and RunnerStates is the one the control plane
            // derives.
            rows.Insert(0, new RunnerRow(
                Mine: true,
                Here: Ours,
                Runner: Short(mine),
                State: RunnerStates.Offline,
                Work: "",
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

    /// <summary>The mark against this machine's own runner.</summary>
    private const string Ours = "→";

    private static RunnerRow Row(RunnerSummary runner, bool mine) => new(
        Mine: mine,
        Here: mine ? Ours : " ",
        Runner: Short(runner.RunnerId) + (runner.Label is { Length: > 0 } label
            ? "  " + label
            : ""),
        State: runner.State,
        Work: runner.CurrentFlightNumber ?? "",
        Heard: runner.LastHeartbeatAt is { } at ? at.ToString("u") : "never");

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
}
