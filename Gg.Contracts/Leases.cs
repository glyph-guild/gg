namespace Gg.Contracts;

/// <summary>
/// A runner saying it is alive, and refreshing what it can be given.
/// </summary>
/// <remarks>
/// <para>
/// Liveness ONLY. There is deliberately no field here by which a runner
/// reports its status: status is derived control-plane-side from heartbeat
/// age, lease and current flight. A runner that could report "busy" could also
/// report it while dead, and a dead runner that looks busy blocks a takeover
/// forever.
/// </para>
/// <para>
/// A heartbeat is NOT a renewal. This says the process is alive; it says
/// nothing about any lease it holds. Collapsing the two is the obvious
/// simplification and it breaks takeover - a wedged runner that still
/// heartbeats would keep a flight it is no longer working on.
/// </para>
/// </remarks>
[PinnedId("25cb421c-b25e-4d28-a867-402276ae94d5")]
public sealed record RunnerHeartbeat
{
    /// <summary>
    /// What this runner can be given work for. Sent on every heartbeat rather
    /// than only at registration, so a runner that gains a capability does not
    /// have to re-register to be offered work that needs it.
    /// </summary>
    public required IReadOnlyList<string> Labels { get; init; }
}

/// <summary>How long the control plane expects to wait before worrying.</summary>
/// <remarks>
/// Server-supplied, like the device-flow poll interval: the client respects a
/// cadence rather than inventing one, so the staleness threshold and the
/// heartbeat rate cannot drift apart across a deploy.
/// </remarks>
[PinnedId("c750d3c6-c2ef-415e-be27-7becd441dda9")]
public sealed record HeartbeatAccepted
{
    /// <summary>Seconds the runner should wait before the next heartbeat.</summary>
    public required int NextHeartbeatSeconds { get; init; }
}

/// <summary>Asks for a flight to work on, and waits.</summary>
[PinnedId("61a59211-522b-40dd-9d4e-eefface5ec69")]
public sealed record LeaseClaimRequest
{
    /// <summary>The runner asking. Its credential proves it; this names it.</summary>
    public required string RunnerId { get; init; }

    /// <summary>
    /// What this runner can do. A flight requiring a label this list does not
    /// contain is not offered - matched by containment, so a label added later
    /// widens what a runner is eligible for without any schema change.
    /// </summary>
    public required IReadOnlyList<string> Labels { get; init; }

    /// <summary>
    /// How long the control plane may hold the request open before answering
    /// "nothing". A long poll, so an idle runner is neither busy-looping nor
    /// waiting on a fixed interval it did not choose.
    /// </summary>
    public required int MaxWaitSeconds { get; init; }

    /// <summary>
    /// One flight this runner is asking for by name, or null for whatever the
    /// fleet has ready.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A person at a terminal is a runner that wants a specific flight</b> —
    /// the one they just opened. The ordinary claim asks the queue what is
    /// available, and there is no way to express "that one" in it, so a
    /// hand-flight without this would be a race whose failure is not an error:
    /// the person waits at a prompt while their flight is cloned on somebody
    /// else's laptop.
    /// </para>
    /// <para>
    /// <b>Null is every claim ever made before this member existed</b>, and it
    /// is absent on the wire rather than null so the fleet's request body is
    /// byte-for-byte what it was. The two repositories are not upgraded in step.
    /// </para>
    /// <para>
    /// <b>It asks; it does not decide.</b> Whether this runner may have this
    /// flight is settled on the other side, and there are SIX checks rather than
    /// the two that are easy to remember: an unresolved gate and a runner-less
    /// work kind live in a trigger on the ready table, and a readiness floor,
    /// label containment, the flight's own direction, the runner's reservation
    /// and a live-lease exclusion live in the claim's own pick. A grant that
    /// writes no ready row fires neither the trigger nor the pick, so all six
    /// have to be re-asserted where the grant happens.
    /// </para>
    /// </remarks>
    public string? FlightId { get; init; }
}

