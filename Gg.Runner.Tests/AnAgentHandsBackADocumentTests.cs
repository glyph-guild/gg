using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight hands an airspace document back through a tool, and gg reads the
/// call out of the stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape <c>flight.nomination</c> established, for the second request in
/// the vocabulary.</b> A nomination asks that a flight exist; this asks that a
/// document change. Declared through a tool and never inferred from prose,
/// because <c>loop.outcome</c>'s <c>blocked</c> already settled that question:
/// "a classifier over repository content is injectable".
/// </para>
/// <para>
/// <b>Its own move and its own whole name.</b> The map's own comment refuses the
/// alternative: a prefix grant "would retroactively grant every tool this
/// platform later adds to its own server, for every envelope in force, with
/// nothing in the record marking the day it changed". This is the fourth tool on
/// that server and takes the same terms as the second and third.
/// </para>
/// <para>
/// <b>Three spellings have to agree</b> - the launch's <c>--allowedTools</c>, the
/// server's <c>tools/list</c>, and this extractor - and the failure of any one is
/// silent: an agent granted a tool that does not exist, or a value it declared
/// that is never found.
/// </para>
/// </remarks>
public class AnAgentHandsBackADocumentTests
{
    private const string Role = "work-kind";

    private static string Stream(string id, bool answered)
    {
        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + DocumentProposalTool.Qualified + "\","
          + "\"input\":{\"role\":\"" + Role + "\",\"name\":\"ui-preview\","
          + "\"document\":\"based-on: ui-preview@v7\\n\"}}]}}\n";

        var result =
            "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"" + id + "\",\"content\":\"recorded\"}]}}\n";

        return answered ? call + result : call;
    }

    [Test]
    public async Task The_move_grants_the_tool_by_its_whole_name()
    {
        await Assert.That(LoopMoves.All).Contains(LoopMoves.ProposeDocument);

        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.ProposeDocument))
            .IsEqualTo(DocumentProposalTool.Qualified)
            .Because("its own move and its own whole name - one move granting two tools is "
                   + "the prefix grant the map already refuses, arriving by another route.");
    }

    [Test]
    public async Task A_call_that_came_back_clean_is_read_out_of_the_stream()
    {
        var proposal = TranscriptDigest.Document(Stream("call-1", answered: true));

        await Assert.That(proposal).IsNotNull();
        await Assert.That(proposal!.Role).IsEqualTo(Role);
        await Assert.That(proposal.Name).IsEqualTo("ui-preview");
        await Assert.That(proposal.Document).Contains("based-on:");
    }

    /// <summary>
    /// A call whose result never came back is not a proposal.
    /// </summary>
    /// <remarks>
    /// The nomination extractor's own rule, and the reason is the same: the tool
    /// validates before it answers, so a call with no result is one the server
    /// refused or never saw. Reading it anyway would let an agent propose a
    /// document the server rejected, by writing it and not waiting.
    /// </remarks>
    [Test]
    public async Task A_call_with_no_result_is_not_one()
    {
        await Assert.That(TranscriptDigest.Document(Stream("call-1", answered: false)))
            .IsNull();
    }

    [Test]
    public async Task A_stream_that_never_called_it_proposes_nothing()
    {
        await Assert.That(TranscriptDigest.Document(
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\","
          + "\"text\":\"I would change ui-preview like this: ...\"}]}}\n"))
            .IsNull()
            .Because("prose describing a document is not a document handed back, and reading "
                   + "it as one is the classifier this vocabulary already refuses.");
    }
}
