namespace Gg.Contracts;

/// <summary>The kinds of intent a flight may be opened with.</summary>
/// <remarks>
/// <para>
/// Three, and the third arrived the way this note said it would: <i>adding a
/// third - an issue, a pull request, a ticket - is a contract change, which is
/// what makes it visible; the FIELD to carry it already exists, which is what
/// makes it cheap.</i> The prediction was half right. The change is visible,
/// and the fields did NOT already exist - a work item is a provider and an id,
/// and neither is a URI or a sentence.
/// </para>
/// <para>
/// A fourth is a contract version, not a string.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class FlightIntentKinds
{
    /// <summary>What a person typed. Carried in <see cref="FlightIntent.Text"/>.</summary>
    public const string Text = "text";

    /// <summary>Something addressable. Carried in <see cref="FlightIntent.Uri"/>.</summary>
    public const string Uri = "uri";

    /// <summary>
    /// A work item in a tracker, carried in <see cref="FlightIntent.Provider"/>
    /// and <see cref="FlightIntent.Id"/>.
    /// </summary>
    /// <remarks>
    /// <b>Declared, never addressed.</b> The URI of a work item can be rendered
    /// from these two for a person to click; the reverse - an id lifted out of a
    /// URI somebody typed - is the move slice nine retired for repositories, and
    /// it breaks the same way on a vanity host and on an item moved between
    /// projects.
    /// </remarks>
    public const string Ticket = "ticket";

    /// <summary>Every kind validation accepts.</summary>
    public static IReadOnlyList<string> All { get; } = [Text, Uri, Ticket];
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
    /// Which tracker the work item lives in, when the intent is one.
    /// </summary>
    /// <remarks>
    /// <b>A free string, and it stays one.</b> gg is public and distributed and
    /// names no forge; a closed provider vocabulary here would be that mistake
    /// one noun over. Which providers actually RESOLVE is the control plane's
    /// knowledge and the Intent port's problem, and a provider nobody can
    /// resolve is still a correct thing to have declared.
    /// </remarks>
    public string? Provider { get; init; }

    /// <summary>
    /// The work item's identifier at that provider, as the tracker issues it.
    /// </summary>
    /// <remarks>
    /// <b>A token, never a path and never a URL.</b> Every tracker issues
    /// something without separators - <c>4471</c>, <c>PROJ-123</c> - and an id
    /// carrying a path is somebody handing over a URL and hoping the last
    /// segment gets used. Refused, because that hope is exactly what breaks on
    /// a vanity host.
    /// </remarks>
    public string? Id { get; init; }

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

        // A TICKET IS ONE PAYLOAD CARRIED IN TWO FIELDS, and that is the only
        // structurally new thing here. "How many payloads is this" used to be a
        // count of non-empty strings; counting a half-filled ticket as one
        // payload is what lets the arm below diagnose the MISSING FIELD rather
        // than reporting "this one has neither", which is the wrong sentence
        // and sends somebody looking in the wrong half of their document.
        var hasProvider = !string.IsNullOrWhiteSpace(intent.Provider);
        var hasId = !string.IsNullOrWhiteSpace(intent.Id);
        var hasTicket = hasProvider || hasId;

        var payloads = (hasText ? 1 : 0) + (hasUri ? 1 : 0) + (hasTicket ? 1 : 0);

        if (payloads > 1)
        {
            // The sentence that was already here, extended rather than joined by
            // a second one - two sentences saying the same thing is how the
            // readers in two repositories come to disagree.
            return "A flight intent carries one payload. This one has "
                 + string.Join(" and ", Carried(hasText, hasUri, hasTicket))
                 + ", and which of them wins would be decided by whichever reader saw it first.";
        }

        if (payloads == 0)
        {
            // "Neither" was accurate while there were two. Saying it with three
            // would be the wording quietly going stale behind a passing test,
            // which is a small instance of the thing this slice is about.
            return "A flight intent carries one payload - text, a uri, or a ticket. This one "
                 + "has none of them.";
        }

        if (hasTicket)
        {
            // Named separately from the kind check below, because a ticket
            // missing half of itself is a different mistake from a kind that
            // disagrees with its fields, and only one of them is a typo.
            if (!hasProvider)
            {
                return "A ticket intent names the tracker it lives in. This one has an id and "
                     + "no provider, and an id on its own does not say which 4471 it is.";
            }

            if (!hasId)
            {
                return "A ticket intent names the work item. This one has a provider and no id.";
            }

            if (UnparseableId(intent.Id!) is { } wrong)
            {
                return wrong;
            }
        }

        // The kind and the populated fields must agree, or a consumer renders a
        // uri as prose, tries to fetch free text, or resolves a work item that
        // was never declared as one.
        return intent.Kind switch
        {
            FlightIntentKinds.Text when !hasText =>
                $"Intent kind '{FlightIntentKinds.Text}' carries its payload in text, and text is empty.",
            FlightIntentKinds.Uri when !hasUri =>
                $"Intent kind '{FlightIntentKinds.Uri}' carries its payload in uri, and uri is empty.",
            FlightIntentKinds.Ticket when !hasTicket =>
                $"Intent kind '{FlightIntentKinds.Ticket}' carries its payload in provider and id, "
              + "and both are empty.",
            _ => null,
        };
    }

    /// <summary>What this intent is actually carrying, for the refusal above.</summary>
    private static IEnumerable<string> Carried(bool hasText, bool hasUri, bool hasTicket)
    {
        if (hasText)
        {
            yield return "text";
        }

        if (hasUri)
        {
            yield return "a uri";
        }

        if (hasTicket)
        {
            yield return "a ticket";
        }
    }

    /// <summary>
    /// Why an id is not one, or null when it is.
    /// </summary>
    /// <remarks>
    /// <b>Slice nine's refusal, one noun over.</b> Deriving a repository's
    /// provider from an intent URI's host put a host in the request path, and
    /// slice nine took it back out. Pulling an id out of a URI is the smaller
    /// version of the same move: it breaks on a vanity host, and on a work item
    /// moved between projects, and neither failure is visible at the moment
    /// somebody types it.
    /// <para>
    /// Two shapes are refused, and the second is the one an absolute-URI check
    /// alone would miss: no scheme, so it is not a URI, and it is still a path
    /// somebody pasted hoping the last segment gets used.
    /// </para>
    /// </remarks>
    private static string? UnparseableId(string id)
    {
        if (System.Uri.TryCreate(id, UriKind.Absolute, out _))
        {
            return $"A ticket's id is the identifier the tracker issues - '4471', 'PROJ-123' - "
                 + $"and this one is a URI: '{id}'. The id is a DECLARED field: nothing here "
                 + "derives one from a link, because a link's shape is whatever host somebody "
                 + "happened to type.";
        }

        return id.Contains('/', StringComparison.Ordinal) || id.Contains('\\', StringComparison.Ordinal)
            ? $"A ticket's id is a token and not a path, and this one is a path: '{id}'. Name "
            + "the work item's own identifier, not where it can be read."
            : null;
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

    /// <summary>
    /// Which runner this flight is for, by id. Null means any runner that may
    /// take it, which is nearly every flight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The other half of reserving.</b> A reservation says whose work a
    /// runner takes; this says which machine a piece of work is for. Without it,
    /// reserving a laptop means its holder's work lands there and nothing else
    /// can be sent there deliberately.
    /// </para>
    /// <para>
    /// <b>The runner still PULLS.</b> "Push" is a person choosing a machine, not
    /// the control plane opening a connection to one: the flight is narrowed,
    /// and the runner it names claims it the way it claims anything else.
    /// </para>
    /// <para>
    /// <b>An id rather than a label.</b> A label says what a machine can do and
    /// several may answer to it; this names one machine. Spelling it as a label
    /// would make "this one" unsayable, which is the thing being added.
    /// </para>
    /// </remarks>
    public string? Runner { get; init; }
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
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> Every
    /// serialized contract type has a required member, so System.Text.Json
    /// builds it through the parameterized creator, which assigns every member
    /// from its argument array - this one as null when the key is absent,
    /// overwriting the <c>= []</c>. Non-nullable is a promise to every caller
    /// that it can be dereferenced; <c>AbsentCollectionsSurviveTheWireTests</c>
    /// holds it for the whole contract.
    /// </remarks>
    public IReadOnlyList<string> RequiredLabels
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>
    /// Why this flight cannot start, by name - or null when it is not waiting.
    /// </summary>
    /// <remarks>
    /// <b>Refusal at apply, waiting at flight.</b> Apply has an actor at the
    /// keyboard to inform; a queued flight has nobody, so it waits loudly
    /// instead of dying quietly. A <see cref="Reason"/> since 0.54.0 - the
    /// wire NAME survives and the type changes, a loud break chosen over the
    /// silent-health flip a rename would cause in old readers. The sentence
    /// a person reads derives from the kind via <see cref="Reason.Sentence"/>.
    /// Null means not waiting - the <see cref="LeaseClaimStatus.Lease"/>
    /// absence rule - and "waiting: nothing" is not a state this can express.
    /// </remarks>
    public Reason? Waiting { get; init; }

    /// <summary>How this flight stands: one of <see cref="FlightStates.All"/>.</summary>
    /// <remarks>
    /// <para>
    /// <b>Derived control-plane-side, like <see cref="RunnerSummary.State"/>,
    /// and reported by nobody.</b> It is a function of what was recorded when
    /// the flight ended, and of what can be derived at read for flights that
    /// ended before there was anywhere to record one. Nothing on the wire may
    /// SET it.
    /// </para>
    /// <para>
    /// <b>Defaulted rather than required, deliberately.</b> A control plane
    /// older than 0.70.0 answers without this member and its flights would
    /// otherwise fail to deserialize, taking away the queue entirely to add a
    /// column to it. <c>unknown</c> is the honest reading of a flight from a
    /// build that could not say — which is the same sentence this member exists
    /// to make sayable.
    /// </para>
    /// </remarks>
    public string State { get; init; } = FlightStates.Unknown;
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

/// <summary>Why a flight's question stopped applying.</summary>
/// <remarks>
/// <para>
/// <b>The reason is required, and that is the whole design.</b>
/// <see cref="FlightStates.Withdrawn"/> is the most reachable sentence in the
/// terminal vocabulary — <i>the question ceased to apply</i> fits almost
/// anything somebody finds inconvenient — so the one thing standing between it
/// and a garbage collector is having to say which question, and what stopped
/// applying.
/// </para>
/// <para>
/// <b>Who is not on this.</b> Article XII attributes the act to the
/// authenticated principal, derived control-plane-side; a caller naming
/// somebody else would be a caller choosing its own attribution, which is the
/// one thing attribution may never be.
/// </para>
/// <para>
/// <b>Withdrawing is not grounding.</b> Grounding is a person stopping work
/// that could still have been done; withdrawing says the work no longer has a
/// question to answer. They are told apart by what became untrue, which is why
/// this record carries a reason and nothing else.
/// </para>
/// </remarks>
[PinnedId("d5c1f0a7-3e62-4b19-8f0d-6a71c4e2b93f")]
public sealed record FlightWithdrawalRequest
{
    /// <summary>What stopped applying. Not optional, deliberately.</summary>
    public required string Because { get; init; }
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
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> Every
    /// serialized contract type has a required member, so System.Text.Json
    /// builds it through the parameterized creator, which assigns every member
    /// from its argument array - this one as null when the key is absent,
    /// overwriting the <c>= []</c>. Non-nullable is a promise to every caller
    /// that it can be dereferenced; <c>AbsentCollectionsSurviveTheWireTests</c>
    /// holds it for the whole contract.
    /// </remarks>
    public IReadOnlyList<AdvertisedLabel> Labels
    {
        get => field ?? [];
        init;
    } = [];
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

/// <summary>The states a flight may be derived to be in.</summary>
/// <remarks>
/// <para>
/// <b>ADR-0017, and the thing this platform did not have.</b> A flight ends when
/// its destination admits, a person stops it, or its question ceases to apply —
/// and none of those requires a runner to have touched it. Before this,
/// termination was a property of the LEASE: <i>in the air</i> meant the newest
/// lease's disposition was not terminal, so the three work kinds that are never
/// leased were in the air permanently.
/// </para>
/// <para>
/// <b>Derived, like <see cref="RunnerStates"/>, and reported by nobody.</b> It
/// is a function of what the control plane recorded when the flight ended, and
/// of what can be derived at read for flights that ended before there was
/// anywhere to record it. Nothing on the wire may SET a flight's state.
/// </para>
/// <para>
/// <b>Two of the six are readings rather than exits.</b> <c>open</c> is the
/// absence of an ending and <c>unknown</c> is the absence of a record; neither
/// happened to a flight, so neither is in <see cref="Exits"/> and neither may be
/// written down as something that did.
/// </para>
/// <para>
/// <b>Ending is not the same axis as stopping.</b> A flight that halted,
/// exhausted its budget or was abandoned is <i>open</i> — it is stopped, and
/// resumable, which is the product's central claim. There is deliberately no
/// state here for any of them, and a terminal state must never be inferred from
/// a flight having gone quiet.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class FlightStates
{
    /// <summary>No ending has been reached, and one still can be.</summary>
    public const string Open = "open";

    /// <summary>The destination admitted. Every work kind, runner or not.</summary>
    public const string Landed = "landed";

    /// <summary>A person stopped it.</summary>
    public const string Grounded = "grounded";

    /// <summary>The question ceased to apply.</summary>
    /// <remarks>
    /// The most reachable sentence in this vocabulary, and the one that will be
    /// reached for whenever something is inconvenient — so it is attributed, it
    /// states why, and the things that may write it are counted.
    /// </remarks>
    public const string Withdrawn = "withdrawn";

    /// <summary>The work concluded without the destination admitting.</summary>
    /// <remarks>
    /// A conclusion a runner reached, not an interruption: handing the flight
    /// out again would repeat the work. <c>abandoned</c> and <c>expired</c> are
    /// the interruptions, they leave the work outstanding, and they are not
    /// endings.
    /// </remarks>
    public const string Failed = "failed";

    /// <summary>No ending was recorded and none can be derived.</summary>
    /// <remarks>
    /// Article XI, and the reason the fix for <c>disposition IS NULL</c> is not
    /// to flip it. A flight nobody can account for is neither finished nor
    /// working — it says so by name, because a terminality query that quietly
    /// matched nothing would return the empty queue this whole design is trying
    /// to produce.
    /// </remarks>
    public const string Unknown = "unknown";

    public static IReadOnlyList<string> All { get; } =
        [Open, Landed, Grounded, Withdrawn, Failed, Unknown];

    /// <summary>The states that are endings, and the only ones a flight records.</summary>
    /// <remarks>
    /// What a work kind declares it can reach, and what the exit store accepts.
    /// A kind that can reach none of these fails the build: an envelope that
    /// cannot run is worse than one that does not exist, and a work kind whose
    /// flights cannot finish is that defect wearing the lifecycle's clothes.
    /// </remarks>
    public static IReadOnlyList<string> Exits { get; } = [Landed, Grounded, Withdrawn, Failed];
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
