using System.Text;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// One plan, opened: what it is about, and every leg under it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, and no terminal anywhere in it</b> — <c>BoardDetails</c>' shape and
/// its reasons. What the modal draws is decided here so a test can read it
/// without a screen, and so the copy-out rendering and the widgets cannot come
/// to disagree about what the modal says.
/// </para>
/// <para>
/// <b>It exists because the table cannot hold it.</b> A leg's sentence is an
/// agent's prose and runs past a hundred characters; the pane cuts it at
/// <see cref="Rows.LegReasonFits"/>, and the whole of it has to be somewhere. A
/// leg's subject is a hash by construction, so that sentence is the only thing
/// telling two legs of one plan apart.
/// </para>
/// </remarks>
public static class ItineraryDetails
{
    /// <summary>The plan the tab's cursor is on, or null.</summary>
    /// <remarks>
    /// Through <see cref="Rows.Itineraries"/> rather than the page directly, so
    /// the modal is about the row a person was LOOKING at: the table sorts and
    /// filters, and an index into the unsorted page would open a different plan
    /// from the one under the cursor.
    /// </remarks>
    public static ItineraryRow? Under(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var plans = Rows.Itineraries(state);

        return state.ItinerariesSelected >= 0 && state.ItinerariesSelected < plans.Count
            ? plans[state.ItinerariesSelected]
            : null;
    }

    /// <summary>Every leg of that plan, newest first.</summary>
    public static IReadOnlyList<ItineraryLegRow> Legs(AppState state) =>
        Rows.ItineraryLegs(state);

    /// <summary>The leg the modal's own cursor is on, or null.</summary>
    public static ItineraryLegRow? LegUnder(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var legs = Legs(state);

        return state.ItineraryLegSelected >= 0 && state.ItineraryLegSelected < legs.Count
            ? legs[state.ItineraryLegSelected]
            : null;
    }

    /// <summary>What the modal is called: the plan's number.</summary>
    public static string Title(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Under(state) is { } plan && plan.Plan.Length > 0
            ? plan.About.Length > 0 ? $"{plan.Plan}  {plan.About}" : plan.Plan
            : PaneText.ModalTitle(UiMode.ItineraryDetail);
    }

    /// <summary>
    /// The flight the chosen leg opened into, when this console has it loaded.
    /// </summary>
    /// <remarks>
    /// <b>Null for two different reasons, and the key wants neither</b> —
    /// <c>BoardDetails.FlightInTheList</c>'s rule, for its reasons. A standing
    /// leg opened into nothing, and the flights tab is PAGED, so a leg answered
    /// a fortnight ago names a flight this console has not loaded. Moving the
    /// cursor onto a row that is not there would leave it somewhere arbitrary.
    /// The modal still prints the number, which is the part a person can act on
    /// by typing it.
    /// </remarks>
    public static string? FlightInTheList(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (LegNominationUnder(state) is not { FlightId: { } flight })
        {
            return null;
        }

        // THE ID, NOT THE NUMBER, because GoToTheFlight matches on the id and
        // this answer is handed straight to it. Returning the number would
        // make the shared arm need an arm of its own, which is the whole thing
        // reusing the command avoids.
        var wanted = flight.ToString();

        return Rows.Flights(state).Any(
                   r => string.Equals(r.FlightId, wanted, StringComparison.OrdinalIgnoreCase))
            ? wanted
            : null;
    }

    /// <summary>
    /// The chosen plan's legs as the control plane sent them, newest first.
    /// </summary>
    /// <remarks>
    /// <b>NOT the rows.</b> <see cref="Rows.ItineraryLegs"/> clips each
    /// sentence to the width of a table cell, and this modal exists because
    /// that cell is too narrow to read - so a copy taken from the rows would
    /// hand somebody an ellipsis and call it the record.
    /// </remarks>
    private static IReadOnlyList<NominationSummary> LegsSent(AppState state)
    {
        if (Under(state) is not { Key.Length: > 0 } plan)
        {
            return [];
        }

        return
        [
            .. (state.Itineraries?.Nominations ?? [])
                .Where(n => string.Equals(n.Nominator, plan.Key, StringComparison.Ordinal))
                .OrderByDescending(n => n.MadeAt),
        ];
    }

