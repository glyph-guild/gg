using Gg.Contracts;

namespace Gg.Runner.Pools;

/// <summary>What a pool adapter can do, and which provider it is.</summary>
public sealed record PoolCapabilities
{
    /// <summary>A key like "docker", never a host.</summary>
    public required string Provider { get; init; }
}

/// <summary>One member of a managed pool, by name.</summary>
public sealed record PoolMember
{
    public required string Name { get; init; }
}

/// <summary>What one action observed, in the attestation's own words.</summary>
public sealed record PoolObservation
{
    /// <summary>One of <see cref="PoolOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>The image the member converged to, from the daemon's own inspect.</summary>
    public string? ImageDigest { get; init; }

    /// <summary>One of <see cref="EnvironmentProvenance"/>, when the action can say.</summary>
    public string? Provenance { get; init; }

    /// <summary>What went wrong, when the outcome is failed.</summary>
    public string? Diagnosis { get; init; }
}

/// <summary>
/// What probing the scope bound found: a reach outside the pool prefix was
/// refused, or it was not.
/// </summary>
/// <remarks>
/// <b>Held is true only when the reach was REFUSED.</b> An allowed reach or
/// an error is a broken bound with a diagnosis — unknown is not false, slice
/// eleven's rule applied to infrastructure.
/// </remarks>
public sealed record ScopeProbe
{
    public required bool Held { get; init; }

    public required DateTimeOffset ProbedAt { get; init; }

    public string? Diagnosis { get; init; }
}

/// <summary>
/// The pool adapter port: the ONLY thing in the product that touches a
/// container runtime, and it lives here — on the customer's host, behind the
/// proxy that scopes it — never in the control plane.
/// </summary>
public interface IPoolAdapter
{
    PoolCapabilities Capabilities { get; }

    /// <summary>The pool's members, by the pool's own name prefix.</summary>
    Task<IReadOnlyList<PoolMember>> ListAsync(string pool, CancellationToken cancellationToken = default);

    /// <summary>Inspect one member. Changes nothing.</summary>
    Task<PoolObservation> VerifyAsync(PoolMember member, CancellationToken cancellationToken = default);

    /// <summary>Make a member current and running — create, start, or converge.</summary>
    Task<PoolObservation> RefreshAsync(
        string pool, string member, string image, CancellationToken cancellationToken = default);

    /// <summary>Destroy a member and recreate it from the pinned image.</summary>
    Task<PoolObservation> ResetAsync(
        string member, string image, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reach for a container OUTSIDE the pool prefix and report whether
    /// something that is not us refused it.
    /// </summary>
    Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Where the pool endpoint comes from: the environment, or nowhere.
/// </summary>
/// <remarks>
/// <b><c>GG_POOL_ENDPOINT</c> points at the scope-enforcing proxy, never the
/// raw socket.</b> The endpoint is deployment configuration on the host the
/// runner resides on — it never crosses the wire and no policy document can
/// hold it, which is the containment the strategy's closed key set enforces
/// from the other side. Null is a real answer: a runner with no endpoint is
/// not a resident, and <c>gg runner maintain</c> refuses loudly rather than
/// guessing a socket path.
/// </remarks>
public sealed record PoolConfiguration
{
    public required string Endpoint { get; init; }

    public static PoolConfiguration? FromEnvironment() =>
        Environment.GetEnvironmentVariable("GG_POOL_ENDPOINT") is { Length: > 0 } endpoint
            ? new PoolConfiguration { Endpoint = endpoint }
            : null;
}

/// <summary>
/// The pools half of the runner protocol: pull decided actions, attest
/// outcomes. Deliberately not part of <see cref="IRunnerProtocol"/> — that
/// interface is four lease operations and no more, and a routine action has
/// no lease.
/// </summary>
public interface IPoolProtocol
{
    /// <summary>The decided actions for this pool. Serving is the claim, control-plane-side.</summary>
    Task<PoolActionList> PullActionsAsync(string pool, CancellationToken cancellationToken = default);

    /// <summary>Attest one action's outcome. Idempotent on the attestation id.</summary>
    Task AttestAsync(
        string pool, PoolAttestation attestation, CancellationToken cancellationToken = default);
}
