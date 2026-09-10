namespace Gg.Local;

/// <summary>
/// The tool a drafting agent hands an envelope document back with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree</b> — the launch
/// puts the qualified name in <c>--allowedTools</c>, the server declares the
/// bare name in its <c>tools/list</c>, and whatever reads the result looks for
/// the qualified one. This is the fourth tool in this area to need that
/// agreement, and every previous disagreement of its kind was silent in the
/// worst direction: an agent granted a tool that does not exist, or gg waiting
/// for a call the agent was never offered.
/// </para>
/// <para>
/// <b>The same server as <see cref="IntentTool"/>.</b> An MCP tool arrives as
/// <c>mcp__&lt;server&gt;__&lt;tool&gt;</c>, so a second server key would be a
/// second prefix to keep straight — and the one thing a second key definitely
/// does is shadow the first if an operator ever configures a reader under it.
/// One server, granted individually.
/// </para>
/// <para>
/// <b>Why a tool rather than letting the agent write the file.</b> Not to stop
/// it: the console's launch does not pass <c>--strict-mcp-config</c> and
/// <c>--allowedTools</c> does not remove a built-in <c>Write</c>, so an agent
/// that edits the tree directly produces exactly what a person editing the tree
/// produces — a change <c>diff</c> shows and <c>apply</c> gates, with nothing
/// bypassed. What the tool buys is different and worth having: the document is
/// <b>validated before it lands</b>, so a refusal teaches the schema; the path
/// is computed from a name rather than chosen; and the <c>based-on:</c>
/// precondition survives, which a hand-written file would silently clear.
/// </para>
/// </remarks>
public static class DocumentTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "submit_document";

    /// <summary>
    /// The tool as the agent sees it, and as a launch grants it.
    /// </summary>
    /// <remarks>
    /// Granted whole. A grant of the <c>mcp__gg</c> prefix would widen what an
    /// already-declared move permits every time this platform adds another tool
    /// to its own server.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>Which document: one of the declarable roles.</summary>
    /// <remarks>
    /// <b>Asked for rather than inferred from the text.</b> A narrowing and a
    /// work kind are different types with different parsers, and reading which
    /// one a document claims to be would accept a complete envelope anywhere —
    /// ADR-0018 § 7's fourth refusal, the one that is easy to miss because the
    /// document is not malformed, only misplaced.
    /// </remarks>
    public const string RoleArgument = "role";

    /// <summary>Which name in the topology this document is for.</summary>
    public const string NameArgument = "name";

    /// <summary>The document itself, as YAML.</summary>
    public const string DocumentArgument = "document";

    /// <summary>
    /// The environment variable naming the working copy a draft lands in.
    /// </summary>
    /// <remarks>
    /// <b>Read by the verb that starts the server, never by the server.</b> The
    /// server is a function of what it is handed — that is what makes it safe
    /// to run as a child of a process the threat model treats as compromised —
    /// so the root is an argument to it rather than something it goes and
    /// finds.
    /// </remarks>
    public const string RootVariable = "GG_DOCUMENT_ROOT";
}
