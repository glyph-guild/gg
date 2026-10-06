namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.4-02</b> - a check that fails is reported beside the draft, and the change that
/// preceded it still stands.
/// </summary>
/// <remarks>
/// The preview is a read about the draft, not part of the edit. A control plane that blinks
/// between two calls must not cost the agent the leg it just drafted.
/// </remarks>
public class APreviewThatFailsKeepsTheDraftTests
{
    [Test]
    public async Task The_leg_is_kept_and_the_failure_is_said()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "three findings" })
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" });
        server.Reads.CheckFails = new HttpRequestException("the control plane did not answer (502)");

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[1]);
        await Assert.That(isError).IsFalse();
        await Assert.That(text).Contains("502");
        await Assert.That(text).Contains("the icon");
        var draft = ((Gg.Client.DraftRead.Held)server.Drafts.Read("draft")).Draft;
        await Assert.That(draft.Legs.Single().Subject).IsEqualTo("the icon");
    }
}
