namespace Gg.Local;

/// <summary>
/// The tool a drafting agent asks what an envelope is with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree</b> — the launch
/// puts the qualified name in <c>--allowedTools</c>, the server declares the
/// bare name in its <c>tools/list</c>, and <c>submit_document</c>'s own
/// description points at it. That third reader is new and is the whole design:
/// the tool list reaches the model before its first token, so a pointer sitting
/// in the description an agent reads when it decides to write is the one place
/// an instruction cannot be missed.
/// </para>
/// <para>
/// <b>PULLED RATHER THAN PUSHED.</b> An MCP server may declare
/// <c>instructions</c> at <c>initialize</c>, and they reach the model with no
/// prompt and no call — but they are the SERVER's, and this one also serves
/// nomination, decision and triage flights that want no envelope doctrine at
/// all. A tool costs nothing until somebody calls it, the call is in the
/// transcript so "did it read the rules" is observable rather than assumed,
/// and being a call means the answer can be computed.
/// </para>
/// <para>
/// <b>And computing it is the point.</b> A static essay could say what a
/// narrowing is. Only a call can say what THIS tenant's airspace holds, what
/// the documents are called, and show one of them — which is what an agent
/// actually lacks, because "in the form the working copy already uses" points
/// at nothing for a role the tenant has no document for, and having no
/// narrowings is the ordinary case for somebody about to write their first.
/// </para>
/// <para>
/// <b>It reads and answers; it changes nothing.</b> The working copy is the
/// only thing it touches, and it is the same directory the server already
/// writes documents into — strictly less reach than the tool beside it.
/// </para>
/// </remarks>
public static class AirspaceContextTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "describe_airspace";

    /// <summary>
    /// The tool as the agent sees it, and as a launch grants it.
    /// </summary>
    /// <remarks>
    /// Granted whole, for <see cref="DocumentTool.Qualified"/>'s reason: a
    /// grant of the <c>mcp__gg</c> prefix would widen what an already-declared
    /// move permits every time this platform adds another tool.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>
    /// The working copy it describes.
    /// </summary>
    /// <remarks>
    /// The same one a document lands in, and deliberately the same variable:
    /// two names for one directory would let a session describe one tree and
    /// write into another.
    /// </remarks>
    public const string RootVariable = DocumentTool.RootVariable;

    /// <summary>
    /// The rules in force, rendered, for the session to show an agent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>HANDED, NOT FETCHED, and that is the whole reason it is a variable
    /// rather than a call.</b> The composed envelope comes off the control
    /// plane, and the tool server holds no client, no session store and no
    /// credential — which is what makes it safe to run as a child of a process
    /// the threat model treats as compromised. The console has already read it
    /// before the child starts, for its own panel; this hands over what it
    /// already has.
    /// </para>
    /// <para>
    /// <b>Rendered by the console, because rendering is the contract's job and
    /// there is one renderer.</b> <c>EnvelopeText.RenderComposed</c> is it, and
    /// a second wording here would be a second thing to keep in agreement with
    /// the pane a person is reading beside the agent.
    /// </para>
    /// </remarks>
    public const string EnvelopeVariable = "GG_AIRSPACE_ENVELOPE";
}
