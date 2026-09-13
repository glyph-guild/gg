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
    /// <summary>One row of the filter modal.</summary>
    /// <param name="Facet">Which dimension it narrows.</param>
    /// <param name="Value">The value, or null for the row that clears this dimension.</param>
    /// <param name="Said">What the row reads as on screen.</param>
    /// <param name="Chosen">Whether this value is currently part of the filter.</param>
    public sealed record Choice(BrowseFacet Facet, string? Value, string Said, bool Chosen);

    /// <summary>Every choice on offer, in the order the modal draws them.</summary>
    public static IReadOnlyList<Choice> Rows(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var facets = state.Facets;

        List<Choice> rows =
        [
            .. Group(
                BrowseFacet.AreaPath, "Area", facets?.AreaPaths ?? [],
                value => string.Equals(value, state.ChosenAreaPath, StringComparison.Ordinal),
                state.ChosenAreaPath is null),

            .. Group(
                BrowseFacet.Iteration, "Sprint", facets?.Iterations ?? [],
                value => string.Equals(value, state.ChosenIteration, StringComparison.Ordinal),
                state.ChosenIteration is null),

            .. Group(
                BrowseFacet.State, "State", facets?.States ?? [],
                value => state.ChosenStates.Contains(value, StringComparer.Ordinal),
                state.ChosenStates.Count == 0),
        ];

        return rows;
    }

    private static IEnumerable<Choice> Group(
        BrowseFacet facet, string label, IReadOnlyList<string> offered,
        Func<string, bool> chosen, bool none)
    {
        yield return new Choice(facet, null, $"{label,-7} any", none);

        foreach (var value in offered)
        {
            yield return new Choice(facet, value, $"{label,-7} {value}", chosen(value));
        }
    }

    /// <summary>
    /// The row a cursor is on, or null where there is nothing to be on.
    /// </summary>
    /// <remarks>
    /// Clamped rather than trusted: a list that shrank under a cursor - a
    /// second reader, a tracker that lost a sprint - would otherwise pick by
    /// index into nothing.
    /// </remarks>
    public static Choice? Under(AppState state)
    {
        var rows = Rows(state);

        return rows.Count == 0
            ? null
            : rows[Math.Clamp(state.FilterSelected, 0, rows.Count - 1)];
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
