namespace Gg.Cli.Tests;

/// <summary>
/// <b>S66.1-01</b> - every tool result is also written whole to <c>&lt;draft&gt;.result.txt</c>
/// beside the draft, and a refusal's result too.
/// </summary>
/// <remarks>
/// <b>The panel's only source of verdicts</b> (slice sixty-six rule 3). The check runs in the tool
/// server's process; the mux's panel may not call the control plane, so it reads what the server
/// last said.
/// </remarks>
public class TheServerLeavesItsLastResultTests
{
    [Test]
    public async Task Each_result_is_left_beside_the_draft()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "three findings in one bug" })
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" });

        var answers = await server.RunAsync();

        var left = await File.ReadAllTextAsync(server.Drafts.ResultPathOf("draft"));
        await Assert.That(left).IsEqualTo(ItineraryServerHarness.Result(answers[^1]).Text)
            .Because("the last result, exactly as the agent was given it.");
        await Assert.That(left).Contains("would open");
    }

    [Test]
    public async Task A_refusal_is_left_too()
    {
        using var server = new ItineraryServerHarness()
            .Call("set_intent", new { text = "kept" })
            .Call("revise_leg", new { subject = "nothing like this", reason = "x" });

        var answers = await server.RunAsync();

        await Assert.That(await File.ReadAllTextAsync(server.Drafts.ResultPathOf("draft")))
            .IsEqualTo(ItineraryServerHarness.Result(answers[^1]).Text);
    }

    [Test]
    public async Task The_result_lives_beside_the_draft()
    {
        var drafts = new Gg.Client.ItineraryDrafts("/state/itineraries");

        await Assert.That(drafts.ResultPathOf("console")).IsEqualTo("/state/itineraries/console.result.txt");
    }
}
