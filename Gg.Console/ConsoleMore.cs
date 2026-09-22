using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// The next page of a list somebody has reached the end of.
/// </summary>
/// <remarks>
/// <para>
/// <b>Added, never replaced.</b> Every other read in this family answers with
/// what a pane should now show; this one answers with what should be UNDER what
/// it already shows. A patch that replaced the list would take away the rows
/// somebody scrolled through to ask for these, which is the one thing an
/// infinite scroll must not do.
/// </para>
/// <para>
/// <b>Patches, not models</b> - <c>BackgroundReads</c>' own rule, and it earns
/// its keep here more than anywhere else. The cursor that asked for this page
/// was on the last row when the request went out and may be anywhere by the
/// time it lands; a read answering with a whole <see cref="AppState"/> would be
/// a snapshot of a list taken before somebody moved and applied after.
/// </para>
/// <para>
/// <b>And it folds onto what is there NOW, which may not be what asked.</b> A
/// thirty-second refresh can land between the ask and the answer, so the page is
/// merged by identity rather than concatenated: a row already held is not held
/// twice.
/// </para>
/// <para>
/// <b>Through <c>ConsoleProjection.Apply</c>, like its neighbours.</b> That arm
/// is where a flight list becomes state - it is also where the cursor is
/// re-anchored to the flight it was on - and a module that assigned the field
/// itself would be a second opinion about both.
/// </para>
/// </remarks>
public static class ConsoleMore
{
    /// <summary>What to fold once the next page of flights has landed.</summary>
    public static Func<AppState, AppState> FlightsPatch(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        // NOTHING TO ASK FOR. The reducer already decided this, and it is
        // checked again because the answer came from a state that is one tick
        // old by the time the read runs.
        if (state.Flights?.Next is not { Length: > 0 } cursor)
        {
            return Nothing;
        }

        try
        {
            if (data.ListAsync(after: cursor).GetAwaiter().GetResult()
                is not VerbResult.Flights page)
            {
                return Nothing;
            }

            return current =>
            {
                if (current.Flights is not { } held)
                {
                    return ConsoleProjection.Apply(current, page);
                }

                var already = held.Flights
                    .Select(f => f.FlightId)
                    .ToHashSet(StringComparer.Ordinal);

                return ConsoleProjection.Apply(
                    current,
                    new VerbResult.Flights(new FlightList
                    {
                        Flights =
                        [
                            .. held.Flights,
                            .. page.Value.Flights.Where(f => already.Add(f.FlightId)),
                        ],
                        Next = page.Value.Next,
                    }));
            };
        }
        catch (Exception failed)
        {
            return Said(failed);
        }
    }

    /// <summary>What to fold once the next page of nominations has landed.</summary>
    /// <remarks>
    /// <b>Ended rows too, as the board's own read asks for.</b> The next page
    /// answers the same question as the first; a page that quietly dropped them
    /// would be a board that changed what it was showing halfway down.
    /// </remarks>
    public static Func<AppState, AppState> BoardPatch(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (state.Board?.Next is not { Length: > 0 } cursor)
        {
            return Nothing;
        }

        try
        {
            if (data.BoardAsync(ended: true, after: cursor).GetAwaiter().GetResult()
                is not VerbResult.Board page)
            {
                return Nothing;
            }

            return current =>
            {
                if (current.Board is not { } held)
                {
                    return ConsoleProjection.Apply(current, page);
                }

                var already = held.Nominations.Select(n => n.NominationId).ToHashSet();

                return ConsoleProjection.Apply(
                    current,
                    new VerbResult.Board(new BoardPage
                    {
                        Nominations =
                        [
                            .. held.Nominations,
                            .. page.Value.Nominations.Where(n => already.Add(n.NominationId)),
                        ],

                        // BOTH PAGES' ANSWER, and they are the same answer
                        // because both reads asked the same way. The pane
                        // captions its absence from this, so an `&&` here would
                        // let one short page recaption the whole board.
                        IncludedEnded = held.IncludedEnded || page.Value.IncludedEnded,
                        Next = page.Value.Next,
                    }));
            };
        }
        catch (Exception failed)
        {
            return Said(failed);
        }
    }

    /// <summary>The page it was already on.</summary>
    private static AppState Nothing(AppState state) => state;

    /// <summary>
    /// A failed page is a sentence, never a throw.
    /// </summary>
    /// <remarks>
    /// This runs on a task beside a console somebody is scrolling, and an
    /// exception escaping it would take the process down over rows they could
    /// have done without. What is on screen stays on screen.
    /// </remarks>
    private static Func<AppState, AppState> Said(Exception failed) =>
        current => current with
        {
            Diagnosis = "The next page did not arrive: " + failed.Message,
        };
}
