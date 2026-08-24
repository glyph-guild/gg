namespace Gg.Contracts;

/// <summary>The kinds of intent a flight may be opened with.</summary>
/// <remarks>
/// Two today. Adding a third - an issue, a pull request, a ticket - is a
/// contract change, which is what makes it visible; the FIELD to carry it
/// already exists, which is what makes it cheap.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class FlightIntentKinds
{
    /// <summary>What a person typed. Carried in <see cref="FlightIntent.Text"/>.</summary>
    public const string Text = "text";

    /// <summary>Something addressable. Carried in <see cref="FlightIntent.Uri"/>.</summary>
    public const string Uri = "uri";

    /// <summary>Every kind validation accepts.</summary>
    public static IReadOnlyList<string> All { get; } = [Text, Uri];
}

/// <summary>
/// Why a flight was opened.
/// </summary>
/// <remarks>
/// <para>
/// Three fields from the start, with one populated. <c>gg fly "fix the login
/// bug"</c> wants a string and a flight opened from an issue wants a typed
/// reference; a contract that shipped the string first and grew the reference
/// later would be a migration of stored data and of every consumer. Two empty
/// fields cost nothing today.
/// </para>
/// <para>
/// Validation is Article XI throughout: an intent naming no kind, or naming
/// one nothing understands, or carrying two payloads, HALTS. None of them
/// becomes an empty flight, because a flight created from an intent nobody
/// could read is worse than one that was refused.
/// </para>
/// </remarks>
[PinnedId("68768ca3-de80-45c8-ae8b-46e3ee269897")]
public sealed record FlightIntent
{
    /// <summary>Which of the fields below carries the intent.</summary>
    public required string Kind { get; init; }

    /// <summary>The addressable thing, when there is one.</summary>
    public string? Uri { get; init; }

    /// <summary>The words somebody wrote, when there are any.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// The diagnosis, or null when there is nothing wrong.
    /// </summary>
    /// <remarks>
    /// Returns a sentence rather than a bool because Article XI asks for a
    /// diagnosis, and "invalid intent" tells whoever hit it nothing about
    /// which of four things went wrong.
    /// </remarks>
    public static string? Validate(FlightIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (string.IsNullOrWhiteSpace(intent.Kind))
        {
            return "A flight intent must name its kind. Expected one of: "
                 + string.Join(", ", FlightIntentKinds.All) + ".";
        }

        if (!FlightIntentKinds.All.Contains(intent.Kind))
        {
            // Naming the offending value matters: the alternative is a
            // diagnosis that sends somebody reading their own code to find out
            // what they sent.
            return $"Unknown flight intent kind '{intent.Kind}'. Expected one of: "
                 + string.Join(", ", FlightIntentKinds.All) + ".";
        }

        var hasText = !string.IsNullOrWhiteSpace(intent.Text);
        var hasUri = !string.IsNullOrWhiteSpace(intent.Uri);

        if (hasText && hasUri)
        {
            return "A flight intent carries one payload. This one has both text and a uri, "
                 + "and which of them wins would be decided by whichever reader saw it first.";
        }

        if (!hasText && !hasUri)
        {
            return "A flight intent carries one payload. This one has neither.";
        }

        // The kind and the populated field must agree, or a consumer renders a
        // uri as prose, or tries to fetch free text.
        return intent.Kind switch
        {
            FlightIntentKinds.Text when !hasText =>
                $"Intent kind '{FlightIntentKinds.Text}' carries its payload in text, and text is empty.",
            FlightIntentKinds.Uri when !hasUri =>
                $"Intent kind '{FlightIntentKinds.Uri}' carries its payload in uri, and uri is empty.",
            _ => null,
        };
    }
}

/// <summary>Opens a flight.</summary>
/// <remarks>
/// No tenant id. The caller already is a tenant, and an endpoint that accepted
/// one would be an endpoint somebody could name a different one to.
/// </remarks>
[PinnedId("1b3fabe4-1723-41f7-8539-d772e3d61b11")]
public sealed record FlightLaunchRequest
{
    /// <summary>
    /// What this flight is called. Externally-sourced, so it is stripped of
    /// control sequences before it is stored.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Why the flight exists.</summary>
    public required FlightIntent Intent { get; init; }

    /// <summary>
    /// What the flight is FOR - a work-kind name in the tenant's topology.
    /// Null means implement, the kind every flight before kinds existed was.
    /// </summary>
    /// <remarks>
    /// Knowable at the start, which is what reconciled declaring it with the
    /// classification rejection: "am I researching or implementing" cannot
    /// change mid-flight, and what the work turns out to TOUCH stays a
    /// narrowing attached from facts (ADR-0014). A wrong kind is a mistake
    /// and not a hole - it can only narrow root, so choosing wrong grants
    /// nothing root withheld.
    /// </remarks>
    public string? WorkKind { get; init; }

