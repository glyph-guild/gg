namespace Gg.Local;

/// <summary>
/// The tool a drafting agent renders the airspace with, through the verb a
/// person would type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree</b> — the launch
/// grants the qualified name, the server declares the bare one, and
/// <see cref="AirspaceContextTool"/> names it to an agent standing in an empty
/// tree.
/// </para>
/// <para>
/// <b>WHY IT GRANTS NOTHING NEW.</b> A drafting session is attended and
/// launched with <c>--allowedTools</c>, which auto-approves rather than
/// restricts — <c>--tools</c> is the restricting flag and the launch does not
/// pass it. So the agent has a shell and <c>gg</c> is on the path: it can run
/// the verb today by guessing at a command line. What the tool adds is
/// <see cref="DocumentTool"/>'s answer one verb over: a described channel, the
/// right working copy, and gg's own refusals relayed as themselves rather than
/// as whatever a guessed invocation printed.
/// </para>
/// <para>
/// <b>A CHILD, NOT A CLIENT.</b> The tool server holds no HTTP client, no
/// session store and no control-plane client, and that is load-bearing: it
/// runs as a child of a process the threat model treats as compromised, so
/// what an injected agent can reach through it is the whole question. Pulling
/// re-execs gg under a verb, which keeps the credential and the network in the
/// process whose job they are — the boundary <c>SelfInvocation.Under</c> exists
/// to cross.
/// </para>
/// <para>
/// <b>Safe to grant rather than prompt, because of a refusal.</b>
/// <c>AirspacePullAsync</c> raises <c>DirtyWorkingCopyException</c> as its
/// first statement, before it reaches the network, so a pull cannot bury
/// uncommitted work — including drafts written moments earlier in the same
/// session. That refusal has to be in the description before it is ever hit:
/// an agent that reads a refusal as a failure retries it or works around it.
/// </para>
/// </remarks>
public static class AirspacePullTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "pull_airspace";

    /// <summary>The tool as the agent sees it, and as a launch grants it.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>
    /// The variable that tells the child which working copy to render into.
    /// </summary>
    /// <remarks>
    /// <b>Forced, never inferred.</b> <c>gg airspace pull</c> resolves its root
    /// through the configuration and falls back to the current directory — and
    /// the tool server's directory is whatever the MCP client started it in.
    /// Left to the fallback, the verb succeeds and writes a tree nowhere
    /// anybody is looking, which is the silent-write hazard this setting was
    /// added to close.
    /// </remarks>
    public const string RootVariable = "GG_AIRSPACE";
}
