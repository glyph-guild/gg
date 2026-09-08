namespace Gg.Runner;

/// <summary>
/// Where this runner asks what it looks like from outside, if anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Configuration rather than a literal, and the rule that says so is a good
/// one.</b> gg forbids a source file naming an identity provider — it talks to
/// the control plane and to nothing else — and the well-known public STUN
/// servers are run by exactly those companies. A default in source would point
/// every runner in every deployment at somebody's free service that nobody
/// chose, on a path carrying the shape of a customer's private network.
/// </para>
/// <para>
/// <b>Empty is a real answer and the default.</b> Host candidates alone work
/// between machines that can already reach each other, which is a runner and a
/// console on one network — and step 0 measured that STUN alone was enough to
/// cross a home NAT and a hosted machine's stateful firewall with no rule added
/// anywhere, so a deployment that wants that reach configures one. TURN is
/// S34.Q-04 and still open; nothing here pretends to answer it.
/// </para>
/// </remarks>
public static class StunConfiguration
{
    /// <summary>The variable a deployment sets, comma separated.</summary>
    public const string Variable = "GG_STUN_SERVERS";

    /// <summary>What this machine was told to use. Empty when it was told nothing.</summary>
    public static IReadOnlyList<string> FromEnvironment(string? declared = null)
    {
        var raw = declared ?? Environment.GetEnvironmentVariable(Variable);

        if (raw is not { Length: > 0 })
        {
            return [];
        }

        return
        [
            .. raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                // A SCHEME IS REQUIRED rather than assumed. `stun:` and `stuns:`
                // are what a peer connection understands, and quietly prefixing
                // one would turn a typo into a server nobody meant.
                .Where(u => u.StartsWith("stun:", StringComparison.OrdinalIgnoreCase)
                         || u.StartsWith("stuns:", StringComparison.OrdinalIgnoreCase)),
        ];
    }
}
