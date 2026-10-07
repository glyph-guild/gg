using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// Proposing a draft again sends the plan it was last proposed as, so the door replaces that plan
/// rather than minting a second (slice sixty-eight, S68.4-01).
/// </summary>
/// <remarks>
/// <b>Found on ITN-61 and ITN-62</b>: the same draft, edited, became two plans both waiting on a
/// gate, the second holding the first's legs again.
/// </remarks>
public class ProposingAgainReplacesThePlanTests
{
    [Test]
    public async Task The_second_proposal_supersedes_the_first()
    {
        using var server = TheToolServerProposesOnceTests.AFinishedDraft()
            .Call("propose")
            .Call("draft_leg", new { subject = "the docs", work_kind = "implement", reason = "found later" })
            .Call("propose");

        await server.RunAsync();

        await Assert.That(server.Proposals.Sent.Count).IsEqualTo(2);
        await Assert.That(server.Proposals.Sent[0].Supersedes).IsNull()
            .Because("the first proposal of a draft replaces nothing.");
        await Assert.That(server.Proposals.Sent[1].Supersedes).IsEqualTo("ITN-7")
            .Because("the draft was proposed as ITN-7, so proposing it again replaces ITN-7.");
    }

    [Test]
    public async Task A_replacement_the_door_refuses_keeps_the_record_and_says_why()
    {
        using var server = TheToolServerProposesOnceTests.AFinishedDraft()
            .Call("propose")
            .Call("draft_leg", new { subject = "the docs", work_kind = "implement", reason = "found later" });
        server.Proposals.RefuseAfter(1,
            "ITN-7 was approved and its legs are flying, so it cannot be replaced. Start a new draft "
          + "for more work, and propose that as a plan of its own.");
        server.Call("propose");

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("was approved and its legs are flying");
        await Assert.That(server.Drafts.Proposed("draft")!.Itinerary).IsEqualTo("ITN-7")
            .Because("nothing replaced it, so the record still names the plan the draft became.");
    }
}
