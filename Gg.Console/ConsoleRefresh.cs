using Gg.Client;

namespace Gg.Console;

/// <summary>
/// What one tab needs re-read, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A patch, not a model.</b> Each of these answers with a function that is
/// applied to whatever is on screen when it lands - so a person who moved the
/// cursor while the request was in the air keeps their cursor, and nothing here
/// has to list which fields are the read plane.
/// </para>
/// <para>
/// <b>A composition-root function, like its neighbours.</b>
/// <see cref="AutoRefresh"/> is handed something it can call and never a read
/// surface; a <see cref="ConsoleData"/> inside its type would be one step from a
/// read surface inside a UI session, which is the rule this whole arrangement
/// is built around.
/// </para>
/// <para>
/// <b>Every failure is one refresh's worth.</b> Rule 5's third sentence: the
/// rest of the model is still true, and emptying it because one read failed is
/// the shape that rule exists to stop. A refresh that cannot reach anybody says
/// so in the diagnosis and leaves the screen alone.
/// </para>
/// </remarks>
public static class ConsoleRefresh
{
    /// <summary>How many logs to have in the air at once, as at boot.</summary>
    private const int LogsAtOnce = 8;

    public static async Task<Func<AppState, AppState>> ForTabAsync(
        ConsoleData data, TabId tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            return tab switch
            {
                TabId.Queue or TabId.Flights => await TheFleetAndItsWorkAsync(
                    data, cancellationToken),
                TabId.Runners => Apply(await data.RunnersAsync(cancellationToken)),
                TabId.Repositories => Apply(await data.RepositoriesAsync(cancellationToken)),
                TabId.Checklist => Apply(await data.PlanAsync(null, cancellationToken)),
                TabId.Envelope => Apply(await data.EnvelopeAsync(cancellationToken)),
                _ => Nothing,
            };
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or FlightNotFoundException
                                            or NoEnvelopeException
                                            or HttpRequestException)
        {
            return state => state with
            {
                Diagnosis = "The last refresh did not finish: " + failure.Message,
            };
        }
    }

    /// <summary>Nothing to ask anybody: the tab reads a local file or a child.</summary>
    private static AppState Nothing(AppState state) => state;

    private static Func<AppState, AppState> Apply(VerbResult result) =>
        state => ConsoleProjection.Apply(state, result);

    /// <summary>
    /// The queue and the flights list, which are the same four reads.
    /// </summary>
    /// <remarks>
    /// <b>The queue is derived, so its inputs travel together.</b> Folding a
    /// new flight list without the runners and the gates it was ranked against
    /// would leave rows explained by an answer that has moved.
    /// </remarks>
    private static async Task<Func<AppState, AppState>> TheFleetAndItsWorkAsync(
        ConsoleData data, CancellationToken cancellationToken)
    {
        var listing = data.ListAsync(cancellationToken);
        var fleet = data.RunnersAsync(cancellationToken);
        var waiting = data.GatesAsync(cancellationToken);

        await Task.WhenAll(listing, fleet, waiting);

        var flights = (VerbResult.Flights)await listing;
        var runners = (VerbResult.Runners)await fleet;
        var gates = await waiting is VerbResult.Gates open ? open.Value : null;

        // A LOG FOR EVERY FLIGHT STILL FLYING, as at boot and for the same
        // reason: those are the only ones whose log can put a row in the queue.
        using var room = new SemaphoreSlim(LogsAtOnce);

        var reading = flights.Value.Flights
            .Where(flight => flight.State == Gg.Contracts.FlightStates.Open)
            .Select(async flight =>
            {
                await room.WaitAsync(cancellationToken);
                try
                {
                    return (flight.FlightId, Answer: await data.LogAsync(
                        flight.FlightId, cancellationToken));
                }
                finally
                {
                    room.Release();
                }
            })
            .ToList();

        var fetched = await Task.WhenAll(reading);

        return state =>
        {
            var logs = new Dictionary<string, Gg.Contracts.FlightLog>(
                state.Logs, StringComparer.Ordinal);

            foreach (var (flightId, answer) in fetched)
            {
                if (answer is VerbResult.Log log)
                {
                    logs[flightId] = log.Value;
                }
            }

            var folded = ConsoleProjection.Apply(state, flights);
            folded = ConsoleProjection.Apply(folded, runners);

            return Reducer.Detail(folded with
            {
                Queue = ConsoleProjection.Queue(flights.Value, logs, runners.Value, gates),
                Gates = gates,
                Logs = logs,
            });
        };
    }
}
