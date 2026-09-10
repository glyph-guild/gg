using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// Reading proposed changes out of what the agent actually did.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.3-05 and S35.3-06.</b> From the tool call and never from prose, on
/// the argument <c>NominationExtractionTests</c> makes one fact over: a closing
/// summary mentions what it just did, and a sentence is something an agent can
/// be TOLD to write - by a file in a customer's tree, among other places. A
/// tool call is a thing the agent chose to make, in a shape read mechanically.
/// </para>
/// <para>
/// <b>MANY, where a nomination is one.</b> A classifier nominates a single work
/// kind and the last answered call wins; a triage reads a backlog and proposes
/// a dozen changes to it, so every answered call is a proposal and their ORDER
/// is part of the record - a link proposed after a re-field was proposed by an
/// agent that had already decided the first one.
/// </para>
/// <para>
/// <b>And that is why a malformed one is loud here and silent there.</b> A
/// missing nomination means the classifier declined, which is a real answer the
/// absence states correctly. A missing proposal means nothing at all: an agent
/// that proposed eleven changes and an agent whose twelfth was dropped are
/// indistinguishable in the record, and the second one is a bug nobody will
/// ever see. Since the server refuses every malformation BEFORE answering, and
/// this reads only answered calls, a malformed answered call is a transcript
/// that did not come from this server - which is exactly the case worth
/// stopping the flight over rather than quietly shipping eleven.
/// </para>
/// </remarks>
public class ProposalExtractionTests
{
    /// <summary>One assistant turn calling the proposal tool, and its paired result.</summary>
    private static string Called(
        string id, string arguments, bool failed = false, bool paired = true)
    {
        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + WorkItemProposalTool.Qualified + "\","
          + "\"input\":" + arguments + "}]}}";

        if (!paired)
        {
            return call;
        }

        var result =
            "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
          + "\"tool_use_id\":\"" + id + "\""
          + (failed ? ",\"is_error\":true" : "")
          + ",\"content\":\"Recorded\"}]}}";

        return call + "\n" + result;
    }

    /// <summary>
    /// One call's arguments, with the fields a `field` proposal must carry.
    /// </summary>
    /// <remarks>
    /// <b>A field proposal that sets nothing is refused by the contract</b>, so
    /// these carry an edit - which is the rule arriving in this file rather
    /// than a convenience. It bit here first: every `field` case in this suite
    /// was written before the fields member existed and threw the moment it
    /// did, which is the contract reaching a caller that had been getting away
    /// with half a proposal.
    /// </remarks>
    private static string Argue(string operation, string target, string reason) =>
        string.Equals(operation, WorkItemOperations.Field, StringComparison.Ordinal)
            ? $$"""
              {"operation":"{{operation}}","target":"{{target}}","reason":"{{reason}}",
               "fields":[{"path":"System.State","value":"Active"}]}
              """.ReplaceLineEndings(" ")
            : $$"""{"operation":"{{operation}}","target":"{{target}}","reason":"{{reason}}"}""";

    [Test]
    public async Task Every_answered_call_is_a_proposal_and_they_keep_their_order()
    {
        var transcript = string.Join('\n',
            Called("a", Argue(WorkItemOperations.Field, "1421", "it is a bug not a task")),
            Called("b", Argue(WorkItemOperations.Link, "1421", "duplicate of 1189")),
            Called("c", Argue(WorkItemOperations.Score, "1189", "two repros")
                .Replace("}", ",\"score\":\"P1\"}", StringComparison.Ordinal)));

        var proposed = TranscriptDigest.Proposals(transcript);

        // JOINED RATHER THAN COMPARED AS A SET, because the order is the
        // assertion: a link proposed after a re-field was proposed by an agent
        // that had already decided the first one, and a set loses that.
        await Assert.That(string.Join(", ", proposed.Select(p => p.Operation)))
            .IsEqualTo($"{WorkItemOperations.Field}, {WorkItemOperations.Link}, "
                     + WorkItemOperations.Score);

        await Assert.That(proposed[2].Score).IsEqualTo("P1");
        await Assert.That(proposed[1].Target).IsEqualTo("1421");
    }

