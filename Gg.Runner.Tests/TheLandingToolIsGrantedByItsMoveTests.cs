using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The tool an agent states its landing with, and what grants it.
/// </summary>
/// <remarks>
/// <b>A move is the only thing an envelope declares that the runner turns into
/// a granted tool.</b> <c>LoopMoves.All</c> is what an envelope may name,
/// <c>ClaudeCodeExecutor.ToolFor</c> is the single map from a named move to a
/// qualified tool, and a tool with no move behind it is a tool nothing can ask
/// for. Without the move this one would have to be granted unconditionally or
/// named outside the envelope, and either is a capability a governed flight
/// holds that its own record does not declare.
/// </remarks>
public class TheLandingToolIsGrantedByItsMoveTests
{
    [Test]
    public async Task The_move_grants_it_and_grants_only_it()
    {
        // THE WHOLE NAME RATHER THAN THE SERVER'S PREFIX. A prefix grant would
        // retroactively grant every tool this platform later adds to its own
        // server, for every envelope in force, with nothing in the record
        // marking the day it changed - which is the argument `write` was
        // created under, one layer over.
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.ProposeLanding))
            .IsEqualTo(LandingProposalTool.Qualified);

        await Assert.That(LandingProposalTool.Qualified)
            .IsEqualTo($"mcp__{LandingProposalTool.Server}__{LandingProposalTool.Name}");
    }

    [Test]
    public async Task It_lives_on_the_platforms_own_server_beside_the_others()
    {
        // One server, and its tools are granted one move at a time. A second
        // server key would be a second thing an operator could shadow with a
        // reader they configured.
        await Assert.That(LandingProposalTool.Server)
            .IsEqualTo(WorkItemProposalTool.Server);

        await Assert.That(LandingProposalTool.Name).IsNotEqualTo(WorkItemProposalTool.Name);
    }

    [Test]
    public async Task A_flight_that_was_not_granted_it_cannot_call_it()
    {
        // The map answers the move itself for anything it does not turn into a
        // tool, which is what keeps an ungranted move from silently becoming a
        // tool name the launch would allow.
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.Read))
            .IsNotEqualTo(LandingProposalTool.Qualified);
    }
}
