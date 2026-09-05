using Gg.Local;
namespace Gg.Runner.Execution;

/// <summary>
/// The tool an agent calls to say it needs a decision it is not allowed to
/// make.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its NAME lands before its server does, and that is not an accident of
/// ordering.</b> The outcome is decided from the stream, so the runner has to
/// recognise the call before anything serves it - and a name is what the
/// recognition is keyed on. The server, and the paragraph in the prompt that
/// tells an agent the tool exists, are the next step.
/// </para>
/// <para>
/// <b>Same server as the nomination tool, because it is the same server.</b>
/// One channel by which an agent declares a value rather than writing prose
/// about it, with a second tool on it. The third structured declaration should
/// not build a third extractor either.
/// </para>
/// <para>
/// <b>It is not a move, and no envelope may withhold it.</b> A move bounds what
/// an agent may do to a customer's code; asking a question touches nothing. An
/// envelope able to withhold this would be an envelope that makes a stuck agent
/// silent, which is the failure this exists to fix - so unlike
/// <see cref="NominationTool"/> there is no move to declare and nothing to
/// refuse when it is absent.
/// </para>
/// </remarks>
public static class HelpTool
{
    /// <summary>The platform's own server - the one the nomination tool is on.</summary>
    public const string Server = NominationTool.Server;

    public const string Name = "ask_for_decision";

    /// <summary>
    /// The whole name, never the server's prefix.
    /// </summary>
    /// <remarks>
    /// A prefix grant would retroactively grant every tool this platform later
    /// adds to its own server, for every envelope in force, with nothing in the
    /// record marking the day it changed.
    /// </remarks>
    public const string Qualified = $"mcp__{Server}__{Name}";
}
