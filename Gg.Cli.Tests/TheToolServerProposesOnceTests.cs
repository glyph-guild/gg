using Gg.Client;

namespace Gg.Cli.Tests;

/// <summary>
/// <b>S65.5-02</b> - the tool server's <c>propose</c> sends the draft with <c>via</c> naming the
/// server, and its result is the draft beside the plan's number and gate; it is the server's one
/// write.
/// </summary>
/// <remarks>
/// <b>One write, held apart.</b> The reads stay <see cref="IPlanningReads"/>, two members and no
/// third; the proposal is its own interface with one member, so a second write would have to be
/// handed in where this test sees it.
/// </remarks>
public class TheToolServerProposesOnceTests
{
    private static ItineraryServerHarness AFinishedDraft() => new ItineraryServerHarness()
        .Method("initialize", client: "claude-code")
        .Call("set_intent", new { text = "three findings in one bug" })
        .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
        .Call("draft_leg", new { subject = "the padding", work_kind = "implement", reason = "shared", after = "the icon" });

    [Test]
    public async Task The_draft_is_proposed_naming_the_agent_and_the_server()
    {
        using var server = AFinishedDraft().Call("propose");

        var answers = await server.RunAsync();

        var sent = server.Proposals.Sent.Single();
        await Assert.That(sent.Draft.Legs.Select(l => l.Subject ?? "")).IsEquivalentTo(["the icon", "the padding"]);
        await Assert.That(sent.Via).IsEqualTo("claude-code (gg-itinerary)")
            .Because("which agent acted is the client the server was opened by, and the server it used.");

        var (text, isError) = ItineraryServerHarness.Result(answers[^1]);
        await Assert.That(isError).IsFalse();
        await Assert.That(text).Contains("ITN-7");
        await Assert.That(text).Contains("platform-owner");
        await Assert.That(text).Contains("the padding")
            .Because("the result is the draft beside the plan's number, as every result is.");
    }

    [Test]
    public async Task An_unfinished_draft_is_not_proposed()
    {
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "named first" })
            .Call("propose");

        var answers = await server.RunAsync();

        await Assert.That(ItineraryServerHarness.Result(answers[^1]).IsError).IsTrue();
        await Assert.That(server.Proposals.Sent).IsEmpty()
            .Because("a plan about nothing is refused whole; sending it would be asking to be told so.");
    }

    [Test]
    public async Task Proposing_is_the_one_write_the_server_is_handed()
    {
        var members = typeof(IPlanningProposals).GetMethods().Select(m => m.Name).ToList();

        await Assert.That(members).IsEquivalentTo((string[])["ProposeAsync"]);
    }
}
