using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The move that lets a flight propose a change to a work item, and the one
/// tool it grants.
/// </summary>
/// <remarks>
/// <para>
/// <b>A move is how an envelope says what an agent's hands may do</b>, and
/// slice thirty-five's whole posture rests on this one granting a tool that
/// ACTS ON NOTHING. Proposing is not doing: the call records what was proposed,
/// the control plane admits it or refuses, and the runner writes. So the
/// interesting property is not what the tool can express — deliberately,
/// everything — but that nothing can call it without the envelope saying so.
/// </para>
/// <para>
/// <b>The whole qualified name, never the server's prefix.</b>
/// <c>ClaudeCodeExecutor.Tool</c> already says why for <c>propose</c>: a prefix
/// grant would retroactively hand every tool this platform later adds to its
/// own server to every envelope in force, with nothing in the record marking
/// the day it changed. This is the second tool on that server, which is the day
/// that argument stops being hypothetical.
/// </para>
/// <para>
/// <b>And it costs a contract version</b>, on <c>Propose</c>'s reasoning: the
/// only safe response to an unknown value in a closed vocabulary is to halt, so
/// an added value breaks every prior reader by design. Envelopes in force are
/// unchanged in meaning — they cannot propose a work item, which they could not
/// before either.
/// </para>
/// </remarks>
public class AMoveGrantsOneToolTests
{
    private static readonly Gg.Local.SelfInvocation Self =
        Gg.Local.SelfInvocation.For("/bin/gg", null)!;

    private static ExecutorRequest Request(IReadOnlyList<string> moves) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "triage",
        Moves = moves,
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    private static IReadOnlyList<string> Arguments(IReadOnlyList<string> moves) =>
        AttendedExecutor.StartInfoFor(Request(moves), [], null, Self).ArgumentList!;

    [Test]
    public async Task The_move_grants_exactly_one_tool_by_its_whole_name()
    {
        var granted = ClaudeCodeExecutor.ToolFor(LoopMoves.ProposeWorkItem);

        await Assert.That(granted).IsEqualTo(Gg.Local.WorkItemProposalTool.Qualified)
            .Because("one move, one tool, named whole. Granted: " + granted);

        await Assert.That(granted).StartsWith("mcp__")
            .Because("it is served by this platform rather than built into the agent.");

        await Assert.That(granted.EndsWith("__", StringComparison.Ordinal)).IsFalse()
            .Because("a server prefix would grant every tool this platform later adds to "
                   + "that server, for every envelope in force, with nothing in the record "
                   + "marking the day it changed.");
    }

    [Test]
    public async Task An_envelope_that_does_not_declare_it_cannot_call_it()
    {
        // THE POINT OF THE WHOLE THING. A flight that never asked for this must
        // not be able to propose a change to somebody's backlog, and the two
        // tools on this server must not grant each other.
        var arguments = Arguments([LoopMoves.Read]);

        await Assert.That(arguments.Contains(Gg.Local.WorkItemProposalTool.Qualified)).IsFalse()
            .Because("read was the only move. Passed: " + string.Join(" ", arguments));

        await Assert.That(arguments.Contains(Gg.Local.NominationTool.Qualified)).IsFalse()
            .Because("and the neighbouring tool on the same server is not granted either - "
                   + "which is what naming the whole tool rather than the server buys.");
    }

    [Test]
    public async Task Declaring_it_grants_it_and_nothing_beside_it()
    {
        var arguments = Arguments([LoopMoves.Read, LoopMoves.ProposeWorkItem]);

        await Assert.That(arguments).Contains(Gg.Local.WorkItemProposalTool.Qualified);

        await Assert.That(arguments.Contains(Gg.Local.NominationTool.Qualified)).IsFalse()
            .Because("proposing a work item is not nominating a work kind, and one move "
                   + "granting both would be the prefix grant wearing a different hat. "
                   + "Passed: " + string.Join(" ", arguments));
    }

    [Test]
    public async Task Every_move_still_maps_to_something()
    {
        // THE GUARD THAT ALREADY EXISTED, and the reason adding a move without
        // mapping it is caught before anybody writes a test for it: a move in
        // the vocabulary that maps to itself is a grant of a tool called
        // "propose-work-item", which no agent has.
        foreach (var move in LoopMoves.All)
        {
            await Assert.That(ClaudeCodeExecutor.ToolFor(move)).IsNotEqualTo(move)
                .Because($"'{move}' is in the vocabulary and maps to nothing.");
        }
    }
}
