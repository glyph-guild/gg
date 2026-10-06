namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.2-01</b> - <c>gg itinerary tools</c> answers <c>initialize</c> and lists exactly
/// <c>set_intent</c>, <c>draft_leg</c>, <c>revise_leg</c>, <c>drop_leg</c> and <c>show_plan</c>,
/// under the key <c>gg-itinerary</c>.
/// </summary>
/// <remarks>
/// <b>Its own key</b> (rule 9): it holds the person's session, and <c>gg</c> is the runner's
/// server. Nothing that proposes, opens or nominates is among the five (rule 10).
/// </remarks>
public class TheItineraryToolServerListsItsToolsTests
{
    [Test]
    public async Task It_introduces_itself_under_its_own_key()
    {
        using var server = new ItineraryServerHarness();

        var answers = await server.Method("initialize").RunAsync();

        await Assert.That(ItineraryToolServer.Server).IsEqualTo("gg-itinerary");
        await Assert.That(answers[0].GetProperty("result").GetProperty("serverInfo")
            .GetProperty("name").GetString()).IsEqualTo(ItineraryToolServer.Server);
    }

    [Test]
    public async Task It_lists_exactly_the_five_drafting_tools()
    {
        using var server = new ItineraryServerHarness();

        var answers = await server.Method("tools/list").RunAsync();

        var names = answers[0].GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();

        await Assert.That(names).IsEquivalentTo(
            (string?[])["set_intent", "draft_leg", "revise_leg", "drop_leg", "show_plan"]);
    }

    [Test]
    public async Task A_leg_needs_a_subject_a_kind_and_a_reason()
    {
        using var server = new ItineraryServerHarness();

        var answers = await server.Method("tools/list").RunAsync();

        var draft = answers[0].GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == "draft_leg");
        var required = draft.GetProperty("inputSchema").GetProperty("required").EnumerateArray()
            .Select(r => r.GetString()).ToList();

        await Assert.That(required).Contains("subject")
            .Because("GG-380 left the subject out when prose asked for it; the schema does not ask.");
        await Assert.That(required).Contains("work_kind");
        await Assert.That(required).Contains("reason");
    }
}
