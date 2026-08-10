using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Everything the console can load. Every method is a verb.
/// </summary>
/// <remarks>
/// <para>
/// The console's data layer IS the verb layer. Each method here calls the same
/// <see cref="FlightCommands"/> method the corresponding <c>gg</c> verb calls
/// and returns the same <see cref="VerbResult"/>, so "every verb has a console
/// equivalent and every console action a verb, and both render the same
/// structured result" is true by construction rather than by discipline.
/// </para>
/// <para>
/// <b>There is no second way to get the data.</b> This type holds a
/// <see cref="FlightCommands"/> and nothing else - no HTTP client, no client,
/// no path of its own - which is asserted structurally, because a pane that
/// could fetch by a route no verb uses is a pane whose output the JSON cannot
/// reproduce.
/// </para>
/// <para>
/// Different renderers over one result type. Never a second way to get the
/// data.
/// </para>
/// </remarks>
public sealed class ConsoleData(FlightCommands commands)
{
    private readonly FlightCommands _commands = commands;

    /// <summary>`gg flights`.</summary>
    public Task<VerbResult> ListAsync(CancellationToken cancellationToken = default) =>
        _commands.ListAsync(cancellationToken);

    /// <summary>`gg show`.</summary>
    public Task<VerbResult> ShowAsync(string reference, CancellationToken cancellationToken = default) =>
        _commands.ShowAsync(reference, cancellationToken);

    /// <summary>`gg log`.</summary>
    public Task<VerbResult> LogAsync(string reference, CancellationToken cancellationToken = default) =>
        _commands.LogAsync(reference, cancellationToken);

    /// <summary>`gg runners`.</summary>
    public Task<VerbResult> RunnersAsync(CancellationToken cancellationToken = default) =>
        _commands.RunnersAsync(cancellationToken);
}

/// <summary>
/// Turns verb results into console state.
/// </summary>
/// <remarks>
/// <para>
/// The one place a <see cref="VerbResult"/> becomes an <see cref="AppState"/>.
/// It reads only what the verbs returned, so a pane cannot show something no
/// verb can produce - which is the same guarantee from the other end.
/// </para>
/// <para>
/// The queue is derived here rather than fetched, because <b>the queue is not
/// a flight list</b>. Its rows are flights NEEDING ME, and the conditions that
/// put one there are facts about leases and runners that <c>gg log</c> and
/// <c>gg runners</c> already return. Fetching "the queue" from an endpoint
/// would make it a server-side list that happens to be short, which is the
/// dashboard this console exists not to be.
/// </para>
/// </remarks>
public static class ConsoleProjection
{
    /// <summary>Two expiries is a pattern rather than an incident.</summary>
    public const int ExpiriesThatMeanTrouble = 2;

    /// <summary>Applies whatever a verb returned to the model.</summary>
    public static AppState Apply(AppState state, VerbResult result)
    {
        ArgumentNullException.ThrowIfNull(state);

        return result switch
        {
            VerbResult.Flight flight => state with { Flight = flight.Value, Diagnosis = null },
            VerbResult.Log log => state with { FlightLog = log.Value, Diagnosis = null },
            VerbResult.Runners runners => state with { Runners = runners.Value, Diagnosis = null },
            // A flight LIST is not the queue. It is the raw material the queue
            // is derived from, and nothing renders it directly.
            VerbResult.Flights => state with { Diagnosis = null },
            _ => state,
        };
    }

    /// <summary>
    /// Which flights need me, from what the verbs returned.
    /// </summary>
    /// <remarks>
    /// Thin, and honestly thin: two conditions, both of which step 3 actually
    /// produces. Credential-unresolvable joins at step 5 and is absent rather
    /// than stubbed - a row that could never appear is worse than a short
    /// queue, because it makes the queue look like it covers more than it does.
    /// </remarks>
    public static IReadOnlyList<QueueRow> Queue(
        FlightList flights, IReadOnlyDictionary<string, FlightLog> logs, RunnerList runners)
    {
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(runners);

        var rows = new List<QueueRow>();

        foreach (var flight in flights.Flights)
        {
            if (!logs.TryGetValue(flight.FlightId, out var log))
            {
                continue;
            }

            var expiries = log.Entries.Where(e => e.Kind == "lease-expired").ToList();
            if (expiries.Count >= ExpiriesThatMeanTrouble)
            {
                rows.Add(Row(flight, QueueReason.LeaseExpiredTwice, expiries[^1].At));
                continue;
            }

            // A runner that stopped heartbeating while holding this flight. The
            // control plane derives "offline"; nothing here second-guesses it.
            var stranded = runners.Runners.FirstOrDefault(
                r => r.State == RunnerStates.Offline && r.CurrentFlightId == flight.FlightId);

            if (stranded is not null)
            {
                rows.Add(Row(flight, QueueReason.RunnerOffline, stranded.LastHeartbeatAt ?? flight.CreatedAt));
            }
        }

        return QueueSort.Default.Order(rows);
    }

    private static QueueRow Row(FlightSummary flight, QueueReason reason, DateTimeOffset since) => new()
    {
        FlightId = flight.FlightId,
        FlightNumber = flight.FlightNumber,
        Name = flight.Name,
        Reason = reason,
        Since = since,
    };
}
