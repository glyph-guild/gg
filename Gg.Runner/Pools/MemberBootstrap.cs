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
}
