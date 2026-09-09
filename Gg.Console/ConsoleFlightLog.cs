using Gg.Client;

namespace Gg.Console;

/// <summary>
/// One flight's story, read when somebody opens it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The boot used to have all of them and that is what made it slow.</b> A
/// log per flight ever flown, fetched before the console drew anything, so that
/// the enter key would cost nothing - and on a tenant with fifty flights that
/// was fifty round trips for the two or three the queue could use. The boot now
/// reads only what is still in the air; this is where the rest arrive.
/// <para>
/// <b>The STORY rather than the log, since slice thirty-two.</b> One request
/// either way, so the price the enter key pays does not move - S32.0-02 flags
/// an unmeasured cost for a read fetched per selected row, and swapping which
/// endpoint an existing round trip calls is not that. What changes is that the
/// modal can render sentences instead of kinds beside raw JSON.
/// </para>
/// <para>
/// The boot still fetches LOGS for flights in the air, because the queue's two
/// log-derived reasons are counted from log entries and nothing here changes
/// that.
/// </para>
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
    /// <summary>
    /// The same read, as a patch applied to whatever is on screen when it lands.
    /// </summary>
    /// <remarks>
    /// <b>A patch and not a model</b>, which is <c>AutoRefresh</c>'s argument:
    /// a read answering with a whole <see cref="AppState"/> is a snapshot taken
    /// before the person moved the cursor and applied after. This one runs
    /// beside the console rather than in place of it — the modal is already
    /// open while it is in the air — so between asking and answering somebody
    /// may well have moved.
    /// </remarks>
    public static Func<AppState, AppState> Patch(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (PaneText.Detailed(state) is not { } flight)
        {
            return current => current;
        }

        // ALREADY HELD, ALREADY PAID FOR - the same rule Read has, for the same
        // reason: a flight opened twice in a row costs once.
        if (state.Story is { } held
            && string.Equals(held.FlightId, flight.FlightId, StringComparison.Ordinal))
        {
            return current => current;
        }

        try
        {
            var story = data.StoryAsync(flight.FlightNumber).GetAwaiter().GetResult();

            return current => ConsoleProjection.Apply(current, story);
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the pane, and a different sentence from
            // "nothing happened to this flight" - PaneText already tells the
            // three absences apart.
            var said = "The flight's story could not be read: " + failure.Message;

            return current => current with { Diagnosis = said };
        }
    }

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

        // ALREADY HELD, ALREADY PAID FOR. One story is held at a time, so this
        // is a re-read only when the cursor has moved to another flight - and a
        // flight opened twice in a row costs once. A refresh is what makes a
        // stale one fresh, which is the same answer every other pane gives.
        if (state.Story is { } held
            && string.Equals(held.FlightId, flight.FlightId, StringComparison.Ordinal))
        {
            return state;
        }

        try
        {
            return ConsoleProjection.Apply(
                state, data.StoryAsync(flight.FlightNumber).GetAwaiter().GetResult());
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or HttpRequestException)
        {
            // ITS OWN FAILURE, said in the pane. The modal opens either way and
            // says the story could not be read - which is a different sentence
            // from "nothing happened to this flight", and PaneText already
            // tells those two apart.
            return state with
            {
                Diagnosis = "The flight's story could not be read: " + failure.Message,
            };
        }
    }
}