/// <summary>One repository the leased flight is pinned to.</summary>
/// <remarks>
/// The wire copy of the control plane's own repo reference. It exists
/// separately because this package must not depend on the control plane, and
/// because the two are free to diverge: this one is a protocol change to
/// alter, the other is not.
/// </remarks>
[PinnedId("98955325-4183-466e-b659-b5eac45ace88")]
public sealed record LeaseRepoRef
{
    /// <summary>Provider key. Which providers exist is the control plane's business.</summary>
    public required string Provider { get; init; }

    /// <summary>Provider-scoped identifier, in whatever form that provider uses.</summary>
    public required string Slug { get; init; }

    /// <summary>The exact ref this flight is pinned to.</summary>
    public required string PinnedRef { get; init; }

    /// <summary>
    /// What the change is measured FROM, when that is known.
    /// </summary>
    /// <remarks>
    /// Null when nothing established a base - a bare repository url, or a
    /// pull request whose base branch nobody has looked up yet. A manifest
    /// computed against a guessed default branch would be a false statement
    /// about what a flight examined, so no base means no manifest rather than
    /// a plausible one.
    /// </remarks>
    public string? BaseRef { get; init; }

    /// <summary>
    /// The commit a previous attempt on this flight pushed, when there was one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The runner is told, and does not go looking.</b> A runner that inferred the
    /// previous attempt from whatever branch it found on the remote would be deciding what
    /// work to build on from something anybody with push access can move. Article IX: the
    /// client is not an authority on what this flight has already done.
    /// </para>
    /// <para>
    /// Null on a first attempt, and null after grounding and flying again - starting over
    /// is a different act, and it starts from the pinned base with nothing to continue
    /// from. Present, it is both where the working tree starts and what the change is
    /// measured from, which is what keeps a manifest's label and its base from disagreeing.
    /// </para>
    /// </remarks>
    public string? ContinuesFrom { get; init; }
}

/// <summary>
/// The loop a lease authorises, flattened from the envelope.
/// </summary>
/// <remarks>
/// <para>
/// Flattened rather than carrying the envelope itself. The runner needs four
/// things to run a loop and must not be handed the document that decides what
/// it is allowed to do - policy arriving at a runner is Article IX's failure
/// wearing a convenience.
/// </para>
/// <para>
/// <b>Moves are recorded, not enforced.</b> They are sent so the runner can
/// report which of them it used; nothing here bounds the executor, and the
/// capability declaration says why - the adapter cannot restrict its own tool
/// list. Recording what a flight used is what makes bounding it designable.
/// </para>
/// </remarks>
/// <summary>
/// What a person said when they sent this work back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Advice, never authority, and that is structural rather than conventional.</b> This
/// record carries a sentence and an attribution and nothing else - there is no field
/// here that could widen a move, a scope, an obligation, a destination or a budget,
/// because a reason able to change any of those would be unreviewable configuration
/// arriving one sentence at a time.
/// </para>
/// <para>
/// <b>In the context once.</b> A lease carries the rejection that sent THIS attempt
/// back, and never an earlier one. Reasons that accumulated across attempts would be an
/// envelope by accretion, made of rejection comments - which is the failure the declared
/// context model exists to prevent.
/// </para>
/// <para>
/// <b>Marked as a person's words</b>, so an executor renders it as something somebody
/// said rather than as instruction from the platform. The same rule the agent's own
/// account follows travelling the other way.
/// </para>
/// </remarks>
[PinnedId("0b6e83d1-97f4-42ca-a5c8-31d7e0b96f52")]
public sealed record LeaseFeedback
{
    /// <summary>Which obligation was rejected.</summary>
    public required string ObligationId { get; init; }

    /// <summary>Who rejected it.</summary>
    public required string DecidedBy { get; init; }

    /// <summary>Their words, stripped and bounded before they got here.</summary>
    public required string Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

[PinnedId("2d740fb8-6e51-49a3-8c07-b1f9e35a4d26")]
public sealed record LeaseLoop
{
    /// <summary>Which loop, by its id in the envelope.</summary>
    public required string LoopId { get; init; }

    /// <summary>Which rung runs it.</summary>
    public required string Executor { get; init; }

    /// <summary>What it may do. Recorded, never enforced.</summary>
    public required IReadOnlyList<string> Moves { get; init; }

