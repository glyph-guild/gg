using Gg.Contracts;

namespace Gg.Runner.Pools;

/// <summary>
/// What a member is made of: the image, and what it needs to become a runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaced a bare image string, and the reason is the whole slice.</b> A
/// member created from an image alone starts, finds the built-in localhost
/// default, advertises nothing, and can register with nobody. That is why no pool
/// member has ever run a flight, and why the only working example baked a
/// developer's session into an image.
/// </para>
/// <para>
/// <b><see cref="Nonce"/> is single-use and is not a credential.</b>
/// <c>GET /containers/gg-pool-*/json</c> is reachable through the scope proxy, so
/// anything placed here is readable by an inspect for the life of the container.
/// The nonce is worth nothing once the member has started; the member exchanges it
/// for its real credential over its own connection.
/// </para>
/// </remarks>
public sealed record MemberSpec
{
    /// <summary>The digest-pinned image, as the strategy declares it.</summary>
    public required string Image { get; init; }

    /// <summary>Where the member answers to.</summary>
    public required string ControlPlane { get; init; }

    /// <summary>
    /// The single-use nonce it redeems for a credential, or null when none could
    /// be minted.
    /// </summary>
    /// <remarks>
    /// Null is refused rather than created around: a member that cannot become
    /// anybody claims nothing, reports nothing, and is counted as warm forever.
    /// </remarks>
    public string? Nonce { get; init; }
}

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
        string pool, string member, MemberSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Destroy a member and recreate it from the pinned image.</summary>
    Task<PoolObservation> ResetAsync(
        string member, MemberSpec spec, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Mints the single-use nonce a member redeems for its own credential.
    /// </summary>
    /// <remarks>
    /// <b>The resident's own token authorizes it.</b> A member cannot mint, so
    /// this is the pull point's act — which is what makes it possible to warm a
    /// member without a person present, and without a session baked into an
    /// image.
    /// </remarks>
    Task<Gg.Contracts.MemberCredentialMinted?> MintMemberAsync(
        string pool, string member, CancellationToken cancellationToken = default);

    /// <summary>Attest one action's outcome. Idempotent on the attestation id.</summary>
    Task AttestAsync(
        string pool, PoolAttestation attestation, CancellationToken cancellationToken = default);
}
