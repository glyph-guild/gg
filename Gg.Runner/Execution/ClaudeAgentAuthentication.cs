using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// How the claude agent authenticates.
/// </summary>
/// <remarks>
/// <para>
/// <b>A subscription login, never an API key.</b> The credential is the
/// long-lived OAuth token <c>claude setup-token</c> mints, and the variable is
/// the one the binary reads it from. <c>ANTHROPIC_API_KEY</c> is not set, not
/// stored and not stripped by gg - a decision, held by a scan in
/// <c>TheAgentGetsItsTokenTests</c> - because the allowance meter and the
/// usage report both answer only for a subscription.
/// </para>
/// <para>
/// <b>Measured against Claude Code 2.1.272</b> (<c>SetupTokenSpikeTests</c>):
/// the variable takes precedence over <c>~/.claude/.credentials.json</c> even
/// under <c>--setting-sources project</c>, and a dead value is a 401 with a
/// sentence rather than a hang. It is absent from the binary's <c>--help</c>,
/// so a rename upstream would fail silently; the version is named here so the
/// next measurement knows what the last one saw.
/// </para>
/// <para>
/// <b>This class never reads the agent's own credential file.</b>
/// <c>AllowanceMeter</c>'s rule: gg asks the tool that holds the subscription's
/// credential, and <i>"reading another tool's .credentials.json and calling an
/// API with it would be a different thing entirely."</i> The token gg stores
/// came out of <c>setup-token</c>, which is the tool handing it over.
/// </para>
/// </remarks>
public sealed class ClaudeAgentAuthentication : IAuthenticateAnAgent
{
    public string Provider => "claude";

    public string Locator { get; } = CredentialLocator.ForAgent("claude");

    public string TokenVariable => "CLAUDE_CODE_OAUTH_TOKEN";
}
