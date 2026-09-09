namespace Gg.Local;

/// <summary>
/// The tool a triage flight proposes changes to work items through.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree.</b> The launch
/// puts the qualified name in <c>--allowedTools</c>, the server declares the
/// bare name in its <c>tools/list</c>, and the extractor looks for the
/// qualified name in the transcript. Three spellings of one name is how one of
/// them stops agreeing — and the failure would be silent: the agent granted a
/// tool that does not exist, or the value it declared never found.
/// </para>
/// <para>
/// <b>The same server as the nomination, and that is the interesting part.</b>
/// The platform's server hosts four tools and this is the second that a move
/// grants - so "grant the server" and "grant the tool" are no longer the same
/// set, and the distinction <c>ClaudeCodeExecutor.Tool</c> has always drawn
/// stops being theory. It refused the <c>mcp__gg</c> prefix on the argument
/// that a prefix would retroactively grant whatever came next to every envelope
/// already in force. This is what came next.
/// </para>
/// <para>
/// <b>Calling it acts on nothing.</b> It records what was proposed and answers
/// that it did. The admission is the control plane's and the write is the
/// runner's, so no tracker credential is anywhere near the process that serves
/// this — which is what lets it accept the whole gamut of operations without
/// the shape of its arguments becoming a permission system nobody declared.
/// </para>
/// </remarks>
public static class WorkItemProposalTool
{
    /// <summary>The platform's own server, shared with the nomination.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The bare name, as <c>tools/list</c> declares it.</summary>
    public const string Name = "propose_work_item";

    /// <summary>
    /// How it arrives in the stream, and what the launch grants.
    /// </summary>
    /// <remarks>
    /// An MCP tool arrives as <c>mcp__&lt;server&gt;__&lt;tool&gt;</c>, so the
    /// server key is half the identity — and an operator who configured a
    /// reader under the same key would shadow it.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";
}