    [Test]
    public async Task A_call_the_server_refused_is_not_a_proposal()
    {
        // The nomination's rule: an unanswered call and an errored one are both
        // things the agent tried, and neither is a thing the platform took.
        var transcript = string.Join('\n',
            Called("a", Argue(WorkItemOperations.Field, "1421", "a bug"), failed: true),
            Called("b", Argue(WorkItemOperations.Link, "1421", "dup"), paired: false),
            Called("c", Argue(WorkItemOperations.Update, "1189", "the title says nothing")));

        var proposed = TranscriptDigest.Proposals(transcript);

        await Assert.That(proposed.Count).IsEqualTo(1)
            .Because("one call came back and came back clean. Found: " + string.Join(", ",
                proposed.Select(p => p.Operation + " " + p.Target)));
        await Assert.That(proposed[0].Operation).IsEqualTo(WorkItemOperations.Update);
    }

    [Test]
    public async Task Prose_that_says_what_it_proposed_produces_nothing()
    {
        // THE WHOLE POINT, and it is a security property rather than a purity
        // one. An agent's summary naming the tool and the item reads exactly
        // like a report of work done - and it is a sentence, which a file in
        // the tree can ask for.
        var transcript =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":"
          + "\"I called " + WorkItemProposalTool.Qualified + " to score work item 1421 as P1 "
          + "and to link it to 1189.\"}]}}";

        await Assert.That(TranscriptDigest.Proposals(transcript)).IsEmpty()
            .Because("a sentence about a tool call is not a tool call, and the difference is "
                   + "who chose to write it.");
    }

    [Test]
    public async Task A_malformed_payload_is_refused_loudly_rather_than_dropped()
    {
        // NOT SILENTLY SKIPPED, which is what the nomination does and what would
        // be wrong here: eleven proposals shipped out of twelve looks exactly
        // like eleven proposals made. The server refuses all of these before
        // answering, so an ANSWERED one is a transcript that did not come from
        // this server.
        foreach (var (what, arguments) in ((string, string)[])
            [("no operation", """{"target":"1421","reason":"stale"}"""),
             ("no reason", """{"operation":"update","target":"1421"}"""),
             ("an operation nobody declared", """{"operation":"delete","target":"1421","reason":"dup"}"""),
             ("an update with no target", """{"operation":"update","reason":"stale"}""")])
        {
            var transcript = Called("toolu_017qx", arguments);

            var thrown = Assert.Throws<InvalidOperationException>(
                () => TranscriptDigest.Proposals(transcript));

            // THE CALL ID, not just "a proposal was malformed". A message like
            // that, in a transcript with twelve of them, is a message with no
            // way into the file.
            await Assert.That(thrown!.Message).Contains("toolu_017qx", StringComparison.Ordinal)
                .Because($"{what}: the diagnosis has to name the call. Said: {thrown.Message}");

            // AND WHAT WAS WRONG WITH IT, which is the contract's own sentence
            // rather than a second wording of the same rule kept here.
            await Assert.That(thrown.Message.Length).IsGreaterThan(80)
                .Because($"{what}: Article XI asks for a diagnosis, and a reader who gets "
                       + $"'refused' goes to read our code. Said: {thrown.Message}");
        }
    }

    [Test]
    public async Task A_flight_that_proposed_nothing_is_not_an_error()
    {
        // Declining is a real answer, and it is the answer an empty list states
        // correctly. The loud case above is a MALFORMED proposal, not an absent
        // one - and conflating the two would make "the item did not say enough"
        // fail the flight.
        await Assert.That(TranscriptDigest.Proposals(
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\","
          + "\"text\":\"The item does not say what the expected behaviour is.\"}]}}"))
            .IsEmpty();
    }
}
