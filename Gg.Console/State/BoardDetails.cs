using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// One board row, opened: everything the table had no width for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shaped like <see cref="FlightDetails"/> and deliberately simpler.</b> A
/// flight has an intent, a log and facts, so its modal is a tab strip. A board
/// row has one screen's worth of scalars, so this is a field list - the
/// <c>Runner</c> modal's shape. A tab strip here would be a strip with one tab
/// in it, and the flight modal already shows what the other failure looks like:
/// <c>FlightTab.Facts</c> is in the enum, the reducer cycle and the linear text,
/// and no widget was ever added for it, so choosing it silently shows details.
/// </para>
/// <para>
/// <b>Two shapes, one reader, because the row is already one record for
/// two.</b> A nomination is somebody's question and a watch is the machinery
/// that asked it; they share a pane and a cursor, and a second modal would be a
/// second cursor on one screen - which this console has met before and wrote
/// down.
/// </para>
/// <para>
/// <b>This is where the sentences live now.</b> The table's `why' column held a
/// nominator's whole reason in a cell and clipped it, which is the reason this
/// modal exists rather than a nicety on top of it.
/// </para>
/// </remarks>
public static class BoardDetails
{
    /// <summary>One labelled scalar of a board row.</summary>
    public sealed record BoardField(string Label, string Value);

    /// <summary>The row under the cursor, whichever kind it is.</summary>
    public static BoardRow? Under(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = Rows.Board(state);

        return state.BoardSelected >= 0 && state.BoardSelected < rows.Count
            ? rows[state.BoardSelected]
            : null;
    }

    /// <summary>The nomination under the cursor, standing or ended.</summary>
    /// <remarks>
    /// <b>Not <see cref="Rows.StandingUnder"/>, which is about answering.</b>
    /// An ended row cannot be answered and can certainly be read - a person
    /// opening one is usually asking what became of it and why.
    /// </remarks>
    public static NominationSummary? NominationUnder(AppState state)
    {
        if (Under(state) is not { } row
            || !string.Equals(row.What, BoardRow.Nomination, StringComparison.Ordinal))
        {
            return null;
        }

        return (state.Board?.Nominations ?? [])
            .FirstOrDefault(n => n.NominationId.ToString() == row.Key);
    }

    /// <summary>The watch under the cursor.</summary>
    public static WatchStanding? WatchUnder(AppState state)
    {
        if (Under(state) is not { } row
            || !string.Equals(row.What, BoardRow.Sweep, StringComparison.Ordinal))
        {
            return null;
        }

        return (state.Watches?.Standings ?? [])
            .FirstOrDefault(w => string.Equals(w.Name, row.Key, StringComparison.Ordinal));
    }

    /// <summary>What the modal is called, which is what the row is about.</summary>
    public static string Title(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Under(state) is { } row && row.Subject is { Length: > 0 }
            ? ControlText.Strip(row.Subject)
            : PaneText.ModalTitle(UiMode.BoardDetail);
    }

    /// <summary>The row's scalars, in the order a person reads them.</summary>
    public static IReadOnlyList<BoardField> Fields(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // SCAFFOLDING. The red commit needs this to compile and nothing else;
        // what a row says is the green one.
        return [];
    }

    /// <summary>The row as one block of text.</summary>
    internal static string Linear(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return "";
    }

}
