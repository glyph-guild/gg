namespace Gg.Contracts.Description;

/// <summary>Which credential an endpoint answers to.</summary>
public enum Audience
{
    /// <summary>No credential. Only the path by which one is obtained.</summary>
    Anonymous,

    /// <summary>A person, acting through a browser session or <c>gg</c>.</summary>
    Developer,

    /// <summary>A runner, acting through the runner protocol and nothing else.</summary>
    Runner,
}

/// <summary>One endpoint of the gg-to-control-plane protocol.</summary>
public sealed record Endpoint
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    /// <summary>Which credential this endpoint answers to.</summary>
    public required Audience Audience { get; init; }

    /// <summary>Wire type of the request body, or null if there is none.</summary>
    public Type? Request { get; init; }

    /// <summary>Wire type of the success response body, or null if there is none.</summary>
    public Type? Response { get; init; }

    /// <summary>
    /// Every status this endpoint may answer with. A client must handle all of
    /// them and a server must produce no others.
    /// </summary>
    public required IReadOnlyList<int> Statuses { get; init; }

    /// <summary>Headers the caller must send beyond the version headers.</summary>
    public IReadOnlyList<string> RequiredHeaders { get; init; } = [];
}

/// <summary>
/// The HTTP surface of the protocol, declared once and checked from both sides.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the two halves of the protocol live in two
/// repositories that cannot reference each other. Before it, <c>gg</c> was
/// tested against a stub written in this repo and the control plane against
/// its own edge, so both suites could stay green while the two disagreed
/// about a header name, a status code or a JSON casing - and the first thing
/// to notice would have been a real customer.
/// </para>
/// <para>
/// Now both sides check themselves against THIS, and a disagreement fails
/// somebody's build. It still is not an end-to-end test - nothing here runs a
/// real gg against a real control plane - but it removes the class of
/// divergence that silence was hiding.
/// </para>
/// <para>
/// Header names and the protocol revision live here rather than being
/// declared on each side and asserted equal: one definition cannot drift from
/// itself.
/// </para>
/// </remarks>
// NOT A VOCABULARY, strictly. This declares the protocol - header names and governed
// prefixes - rather than enumerating values a field may hold, and it matches the closure
// check's shape by consequence rather than by intent. Declared as contract because
// changing a header name is unambiguously a wire change; recorded here because a type
// answering a question it was not asked is worth a sentence rather than an exemption.
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ProtocolSurface
{
    /// <summary>
    /// Wire protocol revision. Bumped only when the protocol becomes
    /// incompatible - not when the package version moves.
    /// </summary>
    public const int Revision = 1;

    /// <summary>Carries a developer's session token.</summary>
    public const string SessionHeader = "X-Gg-Session";

    /// <summary>Carries a runner's credential. Deliberately NOT the session header.</summary>
    public const string RunnerHeader = "X-Gg-Runner";

    /// <summary>Carries the revision the caller speaks.</summary>
    public const string ProtocolHeader = "GG-Protocol-Version";

    /// <summary>Carries the calling binary's own version.</summary>
    public const string RunnerVersionHeader = "GG-Runner-Version";

    /// <summary>Carries the fact vocabulary the caller evaluates against.</summary>
    public const string FactVocabularyHeader = "GG-Fact-Vocabulary";

    /// <summary>Names the range a 426 would accept.</summary>
    public const string SupportedProtocolsHeader = "GG-Supported-Protocols";

    /// <summary>Sent on every request to a governed path.</summary>
    public static IReadOnlyList<string> VersionHeaders { get; } =
        [ProtocolHeader, RunnerVersionHeader, FactVocabularyHeader];

    /// <summary>
    /// The path prefixes this declaration governs completely.
    /// </summary>
    /// <remarks>
    /// Scoped rather than "everything under /v1", because the control plane
    /// also serves a tenant API that gg does not speak yet. Within these
    /// prefixes the declaration is CLOSED: a route the control plane serves
    /// and this file does not name is a divergence, and the control plane's
    /// tests fail on it.
    ///
    /// /v1/flights joined at step 4a. The flight read surface sat outside the
    /// declaration while nothing consumed it, which was honest - a declaration
    /// nobody checks is a comment. The console consumes it, so it comes in
    /// under the same closure guarantee as the rest.
    ///
    /// /v1/credentials joined at step 5, and closure matters most here: a
    /// credential route the control plane served and this file did not name
    /// would be an unaudited path into the one table Article VIII is about.
    ///
    /// /v1/invitations joined at slice five. It could have stayed outside -
    /// nothing forces a new prefix in - but an invitation is the strongest
    /// capability the product issues: whoever holds the link becomes a principal
    /// in a tenant. A route under this prefix that this file did not name would
    /// be an undeclared way to make one, which is the same argument
    /// /v1/credentials came in on.
    /// </remarks>
    public static IReadOnlyList<string> GovernedPrefixes { get; } =
        ["/v1/auth", "/v1/runner", "/v1/leases", "/v1/flights", "/v1/telemetry", "/v1/credentials",
         "/v1/envelope", "/v1/invitations"];

    /// <summary>Refusal for a caller below the protocol floor.</summary>
    public const int ProtocolTooOld = 426;

    /// <summary>Every endpoint gg may call.</summary>
    public static IReadOnlyList<Endpoint> Endpoints { get; } =
    [
        new()
        {
            Method = "POST",
            Path = "/v1/auth/device",
            Audience = Audience.Anonymous,
            Request = typeof(DeviceAuthorizationRequest),
            Response = typeof(DeviceAuthorizationStarted),
            Statuses = [200, ProtocolTooOld],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/auth/device/token",
            Audience = Audience.Anonymous,
            Request = typeof(DeviceTokenRequest),
            Response = typeof(SessionIssued),
            // 202 is a WAIT, not a failure. 410 covers expired, denied,
            // already-used and never-existed alike, so an unauthenticated
            // caller cannot probe for live device codes.
            Statuses = [200, 202, 410, ProtocolTooOld],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/invitations",
            Audience = Audience.Developer,
            Request = typeof(InvitationRequest),
            Response = typeof(InvitationIssued),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/auth/whoami",
            Audience = Audience.Developer,
            Response = typeof(WhoAmI),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/auth/logout",
            Audience = Audience.Developer,
            Statuses = [204, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/runners",
            Audience = Audience.Developer,
            Request = typeof(RunnerRegistrationRequest),
            Response = typeof(RunnerRegistered),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/runner/hello",
            Audience = Audience.Runner,
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/runners/{id}/heartbeat",
            Audience = Audience.Runner,
            Request = typeof(RunnerHeartbeat),
            Response = typeof(HeartbeatAccepted),
            // 404 rather than 403 for another runner's id: a runner learning
            // which ids exist is a runner enumerating the tenant's fleet.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases:claim",
            Audience = Audience.Runner,
            Request = typeof(LeaseClaimRequest),
            Response = typeof(LeaseGranted),
            // 204 is "nothing to do", the normal answer for an idle fleet. It
            // is not an error and must not read as one, for the same reason
            // the device poll answers 202.
            Statuses = [200, 204, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases/{id}/renew",
            Audience = Audience.Runner,
            Request = typeof(LeaseRenewalRequest),
            Response = typeof(LeaseRenewed),
            // 409 is the generation fence refusing a stale holder. It is a
            // real outcome of correct client behaviour, not a client bug.
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases/{id}/facts",
            Audience = Audience.Runner,
            Request = typeof(FactBatch),
            // ACCEPTED, not answered. ADR-0012 makes the write a command, so
            // the body reports only what was refused before anything was
            // written - the one part decided from the request itself.
            Response = typeof(FactBatchAccepted),
            // Against the LEASE, because the lease is the authorisation: a
            // runner asserting facts about a flight it does not hold would be
            // a runner writing into somebody else's evidence. 409 is the
            // generation fence refusing exactly that, and it stays synchronous
            // because it too is decided from what the request carries.
            Statuses = [202, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/leases/{id}/admission",
            Audience = Audience.Runner,
            // WHERE THE LANDING DECISION WENT. It rode the facts response while
            // the write was synchronous; it cannot once the answer is computed
            // after the request returns. Asked against the lease, which is the
            // same authorisation shipping facts uses - a runner may ask about
            // the flight it holds and no other.
            Response = typeof(LandingDecision),
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/leases/{id}/release",
            Audience = Audience.Runner,
            Request = typeof(LeaseReleaseRequest),
            Response = typeof(LeaseReleased),
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },

        // The flight read surface. Developer audience throughout: a runner that
        // could read the flight list could enumerate a tenant's work from a
        // credential meant only to let it hold one lease at a time.
        new()
        {
            Method = "POST",
            Path = "/v1/flights",
            Audience = Audience.Developer,
            Request = typeof(FlightLaunchRequest),
            Response = typeof(FlightLaunched),
            // 202, not 200: the edge dispatches a command and the flight is
            // materialized asynchronously. Answering 200 would claim the
            // flight is readable, and the very next GET might disagree.
            // 400 is a refused intent - Article XI, with a diagnosis.
            Statuses = [202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // THE ANSWER PATH, and the only one. Questions travel on a path the agent
            // can write - facts, from a runner token - and answers travel on a path only
            // the decider can write: Developer audience, session header, and no runner
            // credential opens it. A runner able to record a decision could answer for
            // the person it is meant to be waiting on.
            Method = "POST",
            Path = "/v1/flights/{ref}/decisions",
            Audience = Audience.Developer,
            Request = typeof(DecisionRequest),
            // NO RESPONSE BODY. ADR-0012: the write is a command, so the control
            // plane accepts the decision and the caller learns what happened by
            // reading. Answering inline would mean the endpoint had to wait for its
            // own event to land, which is the blocking read this whole change
            // removes.
            Response = null,
            // 202, not 200. The decision is taken and nothing is answered with.
            //
            // 409 SURVIVES, and that is not an inconsistency: a decision made
            // against work that has since moved is refused BEFORE anything is
            // dispatched, from state the request already has. What became
            // asynchronous is the recording, not the admission check on the way in.
            Statuses = [202, 400, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // A LIST AND NOTHING ELSE. No companion endpoint answers a gate, which
            // is what makes "nothing an agent can call can unstick a flight" a
            // property of the declared surface rather than a convention. Step 4
            // adds exactly one, and the declaration is where that will be visible.
            Method = "GET",
            Path = "/v1/gates",
            Audience = Audience.Developer,
            Response = typeof(GateList),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights/{ref}/why",
            Audience = Audience.Developer,
            Response = typeof(FlightAttribution),
            // 404 for a flight nobody has. There is no 'no obligations' status:
            // an envelope that governs nothing is refused at ingress, and a
            // flight governed by nothing answers with an empty list and says so
            // - which is a different thing from a missing flight.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/flights/{id}/takeover",
            Audience = Audience.Developer,
            Request = typeof(TakeoverRecord),
            // No response body. What a person needs is on the flight log a
            // moment later, and a second shape to keep in step buys nothing.
            //
            // DEVELOPER audience, deliberately. A takeover is a person holding a
            // terminal, and a runner able to record one could write a person's
            // name onto its own work.
            Statuses = [202, 400, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights",
            Audience = Audience.Developer,
            Response = typeof(FlightList),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights/{ref}",
            Audience = Audience.Developer,
            Response = typeof(FlightSummary),
            // {ref} is a uuid OR a flight number. Both resolve here, by the
            // one parser in FlightRef; a reference in neither form is a 404
            // rather than a 400, because "GG-nope" names no flight in exactly
            // the way a well-formed id for somebody else's flight does.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/flights/{ref}/log",
            Audience = Audience.Developer,
            Response = typeof(FlightLog),
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/telemetry",
            Audience = Audience.Developer,
            Response = typeof(TelemetryDisclosure),
            // Developer, not anonymous. It says nothing about a tenant, but an
            // unauthenticated endpoint disclosing where a deployment ships its
            // logs is a reconnaissance surface for anyone who finds the host.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        // The credential surface. Developer throughout, and that is the whole
        // authority story: a person registers a credential, a runner resolves
        // one. A runner that could register would be a runner that could point
        // a flight at a secret of its own choosing.
        new()
        {
            Method = "POST",
            Path = "/v1/credentials",
            Audience = Audience.Developer,
            Request = typeof(CredentialRegistrationRequest),
            Response = typeof(CredentialRegistered),
            // 400 is a refused reference - a kind that is not local, a scope
            // wider than read, a malformed locator. Article XI, with a
            // diagnosis, rather than a 500 nobody can act on.
            Statuses = [200, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/credentials",
            Audience = Audience.Developer,
            Response = typeof(CredentialList),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "DELETE",
            Path = "/v1/credentials/{id}",
            Audience = Audience.Developer,
            Response = typeof(CredentialRemoved),
            // 404 covers another tenant's credential and one that never
            // existed alike, for the same reason a flight does.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },

        new()
        {
            Method = "GET",
            Path = "/v1/runners",
            Audience = Audience.Developer,
            Response = typeof(RunnerList),
            // A person reads the fleet; a runner beats. Same path, and the two
            // audiences never overlap.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/envelope",
            Audience = Audience.Developer,
            Response = typeof(EnvelopeState),
            // 404 is a tenant that has never applied one, and it is a
            // DIFFERENT answer from an empty envelope: one of them governs
            // nothing on purpose and the other has never been set up.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "PUT",
            Path = "/v1/envelope",
            Audience = Audience.Developer,
            Request = typeof(Envelope),
            Response = typeof(EnvelopeApplied),
            // 400 is a refused envelope, with the diagnosis Envelope.Validate
            // produced. The control plane checks on its own terms rather than
            // trusting that gg did - both sides fail closed on their own
            // format.
            Statuses = [200, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
    ];

    /// <summary>
    /// Whether a concrete request path is this endpoint, treating <c>{...}</c>
    /// segments as placeholders.
    /// </summary>
    /// <remarks>
    /// The control plane can compare its route patterns to <see cref="Endpoint.Path"/>
    /// literally; a client cannot, because what it actually put on the wire has
    /// a real lease id in it. Both sides need the same answer, so the matching
    /// rule lives here rather than being written twice and drifting.
    /// </remarks>
    public static bool Matches(Endpoint endpoint, string method, string concretePath)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!string.Equals(endpoint.Method, method, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var declared = endpoint.Path.Split('/');
        var actual = concretePath.Split('/');
        if (declared.Length != actual.Length)
        {
            return false;
        }

        for (var i = 0; i < declared.Length; i++)
        {
            var segment = declared[i];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                // A placeholder matches anything except emptiness: an empty
                // segment means the client sent /v1/leases//renew, which is a
                // missing id rather than a match.
                if (actual[i].Length == 0)
                {
                    return false;
                }
                continue;
            }

            if (!string.Equals(segment, actual[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The endpoint a concrete request belongs to, or null.</summary>
    public static Endpoint? Find(string method, string concretePath) =>
        Endpoints.FirstOrDefault(e => Matches(e, method, concretePath));

    /// <summary>
    /// The JSON member names each wire type must serialize to.
    /// </summary>
    /// <remarks>
    /// Declared, not derived. If each side derived these from its own
    /// serializer they would agree with themselves and prove nothing - which
    /// is exactly how a camelCase-versus-PascalCase split survives two green
    /// test suites. Compared as a SET: JSON is not positional, so member order
    /// is not part of the contract.
    /// </remarks>
    public static IReadOnlyDictionary<Type, IReadOnlyList<string>> JsonMembers { get; } =
        new Dictionary<Type, IReadOnlyList<string>>
        {
            [typeof(ObligationAttribution)] =
                ["obligationId", "attachment", "condition", "because", "outcome", "diagnosis"],
            [typeof(GateList)] = ["gates"],
            [typeof(DecisionObservations)] =
                ["interactive", "evidenceRendered", "secondsToDecide"],
            [typeof(DecisionRequest)] =
                ["obligationId", "outcome", "manifestHash", "observations", "reason"],
            [typeof(LeaseFeedback)] = ["obligationId", "decidedBy", "reason", "decidedAt"],
            [typeof(DecisionRecorded)] =
                ["flightNumber", "obligationId", "outcome", "decidedBy", "decidedAt", "admission"],
            [typeof(PendingGate)] =
                ["flightNumber", "obligationId", "approver", "branch", "commit", "manifestHash",
                 "condition", "because", "awaitingSince", "attempt"],
            [typeof(BranchPush)] = ["branch", "baseRef", "slug", "reason"],
            [typeof(FlightAttribution)] =
                ["flightNumber", "envelopeVersion", "obligations", "halt"],
            [typeof(TakeoverRecord)] =
                ["by", "startedAt", "heldForMs", "outcome", "diagnosis", "note"],
            [typeof(TakeoverReturn)] = ["flightId", "outcome", "note"],
            [typeof(HumanAccount)] =
                ["by", "statement", "confirmation", "confirmedAt", "wasProposed"],
            [typeof(LoopDigest)] =
                ["loopId", "filesReadNotEdited", "filesEdited", "searches", "errors",
                 "refusedMoves", "attempts", "stopReason"],
            [typeof(DigestError)] = ["source", "detail"],
            [typeof(DestinationLanded)] =
                ["destinationId", "branch", "pullRequestUri", "pullRequestNumber"],
            [typeof(DestinationPushed)] = ["slug", "branch", "commit"],
            [typeof(DestinationAdmission)] =
                ["destinationId", "branch", "baseRef", "slug", "reason"],
            [typeof(LeaseLoop)] =
                ["loopId", "executor", "moves", "wallClockSeconds", "onExhaustion"],
            [typeof(LoopOutcome)] =
                ["loopId", "outcome", "reason", "executor", "attempts", "durationMs", "movesUsed"],
            [typeof(ArtifactReference)] = ["locator", "sha256", "bytes", "mediaType", "scope"],
            [typeof(ContextBinding)] = ["scope", "constitution"],
            [typeof(Obligation)] = ["id", "check", "when", "rule", "approver", "provenance", "evidence"],
            [typeof(LoopBudget)] = ["wallClock", "attempts"],
            [typeof(Loop)] =
                ["id", "executor", "discharges", "moves", "budget", "onExhaustion"],
            [typeof(Destination)] = ["id", "kind", "requires"],
            [typeof(Envelope)] = ["context", "obligations", "loops", "destinations"],
            [typeof(EnvelopeState)] = ["version", "envelope", "updatedAt", "updatedBy"],
            [typeof(EnvelopeApplied)] = ["version", "appliedAt", "changed"],
            [typeof(ProtocolHello)] = ["protocolVersion", "component", "componentVersion"],
            [typeof(DeviceAuthorizationRequest)] = ["deviceLabel"],
            [typeof(DeviceAuthorizationStarted)] =
                ["deviceCode", "userCode", "verificationUri", "pollIntervalSeconds", "expiresAt"],
            [typeof(DeviceTokenRequest)] = ["deviceCode"],
            [typeof(SessionIssued)] = ["sessionToken", "expiresAt", "principalDisplay", "tenantId"],
            [typeof(WhoAmI)] = ["principalId", "principalDisplay", "tenantId", "expiresAt", "notices"],
        // An invitation names nobody: no address, no display, no tenant. The
        // request really is empty, and the empty set is the assertion.
        [typeof(InvitationRequest)] = [],
        [typeof(InvitationIssued)] = ["invitationUrl", "expiresAt"],
            [typeof(TenantNotice)] = ["code", "detail", "remedy", "blocking"],
            [typeof(RunnerRegistrationRequest)] = ["label", "protocolVersion"],
            [typeof(RunnerRegistered)] = ["runnerId", "runnerToken", "expiresAt"],
            [typeof(RunnerHeartbeat)] = ["labels"],
            [typeof(HeartbeatAccepted)] = ["nextHeartbeatSeconds"],
            [typeof(LeaseClaimRequest)] = ["runnerId", "labels", "maxWaitSeconds"],
            [typeof(LeaseRepoRef)] = ["provider", "slug", "pinnedRef", "baseRef", "continuesFrom"],
            [typeof(LeaseGranted)] =
                ["leaseId", "generation", "flightId", "flightNumber", "repos", "credentials",
                 "classificationCeiling", "classificationRules", "expiresAt", "renewWithinSeconds",
                 "intentUri", "loop", "feedback"],
            [typeof(LeaseRenewalRequest)] = ["generation"],
            [typeof(LeaseRenewed)] = ["expiresAt", "generation"],
            [typeof(LeaseReleaseRequest)] = ["generation", "disposition", "detail", "credentialFailure"],
            [typeof(LeaseReleased)] = ["flightId", "disposition"],
            [typeof(FlightIntent)] = ["kind", "uri", "text"],
            [typeof(FlightLaunchRequest)] = ["name", "intent"],
            [typeof(FlightLaunched)] = ["flightId", "flightNumber"],
            [typeof(FlightSummary)] =
                ["flightId", "flightNumber", "name", "intent", "createdAt",
                 "runnerProtocolVersion", "factVocabularyVersion", "constitutionVersion", "envelopeVersion",
                 "attempts",
                 "facts"],
            [typeof(FlightList)] = ["flights"],
            [typeof(FlightLogEntry)] = ["at", "kind", "detail"],
            [typeof(FlightLog)] = ["flightId", "flightNumber", "entries"],
            [typeof(RunnerSummary)] =
                ["runnerId", "label", "state", "currentFlightId", "currentFlightNumber", "lastHeartbeatAt"],
            [typeof(RunnerList)] = ["runners"],
            [typeof(TelemetryDisclosure)] = ["exporting", "destination"],
            // Four members, and none of them a secret. Declared here as well
            // as asserted over the type, because a [JsonPropertyName] can add
            // a wire member without adding a property name.
            [typeof(CredentialReference)] = ["kind", "locator", "identity", "scopes"],
            [typeof(CredentialRegistrationRequest)] = ["repo", "reference"],
            [typeof(CredentialRegistered)] = ["credentialId", "reference", "addedAt"],
            [typeof(CredentialSummary)] = ["credentialId", "repo", "reference", "addedAt"],
            [typeof(CredentialList)] = ["credentials"],
            [typeof(CredentialRemoved)] = ["credentialId", "reference"],
            [typeof(CredentialResolutionFailure)] = ["reference", "problem"],
            // Paths, counts and hashes. There is no member on any of these a
            // file's contents could travel in, which is asserted over their
            // shape as well as declared here.
            [typeof(LockHash)] = ["path", "sha256"],
            [typeof(ToolVersion)] = ["name", "version"],
            [typeof(EnvironmentIdentity)] =
                ["hostFingerprint", "imageDigest", "locks", "tools", "provenance",
                 "moveEnforcement", "movesProbed"],
            [typeof(SourceProvenance)] =
                ["provider", "slug", "requestedRef", "resolvedRef", "headCommit",
                 "headIsFork", "forkSlug", "fileCount", "bytes"],
            [typeof(FactEnvelope)] =
                ["idempotencyKey", "kind", "digest", "observedAt", "environment", "source", "change",
                 "loop", "transcript", "landed", "pushed", "loopDigest", "human"],
            [typeof(FactBatch)] = ["generation", "facts"],
            [typeof(FactRejection)] = ["idempotencyKey", "reason"],
            // Refusals only: accepted and duplicates are answers the write has,
            // and the write is a command now. Push and admission moved to
            // LandingDecision, which carries `settled` so a runner can tell
            // "not yet" from "no" - absence means refusal only once it is true.
            [typeof(FactBatchAccepted)] = ["rejected"],
            [typeof(LandingDecision)] = ["settled", "push", "admission"],
            [typeof(ClassificationRule)] = ["pathGlob", "classification"],
            // Paths and counts. Nothing here a line of a file could travel in,
            // asserted over the shape as well as declared.
            [typeof(ChangedPath)] = ["path", "change", "linesAdded", "linesRemoved", "classification"],
            [typeof(DirectoryChange)] = ["directory", "files", "linesAdded", "linesRemoved"],
            [typeof(LanguageChange)] = ["language", "files", "linesAdded", "linesRemoved"],
            [typeof(ChangeManifest)] =
                ["baseCommit", "headCommit", "resolution", "diffBasis", "paths", "directories",
                 "languages", "filesChanged", "linesAdded", "linesRemoved", "pathsWithheld"],
        };
}
