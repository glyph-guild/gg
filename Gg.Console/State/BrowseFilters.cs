using System.Text;

namespace Gg.Console;

/// <summary>Which part of a listing a choice narrows.</summary>
/// <remarks>
/// <b>Three, because a query takes three</b> - and they behave differently
/// enough that a row has to say which it is: an area path and a sprint replace,
/// a state accumulates.
/// </remarks>
public enum BrowseFacet
{
    /// <summary>Where the tracker files it. One, and items beneath it.</summary>
    AreaPath,

    /// <summary>The sprint. One, exactly.</summary>
    Iteration,

    /// <summary>A state. Any number of them, because a query takes a set.</summary>
    State,
}

/// <summary>
/// The filter modal's views, and how to move between them.
/// </summary>
/// <remarks>
/// <b>Pure, and asked by the keymap as well as the pane</b> - which is
/// <see cref="RunnerViews"/>' rule one modal over. Which key is offered and
/// which view is drawn are the same question, and two answers to it drift.
/// </remarks>
public static class FilterViews
{
    /// <summary>
    /// All three, always.
    /// </summary>
    /// <remarks>
    /// <b>Unconditional, and a tracker that offers none of one still has the
    /// tab.</b> An empty tab is an answer - this project files nothing by
    /// sprint - where a tab that vanished would leave a person wondering which
    /// key they had failed to find.
    /// </remarks>
    public static IReadOnlyList<BrowseFacet> All { get; } =
        [BrowseFacet.AreaPath, BrowseFacet.Iteration, BrowseFacet.State];

    /// <summary>What the tab for a view says.</summary>
    /// <remarks>
    /// <b>Lower case and plural, the words a person would use</b> - the rule
    /// the runner modal's tabs already follow, and they sit along the same kind
    /// of foot. Here rather than in the screen because the screen cannot be
    /// constructed without a terminal, so nothing could ask it what it drew.
    /// </remarks>
    public static string Title(BrowseFacet view) => view switch
    {
        BrowseFacet.Iteration => "sprints",
        BrowseFacet.State => "states",
        _ => "area paths",
    };

    /// <summary>What one row of this view holds, as a column heading.</summary>
    public static string Column(BrowseFacet view) => view switch
    {
        BrowseFacet.Iteration => "sprint",
        BrowseFacet.State => "state",
        _ => "area path",
    };

    /// <summary>The next view round.</summary>
    public static BrowseFacet Next(BrowseFacet showing)
    {
        var at = All.ToList().IndexOf(showing);

        return at < 0 ? All[0] : All[(at + 1) % All.Count];
    }
}

/// <summary>
/// What a tracker offered to narrow by, as rows to walk with a cursor.
/// </summary>
/// <remarks>
/// <para>
/// <b>One flat list, not three.</b> A modal with three cursors is three modals;
/// this is the shape <c>WorkKindChoice</c> already uses over a list whose
/// length is somebody else's, and a person moves through it with the two keys
/// every list in this console is walked with.
/// </para>
/// <para>
/// <b>Every group starts with the row that clears it.</b> Taking the sprint off
/// while keeping the team is a thing people do constantly, and the alternative
/// is a key that clears everything and a person who narrows again from scratch.
/// </para>
/// <para>
/// <b>Pure, so what a keystroke lands on can be asked without a terminal.</b>
/// The reducer and the pane both index this list, and two answers about which
/// row is under the cursor is a modal that picks a different thing from the one
/// it highlighted.
/// </para>
/// </remarks>
public static class BrowseFilters
{
    /// <summary>One row of one of the filter modal's tables.</summary>
    /// <param name="Facet">Which dimension it narrows.</param>
    /// <param name="Value">The value, exactly as the tracker offered it.</param>
    /// <param name="Chosen">Whether it is currently part of the filter.</param>
    public sealed record Choice(BrowseFacet Facet, string Value, bool Chosen);

    /// <summary>
    /// What one view offers, in the order the tracker gave it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exactly what the tracker said, with nothing invented.</b> An "any"
    /// row was here to clear one dimension; in a table it is a row to scroll
    /// past, and the key that picks already takes a pick back - so the row
    /// bought nothing and cost every person one line of every list.
    /// </para>
    /// <para>
    /// <b>Not re-sorted.</b> A tracker's classification tree comes back in its
    /// own order, parents before children, and alphabetising it would separate
    /// a team from the sub-teams under it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Choice> Offered(AppState state, BrowseFacet view)
    {
        ArgumentNullException.ThrowIfNull(state);

        var facets = state.Facets;

        return view switch
        {
            BrowseFacet.Iteration =>
            [
                .. (facets?.Iterations ?? []).Select(value => new Choice(
                    view, value,
                    string.Equals(value, state.ChosenIteration, StringComparison.Ordinal))),
            ],

            BrowseFacet.State =>
            [
                .. (facets?.States ?? []).Select(value => new Choice(
                    view, value, state.ChosenStates.Contains(value, StringComparer.Ordinal))),
            ],

            _ =>
            [
                .. (facets?.AreaPaths ?? []).Select(value => new Choice(
                    view, value,
                    string.Equals(value, state.ChosenAreaPath, StringComparison.Ordinal))),
            ],
        };
    }

    /// <summary>Where the cursor is in the view that is showing.</summary>
    public static int Cursor(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Cursor(state, state.FilterView);
    }

    /// <summary>Where the cursor is in one view, whichever is showing.</summary>
    /// <remarks>
    /// Every table is filled on every render, including the two behind the one
    /// showing - so each needs its own cursor asked for by name, or turning the
    /// bar would land on row zero of a list somebody had already walked.
    /// </remarks>
    public static int Cursor(AppState state, BrowseFacet view)
    {
        ArgumentNullException.ThrowIfNull(state);

        return view switch
        {
            BrowseFacet.Iteration => state.IterationSelected,
            BrowseFacet.State => state.StateSelected,
            _ => state.AreaSelected,
        };
    }

    /// <summary>
    /// The row the cursor is on, or null where the showing view offers none.
    /// </summary>
    /// <remarks>
    /// Clamped rather than trusted: a list that shrank under a cursor - a
    /// second reader, a sprint somebody closed - would otherwise pick by index
    /// into nothing.
    /// </remarks>
    public static Choice? Under(AppState state)
    {
        var rows = Offered(state, state.FilterView);

        return rows.Count == 0
            ? null
            : rows[Math.Clamp(Cursor(state), 0, rows.Count - 1)];
    }

    /// <summary>
    /// The filter in force, as one sentence, or null when nobody narrowed.
    /// </summary>
    /// <remarks>
    /// <b>Null is "nobody narrowed", and it is not "no filter".</b> A pane that
    /// printed a filter line over every unfiltered listing would be noise on
    /// the ordinary case to serve the rare one.
    /// </remarks>
    public static string? Said(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var parts = new StringBuilder();

        if (state.ChosenAreaPath is { Length: > 0 } area)
        {
            parts.Append(area);
        }

        if (state.ChosenIteration is { Length: > 0 } iteration)
        {
            Separated(parts).Append(iteration);
        }

        if (state.ChosenStates.Count > 0)
        {
            Separated(parts).Append(string.Join(", ", state.ChosenStates));
        }

        return parts.Length > 0 ? parts.ToString() : null;
    }

    private static StringBuilder Separated(StringBuilder parts) =>
        parts.Length > 0 ? parts.Append(" · ") : parts;
}
