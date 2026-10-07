namespace Gg.Cli.Tests;

/// <summary>
/// Once a draft has been proposed, every tool result opens with what it was proposed as, and
/// whether it has changed since (slice sixty-eight, S68.2-02).
/// </summary>
/// <remarks>
/// <b>The agent reads results, not the bar.</b> ITN-61's agent carried on drafting in a draft it
/// had already proposed, because nothing it read said so.
/// </remarks>
public class EveryResultSaysWhatWasProposedTests
{
    [Test]
    public async Task Proposed_and_unchanged_says_so()
    {
        using var server = TheToolServerProposesOnceTests.AFinishedDraft()
            .Call("propose")
            .Call("show_plan");

        var answers = await server.RunAsync();

        var (text, _) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(text).StartsWith("proposed as ITN-7 · waiting on plan-reviewed");
    }

    [Test]
    public async Task Changed_since_says_proposing_again_replaces_it()
    {
        using var server = TheToolServerProposesOnceTests.AFinishedDraft()
            .Call("propose")
            .Call("draft_leg", new { subject = "the docs", work_kind = "implement", reason = "found later" });

        var answers = await server.RunAsync();

        var (text, _) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(text).StartsWith("changed since ITN-7 was proposed · proposing again replaces it");
    }

    [Test]
    public async Task A_draft_never_proposed_says_nothing_of_it()
    {
        using var server = TheToolServerProposesOnceTests.AFinishedDraft();

        var answers = await server.RunAsync();

        var (text, _) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(text).DoesNotContain("proposed as");
    }
}