    /// <summary>
    /// Wall clock, in seconds. The one budget this slice enforces.
    /// </summary>
    /// <remarks>
    /// Seconds rather than the envelope's text form, because the runner needs
    /// a number and the grammar that reads "30m" lives on the other side.
    /// Attempts and tokens are NOT enforced - the executor reports both, but
    /// stopping on them needs a decision about what a partial attempt means
    /// and this slice does not make one.
    /// </remarks>
    public required int WallClockSeconds { get; init; }

    /// <summary>What happens when the budget runs out.</summary>
    public required string OnExhaustion { get; init; }

    /// <summary>
    /// What the last attempt tried and ruled out, for a loop resuming its work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Handed over rather than fetched, and the audience is why.</b>
    /// <c>GET /v1/flights/{ref}/seed</c> answers a developer session on purpose: a
    /// runner able to call it could read what every flight in the tenant tried and
    /// ruled out, from a credential meant only to let it hold one lease. So a
    /// resuming loop is given its context on the lease it already has.
    /// </para>
    /// <para>
    /// <b>The rendered seed, not the model.</b> It reaches an executor as declared
    /// context beside the intent - text an agent reads - and rendering it here would
    /// be a second implementation of a document the contract already renders once.
    /// </para>
    /// <para>
    /// <b>Absent on a first attempt, which is the ordinary case.</b> There is nothing
    /// to resume from, and a member that had to be present would make every lease
    /// carry an empty document - so "no prior attempt" and "a prior attempt that
    /// measured nothing" would read the same, which is this project's most repeated
    /// defect.
    /// </para>
    /// <para>
    /// <b>Nothing in it is customer content.</b> It is composed from facts that
    /// already crossed, and <c>TakeSeed</c> carries no absolute path and no machine
    /// name by construction - the transcript is named and never included.
    /// </para>
    /// </remarks>
    public string? ResumesFrom { get; init; }

    /// <summary>
    /// The operator's standing instructions, composed and rendered, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rendered text, not the model</b>, for the reason
    /// <see cref="ResumesFrom"/> gives one member up: rendering at the consumer
    /// would be a second implementation of a document the contract already
    /// renders once, and two wordings for one policy is two policies.
    /// </para>
    /// <para>
    /// <b>Absent when the envelope declares none, which is every envelope
    /// today.</b> Not an empty string: "no standing instructions" and
    /// "instructions that say nothing" must not read the same, which is the
    /// same distinction this member's neighbour draws for a first attempt.
    /// </para>
    /// <para>
    /// <b>Reviewed text, unlike everything else that reaches an agent.</b> A
    /// flight's intent, a rejection reason and a prior attempt's account all
    /// arrive unreviewed; this came through a gated, versioned,
    /// direction-checked document, and the rendering says so in words the agent
    /// can act on.
    /// </para>
    /// </remarks>
    public string? Instructions { get; init; }
}

/// <summary>
/// A flight, granted to one runner for a bounded time.
/// </summary>
/// <remarks>
/// <para>
/// This is the boundary contract in one object: everything a runner is
/// permitted to know about the work, and nothing else. There is no credential
/// here and no repository content - the runner resolves its own secrets
/// locally at a later step, and the control plane never holds them.
/// </para>
/// <para>
/// <see cref="Generation"/> is the fence. It increments on every grant for a
/// flight, and a runner must present it to renew or release. Without it, a
/// runner whose lease quietly expired - paused, partitioned, swapped out -
/// could release the lease that replaced it and terminate another runner's
/// flight. That is silent data loss caused by a client behaving perfectly.
/// </para>
/// </remarks>
[PinnedId("ccbff371-57c5-441e-b204-0292956307b3")]
public sealed record LeaseGranted
{
    public required string LeaseId { get; init; }

    /// <summary>
    /// Increments on every grant for this flight. Present it to renew or
    /// release; a stale generation is refused and the refusal is recorded.
    /// </summary>
    public required int Generation { get; init; }

    public required string FlightId { get; init; }

    /// <summary>Human-facing flight number, already rendered.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>Repositories this flight operates on, pinned to exact refs.</summary>
    public required IReadOnlyList<LeaseRepoRef> Repos { get; init; }

