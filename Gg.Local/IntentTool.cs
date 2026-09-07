namespace Gg.Local;

/// <summary>
/// The tool a composing agent calls to hand its intent back to gg.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named in one place because three things have to agree</b> — the launch
/// puts the qualified name in <c>--allowedTools</c>, the server declares the
/// bare name in its <c>tools/list</c>, and whatever reads the result looks for
/// the qualified one. This is the THIRD tool in this feature area to need that
/// agreement, and every previous disagreement of its kind was silent in the
/// worst direction: an agent granted a tool that does not exist, or gg waiting
/// for a call the agent was never offered.
/// </para>
/// <para>
/// <b>The same server as <see cref="NominationTool"/>.</b> An MCP tool arrives
/// as <c>mcp__&lt;server&gt;__&lt;tool&gt;</c>, so a second server key would be
/// a second prefix to keep straight — and the one thing a second key definitely
/// does is shadow the first if an operator ever configures a reader under it.
/// Three tools, one server, granted individually.
/// </para>
/// <para>
/// <b>Why a tool at all, rather than reading what the agent said.</b> Opening a
/// flight is a governance decision: it takes a number, it is attributed, and it
/// is a record somebody has to explain. <c>instructions-in-the-envelope</c>
/// rule 7 forbids parsing prose into one, and the argument is already written on
/// the server this joins — a closing summary that happens to mention an intent
/// is a sentence somebody could have written about anything, while a tool call
/// is a thing the agent chose to make.
/// </para>
/// </remarks>
public static class IntentTool
{
    /// <summary>The server key, and therefore the tool-name prefix.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The tool, as the server declares it.</summary>
    public const string Name = "submit_intent";

    /// <summary>
    /// The tool as the agent sees it, and as a launch grants it.
    /// </summary>
    /// <remarks>
    /// Granted whole. A grant of the <c>mcp__gg</c> prefix would widen what an
    /// already-declared move permits every time this platform adds another tool
    /// to its own server.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>The one argument, and it is the whole intent.</summary>
    /// <remarks>
    /// <b>Text only, settled 2026-09-07.</b> A repository, a work kind and an
    /// environment are all things a composing agent could reasonably have
    /// concluded — and <see cref="NominationTool"/> carries exactly that
    /// argument one feature over, where the answer was a bounded menu rather
    /// than free choice. Adding them here would be a second bounding scheme
    /// beside the destination's may-select; if one is ever wanted it belongs
    /// there.
    /// </remarks>
    public const string IntentArgument = "intent";

    /// <summary>
    /// The environment variable naming where a composing session's intent goes.
    /// </summary>
    /// <remarks>
    /// <b>Read by the verb that starts the server, never by the server.</b> The
    /// server is a function of what it is handed — that is what makes it safe to
    /// run as a child of a process the threat model treats as compromised — so
    /// the path is an argument to it rather than something it goes and finds.
    /// </remarks>
    public const string PathVariable = "GG_INTENT_PATH";
}
