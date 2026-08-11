using Gg.Contracts;

namespace Gg.Runner;

/// <summary>What a lease claim came back with.</summary>
public abstract record ClaimResult
{
    /// <summary>A flight to work on.</summary>
    public sealed record Granted(LeaseGranted Lease) : ClaimResult;

    /// <summary>
    /// Nothing to do. The normal answer for an idle fleet, and not an error -
    /// 204 at the wire for the same reason the device poll answers 202.
    /// </summary>
    public sealed record Nothing : ClaimResult;
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

    Task<ClaimResult> ClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        CancellationToken cancellationToken = default);

    Task<RenewResult> RenewAsync(string leaseId, int generation, CancellationToken cancellationToken = default);

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
