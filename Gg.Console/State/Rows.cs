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
