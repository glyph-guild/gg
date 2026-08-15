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
public sealed class ConsoleData(FlightCommands commands, CredentialCommands credentials)
{
    private readonly FlightCommands _commands = commands;
    private readonly CredentialCommands _credentials = credentials;

    /// <summary>
    /// `gg bundle`, from the state the console is holding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It takes the state and reads almost none of it.</b> That is the
    /// point. The console holds the live channel - whatever a runner printed,
    /// including whatever a tool echoed that it should not have - and this is
    /// the one place where a bundle could pick it up. Passing the state in and
    /// deliberately not touching <c>Live</c> or <c>Held</c> is what makes the
    /// redaction test meaningful: the needle is right there, in scope, and
    /// still does not come out.
    /// </para>
    /// <para>
    /// Static because it decides nothing that needs a control plane. Every
    /// input has already been through a verb.
    /// </para>
    /// </remarks>
    public static DiagnosticsBundle BundleFrom(
        AppState state,
        DateTimeOffset takenAt,
        EnvironmentIdentity environment,
        DoctorReport doctor,
        FlightLog? flightLog)
    {
        ArgumentNullException.ThrowIfNull(state);

        // The flight log comes from the control plane through a verb, never
        // from the console's own copy: state.FlightLog is a projection that
        // may be stale, and a bundle carrying a stale log is worse than one
        // carrying none.
        return Bundle.Build(takenAt, environment, doctor, flightLog);
    }

    /// <summary>`gg flights`.</summary>
    public Task<VerbResult> ListAsync(CancellationToken cancellationToken = default) =>
        _commands.ListAsync(cancellationToken);

    /// <summary>
    /// `gg why` — why each obligation applied to a flight, or did not.
    /// </summary>
    /// <remarks>
    /// The console shows the same attribution the verb does, from the same
    /// fetch. Both are renderers: an obligation that did not attach is the thing
    /// hardest to notice, and a console that could not show it would be a
    /// surface where non-attachment is invisible again.
    /// </remarks>
    /// <summary>
    /// `gg gates` - what is waiting on a person.
    /// </summary>
    /// <remarks>
    /// The console shows the same list the verb does, from the same fetch. It cannot
    /// answer one: there is no decision path in this build, and a console pane that
    /// offered one would be the exit this step asserts does not exist.
    /// </remarks>
    public Task<VerbResult> GatesAsync(CancellationToken cancellationToken = default) =>
        _commands.GatesAsync(cancellationToken);

    /// <summary>
    /// `gg decide` - records a decision about an obligation waiting on a person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Present for parity and wired to no key. The console's modal is step 6, and this is
    /// the data path it will use when it arrives - the same one the verb uses, so the
    /// console cannot end up with a second way to decide.
    /// </para>
    /// <para>
    /// <b>It records nothing locally.</b> Whatever the control plane answers is what the
    /// console will show: a pane that marked the obligation satisfied when a key was
    /// pressed would advance on a claim rather than on a decision, which is the failure
    /// the round-trip refusal test exists to catch.
    /// </para>
    /// </remarks>
    public Task<VerbResult> DecideAsync(
        string reference,
        string obligation,
        string outcome,
        DecisionObservations observations,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        _commands.DecideAsync(
            reference, obligation, outcome, observations, reason,
            cancellationToken: cancellationToken);

    public Task<VerbResult> WhyAsync(
        string reference, string? obligation = null, CancellationToken cancellationToken = default) =>
        _commands.WhyAsync(reference, obligation, cancellationToken);

    /// <summary>`gg credential list`.</summary>
    public Task<VerbResult> ListCredentialsAsync(CancellationToken cancellationToken = default) =>
        _credentials.ListCredentialsAsync(cancellationToken);

    /// <summary>
    /// `gg credential rm`.
    /// </summary>
    /// <remarks>
    /// Here because the equivalence rule reaches it, not because a pane binds
    /// a key to it yet. A store you cannot clean is a store people work
    /// around, and a console that could only add would be exactly that.
    /// </remarks>
    public Task<VerbResult> RemoveCredentialAsync(
        string credentialId, CancellationToken cancellationToken = default) =>
        _credentials.RemoveCredentialAsync(credentialId, cancellationToken);

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
            // References, never secrets. There is nothing in a CredentialList
            // to withhold, which is why the flight pane can show it.
            VerbResult.Credentials credentials => state with
            {
                Credentials = credentials.Value,
                Diagnosis = null,
            },
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
