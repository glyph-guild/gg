namespace Gg.Contracts;

/// <summary>
/// Registers a runner with the control plane.
/// </summary>
/// <remarks>
/// Registration only. Heartbeat, lease acquisition and the ready queue are a
/// later step and are deliberately absent from this contract.
/// </remarks>
[PinnedId("34fd5791-5fce-4c7b-ad8e-31e4d35461f6")]
public sealed record RunnerRegistrationRequest
{
    /// <summary>Human-readable label for this runner.</summary>
    public required string Label { get; init; }

    /// <summary>Protocol revision this runner speaks.</summary>
    public required int ProtocolVersion { get; init; }

    /// <summary>
    /// Reserve this runner to whoever is registering it. Defaults to false,
    /// which is what every runner does today: take the tenant's public work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because labels only ever say yes.</b> A flight's requirements are
    /// matched by containment against what a runner advertises, which is
    /// monotone — adding a label can only add work. An untargeted flight
    /// requires nothing, and nothing is contained by every label set, so there
    /// is no value a runner can advertise that makes it take LESS.
    /// </para>
    /// <para>
    /// <b>Not a label, and it needs no charted environment.</b> A reserved
    /// runner may advertise nothing and still be reachable by its holder's
    /// flights. Spelling this as a label would make reserving a laptop require
    /// charting an environment, and would inherit containment's one direction.
    /// </para>
    /// <para>
    /// <b>A boolean rather than a principal, deliberately.</b> This says
    /// <i>reserve it to me</i>, and the control plane is what knows who "me" is.
    /// Reserving somebody ELSE's runner routes work at a person who did not ask
    /// for it — a different act, with a different approver — and a member here
    /// that could name a principal would make it reachable through a request the
    /// runner itself composes.
    /// </para>
    /// <para>
    /// <b>At registration, so there is no unreserved window.</b> A runner
    /// reserved by a later call takes public work until that call lands, which
    /// on a busy tenant is every flight in the queue.
    /// </para>
    /// </remarks>
    public bool Reserved { get; init; }
}

/// <summary>
/// The registered runner and the credential it will authenticate with.
/// </summary>
/// <remarks>
/// <para>
/// The token ATTRIBUTES to the principal that registered the runner, so every
/// action remains traceable to a person. It is AUTHORIZED for the runner
/// protocol surface and nothing else: it cannot register another runner, and
/// it cannot reach a developer or tenant surface.
/// </para>
/// <para>
/// It expires on its own schedule, independent of the session that minted it,
/// so closing a console never drops a running runner.
/// </para>
/// </remarks>
[PinnedId("2c5b6a1e-7d84-4f39-b0c6-5e9a8d3f1742")]
public sealed record RunnerRegistered
{
    public required string RunnerId { get; init; }

    /// <summary>The runner's bearer credential. Shown once.</summary>
    public required string RunnerToken { get; init; }

    /// <summary>When the runner credential expires, independent of any session.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Ask for a runner to be reserved to the caller.
/// </summary>
/// <remarks>
/// <b>Empty on purpose, and it is not a placeholder.</b> The act is "reserve
/// this to me"; the control plane knows who me is, and the runner is named by
/// the path. A member here could only ever be a principal, and reserving
/// somebody else's runner routes work at a person who did not ask for it — a
/// different act, with a different approver, and deliberately not this version.
/// </remarks>
[PinnedId("bcb45c07-9f5f-4c0e-9c6f-0ffd94bc9de2")]
public sealed record RunnerReservationRequest;

/// <summary>
/// A runner's reservation, as it stands after the call.
/// </summary>
/// <remarks>
/// <b>A DISPLAY, never an id.</b> <c>TakeoverHeld.By</c> settled this one: "a
/// principal's display, never a name typed in". "Somebody else has this" sends a
/// person nowhere, and a uuid sends them somewhere worse.
/// </remarks>
[PinnedId("2fd4a0a1-6a02-45e5-8e1c-9a0dbd3fb9e6")]
public sealed record RunnerReserved
{
    /// <summary>The runner this is about.</summary>
    public required string RunnerId { get; init; }

    /// <summary>Who holds it, as a display — or null when nobody does.</summary>
    public string? ReservedTo { get; init; }

    /// <summary>When it was reserved, or null.</summary>
    public DateTimeOffset? ReservedAt { get; init; }
}
