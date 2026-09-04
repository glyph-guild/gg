namespace Gg.Runner.Execution;

/// <summary>
/// The platform's own tool server, and the one tool on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree.</b> The launch
/// puts the qualified name in <c>--allowedTools</c>, the server declares the
/// bare name in its <c>tools/list</c>, and the extractor looks for the
/// qualified name in the transcript. Three spellings of one name is how one of
/// them stops agreeing - and the failure would be silent: the agent would be
/// granted a tool that does not exist, or the value it declared would never be
/// found.
/// </para>
/// <para>
/// <b>The server key is what makes the prefix.</b> An MCP tool arrives in the
/// stream as <c>mcp__&lt;server&gt;__&lt;tool&gt;</c>, so the key is not
/// cosmetic: it is half the identity of every tool this platform ever hosts,
/// and an operator who configured a reader under the same key would shadow it.
/// </para>
/// </remarks>
public static class NominationTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = "gg";

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "nominate_work_kind";

    /// <summary>
    /// The tool as the agent sees it, and as the transcript records it.
    /// </summary>
    /// <remarks>
    /// Granted whole. A grant of the <c>mcp__gg</c> prefix would widen what an
    /// already-declared move permits every time this platform adds a second
    /// tool to its own server.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";
}
