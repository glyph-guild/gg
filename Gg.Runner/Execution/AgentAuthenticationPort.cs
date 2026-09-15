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
/// <b>Every member is branched on, for <c>ExecutorCapabilities</c>'s
/// reason.</b> Seven declared capabilities were deleted there because nothing
/// consulted them: <i>"a declaration nothing consults is documentation with a
/// test on it."</i> The three names are read by the executor at launch; the
/// probe and the recogniser are read by the runner, which holds on the first
/// and demotes itself on the second. They arrived with that runner, not before.
/// </para>
/// <para>
/// <b>The probe measures SOURCE, not validity.</b> <c>claude auth status</c>
/// answers "has a credential" for a token it never checked, so
/// <c>Authenticated</c> means "can be started", and a dead credential is
/// learned from the run that fails - which is what <see cref="NeedsLogin"/>
/// recognises, and why both halves live on one adapter.
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

    /// <summary>
    /// Whether the agent can start, measured now, with the token gg holds
    /// placed the way a launch would place it.
    /// </summary>
    /// <remarks>
    /// Never throws for a probe that could not run: unknown is not false, and
    /// it is not ready either - an unmeasured standing is <c>needs-login</c>
    /// with a diagnosis saying so.
    /// </remarks>
    Task<AgentStanding> ProbeAsync(string? token, CancellationToken cancellationToken);

    /// <summary>
    /// Whether what the agent said, when a run failed, is that it could not
    /// authenticate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sentences are the agent's own, measured on a live member and against
    /// the real binary; a runner that did not recognise them would read a dead
    /// token as a broken bound and exit.
    /// </para>
    /// <para>
    /// <b>The agent's words, never a person's.</b> A rejection reason a person
    /// wrote is passed through and never interpreted - RejectionContextTests
    /// holds that over this project - and this reads something else entirely:
    /// the executor's account of why its child stopped.
    /// </para>
    /// </remarks>
    bool NeedsLogin(string? said);
}

/// <summary>What a probe measured about the agent's credential.</summary>
/// <param name="Authenticated">Whether the agent can be started.</param>
/// <param name="Source">One of <c>AgentCredentialSources</c>.</param>
/// <param name="Diagnosis">
/// The adapter's own sentence, for a person - never what the agent printed,
/// which may carry an account's email or organisation.
/// </param>
/// <param name="MeasuredAt">When, so the far side can see how old it is.</param>
public sealed record AgentStanding(
    bool Authenticated, string Source, string Diagnosis, DateTimeOffset MeasuredAt);