    /// <summary>The nomination behind the leg the cursor is on.</summary>
    /// <remarks>
    /// Matched by the row's key, which is the nomination id: the rows are
    /// sorted and the page is not, so an index into the page would find a
    /// different leg from the one a person is pointing at.
    /// </remarks>
    private static NominationSummary? LegNominationUnder(AppState state)
    {
        if (LegUnder(state) is not { } leg)
        {
            return null;
        }

        return LegsSent(state).FirstOrDefault(n => string.Equals(
            n.NominationId.ToString(), leg.Key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The tracker item this plan is about, when a reader here can read it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>From the PLAN, not from the leg.</b> A leg's subject is
    /// <c>leg:{kind}@{digest}</c> - a hash, by construction, so identical pairs
    /// converge - and there is no ticket in it. Every leg of a plan is about
    /// the same piece of work the planning flight read, and that is what the
    /// nomination's intent key carries.
    /// </para>
    /// <para>
    /// <b>And only where a reader is configured</b>, which is
    /// <c>FlightDetails.TicketAReaderHere</c>'s rule: an id this console cannot
    /// fetch opens a modal that can only say so.
    /// </para>
    /// </remarks>
    public static (string Provider, string Id)? TicketAReaderHere(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Under(state) is not { About.Length: > 0 } plan)
        {
            return null;
        }

        // THROUGH THE RULE THAT ALREADY OWNS IT. `gg fly --ticket ado#18291`
        // splits a reference this way and CliArgs delegates here rather than
        // repeating it; a second split written in the console is how the two
        // come to disagree about a provider with a `#` in its name.
        var (provider, id) = Gg.Client.PastedIntent.SplitTicket(plan.About);

        if (provider is not { Length: > 0 } || id is not { Length: > 0 })
        {
            return null;
        }

        return state.ReaderKeys.Contains(provider, StringComparer.Ordinal)
            ? (ControlText.Strip(provider), ControlText.Strip(id))
            : null;
    }

    /// <summary>What the legs pane is called: how many, and how they stand.</summary>
    public static string LegsTitle(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var legs = LegsSent(state).Count;

        return legs == 1 ? "1 leg" : $"{legs} legs";
    }

    /// <summary>
    /// What the detail pane is called: which leg is being read.
    /// </summary>
    /// <remarks>
    /// The KIND and the flight rather than "what it is", because the pane's
    /// body is the sentence and a title repeating the heading above it tells a
    /// person nothing about which of five rows they are looking at.
    /// </remarks>
    public static string DetailTitle(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (LegNominationUnder(state) is not { } leg)
        {
            return "what it is";
        }

        // WHICH OF HOW MANY, because every leg of a plan is usually the same
        // kind - both of the first real itinerary's are `implement` - so a
        // title naming only the kind cannot say which row is being read.
        var which = $"leg {state.ItineraryLegSelected + 1} of {LegsSent(state).Count}";

        return leg.FlightNumber is { Length: > 0 } flight
            ? $"{which}  {leg.WorkKind}  {flight}"
            : $"{which}  {leg.WorkKind}";
    }

    /// <summary>
    /// The chosen leg's sentence, whole, wrapped to the pane it is drawn in.
    /// </summary>
    /// <remarks>
    /// <b>THE WHOLE POINT OF THE PANE.</b> The table above cuts each sentence
    /// at <see cref="Rows.LegReasonFits"/> so the columns after it survive, and
    /// a detail pane repeating the cut would make this modal a bigger box
    /// around the same ellipsis.
    /// </remarks>
    public static IReadOnlyList<string> DetailLines(AppState state, int columns)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (LegNominationUnder(state) is not { } leg)
        {
            return ["no leg is selected."];
        }

        if (leg.Reason is not { Length: > 0 } said)
        {
            // ABSENT IS "NOBODY SAID", and a pane drawn empty reads as one
            // still loading. Every leg that mattered carries one; a plan from
            // before the member does not.
            return ["this leg came with no reason."];
        }

        return [.. PaneText.Wrapped(said, Math.Max(columns, 20)).Split('\n')];
    }

    /// <summary>The whole modal as one document, for copying out.</summary>
    /// <remarks>
    /// <b>The sentences UNCUT, which is most of why this modal exists.</b> The
    /// table clips a leg at <see cref="Rows.LegReasonFits"/> because the columns
    /// after it have to survive; a copy that reproduced the clipped form would
    /// hand somebody an ellipsis and call it the record.
    /// </remarks>
    internal static string Linear(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Under(state) is not { } plan)
        {
            return "no plan is open.";
        }

        var text = new StringBuilder();

        text.Append(plan.Plan);

        if (plan.About.Length > 0)
        {
            text.Append("  ").Append(plan.About);
        }

        text.Append('\n').Append(plan.Legs).Append(
            string.Equals(plan.Legs, "1", StringComparison.Ordinal) ? " leg, " : " legs, ")
            .Append(plan.State).Append(", proposed ").Append(plan.Since).Append(" ago.\n");

        var legs = LegsSent(state);

        for (var at = 0; at < legs.Count; at++)
        {
            var leg = legs[at];

            // THE CURSOR, DRAWN. `f` acts on one of these and this modal is
            // prose rather than a table, so without a mark a person is asked
            // to act on a leg the screen never said they were on. Two spaces
            // where it is not, so the lines stay aligned and only one of them
            // moves as the cursor does.
            text.Append('\n').Append(at == state.ItineraryLegSelected ? "> " : "  ")
                .Append(leg.WorkKind);

            if (leg.FlightNumber is { Length: > 0 } flight)
            {
                text.Append("  ").Append(flight);
            }

            text.Append("  ").Append(leg.Ending is { Length: > 0 } ended ? ended : leg.Mode)
                .Append('\n');

            if (leg.Reason is { Length: > 0 } said)
            {
                // WHOLE, and this is the line the modal is for. The table cut
                // it at Rows.LegReasonFits so the columns after it survived;
                // repeating that cut here would make this modal a wider copy
                // of the pane a person opened it to get past.
                text.Append("    ").Append(said.ReplaceLineEndings("\n    ")).Append('\n');
            }
        }

        return text.ToString();
    }
}