    /// <summary>
    /// Where the flight runs, validated against the composed envelope's bound.
    /// Null inherits the bound.
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// Which repository the flight is about, validated the same way. Null
    /// inherits.
    /// </summary>
    public string? Repository { get; init; }
}

/// <summary>The flight that was opened.</summary>
/// <remarks>
/// The id is what is known at the moment a flight is accepted, so it is what
/// comes back required. The number is not: it is minted by whatever handles
/// the launch, and a control plane answering 202 has not seen it yet.
/// </remarks>
[PinnedId("e48f8814-947e-4ff6-a6b6-efc3f038fea4")]
public sealed record FlightLaunched
{
    public required string FlightId { get; init; }

    /// <summary>
    /// Rendered, e.g. GG-1042 - or null when the number has not been minted.
    /// </summary>
    /// <remarks>
    /// Nullable so it can be ABSENT, not so it can be forgotten. The
    /// alternative was a required field and an empty string, which makes "not
    /// minted yet" and "minted as nothing" the same value; a caller cannot
    /// tell those apart, and the one it would guess wrong about is the one
    /// that matters. A control plane that later mints synchronously fills this
    /// in without a contract change, which is why the field is here at all.
    /// </remarks>
    public string? FlightNumber { get; init; }
}

/// <summary>
/// One flight, as a person reads it.
/// </summary>
/// <remarks>
/// <para>
/// No tenant is echoed back. The caller already is the tenant, and returning it
/// invites a client to start passing it.
/// </para>
/// <para>
/// The four version fields are here because they are what governed the flight,
/// and a flight log that cannot say which constitution was in force is a log
/// nobody can act on later.
/// </para>
/// </remarks>
[PinnedId("132c39a9-2d0d-426e-b34a-55c4887e43ff")]
public sealed record FlightSummary
{
    public required string FlightId { get; init; }

    /// <summary>Rendered, e.g. GG-1042. The int is what is stored.</summary>
    public required string FlightNumber { get; init; }

    public required string Name { get; init; }

    /// <summary>Why this flight exists.</summary>
    public required FlightIntent Intent { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Wire protocol revision the runner must speak.</summary>
    public required int RunnerProtocolVersion { get; init; }

    public required string FactVocabularyVersion { get; init; }

    public required string ConstitutionVersion { get; init; }

    /// <summary>Version of the governing envelope, or "none".</summary>
    public required string EnvelopeVersion { get; init; }

    /// <summary>
    /// How many times a loop has actually run on this flight.
    /// </summary>
    /// <remarks>
    /// <b>Loops that ran, not leases that were granted.</b> A lease is permission to
    /// attempt; an attempt is a loop that produced an outcome. Counting permissions makes
    /// a runner that died before invoking anything look like an attempt, and "three
    /// attempts" meaning "two attempts and a crashed runner" is a well-formed wrong number
    /// in a field somebody reads while deciding whether to keep going.
    /// </remarks>
    public required int Attempts { get; init; }

    /// <summary>
    /// What the runner observed, as the control plane recorded it.
    /// </summary>
    /// <remarks>
    /// On the SUMMARY rather than behind a route of its own, so the console
    /// renders facts through the verb it already has. A pane that could fetch
    /// by a route no verb uses is a pane whose output <c>--json</c> cannot
    /// reproduce - and this is the first thing the console shows that no part
    /// of the control plane could have known.
    /// </remarks>
    public required IReadOnlyList<FactEnvelope> Facts { get; init; }

    /// <summary>
    /// The labels this flight's lease requires, exactly as the matcher reads
    /// them. Empty for a flight created under an envelope that selects nothing
    /// - which is every flight created before selections existed.
    /// </summary>
    public IReadOnlyList<string> RequiredLabels { get; init; } = [];

    /// <summary>
    /// Why this flight cannot start, by name - or null when it is not waiting.
    /// </summary>
    /// <remarks>
    /// <b>Refusal at apply, waiting at flight.</b> Apply has an actor at the
    /// keyboard to inform; a queued flight has nobody, so it waits loudly
    /// instead of dying quietly. The sentence names the labels no live runner
    /// advertises, because a name is what somebody can act on. Null means not
    /// waiting - the <see cref="LeaseClaimStatus.Lease"/> absence rule -
    /// and "waiting: nothing" is not a state this member can express.
    /// </remarks>
    public string? Waiting { get; init; }
}

/// <summary>
/// The tenant's flights.
/// </summary>
/// <remarks>
/// An envelope rather than a bare array. A bare array has nowhere to put the
/// paging this will grow, and adding an envelope later would be a breaking
/// change for every consumer.
/// </remarks>
[PinnedId("6aea55e0-aedb-440b-a8cd-f97973073c55")]
public sealed record FlightList
{
    public required IReadOnlyList<FlightSummary> Flights { get; init; }
}

/// <summary>One thing that happened to a flight.</summary>
/// <remarks>
/// <c>Detail</c> is a rendered string, not a nested object. The log is read by
/// people and by a support bundle; a shape that varied per kind would make
/// both of those parse a union.
/// </remarks>
[PinnedId("a30ab28e-b377-4734-b672-00e90eaeccd8")]
public sealed record FlightLogEntry
{
    public required DateTimeOffset At { get; init; }

