namespace Gg.Local;

/// <summary>
/// The tool a classifier nominates a work kind through.
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
    /// already-declared move permits every time this platform adds a tool to
    /// its own server - which it has now done three times, so the sentence
    /// above is a description of what happened rather than a precaution.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>
    /// The arguments the tool takes when the server was started for a sweep.
    /// </summary>
    /// <remarks>
    /// <b>Named here for the reason the tool is</b>: the server declares them and
    /// the extractor reads them back out of the transcript, and two spellings is
    /// how a sweep's nominations would silently stop being found.
    /// </remarks>
    public static class Sweep
    {
        /// <summary>The flag <c>gg runner tools</c> is started with for a sweep.</summary>
        public const string Flag = "--sweep";

        /// <summary>What names the item, from the watch's mapping.</summary>
        public const string Subject = "subject";

        /// <summary>Which version of it, from the watch's mapping.</summary>
        public const string Version = "version";

        /// <summary>What names it outside gg, from the watch's mapping.</summary>
        public const string IntentKey = "intent_key";
    }
}