    /// <summary>
    /// What the flight is for, as an addressable reference.
    /// </summary>
    /// <remarks>
    /// <b>The reference, never the body.</b> An issue's text is customer
    /// content and does not cross: the control plane holds the URI, and the
    /// runner resolves what it points at with the customer's own credential,
    /// in the customer's environment. That is the same shape as credentials
    /// and it also avoids a real cost - reading an issue needs a permission
    /// this platform's app does not have, and ADDING an app permission makes
    /// every existing installation re-approve.
    /// </remarks>
    public string? IntentUri { get; init; }

    /// <summary>
    /// The tracker a work-item intent names, or null when the flight is not
    /// about one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Beside <see cref="IntentUri"/> rather than instead of it.</b> A ticket
    /// is a provider and an id; a link is a link. Collapsing them would mean
    /// composing a URL out of these two fields, and this repository names no
    /// forge and could not - which is the same derivation slice nine retired
    /// when it stopped lifting a provider out of a URI's host.
    /// </para>
    /// <para>
    /// <b>Without this a ticket flight is leased and never worked.</b> The
    /// runner invokes on a non-empty intent, and a ticket had none to offer, so
    /// every one of them materialized a tree and returned - no refusal, no fact,
    /// nothing to read.
    /// </para>
    /// </remarks>
    public string? IntentProvider { get; init; }

    /// <summary>
    /// The work item's identifier in that tracker, or null.
    /// </summary>
    /// <remarks>
    /// <b>Declared and never parsed</b>, which is contract 0.86.0's own rule for
    /// this field one layer up. Nothing reads structure out of it: an id that
    /// looks like a path or a URL is still just an id, and deriving meaning from
    /// a string somebody typed breaks on a vanity host and on a work item moved
    /// between projects.
    /// </remarks>
    public string? IntentId { get; init; }

    /// <summary>
    /// The words somebody typed, when the flight is about those rather than
    /// about something addressable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without this a text flight is leased and never worked</b> - the
    /// sentence <see cref="IntentProvider"/> carries, one intent kind over. A
    /// runner invokes on an intent that names work, and typed words had nothing
    /// to offer: <c>gg fly "&lt;text&gt;"</c> cloned a tree, renewed twice and
    /// reported landed with no agent invoked and nothing said. Measured on a
    /// live tenant: every free-text flight it had ever flown recorded no
    /// <c>loop.outcome</c>, including three smoke tests whose own text says they
    /// verify the agent loop end to end.
    /// </para>
    /// <para>
    /// <b>This is not the body <see cref="IntentUri"/> refuses to carry.</b>
    /// That rule is about an ISSUE's text - customer content in a tracker,
    /// which the runner resolves with the customer's own credential because
    /// reading it needs a permission this platform does not have. These words
    /// are the operator's own, typed at their terminal; the control plane
    /// already holds them and already prints them in <c>gg flights</c>, so
    /// handing them back to that operator's runner exposes nothing that has not
    /// already crossed. A rule about what we are not entitled to read does not
    /// reach a sentence somebody typed at us.
    /// </para>
    /// </remarks>
    public string? IntentText { get; init; }

    /// <summary>
    /// What the agent that nominated this flight told whoever picks it up, or
    /// null when nobody nominated it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Beside <see cref="Feedback"/> because it is the same kind of thing.</b>
    /// A person's rejection and a classifier's handover are both prose from
    /// outside this platform that a prompt must carry and must not obey: advice,
    /// never authority. Scope, moves and budget come from the envelope, and a
    /// note asking for more fails at the manifest check like anything else.
    /// </para>
    /// <para>
    /// <b>One hop, and the control plane is what enforces it.</b> A flight
    /// opened from a nomination carries that nomination's note; a flight opened
    /// from THAT flight does not. Nothing here can tell how many times a note
    /// has been forwarded, which is why the rule lives where flights are opened
    /// - recorded here as the thing that side owes, so a reader of this member
    /// does not assume the type is holding the line.
    /// </para>
    /// </remarks>
    public string? NominationNote { get; init; }

