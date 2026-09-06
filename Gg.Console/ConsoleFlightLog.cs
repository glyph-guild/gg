using Gg.Client;

namespace Gg.Console;

/// <summary>
/// One flight's log, read when somebody opens it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The boot used to have all of them and that is what made it slow.</b> A
/// log per flight ever flown, fetched before the console drew anything, so that
/// the enter key would cost nothing - and on a tenant with fifty flights that
/// was fifty round trips for the two or three the queue could use. The boot now
/// reads only what is still in the air; this is where the rest arrive.
/// </para>
/// <para>
/// <b>A composition-root function, not a method on the loop.</b>
/// <see cref="ConsoleLoop"/> is handed something it can call and never a read
/// surface - a <see cref="ConsoleData"/> inside its type would be one step from
/// a read surface inside a UI session, which is rule 3. The same shape as
/// <see cref="ConsoleChecklist"/> and its neighbours.
/// </para>
/// <para>
/// <b>Through <c>Apply</c>, which is rule 2.</b> Assigning the field here would
/// be a second projection one layer down and harder to see.
/// </para>
/// </remarks>
public static class ConsoleFlightLog
{
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        // WHICH FLIGHT THE CURSOR IS ON, from the same function that decides
        // what the modal will show. Two answers to "which flight" is how the
        // pane came to render one row's detail under another row's name.
        if (PaneText.Detailed(state) is not { } flight)
        {
            return state;
        }

        // ALREADY HELD, ALREADY PAID FOR. A flight still in the air arrives with
        // its log from the boot, and one opened twice should not cost twice.
        // A refresh is what makes a stale one fresh, which is the same answer
        // every other pane in this console gives.
        if (state.Logs.ContainsKey(flight.FlightId))
        {
            return state;
        }

        try
        {
            return ConsoleProjection.Apply(
                state, data.LogAsync(flight.FlightId).GetAwaiter().GetResult());
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the pane. The modal opens either way and
            // says the log could not be read - which is a different sentence
            // from "nothing happened to this flight", and PaneText already
            // tells those two apart.
            return state with
            {
                Diagnosis = "The flight's log could not be read: " + failure.Message,
            };
        }
    }
}
