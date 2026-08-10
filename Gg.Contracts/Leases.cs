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
    /// The tenant's classification ceiling. The runner needs it before it
    /// gathers anything, because it bounds what may ever leave the machine.
    /// </summary>
    public required string ClassificationCeiling { get; init; }

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
}

/// <summary>The lease is given back and the flight has moved on.</summary>
[PinnedId("7416c64a-5a6c-49ec-88d5-661f12f6856d")]
public sealed record LeaseReleased
{
    public required string FlightId { get; init; }

    /// <summary>The disposition the control plane recorded.</summary>
    public required string Disposition { get; init; }
}