    /// <summary>
    /// What a classifier on this flight may nominate, already rendered, or null
    /// when its envelope opens no flights.
    /// </summary>
    /// <remarks>
    /// <b>Rendered here for the reason <see cref="LeaseLoop.Instructions"/> is.</b>
    /// The control plane composes the envelope and renders the permitted sets
    /// from the destination that bounds admission; a runner that built its own
    /// list would be a second statement of what admission accepts, and the two
    /// would drift the first time somebody edited the envelope. Null on every
    /// flight that is not a classify flight, which is nearly all of them.
    /// </remarks>
    public string? Menu { get; init; }

    /// <summary>
    /// The loop this flight runs, when its envelope declares one.
    /// </summary>
    /// <remarks>
    /// Null when nothing governs the flight. A runner with no loop does what
    /// it did before an executor existed: materialize, extract, ship. That is
    /// a real state rather than a degraded one - most flights in slice one had
    /// no envelope at all.
    /// </remarks>
    public LeaseLoop? Loop { get; init; }

    /// <summary>
    /// Why the last attempt was sent back, when it was.
    /// </summary>
    /// <remarks>
    /// Null on a first attempt and on any attempt that follows an approval. Present
    /// exactly once per rejection: the attempt it sent back gets it, and the one after
    /// that does not.
    /// </remarks>
    public LeaseFeedback? Feedback { get; init; }

    /// <summary>
    /// Which credentials the runner must resolve, and where they are.
    /// </summary>
    /// <remarks>
    /// References, never secrets - the control plane holds none to send. This
    /// is the boundary in one field: the runner is told WHICH credential to
    /// use, resolves it on its own machine, and the value never crosses in
    /// either direction. The type is incapable of carrying one, asserted over
    /// its shape rather than intended.
    /// </remarks>
    public required IReadOnlyList<CredentialReference> Credentials { get; init; }

    /// <summary>
    /// Repositories on this flight that the control plane could not name a
    /// credential reference for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because an empty list is two different facts.</b> A repository may
    /// have no credential registered, or its reference may not have reached the
    /// control plane's read model yet. Absence alone cannot tell them apart, and
    /// a runner that treated both as "this one needs none" fetched ANONYMOUSLY:
    /// a public repository worked, a private one failed later on git's own words
    /// with nothing pointing at the cause.
    /// </para>
    /// <para>
    /// Naming them converts that into a refusal a person can act on, before
    /// anything is materialized — which is what the push path has always done
    /// and the clone path never did.
    /// </para>
    /// <para>
    /// Empty on a flight whose every repository resolved, which is the ordinary
    /// case.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> This
    /// member is init-only, so System.Text.Json cannot set it after
    /// construction and builds the object through a creator that assigns every
    /// member from an argument array - this one as null when the key is absent,
    /// overwriting the <c>= []</c>. Non-nullable is a promise to every caller
    /// that it can be dereferenced; <c>AbsentCollectionsSurviveTheWireTests</c>
    /// holds it for the whole contract.
    /// </remarks>
    public IReadOnlyList<string> UnresolvedRepos
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>
    /// The tenant's classification ceiling. The runner needs it before it
    /// gathers anything, because it bounds what may ever leave the machine.
    /// </summary>
    public required string ClassificationCeiling { get; init; }

    /// <summary>
    /// The tenant's rules, so the runner can decide what is above that ceiling.
    /// </summary>
    /// <remarks>
    /// Sent because the runner filters BEFORE anything leaves its network, and
    /// it cannot do that without knowing what the tenant considers sensitive.
    /// The control plane keeps its own copy and re-derives from that: what
    /// arrives here is what the runner was told, and a runner that ignored it
    /// is exactly what re-validation is for.
    /// </remarks>
    public required IReadOnlyList<ClassificationRule> ClassificationRules { get; init; }

