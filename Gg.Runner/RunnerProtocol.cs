using Gg.Contracts;

namespace Gg.Runner;

/// <summary>What a lease claim came back with.</summary>
public abstract record ClaimResult
{
    /// <summary>A flight to work on.</summary>
    public sealed record Granted(LeaseGranted Lease) : ClaimResult;

    /// <summary>
    /// Nothing to do. The normal answer for an idle fleet, and not an error -
    /// <c>pending</c> at the wire, for the same reason the device poll answers
    /// 202 rather than treating "not yet" as a failure.
    /// </summary>
    public sealed record Nothing : ClaimResult;

    /// <summary>
    /// A flight is ready and what its lease must carry is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The state that had nowhere to be reported.</b> This and
    /// <see cref="Nothing"/> were one 204, so a runner idling because there is
    /// no work and a runner idling because a credential nobody has registered
    /// has not arrived looked identical - from the outside, both are silence.
    /// </para>
    /// <para>
    /// The repositories by name rather than a count: a number says something is
    /// wrong, a name says which credential to register.
    /// </para>
    /// </remarks>
    public sealed record Waiting(IReadOnlyList<string> Repos) : ClaimResult;

    /// <summary>
    /// A person has withheld this machine from claiming.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own answer, never <see cref="Nothing"/>.</b> An idle fleet and a
    /// machine somebody deliberately took out of service are the two silences
    /// <c>waiting</c> was added to separate, and reading this as "nothing ready"
    /// would rebuild that collapse inside the client — one layer below where it
    /// used to live and harder to see.
    /// </para>
    /// <para>
    /// <b>No reason travels with it.</b> The parking reason is a member of the
    /// PARKING, quoted back by the surfaces a person reads about a flight; the
    /// claim wire carries the state alone. A runner does not need to know why it
    /// was withheld in order to stop asking.
    /// </para>
    /// </remarks>
    public sealed record Parked : ClaimResult;

    /// <summary>
    /// The request outlived its window. Terminal.
    /// </summary>
    /// <remarks>
    /// Not an error and not retryable under the same name: a runner that kept
    /// polling an expired request would poll something the control plane has
    /// finished with forever. The answer is a new request.
    /// </remarks>
    public sealed record Expired : ClaimResult;
}

/// <summary>What came back from asking for work.</summary>
/// <remarks>
/// <para>
/// <b>Accepted rather than answered.</b> Whether a flight can be handed over
/// depends on state that arrives asynchronously, so at the moment the request is
/// taken the answer does not exist yet.
/// </para>
/// </remarks>
public abstract record ClaimAcceptance
{
    /// <summary>
    /// The control plane took the request. Ask about it after
    /// <paramref name="PollAfter"/>.
    /// </summary>
    /// <remarks>
    /// <b><paramref name="PollAfter"/> is not advice.</b> The claim used to be a
    /// long poll and the control plane holding the request open WAS the rate
    /// limiter - this runner has no backoff of its own. An interval the runner
    /// invented would either hammer an endpoint that now answers instantly or
    /// idle past work that was ready.
    /// </remarks>
    public sealed record Accepted(string RequestId, TimeSpan PollAfter) : ClaimAcceptance;

    /// <summary>
    /// An older control plane answered the claim inline.
    /// </summary>
    /// <remarks>
    /// Tolerated for the same reason the decisions endpoint's two answers were,
    /// and it is what lets the two repositories land this in either order. When
    /// no control plane answers a claim with a lease, this case is dead and
    /// deleting it is a change with a reason of its own.
    /// </remarks>
    public sealed record Inline(ClaimResult Answer) : ClaimAcceptance;
}

/// <summary>Outcome of a renewal.</summary>
public abstract record RenewResult
{
    /// <summary>Extended. The control plane's new expiry, not one we computed.</summary>
    public sealed record Renewed(DateTimeOffset ExpiresAt) : RenewResult;

