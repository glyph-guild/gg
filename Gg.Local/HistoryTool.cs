namespace Gg.Local;

/// <summary>
/// The tool a flight reads a repository's history with: <c>git log</c>,
/// <c>show</c>, <c>blame</c> and <c>diff</c>, run by this platform's own server
/// in the repository's directory. Reads only; granted by the <c>history</c> move.
/// </summary>
/// <remarks>
/// <para>
/// <b>The sixth tool on this platform's own server, and its own whole name</b>,
/// on the terms <see cref="LandingProposalTool"/> and
/// <see cref="DocumentProposalTool"/> set: one move granting two tools would be
/// the prefix grant <see cref="WorkItemProposalTool"/> refuses, arriving by
/// another route.
/// </para>
/// <para>
/// <b>Why a tool and not a Bash rule.</b> Measured on Claude Code 2.1 under
/// <c>--permission-mode default</c>: an allow rule <c>Bash(git log:*)</c>
/// admits <c>git log</c> typed at the repository's own root and nothing else.
/// <c>git -C repo log</c> and <c>git --git-dir=repo/.git log</c> fall outside
/// it, and every <c>cd</c> form is stopped by the client's own guard against
/// changing directory before a version-control command. A flight's working
/// directory is the tree ABOVE its repositories, so a Bash grant would have
/// been a grant of nothing - which is what GG-1081 found, eleven refusals in a
/// row, when its instructions asked it to read the commits that closed the
/// story its ticket named.
/// </para>
/// <para>
/// <b>The tree reaches the server through the environment</b>, as a composing
/// session's intent path does: the server is a function of what it is handed,
/// and a server that went looking for a tree would be one an agent could point
/// somewhere else.
/// </para>
/// </remarks>
public static class HistoryTool
{
    /// <summary>The platform's own server, shared with the proposals.</summary>
    public const string Server = NominationTool.Server;

    /// <summary>The bare name, as <c>tools/list</c> declares it.</summary>
    public const string Name = "read_history";

    /// <summary>How it arrives in the stream, and what the launch grants.</summary>
    public const string Qualified = $"mcp__{Server}__{Name}";

    /// <summary>
    /// The variable the launch sets to the flight's tree, which the server
    /// resolves every repository under.
    /// </summary>
    public const string TreeVariable = "GG_TREE_ROOT";

    /// <summary>The argument naming which repository in the tree, by its directory.</summary>
    public const string RepositoryArgument = "repository";

    /// <summary>The argument carrying which of the four subcommands.</summary>
    public const string CommandArgument = "command";

    /// <summary>The argument carrying what follows the subcommand.</summary>
    public const string ArgumentsArgument = "arguments";

    /// <summary>The four reads, and the whole of what the tool runs.</summary>
    public static readonly IReadOnlyList<string> Commands = ["log", "show", "blame", "diff"];

    /// <summary>
    /// The flags that make a read write, refused by name: <c>--output</c> puts
    /// the result in a file, <c>--ext-diff</c> runs whatever the repository's
    /// configuration names.
    /// </summary>
    public static readonly IReadOnlyList<string> RefusedFlags = ["--output", "--ext-diff"];

    /// <summary>
    /// How much of git's output is handed back before it is cut, in characters.
    /// An agent told its output was cut narrows the query.
    /// </summary>
    public const int MaxOutput = 64 * 1024;
}
