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
    /// Wire type of a 202 answer, when the endpoint may defer to a flight.
    /// </summary>
    /// <remarks>
    /// A registration door's done shape carries required members a pending
    /// answer cannot honestly fill - who registered it and when, which have
    /// not happened yet - so the deferral is its own declared body rather
    /// than a convention layered on the success type.
    /// </remarks>
    public Type? PendingResponse { get; init; }

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
         "/v1/envelope", "/v1/invitations", "/v1/environments",
         // The topology decides which envelope names are REACHABLE, so an
         // undeclared route under it would be an unaudited way to widen what
         // every tenant's envelopes can reach - the /v1/environments
         // argument, one level up.
         "/v1/airspace",
         // The pools surface: what a resident runner pulls and attests, and
         // what a person reads about a managed pool. Governed for the lease
         // prefix's reason - a runner-audience route nobody declared would be
         // an unaudited way for a runner to reach the control plane.
         "/v1/pools"];

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
        // WHOSE RUNNER THIS IS, set and cleared after registration. A person's
        // act on both verbs: the value decides what work the runner is offered,
        // so a runner able to change it could widen its own queue.
        new()
        {
            Method = "POST",
            Path = "/v1/runners/{id}/reservation",
            Audience = Audience.Developer,
            Request = typeof(RunnerReservationRequest),
            Response = typeof(RunnerReserved),
            // 404 for a runner that is not this tenant's, per the heartbeat
            // route: the shape of a refusal must not tell a caller which ids
            // exist. 409 for one somebody else already holds - taking it is a
            // different act and is not this one.
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "DELETE",
            Path = "/v1/runners/{id}/reservation",
            Audience = Audience.Developer,
            Response = typeof(RunnerReserved),
            // NO 409. Releasing a runner nobody reserved is the state the caller
            // asked for, and refusing it would make "make sure this is free" a
            // two-step dance with a race in the middle.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
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
            // ACCEPTED, not answered. Whether a flight can be handed over
            // depends on state that arrives asynchronously - the control plane
            // learns which credential references a flight needs from what
            // identity announced - so at the moment the request is taken the
            // answer does not exist yet.
            //
            // 204 goes with the 200, and that is the point rather than a side
            // effect: an idle fleet and a fleet waiting on something were the
            // same answer, so a runner could not tell "nothing to do" from
            // "something is missing and somebody should look".
            Response = typeof(LeaseClaimAccepted),
            Statuses = [202, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/leases/claims/{id}",
            Audience = Audience.Runner,
            // What became of the request. The lease is here once there is one,
            // and absent otherwise - which is safe only because `state` carries
            // the question separately from the answer, the arrangement
            // LandingDecision uses for exactly the same reason.
            Response = typeof(LeaseClaimStatus),
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
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
        // THE HOLD, in three routes where there was one.
        //
        // What was here was POST /v1/flights/{id}/takeover, which took a
        // TakeoverRecord carrying heldForMs - so it was posted when somebody had
        // already finished. That is a record and not a hold: two people on two
        // machines both saw a takeable flight, both took it, and both found out
        // afterwards. It is deleted rather than kept beside these, because a route
        // that records a takeover without holding anything sitting next to the
        // claim that replaces it is a shape somebody will build against.
        //
        // Developer throughout. A runner able to claim a takeover could hold a
        // flight against the person it is meant to be waiting for.
        new()
        {
            Method = "POST",
            Path = "/v1/flights/{ref}/takeover:claim",
            Audience = Audience.Developer,
            Response = typeof(TakeoverClaimed),
            // 409 IS THE POINT, and it carries TakeoverHeld. A second claimant is
            // refused, and refusal is an outcome of correct client behaviour rather
            // than a client bug - two people looking at the same stopped flight is
            // the ordinary case this exists for.
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/flights/{ref}/takeover:renew",
            Audience = Audience.Developer,
            Request = typeof(TakeoverRenewalRequest),
            Response = typeof(TakeoverRenewed),
            // The same generation fence POST /v1/leases/{id}/renew uses, for the
            // same reason: a holder whose hold lapsed and was claimed by somebody
            // else is told it is not theirs rather than handed it back.
            Statuses = [200, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "POST",
            Path = "/v1/flights/{ref}/takeover:return",
            Audience = Audience.Developer,
            Request = typeof(TakeoverReturnRequest),
            // NO RESPONSE BODY. The write is a command: the record is appended and
            // what a person needs is on the flight log a moment later. Answering
            // inline would mean waiting for its own event to land.
            Response = null,
            Statuses = [202, 400, 401, 403, 404, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // THE QUEUE MEANS WHAT ITS DESCRIPTION SAYS, since slice fourteen.
            // This returned every flight the tenant had ever opened, which was
            // the only thing it could return while nothing recorded an ending -
            // so `gg flights` grew forever and the one-line description
            // ("what's in the air") was aspirational.
            //
            // ?all= is a parameter rather than a second route because the
            // question is the same one; what changed is that there is now an
            // honest default answer to it. `unknown` stays in the default view
            // deliberately: a flight nobody can account for is exactly what
            // somebody should see.
            Method = "GET",
            Path = "/v1/flights",
            Audience = Audience.Developer,
            Response = typeof(FlightList),
            Statuses = [200, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // A PERSON WITHDRAWS A FLIGHT WHOSE QUESTION STOPPED APPLYING
            // (ADR-0017). The system reaches this exit too - pool recovery
            // withdraws the maintenance flight whose pull point came back up -
            // and this is the door for the case only a person can see.
            //
            // NO 200, like the retirement door one estate over: the answer is
            // that the flight is over, and what a caller does next is read it.
            // 409 is a flight that has ALREADY ended, refused rather than
            // silently accepted - accepting would let a withdrawal appear to
            // rewrite an ending that already happened.
            Method = "POST",
            Path = "/v1/flights/{ref}/withdrawal",
            Audience = Audience.Developer,
            Request = typeof(FlightWithdrawalRequest),
            Statuses = [202, 400, 401, 403, 404, 409, ProtocolTooOld],
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
            // WHERE THE HANDOFF LIVES NOW. The seed used to be composed on the
            // machine that ran the flight, from a digest on its disk, and placed on
            // that machine's clipboard - so a flight was resumable by whoever was
            // sitting at that keyboard and by nobody else. Composed control-plane
            // side from facts already held, it is resumable by anyone.
            //
            // DEVELOPER, and the consequence is deliberate: a runner cannot fetch a
            // seed. One that could would be able to read what every flight in the
            // tenant tried and ruled out, from a credential meant only to let it
            // hold one lease. So a resuming loop is HANDED its seed on the lease
            // rather than asking for one.
            Method = "GET",
            Path = "/v1/flights/{ref}/seed",
            Audience = Audience.Developer,
            Response = typeof(TakeSeed),
            // 404 for a flight nobody has and for another tenant's alike, the same
            // rule GET /v1/flights/{ref} follows. There is no "nothing to resume"
            // status: a flight that ran and measured nothing still has a seed, and
            // empty measurements are measurements.
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
        new()
        {
            // THE CHART. What an envelope may select, and the registry the
            // "uncharted" refusal points at. Charting in v0 is unrestricted
            // AND attributed - who may chart is an open question elsewhere;
            // that it is logged is not.
            Method = "POST",
            Path = "/v1/environments",
            Audience = Audience.Developer,
            Request = typeof(ChartEnvironmentRequest),
            Response = typeof(EnvironmentCharted),
            PendingResponse = typeof(RegistrationPending),
            // 400 is a malformed name, refused with a diagnosis rather than
            // stored - the registry is what apply refusals point people at,
            // and a chart that could hold a blank line would make that advice
            // a trap.
            // 202 is the gated path: the registration widens what the tenant
            // can reach, so it rides a flight and the answer says who decides.
            Statuses = [200, 202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/environments",
            Audience = Audience.Developer,
            Response = typeof(EnvironmentChart),
            // An empty chart is 200 with nothing in it, not 404: a tenant that
            // has charted nothing is set up and has said nothing, which is a
            // different fact from a tenant that does not exist.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // THE STRATEGY DOOR. A management document applies to a name whose
            // topology role is strategy, through the same per-name stream and
            // version counter as every other document. 400 is a refusal -
            // EnvironmentStrategy.Validate's diagnosis, an uncharted furnished
            // label, or an unknown name/role; 202 is an inventory extension
            // diverted to the widening gate.
            Method = "PUT",
            Path = "/v1/airspace/strategies/{name}",
            Audience = Audience.Developer,
            Request = typeof(EnvironmentStrategy),
            Response = typeof(EnvelopeApplied),
            Statuses = [200, 202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/airspace/strategies/{name}",
            Audience = Audience.Developer,
            Response = typeof(EnvironmentStrategyState),
            // 404 is a name with no strategy in force - different from a
            // strategy that manages nothing, which cannot exist: the document
            // requires its inventory.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/airspace/strategies",
            Audience = Audience.Developer,
            Response = typeof(StrategyList),
            // An empty list is 200 with nothing in it: a tenant managing no
            // pools is the null strategy, which is a state and not an error.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // THE ESTATE'S FIRST READ OF ITSELF. Rendering a working copy is a
            // fan-out over the topology by construction - ADR-0014 accepted
            // that cost when it chose a stream per name - so the fan-out
            // happens here, once, rather than once per name across the wire.
            Method = "GET",
            Path = "/v1/airspace/envelopes",
            Audience = Audience.Developer,
            Response = typeof(NamedEnvelopeList),
            // An empty list is 200: a tenant whose estate is root alone has an
            // estate, and pull writes it a tree with one file in it.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/airspace/envelopes/{name}",
            Audience = Audience.Developer,
            Response = typeof(NamedEnvelopeState),
            // 404 is a declared name with no document in force - reachable and
            // empty, which is a different answer from undeclared.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // APPLY BY NAME - the door slice nine deferred three times, and the
            // floor the whole working copy stands on: without it, two of the
            // four directories ADR-0016 draws cannot be written at all.
            //
            // 202 is a widening diverted to the gate. Before this door a
            // widening of a NAMED document was refused outright, because there
            // was no flight for it to ride - which made "retire and redeclare"
            // the only path, and made retirement the thing you needed first.
            Method = "PUT",
            Path = "/v1/airspace/envelopes/{name}",
            Audience = Audience.Developer,
            Request = typeof(NamedEnvelopeApply),
            Response = typeof(EnvelopeApplied),
            // 409 is the precondition, stated as ?based-on= and overtaken by the
            // stream. It is a query parameter rather than a body member because
            // the body's stored form is the idempotence key: a member that
            // changed on every pull would mint a version per document per pull.
            Statuses = [200, 202, 400, 401, 403, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // RETIREMENT, AND IT IS A VERSION RATHER THAN A DELETION. ADR-0014:
            // the only way to retire a name is to apply a terminal version of
            // it, because retiring by deleting a topology entry is a
            // governance-critical change wearing bookkeeping's clothes - the
            // constraint stops attaching and no version records that it did.
            //
            // NO 200. A document that stops applying removes every constraint
            // in it at once, so this is a widening by construction and always
            // rides the gate - registration's rule in the other direction.
            Method = "POST",
            Path = "/v1/airspace/envelopes/{name}/retirement",
            Audience = Audience.Developer,
            Response = typeof(EnvelopeApplied),
            Statuses = [202, 400, 401, 403, 409, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // THE PULL POINT. Serving is the claim, control-plane-side: a
            // decided action appears in exactly one answer, so two resident
            // runners polling one pool get disjoint sets.
            Method = "GET",
            Path = "/v1/pools/{pool}/actions",
            Audience = Audience.Runner,
            Response = typeof(PoolActionList),
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            // MINTING A MEMBER'S IDENTITY. The resident runner's own token
            // authorizes it, and 403 is the arm that matters: a MEMBER
            // presenting its own runner token must not mint another, or one
            // compromised member mints an unbounded supply.
            //
            // Both the pool and the member are in the path. A credential is
            // minted FOR one member and is not a tenant-wide grant.
            Method = "POST",
            Path = "/v1/pools/{pool}/members/{member}/credential",
            Audience = Audience.Runner,
            Request = typeof(MemberCredentialRequest),
            Response = typeof(MemberCredentialMinted),
            Statuses = [200, 400, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            // REDEEMING IT, and this one is ANONYMOUS by necessity: a member has
            // no credential yet, which is the whole point. The nonce is the
            // authorization, it is single-use, and 409 is the second attempt
            // being told it is spent rather than handed another identity.
            Method = "POST",
            Path = "/v1/pools/members/redeem",
            Audience = Audience.Anonymous,
            Request = typeof(MemberCredentialRedemption),
            Response = typeof(MemberCredentialIssued),
            Statuses = [200, 400, 404, 409, ProtocolTooOld],
        },
        new()
        {
            // THE ATTESTATION. 202 because the write is a command; the row it
            // becomes is a query resource. 400 is the contract's own Validate
            // refusal - both sides fail closed on their own format.
            Method = "POST",
            Path = "/v1/pools/{pool}/attestations",
            Audience = Audience.Runner,
            Request = typeof(PoolAttestation),
            Statuses = [202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [RunnerHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/pools",
            Audience = Audience.Developer,
            Response = typeof(PoolLedger),
            // An empty ledger is 200 with nothing in it: a tenant whose pools
            // have never attested is the null strategy or a pool that has not
            // come up - states, not errors, and the checklist tells them apart.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // Declaring a name is what makes it reachable at all: an envelope
            // applied to an undeclared name is refused pointing HERE, so the
            // door ships in the same contract as the refusal. v0 is
            // unrestricted and attributed, the chart's shape.
            Method = "POST",
            Path = "/v1/airspace/names",
            Audience = Audience.Developer,
            Request = typeof(DeclareNameRequest),
            Response = typeof(TopologyName),
            PendingResponse = typeof(RegistrationPending),
            // 400 is a malformed or reserved name, a missing parent, or an
            // unknown role - each refused with a diagnosis rather than stored.
            // 202 is the gated path: the registration widens what the tenant
            // can reach, so it rides a flight and the answer says who decides.
            Statuses = [200, 202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/airspace/topology",
            Audience = Audience.Developer,
            Response = typeof(EnvelopeTopology),
            // Never empty and never 404: root is synthesized by the read, so
            // the floor is in the answer before anything is declared.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // Registration is what makes a repository nameable at all - a
            // flight whose intent names an unregistered repository is refused
            // pointing HERE. v0 unrestricted and attributed, the chart's
            // shape. No credential and no host cross this wire, by design.
            Method = "POST",
            Path = "/v1/airspace/repositories",
            Audience = Audience.Developer,
            Request = typeof(RegisterRepositoryRequest),
            Response = typeof(RepositoryRegistered),
            PendingResponse = typeof(RegistrationPending),
            // 202 is the gated path: the registration widens what the tenant
            // can reach, so it rides a flight and the answer says who decides.
            Statuses = [200, 202, 400, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            Method = "GET",
            Path = "/v1/airspace/repositories",
            Audience = Audience.Developer,
            Response = typeof(RegisteredRepositories),
            // Empty is 200 with nothing in it: a tenant that registered
            // nothing is set up and has said nothing.
            Statuses = [200, 401, 403, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // THE TENANT-LEVEL PLAN: what WOULD a flight under the current
            // envelope need, priced against the fleet the moment somebody
            // asks. Reads facts, exercises nothing.
            Method = "GET",
            Path = "/v1/envelope/plan",
            Audience = Audience.Developer,
            Response = typeof(Checklist),
            // 404 is a tenant that has never applied an envelope - a different
            // answer from a plan with nothing on it, the same split GET
            // /v1/envelope draws.
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
            RequiredHeaders = [SessionHeader],
        },
        new()
        {
            // The per-flight checklist, pinned to what THAT flight compiled at
            // creation - applying a new envelope later does not retarget it,
            // the same rule as the envelope version pin.
            Method = "GET",
            Path = "/v1/flights/{ref}/checklist",
            Audience = Audience.Developer,
            Response = typeof(Checklist),
            Statuses = [200, 401, 403, 404, ProtocolTooOld],
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
                ["obligationId", "attachment", "condition", "because", "outcome", "diagnosis",
                 "transitions", "inapplicable"],
            [typeof(AttachmentTransition)] = ["to", "at", "because"],
            [typeof(GateList)] = ["gates"],

            // The member-identity exchange. Pinned like every other wire type:
            // a member redeems across a process boundary, so a renamed property
            // is a bootstrap that stops working with no compiler to say so.
            [typeof(MemberCredentialRequest)] = ["protocolVersion"],
            [typeof(MemberCredentialMinted)] = ["nonce", "expiresAt"],
            [typeof(MemberCredentialRedemption)] = ["nonce"],
            [typeof(MemberCredentialIssued)] =
                ["runnerId", "runnerToken", "labels", "expiresAt"],
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
            [typeof(TakeoverClaimed)] = ["generation", "heldUntil", "renewWithinSeconds"],
            [typeof(TakeoverHeld)] = ["by", "since", "heldUntil"],
            [typeof(TakeoverRenewalRequest)] = ["generation"],
            [typeof(TakeoverRenewed)] = ["generation", "heldUntil"],
            [typeof(TakeoverReturnRequest)] = ["generation", "outcome", "note"],
            [typeof(TakeoverReturn)] = ["flightId", "outcome", "note"],
            [typeof(TakeSeed)] =
                ["revision", "flightNumber", "flightId", "measurements", "account", "accountState",
                 "accountBytes", "accountAbsence", "transcript", "transcriptState",
                 "transcriptAbsence", "priorHuman"],
            [typeof(TakeMeasurements)] =
                ["filesEdited", "filesReadNotEdited", "searches", "errors", "undeclaredMovesUsed",
                 "attempts", "stopReason", "verdict"],
            [typeof(HumanAccount)] =
                ["by", "statement", "confirmation", "confirmedAt", "wasProposed"],
            [typeof(LoopDigest)] =
                ["loopId", "filesReadNotEdited", "filesEdited", "searches", "errors",
                 "refusedMoves", "attempts", "stopReason"],
            [typeof(DigestError)] = ["source", "detail"],
            [typeof(DestinationLanded)] =
                ["destinationId", "branch", "pullRequestUri", "pullRequestNumber"],
            [typeof(DestinationPushed)] = ["slug", "branch", "commit", "preserved"],
            [typeof(DestinationAdmission)] =
                ["destinationId", "branch", "baseRef", "slug", "reason"],
            [typeof(LeaseLoop)] =
                ["loopId", "executor", "moves", "wallClockSeconds", "onExhaustion", "resumesFrom"],
            [typeof(LoopOutcome)] =
                ["loopId", "outcome", "reason", "executor", "attempts", "durationMs", "movesUsed"],
            [typeof(ArtifactReference)] = ["locator", "sha256", "bytes", "mediaType", "scope"],
            [typeof(ContextBinding)] = ["scope", "constitution"],
            [typeof(Obligation)] = ["id", "check", "when", "rule", "approver", "provenance", "evidence"],
            [typeof(LoopBudget)] = ["wallClock", "attempts"],
            [typeof(Loop)] =
                ["id", "executor", "discharges", "moves", "budget", "onExhaustion"],
            [typeof(Destination)] = ["id", "kind", "requires", "preserveUnadmitted"],
            [typeof(Envelope)] =
                ["context", "obligations", "loops", "destinations", "environment", "repository",
                 "accepts", "produces"],
            [typeof(EnvelopeState)] = ["version", "envelope", "updatedAt", "updatedBy"],
            [typeof(EnvelopeApplied)] = ["version", "appliedAt", "changed", "widens", "flight", "awaiting"],
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
            [typeof(RunnerRegistrationRequest)] = ["label", "protocolVersion", "reserved"],
            // Empty on purpose: the act is "reserve this to me" and the runner
            // is named by the path, so there is nothing for a body to say.
            [typeof(RunnerReservationRequest)] = [],
            [typeof(RunnerReserved)] = ["runnerId", "reservedTo", "reservedAt"],
            [typeof(RunnerRegistered)] = ["runnerId", "runnerToken", "expiresAt"],
            [typeof(RunnerHeartbeat)] = ["labels"],
            [typeof(HeartbeatAccepted)] = ["nextHeartbeatSeconds"],
            [typeof(LeaseClaimRequest)] = ["runnerId", "labels", "maxWaitSeconds"],
        [typeof(LeaseClaimAccepted)] = ["requestId", "pollAfterSeconds"],
        // `lease` is absent unless `state` is granted, and `waitingOn` names
        // repositories rather than counting them - a number says something is
        // wrong, a name says which credential to register.
        [typeof(LeaseClaimStatus)] = ["state", "waitingOn", "lease"],
            [typeof(LeaseRepoRef)] = ["provider", "slug", "pinnedRef", "baseRef", "continuesFrom"],
            // unresolvedRepos joined at slice six: an empty `credentials` was
            // two different facts - no credential registered, or one not yet
            // known here - and a runner that could not tell them apart fetched
            // anonymously.
            [typeof(LeaseGranted)] =
                ["leaseId", "generation", "flightId", "flightNumber", "repos", "credentials",
                 "unresolvedRepos", "classificationCeiling", "classificationRules", "expiresAt",
                 "renewWithinSeconds", "intentUri", "intentProvider", "intentId", "loop", "feedback"],
            [typeof(LeaseRenewalRequest)] = ["generation"],
            [typeof(LeaseRenewed)] = ["expiresAt", "generation"],
            [typeof(LeaseReleaseRequest)] = ["generation", "disposition", "detail", "credentialFailure"],
            [typeof(LeaseReleased)] = ["flightId", "disposition"],
            [typeof(FlightIntent)] = ["kind", "uri", "text", "provider", "id"],
            [typeof(FlightLaunchRequest)] =
                ["name", "intent", "workKind", "environment", "repository"],
            [typeof(FlightLaunched)] = ["flightId", "flightNumber"],
            [typeof(FlightSummary)] =
                ["flightId", "flightNumber", "name", "intent", "createdAt",
                 "runnerProtocolVersion", "factVocabularyVersion", "constitutionVersion", "envelopeVersion",
                 "attempts",
                 "facts",
                 "requiredLabels", "waiting", "state"],
            [typeof(FlightList)] = ["flights"],
            [typeof(FlightLogEntry)] = ["at", "kind", "detail"],
            [typeof(FlightLog)] = ["flightId", "flightNumber", "entries"],
            [typeof(FlightWithdrawalRequest)] = ["because"],
            [typeof(RunnerSummary)] =
                ["runnerId", "label", "state", "currentFlightId", "currentFlightNumber", "lastHeartbeatAt",
                 "labels"],
            [typeof(RunnerList)] = ["runners"],
            [typeof(ChartEnvironmentRequest)] = ["name", "meaning"],
            [typeof(EnvironmentCharted)] = ["name", "meaning", "disposition", "chartedBy", "chartedAt"],
            [typeof(RegistrationPending)] = ["flight", "awaiting", "widens"],
            [typeof(Reason)] = ["family", "kind", "params"],
            [typeof(EnvironmentChart)] = ["environments"],
            // The strategy document. No member a host, socket or credential
            // could travel in, asserted over the shape as well as declared.
            [typeof(StrategyInventory)] = ["pool", "size", "warm"],
            [typeof(StrategyBounds)] = ["poolMax", "activeHours"],
            [typeof(EnvironmentStrategy)] =
                ["kind", "environment", "inventory", "pullPoint", "image", "bounds"],
            [typeof(EnvironmentStrategyState)] = ["name", "version", "appliedAt", "strategy"],
            [typeof(NamedEnvelopeState)] =
                ["name", "role", "version", "envelope", "narrowing", "updatedAt", "updatedBy"],
            [typeof(NamedEnvelopeList)] = ["documents"],
            [typeof(NamedEnvelopeApply)] = ["envelope", "narrowing"],
            [typeof(StrategyList)] = ["strategies"],
            // The pools surface. Digests, hashes and stamps only, asserted
            // over the shape as well as declared.
            [typeof(PoolAttestation)] =
                ["attestationId", "pool", "action", "actionId", "outcome", "imageDigest",
                 "locks", "provenance", "scopeProbedAt", "measuredAt", "diagnosis"],
            [typeof(PoolAction)] =
                ["actionId", "pool", "action", "image", "strategyVersion", "decidedAt"],
            [typeof(PoolActionList)] = ["actions"],
            [typeof(PoolStatus)] =
                ["pool", "action", "outcome", "imageDigest", "scopeProbedAt", "measuredAt",
                 "diagnosis"],
            [typeof(PoolLedger)] = ["pools"],
            [typeof(DeclareNameRequest)] = ["name", "role", "parent", "subjectBinding"],
            [typeof(TopologyName)] =
                ["name", "role", "parent", "subjectBinding", "declaredBy", "declaredAt"],
            [typeof(EnvelopeTopology)] = ["names"],
            [typeof(RegisterRepositoryRequest)] =
                ["name", "provider", "id", "path", "credential", "ref", "narrowings"],
            [typeof(RepositoryRegistered)] =
                [
                    "name", "provider", "id", "path", "credential", "ref", "narrowings",
                    "registeredBy", "registeredAt",
                ],
            [typeof(RegisteredRepositories)] = ["repositories"],
            [typeof(AdvertisedLabel)] = ["name", "disposition"],
            [typeof(ChecklistItem)] =
                ["requirement", "verification", "satisfier", "whenUnmet", "disposition"],
            [typeof(Checklist)] =
                ["envelopeVersion", "flightNumber", "environment", "repository", "requiredLabels",
                 "items"],
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
                 "moveEnforcement", "movesProbed", "probedAt"],
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
