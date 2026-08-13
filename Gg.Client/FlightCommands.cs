using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client;

/// <summary>A reference gg could not read at all.</summary>
public sealed class FlightReferenceException(string message) : Exception(message);

/// <summary>A reference that named no flight this tenant has.</summary>
public sealed class FlightNotFoundException(string message) : Exception(message);

/// <summary>An intent the contract's own rule refused.</summary>
public sealed class FlightIntentException(string message) : Exception(message);

/// <summary>No session, so there is nobody to act as.</summary>
public sealed class NotSignedInException(string message) : Exception(message);

/// <summary>
/// The flight verbs: fly, flights, show, log, runners.
/// </summary>
/// <remarks>
/// <para>
/// Every one returns a <see cref="VerbResult"/> and none of them writes
/// anything. That is what makes the console and <c>--json</c> two views of one
/// result rather than two implementations that agree today - there is no
/// second path here to write text down.
/// </para>
/// <para>
/// Two things are decided locally and only two: whether a reference is
/// readable at all, and whether an intent is well formed. Both use the
/// contract's own rule, so gg and the control plane cannot disagree about
/// them, and both save a round trip to be told something gg already knew.
/// Everything else - which flight a reference names, whether it exists, who
/// may see it - belongs to the control plane.
/// </para>
/// </remarks>
public sealed class FlightCommands(ControlPlaneClient client, ISessionStore sessions)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    /// <summary>The tenant's flights.</summary>
    public async Task<VerbResult> ListAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Flights(await _client.ListFlightsAsync(Session(), cancellationToken));

    /// <summary>One flight, by uuid or by the number a person typed.</summary>
    public async Task<VerbResult> ShowAsync(string reference, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var resolved = Readable(reference);

        return new VerbResult.Flight(
            await _client.GetFlightAsync(token, resolved, cancellationToken)
            ?? throw NoSuchFlight(reference));
    }

    /// <summary>
    /// Why each obligation applied to this flight, or did not.
    /// </summary>
    /// <remarks>
    /// <b>Fetched, never derived.</b> The attribution arrives already decided, and
    /// nothing here looks at a fact or a glob. A client that worked out why an
    /// obligation attached could explain a verdict it did not produce, and the two
    /// would drift apart quietly.
    /// </remarks>
    /// <summary>
    /// What is waiting on a person.
    /// </summary>
    /// <remarks>
    /// <b>Fetched, never derived</b>, and there is nothing here that answers a gate.
    /// The reason each gate exists is the attribution the Engine already recorded -
    /// the same value `gg why` renders - so this verb explains itself without
    /// computing anything.
    /// </remarks>
    /// <summary>
    /// Records a decision about an obligation, and renders what came back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here marks anything satisfied.</b> The obligation's state, and whether
    /// the work may now land, are both read off the response. A client that set them
    /// locally when the user pressed a key would produce a demo that works and a record
    /// that disagrees with it - Article IX in its softest clothing, which is the
    /// dangerous kind.
    /// </para>
    /// <para>
    /// <b>The manifest hash comes from the gate.</b> A decision is made against what was
    /// shown, so the hash travels with it - and a control plane whose facts have moved
    /// since refuses rather than recording an approval of something nobody saw.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> DecideAsync(
        string reference,
        string obligation,
        string outcome,
        DecisionObservations observations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);

        if (!DecisionOutcomes.All.Contains(outcome, StringComparer.Ordinal))
        {
            // REJECT LANDS HERE, deliberately. It is absent rather than unimplemented:
            // a verb that accepted it and returned success would record a decision
            // nobody acted on, and the flight would read as answered.
            throw new InvalidOperationException(
                $"'{outcome}' is not a decision this version of gg can record. It knows: "
              + string.Join(", ", DecisionOutcomes.All)
              + ". Rejecting a gate is not built yet - the flight stays waiting, which is what "
              + "it already does, and nothing here will pretend otherwise.");
        }

        var token = Session();
        var resolved = Readable(reference);

        // The gate is what says which fact set this decision is about. Fetched rather
        // than guessed, and a flight with no open gate for this obligation is a refusal
        // rather than a decision recorded against nothing.
        var gate = (await _client.GatesAsync(token, cancellationToken)).Gates
            .FirstOrDefault(g =>
                string.Equals(g.ObligationId, obligation, StringComparison.Ordinal)
                && string.Equals(g.FlightNumber, resolved, StringComparison.OrdinalIgnoreCase));

        if (gate is null)
        {
            throw new InvalidOperationException(
                $"Nothing is waiting on a decision about '{obligation}' for {reference}. "
              + "`gg gates` lists what is.");
        }

        var recorded = await _client.DecideAsync(
            token,
            resolved,
            new DecisionRequest
            {
                ObligationId = obligation,
                Outcome = outcome,
                ManifestHash = gate.ManifestHash,
                Observations = observations,
            },
            cancellationToken)
            ?? throw NoSuchFlight(reference);

        return new VerbResult.Decided(recorded);
    }

    public async Task<VerbResult> GatesAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Gates(await _client.GatesAsync(Session(), cancellationToken));

    public async Task<VerbResult> WhyAsync(
        string reference, string? obligation, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var resolved = Readable(reference);

        var attribution = await _client.WhyAsync(token, resolved, cancellationToken)
            ?? throw NoSuchFlight(reference);

        if (obligation is not { Length: > 0 })
        {
            return new VerbResult.Why(attribution);
        }

        // Narrowed, and a name nothing matches is a refusal rather than an empty
        // answer - "no obligation called that" and "it did not attach" are
        // different things and the second is the one this verb exists to show.
        var one = attribution.Obligations
            .Where(o => string.Equals(o.ObligationId, obligation, StringComparison.Ordinal))
            .ToList();

        if (one.Count == 0)
        {
            throw new InvalidOperationException(
                $"This flight's envelope declares no obligation called '{obligation}'. It declares: "
              + string.Join(", ", attribution.Obligations.Select(o => o.ObligationId))
              + ". An obligation that is absent from the envelope is a different thing from one "
              + "whose condition did not hold.");
        }

        return new VerbResult.Why(attribution with { Obligations = one });
    }

    /// <summary>A flight's log.</summary>    /// <summary>A flight's log.</summary>
    public async Task<VerbResult> LogAsync(string reference, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var resolved = Readable(reference);

        return new VerbResult.Log(
            await _client.GetFlightLogAsync(token, resolved, cancellationToken)
            ?? throw NoSuchFlight(reference));
    }

    /// <summary>The tenant's runners, as the control plane derives them.</summary>
    public async Task<VerbResult> RunnersAsync(CancellationToken cancellationToken = default) =>
        new VerbResult.Runners(await _client.ListRunnersAsync(Session(), cancellationToken));

    /// <summary>
    /// Opens a flight.
    /// </summary>
    /// <remarks>
    /// The intent is validated by <see cref="FlightIntent.Validate"/> - the
    /// contract's rule, not a second one written here - so a request the
    /// control plane would refuse is not sent at all.
    /// </remarks>
    public async Task<VerbResult> FlyAsync(
        string? text, string? uri, string? name = null, CancellationToken cancellationToken = default)
    {
        var token = Session();

        var intent = new FlightIntent
        {
            Kind = uri is { Length: > 0 } ? FlightIntentKinds.Uri : FlightIntentKinds.Text,
            Uri = uri,
            Text = text,
        };

        if (FlightIntent.Validate(intent) is { } diagnosis)
        {
            throw new FlightIntentException(diagnosis);
        }

        var request = new FlightLaunchRequest
        {
            // The name defaults to the intent, because a person who typed one
            // sentence should not have to type it twice. It is stripped and
            // shortened control-plane-side either way.
            Name = name is { Length: > 0 } ? name : (text ?? uri)!,
            Intent = intent,
        };

        return new VerbResult.Launched(await _client.LaunchFlightAsync(token, request, cancellationToken));
    }

    /// <summary>
    /// The reference, as text the control plane will resolve.
    /// </summary>
    /// <remarks>
    /// Passed through in the form it was READ, not translated into a uuid. The
    /// control plane owns resolution; a client that turned GG-42 into an id
    /// first would need its own copy of the tenant's numbering, which is a
    /// second source of truth for the one thing a flight number exists to
    /// avoid.
    /// </remarks>
    private static string Readable(string reference) =>
        FlightRef.TryParse(reference, out var parsed)
            ? parsed.ToString()
            : throw new FlightReferenceException(
                $"'{reference}' is not a flight. Use the number, like {FlightRef.Format(42)}, or the id.");

    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("Not signed in. Run gg login.");

    private static FlightNotFoundException NoSuchFlight(string reference) =>
        new($"No flight {reference}. Run gg flights to see what is there.");
}
