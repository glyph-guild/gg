namespace Gg.Local;

/// <summary>
/// The tool a flight hands an airspace document back through.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree.</b> The launch puts
/// the qualified name in <c>--allowedTools</c>, the server declares the bare name
/// in its <c>tools/list</c>, and the extractor looks for the qualified name in
/// the transcript. Three spellings of one name is how one of them stops agreeing,
/// and every failure of that kind here has been silent: an agent granted a tool
/// that does not exist, or a value it declared that is never found.
/// </para>
/// <para>
/// <b>Not <see cref="DocumentTool"/>, though they do a similar-sounding thing.</b>
/// That one writes a draft into a local airspace working copy, where a person
/// reads the change and submits it. A runner has no working copy - the estate
/// holds documents rather than repositories - so this one writes nothing and
/// returns a receipt, and what carries the document is the fact the runner ships
/// afterwards.
/// </para>
/// <para>
/// <b>The same server as <see cref="NominationTool"/>.</b> A second server key
/// would be a second prefix to keep straight, and the one thing a second key
/// definitely does is shadow the first. One server, granted individually.
/// </para>
/// <para>
/// <b>It applies nothing and says so in its description.</b> An agent handed a
/// tool called <c>propose_document</c> while looking at a governance tree will
/// assume calling it puts the document in force. It does not: the proposal is
/// held, and a person opens the gate the tenant's own envelope declares.
/// </para>
/// </remarks>
public static class DocumentProposalTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "propose_document";

    /// <summary>The name an agent calls and the extractor looks for.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>Which role the document claims to be.</summary>
    public const string RoleArgument = "role";

    /// <summary>The declared name in the tenant's topology it is for.</summary>
    public const string NameArgument = "name";

    /// <summary>The document itself, as its author wrote it.</summary>
    public const string DocumentArgument = "document";
}
