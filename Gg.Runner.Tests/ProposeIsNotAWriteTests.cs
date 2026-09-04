using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The move a classifier declares must not be a write in disguise.
/// </summary>
/// <remarks>
/// <para>
/// <b>The probe measures the machine, not this flight's moves.</b>
/// <see cref="MoveBoundProbe"/> builds its own request with
/// <c>[read]</c> and asks the disk whether anything appeared - so what it
/// answers is "with only read declared, can bytes reach disk on this session",
/// which is a property of the host. A classify loop's declared moves never
/// reach it. That is the right design and it leaves a gap this file closes:
/// nothing measures whether the moves a classifier declares could put bytes on
/// disk in the first place.
/// </para>
/// <para>
/// <b>And reading the launch arguments is not a substitute for the probe.</b>
/// Measured on a real host: with <c>--setting-sources ""</c>,
/// <c>--strict-mcp-config</c> and an allow-list naming exactly two tools, the
/// session reported 28 tools including <c>Bash</c>, <c>Edit</c> and
/// <c>Write</c>. The bound is applied at the CALL. So this is a ratchet on the
/// mapping - what the envelope's vocabulary turns into - and not a claim about
/// what the agent can do.
/// </para>
/// <para>
/// <b>Why it is worth a file.</b> <c>propose</c> is the first move whose tool
/// this runner serves itself, and the tool it serves is one this repository can
/// change without touching any envelope. A second tool on that server, or a
/// mapping edited to reach a built-in that can write, would make every
/// read-only classifier in every tenant a writer, with nothing in any envelope
/// changing and nothing in the record marking the day.
/// </para>
/// </remarks>
public class ProposeIsNotAWriteTests
{
    /// <summary>
    /// The tools the probe attributes a broken bound to, by name.
    /// </summary>
    /// <remarks>
    /// Read off the executor's own mapping rather than spelled here, so a
    /// rename cannot leave this list checking two strings nothing uses. These
    /// are the two the probe's canary and anchor attribute: creation is
    /// Write-shaped, modification is Edit-shaped.
    /// </remarks>
    private static IReadOnlyList<string> PutsBytesOnDisk() =>
    [
        ClaudeCodeExecutor.ToolFor(LoopMoves.Write),
        ClaudeCodeExecutor.ToolFor(LoopMoves.Edit),
    ];

    private static ExecutorRequest Request(IReadOnlyList<string> moves) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "classify",
        Moves = moves,
        WallClock = TimeSpan.FromMinutes(10),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    [Test]
    public async Task The_move_maps_to_this_platforms_own_tool_and_not_to_a_builtin()
    {
        // THE MAPPING ITSELF. Every other move names a tool the agent binary
        // already has, and three of those can put bytes on disk. This one names
        // a server this runner starts, and the whole tool rather than the
        // server's prefix.
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.Propose))
            .IsEqualTo(NominationTool.Qualified);

        await Assert.That(PutsBytesOnDisk())
            .DoesNotContain(ClaudeCodeExecutor.ToolFor(LoopMoves.Propose))
            .Because("if this ever maps onto a tool the probe would attribute a broken bound "
                   + "to, a read-only classifier becomes a writer with no envelope changing.");

        // AND NOT ONTO BASH EITHER, which is the interesting near-miss: the
        // move-to-tool mapping is not injective, `run-tests` maps onto Bash,
        // and Bash can edit files. A tool that can edit files is not a place to
        // put a move whose whole product is a recorded value.
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.Propose))
            .IsNotEqualTo(ClaudeCodeExecutor.ToolFor(LoopMoves.RunTests));
    }

    [Test]
    public async Task A_classifiers_grant_names_no_tool_that_could_write()
    {
        // OVER THE ARGUMENT LIST, because the mapping is only half of it: a
        // grant is also composed from the read tools and from whatever a
        // configured server contributes, and a test that stopped at `Tool`
        // would not see either.
        var granted = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read, LoopMoves.Propose]),
            [],
            self: SelfInvocation.For("/bin/gg", null));

        await Assert.That(granted).Contains(NominationTool.Qualified)
            .Because("the classifier's whole product is one recorded value, and it needs the "
                   + "tool that records it.");

        foreach (var writer in PutsBytesOnDisk())
        {
            await Assert.That(granted).DoesNotContain(writer)
                .Because($"'{writer}' in a classifier's grant is the read-only bound gone, and "
                       + "the envelope that declared read and propose would still read as "
                       + "read-only.");
        }
    }

    [Test]
    public async Task The_liveness_twin_a_loop_that_declares_a_write_is_granted_one()
    {
        // ON THIS ASSERTION'S OWN AXIS. A grant that contained nothing at all,
        // or an ArgumentsFor that returned an empty list, would satisfy every
        // DoesNotContain above and would prove nothing about `propose`.
        var granted = ClaudeCodeExecutor.ArgumentsFor(
            Request([LoopMoves.Read, LoopMoves.Edit, LoopMoves.Write]),
            [],
            self: SelfInvocation.For("/bin/gg", null));

        foreach (var writer in PutsBytesOnDisk())
        {
            await Assert.That(granted).Contains(writer)
                .Because("a kind of work that declares it may change files is granted the tool "
                       + "that changes them - which is what makes the classifier's absence "
                       + "above a measurement rather than an empty list.");
        }

        await Assert.That(granted).DoesNotContain(NominationTool.Qualified)
            .Because("and the move decides in both directions: a loop that never declared "
                   + "propose is not handed the tool because some other loop was.");
    }
}
