namespace Gg.Runner.Execution;

/// <summary>
/// How one agent authenticates: which provider it is, where gg keeps its
/// credential, and where the agent reads it from.
/// </summary>
/// <remarks>
/// <para>
/// <b>An adapter per agent, so the second tool is a class and not a
/// search.</b> Before this, every reader of <c>GG_EXECUTOR_BINARY</c> assumed
/// the agent was claude and the executor placed no credential at all - the
/// agent authenticated with whatever login its OS user happened to have, which
/// inside a pool member is none. What an executor needs to know to change that
/// is three names, and they belong to the agent rather than to the executor.
/// </para>
/// <para>
/// <b>Three members and no fourth, for <c>ExecutorCapabilities</c>'s
/// reason.</b> Seven declared capabilities were deleted there because nothing
/// branched on them: <i>"a declaration nothing consults is documentation with a
/// test on it."</i> Each member here is read by the executor at launch. A probe
/// of whether the credential works, and a recogniser for the sentence an agent
/// prints when it does not, arrive with the runner that holds on them - not
/// before.
/// </para>
/// <para>
/// <b>The token is placed in the environment and nowhere else.</b> Not an
/// argument - <i>"which every <c>ps</c> on the host can read"</i> - and not a
/// tool server's <c>env</c> block, which is that server's credential. The
/// variable is the agent's own, undocumented in its <c>--help</c> and measured
/// to take precedence over the machine's login (<c>SetupTokenSpikeTests</c>).
/// </para>
/// </remarks>
public interface IAuthenticateAnAgent
{
    /// <summary>The adapter key, as <c>GG_EXECUTOR_BINARY</c> declares it.</summary>
    string Provider { get; }

    /// <summary>
    /// Where this agent's credential is kept on the machine:
    /// <c>Gg.Contracts.CredentialLocator.ForAgent(Provider)</c>.
    /// </summary>
    /// <remarks>
    /// Derived through the contract and never spelled here, so the console
    /// that sends the token and the executor that reads it cannot disagree.
    /// </remarks>
    string Locator { get; }

    /// <summary>The environment variable the agent reads its credential from.</summary>
    string TokenVariable { get; }
}
