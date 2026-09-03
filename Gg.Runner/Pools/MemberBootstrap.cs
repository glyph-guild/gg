namespace Gg.Runner.Pools;

/// <summary>
/// How a member becomes somebody: the variable it finds, and the exchange it
/// makes.
/// </summary>
/// <remarks>
/// <b>One name, read by two sides.</b> The adapter writes it into a member's
/// environment and the runner reads it on start, and a variable spelled
/// differently in those two places is a member that silently never registers -
/// exactly the class of failure this slice exists to end.
/// </remarks>
public static class MemberBootstrap
{
    /// <summary>Where a member finds its single-use nonce.</summary>
    public const string NonceVariable = "GG_MEMBER_NONCE";

    /// <summary>
    /// Where an operator says how a MEMBER reaches the control plane, when that
    /// differs from how this host does.
    /// </summary>
    /// <remarks>
    /// A name is the whole documentation of an environment variable, and this
    /// one is about what a member can reach rather than what the host prefers.
    /// </remarks>
    public const string ReachableAsVariable = "GG_MEMBER_CONTROL_PLANE";

    /// <summary>
    /// The address to put in a member's environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The host's own address unless something says otherwise</b>, because on
    /// every real deployment they are the same one public URL - and asking an
    /// operator to state it twice invites them to state it wrong.
    /// </para>
    /// <para>
    /// <b>They differ on a developer's machine, and silently.</b> A host reaches
    /// the control plane at <c>127.0.0.1:5199</c>; a container reaches the same
    /// service at <c>host.docker.internal:5199</c>. Hand a container
    /// <c>127.0.0.1</c> and it points at ITSELF: the member starts, fails to
    /// redeem, and dies, while the pool counts a container that exists.
    /// </para>
    /// </remarks>
    public static string ControlPlaneFor(string hostAddress, string? reachableAs) =>
        reachableAs is { Length: > 0 } members ? members : hostAddress;
}
