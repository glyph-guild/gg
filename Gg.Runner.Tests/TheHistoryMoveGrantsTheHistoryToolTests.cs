using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The <c>history</c> move grants one tool, this platform's <c>read_history</c>,
/// by its whole name - and the launch tells that tool which tree it serves.
/// </summary>
/// <remarks>
/// <para>
/// <b>The sixth tool on this platform's own server, on the terms the fourth
/// and fifth set.</b> Its own move and its own whole name: one move granting
/// two tools would be the prefix grant <c>ClaudeCodeExecutor.Tool</c> refuses,
/// arriving by another route.
/// </para>
/// <para>
/// <b>Why not Bash.</b> Measured on Claude Code 2.1 with
/// <c>--permission-mode default</c>: an allow rule <c>Bash(git log:*)</c>
/// admits <c>git log</c> typed at the repository's root and nothing else -
/// <c>git -C repo log</c> and <c>git --git-dir=repo/.git log</c> fall outside
/// the rule, and every <c>cd</c> form is stopped by the client's own guard
/// against changing directory before a version-control command. A flight's
/// working directory is the tree ABOVE its repositories, so a Bash grant would
/// be a grant of nothing. A served tool takes the repository by name and runs
/// git where it lives.
/// </para>
/// <para>
/// <b>The tree reaches the server through the environment</b>, the way the
/// console hands its server an intent path: the server is a function of what
/// it is handed, and a tool server that went looking for a tree would be one
/// an agent could point somewhere else.
/// </para>
/// </remarks>
public class TheHistoryMoveGrantsTheHistoryToolTests
{
    private static readonly Gg.Local.SelfInvocation Self =
        Gg.Local.SelfInvocation.For("/bin/gg", null)!;

    private static ExecutorRequest Request(IReadOnlyList<string> moves) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "investigate",
        Moves = moves,
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(20),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    private static System.Diagnostics.ProcessStartInfo Launch(IReadOnlyList<string> moves) =>
        AttendedExecutor.StartInfoFor(Request(moves), [], null, Self);

    [Test]
    public async Task The_move_grants_the_history_tool_by_its_whole_name()
    {
        var granted = ClaudeCodeExecutor.ToolFor(LoopMoves.History);

        await Assert.That(granted).IsEqualTo(Gg.Local.HistoryTool.Qualified)
            .Because("one move, one tool, named whole. Granted: " + granted);
        await Assert.That(granted).StartsWith("mcp__")
            .Because("it is served by this platform rather than built into the agent - "
                   + "a Bash rule cannot reach a repository below the working directory.");
    }

    [Test]
    public async Task Declaring_it_grants_it_and_nothing_beside_it()
    {
        var arguments = Launch([LoopMoves.Read, LoopMoves.History]).ArgumentList;

        await Assert.That(arguments).Contains(Gg.Local.HistoryTool.Qualified)
            .Because("history was declared, so the launch configures this platform's server "
                   + "and grants the tool. Passed: " + string.Join(" ", arguments));
        await Assert.That(arguments.Contains(Gg.Local.NominationTool.Qualified)).IsFalse()
            .Because("reading history is not nominating, and the neighbouring tool on the "
                   + "same server is not granted by it.");
        await Assert.That(arguments.Contains(Gg.Local.LandingProposalTool.Qualified)).IsFalse()
            .Because("nor is proposing a landing.");
    }

    [Test]
    public async Task An_envelope_that_does_not_declare_it_cannot_call_it()
    {
        var arguments = Launch([LoopMoves.Read, LoopMoves.ProposeLanding]).ArgumentList;

        await Assert.That(arguments.Contains(Gg.Local.HistoryTool.Qualified)).IsFalse()
            .Because("read and propose-landing were the moves; the server is configured for "
                   + "the landing tool and that grants nothing beside it. Passed: "
                   + string.Join(" ", arguments));
    }

    [Test]
    public async Task The_launch_tells_the_server_which_tree_it_serves()
    {
        var launch = Launch([LoopMoves.Read, LoopMoves.History]);

        await Assert.That(launch.Environment.TryGetValue(Gg.Local.HistoryTool.TreeVariable, out var tree))
            .IsTrue()
            .Because("the tool resolves a repository under the flight's tree and nowhere else, "
                   + "and the tree reaches it through the environment the server inherits.");
        await Assert.That(tree).IsEqualTo("/work/flight");
    }

    [Test]
    public async Task It_is_record_only_so_a_flight_may_declare_it()
    {
        await Assert.That(MoveKinds.Of(LoopMoves.History)).IsEqualTo(MoveKinds.RecordOnly);
    }
}
