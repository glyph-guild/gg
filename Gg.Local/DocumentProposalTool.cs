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

    /// <summary>What was learned, as its author wrote it.</summary>
    /// <remarks>
    /// <b>The only argument, and that is the fix rather than a tidy-up.</b> It took a
    /// <c>role</c> and a <c>name</c> until GG-330 showed what asking for them costs:
    /// the kind being rehearsed is named NOWHERE - not on <c>gg fly</c>, not on the
    /// lease, not in the prompt - so the flight guessed three roles and named its own
    /// kind thirteen times, correctly, from the only information it had. An agent
    /// cannot misname what it is never asked to name, and advice is now keyed by what
    /// it was learned against rather than by who will read it, so there is nothing
    /// left to name: the runner says the document and the entry says its subject.
    /// </remarks>
    public const string DocumentArgument = "document";
}
