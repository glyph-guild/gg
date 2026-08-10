using Gg.Client;

namespace Gg.Console;

/// <summary>
/// The console's first load, through the verbs and nothing else.
/// </summary>
/// <remarks>
/// Every failure lands in the model as a diagnosis rather than on a screen or
/// in a log, so it survives the UI being destroyed. A console that forgets why
/// it is empty is a console that looks like it is working.
/// </remarks>
public static class ConsoleStart
{
    public static async Task<AppState> LoadAsync(ConsoleData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            var flights = (VerbResult.Flights)await data.ListAsync(cancellationToken);
            var runners = (VerbResult.Runners)await data.RunnersAsync(cancellationToken);

            var logs = new Dictionary<string, Gg.Contracts.FlightLog>(StringComparer.Ordinal);
            foreach (var flight in flights.Value.Flights)
            {
                if (await data.LogAsync(flight.FlightId, cancellationToken) is VerbResult.Log log)
                {
                    logs[flight.FlightId] = log.Value;
                }
            }

            return new AppState
            {
                Queue = ConsoleProjection.Queue(flights.Value, logs, runners.Value),
            };
        }
        catch (Exception failure) when (failure is Gg.Client.NotSignedInException
                                            or Gg.Client.ProtocolTooOldException
                                            or HttpRequestException)
        {
            // Named exceptions only. Swallowing everything here would turn a
            // bug in the projection into an empty console with a friendly
            // message, which is the shape that hides the next defect.
            return new AppState { Diagnosis = failure.Message };
        }
    }
}
