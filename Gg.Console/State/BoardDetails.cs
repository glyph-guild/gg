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

    /// <summary>
    /// The flight this row opened into, when this console is holding it.
    /// </summary>
    /// <remarks>
    /// <b>Null for two different reasons, and the key wants neither.</b> A
    /// standing row opened into nothing, and the flights tab is PAGED - so a
    /// row answered a fortnight ago names a flight this console has not
    /// loaded. Moving the cursor to a row that is not there would leave it
    /// somewhere arbitrary and read as a jump gone wrong, so the key is not
    /// offered at all. The modal still prints the number, which is the part a
    /// person can act on by typing it.
    /// </remarks>
    public static string? FlightInTheList(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (NominationUnder(state) is not { FlightId: { } flight })
        {
            return null;
        }

        var wanted = flight.ToString();

        return Rows.Flights(state).Any(
                   r => string.Equals(r.FlightId, wanted, StringComparison.OrdinalIgnoreCase))
            ? wanted
            : null;
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

        var fields = new List<BoardField>();

        if (NominationUnder(state) is { } nomination)
        {
            // NAMED IN THE BODY AND NOT ONLY IN THE TITLE. Two nominations
            // from one watch differ only in their subject, so a question that
            // does not name it is one nobody can answer safely - and the body
            // is what `CopyModal' copies, where a title does not travel.
            fields.Add(new BoardField("subject", ControlText.Strip(nomination.Subject)));
            fields.Add(new BoardField("nominated by", ControlText.Strip(nomination.Nominator)));

            // WHOSE ROW IT IS, the pair the board page sends: the display a
            // person reads and the subject a document spells them with.
            if (nomination.For is { Length: > 0 } whose)
            {
                fields.Add(new BoardField(
                    "for",
                    nomination.ForDisplay is { Length: > 0 } display
                        ? $"{ControlText.Strip(display)} ({ControlText.Strip(whose)})"
                        : ControlText.Strip(whose)));
            }

            if (nomination.WorkKind is { Length: > 0 } kind)
            {
                fields.Add(new BoardField("kind", ControlText.Strip(kind)));
            }

            fields.Add(new BoardField(
                "standing",
                nomination.Ending is { Length: > 0 } ending
                    ? ControlText.Strip(ending)
                    : "waiting for somebody"));

            fields.Add(new BoardField("nominated", $"{nomination.MadeAt:u}"));

            // WHAT IT BECAME, and only once it became something. FlightId is
            // null on every standing row by definition, and a field reading
            // "none" would be a line per row saying nothing has happened yet -
            // on the screen somebody came to to make something happen.
            if (nomination.FlightId is not null)
            {
                // SAID EVEN BEFORE IT IS NUMBERED. The number is minted into a
                // perspective and arrives late, so a row answered seconds ago
                // has a flight and no number - and printing nothing there would
                // read as "it opened into nothing", which is the one thing it
                // did not do.
                fields.Add(new BoardField(
                    "flight",
                    nomination.FlightNumber is { Length: > 0 } number
                        ? ControlText.Strip(number)
                        : "not numbered yet"));
            }

            return fields;
        }

        if (WatchUnder(state) is { } watch)
        {
            fields.Add(new BoardField("watch", ControlText.Strip(watch.Name)));
            fields.Add(new BoardField("executor", ControlText.Strip(watch.Executor ?? "none yet")));

            fields.Add(new BoardField(
                "last heard",
                watch.LastHeardAt is { } heard ? $"{heard:u}" : "never reported"));

            // WHAT THE CONTROL PLANE SAID about the next one, never a time
            // computed here: the schedule is timed between two decisions and
            // latched while a sweep has not reported, so arithmetic on this
            // side would disagree with the planner exactly when somebody is
            // asking why nothing has run.
            fields.Add(new BoardField(
                "next sweep",
                watch.NextSweepAt is { } next
                    ? $"{next:u}"
                    : watch.NextSweepSaid is { Length: > 0 } said
                        ? ControlText.Strip(said)
                        : "not said"));

            fields.Add(new BoardField("cost", Rows.CostOf(watch)));

            if (watch.Account is { Length: > 0 } account)
            {
                fields.Add(new BoardField("read as", ControlText.Strip(account)));
            }

            if (watch.QuietSince is { } since)
            {
                fields.Add(new BoardField("quiet since", $"{since:u}"));
            }

            return fields;
        }

        return fields;
    }

    /// <summary>
    /// The row as one block of text: the fields, then the sentence.
    /// </summary>
    /// <remarks>
    /// <b>The sentence last and on its own</b>, because it is prose and the
    /// rest are scalars - and because it is the long one. It is what the table
    /// used to clip.
    /// </remarks>
    internal static string Linear(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Under(state) is null)
        {
            // SAID, NOT BLANK, and reachable: a board re-read underneath
            // somebody, or a row answered from another console.
            return "There is nothing on this row any more.\n"
                 + "The board may have been read again since you opened it.";
        }

        var text = new System.Text.StringBuilder();

        foreach (var field in Fields(state))
        {
            text.AppendLine($"  {field.Label,-13} {field.Value}");
        }

        if (Because(state) is { Length: > 0 } because)
        {
            text.AppendLine();
            text.AppendLine(ControlText.Strip(because));
        }

        if (Rows.StandingUnder(state) is not null)
        {
            text.AppendLine();
            text.AppendLine(
                "Either answer opens your editor for the reason, and nothing is sent until "
              + "you save and quit. The reason is the only thing that survives to tell a "
              + "later reader why.");
        }

        return text.ToString();
    }

    /// <summary>The row's own sentence: a nominator's reason, or a diagnosis.</summary>
    private static string Because(AppState state) =>
        NominationUnder(state) is { } nomination
            ? nomination.Because ?? ""
            : WatchUnder(state)?.Diagnosis ?? "";
}