    /// <summary>What happened, e.g. lease-granted.</summary>
    public required string Kind { get; init; }

    /// <summary>Human-readable specifics, already stripped of control sequences.</summary>
    public required string Detail { get; init; }
}

/// <summary>A flight's log, oldest first.</summary>
[PinnedId("bf4fdf2b-d551-4f8f-b18b-e81a01e51b6e")]
public sealed record FlightLog
{
    public required string FlightId { get; init; }

    public required string FlightNumber { get; init; }

    public required IReadOnlyList<FlightLogEntry> Entries { get; init; }
}

/// <summary>
/// A runner, as the control plane has worked out it is.
/// </summary>
/// <remarks>
/// <para>
/// <c>State</c> travels in this direction only. It is derived from heartbeat
/// age, whether a live lease exists, and which flight that lease is on - and
/// there is deliberately no request type anywhere carrying a field like it. A
/// runner that could report "busy" could report it while wedged, and a wedged
/// runner that looks busy blocks the takeover that should reclaim its flight.
/// </para>
/// <para>
/// Offline is decided first, so a stale heartbeat outranks a lease that has
/// not expired yet. That combination is precisely a crashed runner.
/// </para>
/// </remarks>
[PinnedId("3ad7e746-f30e-40a1-a7e0-cb90c46f39c3")]
public sealed record RunnerSummary
{
    public required string RunnerId { get; init; }

    public required string Label { get; init; }

    /// <summary>offline, busy or idle. Derived, never reported.</summary>
    public required string State { get; init; }

    /// <summary>The flight this runner holds, when it holds one.</summary>
    public string? CurrentFlightId { get; init; }

    /// <summary>Rendered, e.g. GG-1042, when there is a flight.</summary>
    public string? CurrentFlightNumber { get; init; }

    /// <summary>
    /// When this runner was last heard from, or null if never.
    /// </summary>
    /// <remarks>
    /// Returned alongside the state rather than instead of it, so a person can
    /// see WHY a runner reads offline without having to know the staleness
    /// threshold.
    /// </remarks>
    public DateTimeOffset? LastHeartbeatAt { get; init; }

    /// <summary>
    /// What this runner advertises, each label with its disposition beside it.
    /// </summary>
    /// <remarks>
    /// From the labels the runner heartbeats, so a runner that stops
    /// advertising something stops listing it within a beat. Empty for a
    /// runner that advertises nothing - which is every runner registered
    /// before labels were persisted.
    /// </remarks>
    public IReadOnlyList<AdvertisedLabel> Labels { get; init; } = [];
}

/// <summary>The states a runner may be derived to be in.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class RunnerStates
{
    /// <summary>Heartbeat is stale. Decided before anything else.</summary>
    public const string Offline = "offline";

    /// <summary>Alive and holding a lease.</summary>
    public const string Busy = "busy";

    /// <summary>Alive and holding nothing.</summary>
    public const string Idle = "idle";

    public static IReadOnlyList<string> All { get; } = [Offline, Busy, Idle];
}

/// <summary>The tenant's runners.</summary>
[PinnedId("88e7bf83-738b-4258-8873-882fcf5a381e")]
public sealed record RunnerList
{
    public required IReadOnlyList<RunnerSummary> Runners { get; init; }
}

/// <summary>
/// Whether this control plane sends telemetry anywhere, and where.
/// </summary>
/// <remarks>
/// <para>
/// A customer runs the control plane in their own cloud account, and "is this
/// thing transmitting to anybody" is a question they must be able to ask it
/// directly. The startup line answers it for whoever is watching the console;
/// this answers it for everybody else, which is most people, most of the time.
/// </para>
/// <para>
/// It exists because ambient environment once chose a destination that nothing
/// in either repository had configured. A control plane that can be asked is
/// one where that cannot happen quietly again.
/// </para>
/// </remarks>
[PinnedId("2b5e0f9d-7a41-4c8e-93d6-1f0a5c74b8e2")]
public sealed record TelemetryDisclosure
{
    /// <summary>Whether anything leaves the control plane.</summary>
    public required bool Exporting { get; init; }

    /// <summary>Where it goes, or null when nothing does.</summary>
    public string? Destination { get; init; }
}