    /// <summary>
    /// The generation presented is not the one the control plane holds.
    /// </summary>
    /// <remarks>
    /// This is what a well-behaved runner sees after its lease quietly expired
    /// and somebody else took the flight. It is not a client bug and must not
    /// be retried: the correct response is to stop and claim again, and
    /// emphatically NOT to release - releasing would end the other runner's
    /// flight, which is the exact failure the fence exists to prevent.
    /// </remarks>
    public sealed record Fenced : RenewResult;

    /// <summary>The lease is not there at all.</summary>
    public sealed record Gone : RenewResult;
}

/// <summary>Outcome of a release.</summary>
public abstract record ReleaseResult
{
    public sealed record Released : ReleaseResult;

    /// <summary>Refused by the generation fence. See <see cref="RenewResult.Fenced"/>.</summary>
    public sealed record Fenced : ReleaseResult;

    public sealed record Gone : ReleaseResult;
}

/// <summary>
/// Everything a runner says to the control plane.
/// </summary>
/// <remarks>
/// Deliberately four operations and no more. There is no way to report status
/// here, because status is derived control-plane-side from heartbeat age,
/// lease and current flight - a runner that could say "busy" could say it
/// while wedged, and a wedged runner that looks busy blocks a takeover.
/// </remarks>
public interface IRunnerProtocol
{
    Task<HeartbeatAccepted> HeartbeatAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks for work. Two calls rather than one, deliberately.
    /// </summary>
    /// <remarks>
    /// <paramref name="maxWaitSeconds"/> survives the change and means something
    /// different: it used to be how long the control plane would HOLD the
    /// request open, and it is now how long the request stays alive before it
    /// expires. The runner still says how long it is prepared to wait; the
    /// control plane no longer spends a connection waiting with it.
    /// </remarks>
    Task<ClaimAcceptance> RequestClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        string? flightId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks what became of a request. Reports; never grants.
    /// </summary>
    Task<ClaimResult> ReadClaimAsync(string requestId, CancellationToken cancellationToken = default);

    Task<RenewResult> RenewAsync(string leaseId, int generation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ships a batch of facts about the flight this lease holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Takes <see cref="Facts.FilteredFacts"/> and nothing else, which is the
    /// egress half of the pipeline's ordering: only the filter produces one, so
    /// there is no way to hand this something that has not been through it.
    /// </para>
    /// <para>
    /// Against the lease, with the generation, because the lease is the
    /// authorisation and the fence refuses a runner that no longer holds this
    /// flight.
    /// </para>
    /// </remarks>
    Task<FactBatchAccepted> ShipFactsAsync(
        string leaseId, int generation, Facts.FilteredFacts facts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks whether this flight may push, and whether its work may land.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A separate question because it now has a separate answer.</b> Shipping
    /// facts is accepted rather than answered - the control plane records the
    /// batch and evaluates afterwards - so the decision cannot come back on that
    /// response. Against the lease, which is the same authorisation.
    /// </para>
    /// <para>
    /// <c>Settled</c> false means ask again. It does NOT mean no, and reading it
    /// as no is the failure this route exists to prevent.
    /// </para>
    /// </remarks>
    Task<LandingDecision> ReadAdmissionAsync(
        string leaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the lease back, optionally with a credential diagnosis.
    /// </summary>
    /// <remarks>
    /// The diagnosis is typed and separate from <paramref name="detail"/>
    /// because the control plane records it as a flight-log event of its own
    /// naming the reference. It carries a <see cref="CredentialReference"/>,
    /// which is incapable of holding a secret - so this parameter cannot become
    /// the way one leaves the machine.
    /// </remarks>
    Task<ReleaseResult> ReleaseAsync(
        string leaseId, int generation, string disposition, string? detail = null,
        CredentialResolutionFailure? credentialFailure = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The dispositions a runner may release a lease with.</summary>
/// <remarks>
/// Strings on the wire, constants here, so a typo is a compile error on this
/// side rather than a loud refusal on the other.
/// </remarks>
public static class RunnerDisposition
{
    public const string Completed = "completed";
    public const string Abandoned = "abandoned";
    public const string Failed = "failed";
}
