namespace Gg.Cli.Tests;

/// <summary>
/// A successful propose leaves a record of what the draft was proposed as, which no later tool
/// call overwrites (slice sixty-eight, S68.1-01).
/// </summary>
/// <remarks>
/// <b>Found on ITN-61 and ITN-62.</b> The only record of a proposal was the last result, and the
/// next tool call replaced it - so the bar never said the plan was submitted, the agent kept
/// working in the same draft, and proposed it again as a second plan.
/// </remarks>
public class AProposalIsRecordedBesideItsDraftTests
{
    [Test]
    public async Task Proposing_records_the_plan_and_a_later_call_keeps_it()
    {
        using var server = TheToolServerProposesOnceTests.AFinishedDraft()
            .Call("propose")
            .Call("show_plan");

        await server.RunAsync();

        var kept = server.Drafts.Proposed("draft");
        await Assert.That(kept).IsNotNull()
            .Because("the proposal is recorded beside the draft, and show_plan after it changes nothing.");
        await Assert.That(kept!.Itinerary).IsEqualTo("ITN-7");
        await Assert.That(kept.Pass).IsEqualTo(server.Proposals.Answer.Pass);
        await Assert.That(kept.Gates).IsEquivalentTo((string[])["plan-reviewed"]);
        await Assert.That(server.Drafts.LastResult("draft")).DoesNotStartWith("proposed ITN-7")
            .Because("the last result moved on to show_plan, which is exactly why it cannot be the record.");
    }

    [Test]
    public async Task A_refused_proposal_records_nothing()
    {
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
            .Call("propose");

        await server.RunAsync();

        await Assert.That(server.Drafts.Proposed("draft")).IsNull();
    }
}