    /// <summary>
    /// When this lease stops being valid. Authoritative on its own: a lease
    /// past this instant is expired whether or not any timer fired.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Seconds after which the runner should renew, well before expiry.</summary>
    public required int RenewWithinSeconds { get; init; }
}

/// <summary>Extends one specific lease.</summary>
[PinnedId("d53202e8-c4d1-44a0-a6bb-a35f00b4598c")]
public sealed record LeaseRenewalRequest
{
    /// <summary>The generation the runner believes it holds.</summary>
    public required int Generation { get; init; }
}

/// <summary>The lease now runs until this instant.</summary>
[PinnedId("8cb1daaa-4176-4297-91fa-619bcf026c6e")]
public sealed record LeaseRenewed
{
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Unchanged by a renewal. A renewal extends; it does not re-grant.</summary>
    public required int Generation { get; init; }
}

/// <summary>Gives a lease back, with what became of the work.</summary>
[PinnedId("44ac641c-9ca8-4961-90c9-26d0dd5717ec")]
public sealed record LeaseReleaseRequest
{
    /// <summary>The generation the runner believes it holds.</summary>
    public required int Generation { get; init; }

    /// <summary>
    /// What became of the flight: <c>"completed"</c>, <c>"abandoned"</c> or
    /// <c>"failed"</c>.
    /// </summary>
    /// <remarks>
    /// A string rather than an enum, deliberately. An enum on a pinned wire
    /// contract serializes as a number by default, so inserting a value
    /// silently renumbers every value after it. An unrecognised disposition is
    /// refused loudly rather than mapped to a default - Article XI.
    /// </remarks>
    public required string Disposition { get; init; }

    /// <summary>Optional human-readable detail, recorded on the flight log.</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Set when the flight ended because a credential could not be resolved.
    /// </summary>
    /// <remarks>
    /// Typed rather than folded into <see cref="Detail"/>, because the control
    /// plane records it as a flight-log event of its own naming the reference,
    /// and parsing that back out of prose is how a diagnosis becomes a guess.
    /// The reference it carries cannot hold a secret; nor can this.
    /// </remarks>
    public CredentialResolutionFailure? CredentialFailure { get; init; }
}

/// <summary>The lease is given back and the flight has moved on.</summary>
[PinnedId("7416c64a-5a6c-49ec-88d5-661f12f6856d")]
public sealed record LeaseReleased
{
    public required string FlightId { get; init; }

    /// <summary>The disposition the control plane recorded.</summary>
    public required string Disposition { get; init; }
}

/// <summary>The states a lease request may be in.</summary>
/// <remarks>
/// <para>
/// <b>Two of these were the same answer before.</b> Claiming was a long poll
/// that returned 204 for "nothing came", and an idle fleet and a fleet blocked
/// on something it is waiting for looked identical. They are different facts and
/// a runner should be able to tell them apart — one is the normal state of the
/// system and the other is a thing somebody may need to fix.
/// </para>
/// <para>
/// Derived control-plane-side, like <see cref="RunnerStates"/>: a runner reports
/// none of these about itself, it asks.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class LeaseClaimStates
{
    /// <summary>Nothing is ready for this runner. Not an error, and the common case.</summary>
    public const string Pending = "pending";

    /// <summary>
    /// A flight is ready and what its lease must carry is not.
    /// </summary>
    /// <remarks>
    /// The control plane learns which credential references a flight needs from
    /// what identity announced, and that arrives after the fact. Waiting says
    /// so, rather than handing over a lease with a gap in it.
    /// </remarks>
    public const string Waiting = "waiting";

    /// <summary>The lease is attached. Terminal.</summary>
    public const string Granted = "granted";

    /// <summary>The request outlived its window. Terminal, and recorded rather than forgotten.</summary>
    public const string Expired = "expired";

    /// <summary>
    /// A person has withheld this runner from claiming. Not an error, and not
    /// idle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own value rather than a narrowed-away flight.</b> A parked runner
    /// filtered out inside the matcher would answer <see cref="Pending"/> — the
    /// same answer an idle fleet gets — and collapsing those two silences is the
    /// defect <see cref="Waiting"/> was added to fix.
    /// </para>
    /// <para>
    /// <b>Not offline.</b> A parked runner keeps beating; parking withholds
    /// claims and does not take the machine away. One that stopped beating IS
    /// offline, and takeover must still reclaim its flight.
    /// </para>
    /// <para>
    /// <b>Not terminal.</b> The request is answered and the runner asks again
    /// later, exactly as it does for <see cref="Pending"/>.
    /// </para>
    /// </remarks>
    public const string Parked = "parked";

    public static IReadOnlyList<string> All { get; } =
        [Pending, Waiting, Granted, Expired, Parked];
}

/// <summary>
/// A lease request the control plane has taken, and how often to ask about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Accepted rather than answered.</b> Whether a flight can be handed over
/// depends on state that arrives asynchronously, so at the moment the request is
/// taken the answer does not exist yet. The same reason
/// <c>FactBatchAccepted</c> carries refusals and nothing else.
/// </para>
/// <para>
/// <b><see cref="PollAfterSeconds"/> is server-supplied and load-bearing.</b>
/// The claim used to be a long poll, and the control plane holding the request
/// open WAS the rate limiter — the runner has no backoff of its own. A cadence
/// the runner invented would either hammer this endpoint or idle past work that
/// was ready. <c>DeviceAuthorizationStarted.PollIntervalSeconds</c> is the same
/// arrangement for the same reason.
/// </para>
/// </remarks>
[PinnedId("233442c9-06b4-43ef-90eb-12d64ed140b6")]
public sealed record LeaseClaimAccepted
{
    /// <summary>What to ask about. Not a lease, and not a promise of one.</summary>
    public required string RequestId { get; init; }

    /// <summary>Seconds to wait before asking. Respected, never invented.</summary>
    public required int PollAfterSeconds { get; init; }
}

/// <summary>
/// What became of a lease request.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Lease"/> is absent unless <see cref="State"/> is
/// <c>granted</c>.</b> That is the arrangement <c>LandingDecision.Settled</c>
/// uses and for the identical reason: absence has to mean one thing. A runner
/// reading an absent lease as "nothing yet" and an absent lease as "never" would
/// be reading two facts off one silence.
/// </para>
/// <para>
/// <b><see cref="WaitingOn"/> names repositories rather than counting them.</b>
/// A number tells a person that something is wrong; a name tells them which
/// credential to go and register.
/// </para>
/// </remarks>
[PinnedId("8da2b319-f94d-43b7-a3d0-c9ba4abfdc69")]
public sealed record LeaseClaimStatus
{
    /// <summary>One of <see cref="LeaseClaimStates"/>.</summary>
    public required string State { get; init; }

    /// <summary>
    /// Repositories whose credential reference has not arrived, when the state
    /// is <c>waiting</c>. Empty otherwise.
    /// </summary>
    /// <remarks>
    /// <b>The accessor delivers that, and the initializer does not.</b> This
    /// member is init-only, so System.Text.Json cannot set it after
    /// construction and builds the object through a creator that assigns every
    /// member from an argument array - this one as null when the key is absent,
    /// overwriting the <c>= []</c>. Non-nullable is a promise to every caller
    /// that it can be dereferenced; <c>AbsentCollectionsSurviveTheWireTests</c>
    /// holds it for the whole contract.
    /// </remarks>
    public IReadOnlyList<string> WaitingOn
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>The lease, once there is one.</summary>
    public LeaseGranted? Lease { get; init; }
}

/// <summary>
/// Ask for a runner to be withheld from claiming.
/// </summary>
/// <remarks>
/// <b>A person's declaration about a machine, and the reason is load-bearing.</b>
/// A runner taking nothing for a fortnight with no reason attached is the failure
/// mode this is most likely to produce, so the sentence that says why travels
/// with the state - and is what a withheld flight quotes back.
/// </remarks>
[PinnedId("b1f4c8ae-3c60-4a0f-9d0c-6a4b8e2f5d71")]
public sealed record RunnerParkRequest
{
    /// <summary>Why, in a person's words. Optional, and worth writing.</summary>
    public string? Reason { get; init; }
}

/// <summary>A runner's parking, as it stands after the call.</summary>
[PinnedId("e0a7d9b2-5f31-4c8e-b6a2-1d3f7c05e948")]
public sealed record RunnerParked
{
    /// <summary>The runner this is about.</summary>
    public required string RunnerId { get; init; }

    /// <summary>When it was parked, or null when it is not.</summary>
    public DateTimeOffset? ParkedAt { get; init; }

    /// <summary>Who parked it, as a display — never an id.</summary>
    public string? ParkedBy { get; init; }

    /// <summary>Why, when somebody said.</summary>
    public string? Reason { get; init; }
}
