namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.3-05</b> - when the menu cannot be read, the server still starts, and every tool call
/// answers with why rather than accepting a free string.
/// </summary>
/// <remarks>
/// A server that died would cost the agent its tools for the session; one that fell back to free
/// strings would offer exactly the prose menu Decision 8 exists to replace.
/// </remarks>
public class AMenuThatCannotBeReadSaysSoTests
{
    [Test]
    public async Task The_server_still_answers_and_every_change_says_why_it_cannot()
    {
        using var server = new ItineraryServerHarness()
            .Method("initialize")
            .Method("tools/list")
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "x" })
            .Call("set_intent", new { text = "three findings" });
        server.Reads.MenuFails = new HttpRequestException("the control plane did not answer (503)");

        var answers = await server.RunAsync();

        await Assert.That(answers[0].TryGetProperty("result", out _)).IsTrue();
        await Assert.That(answers[1].TryGetProperty("result", out _)).IsTrue();
        foreach (var answer in answers.Skip(2))
        {
            var (text, isError) = ItineraryServerHarness.Result(answer);
            await Assert.That(isError).IsTrue();
            await Assert.That(text).Contains("503");
        }

        await Assert.That(File.Exists(server.Drafts.PathOf("draft"))).IsFalse();
    }

    [Test]
    public async Task A_menu_the_control_plane_refused_is_answered_with_its_sentence()
    {
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "x" });
        server.Reads.Menu = server.Reads.Menu with
        {
            WorkKinds = [],
            Repositories = [],
            Environments = [],
            Refused = "'plan' opens no flights, so it has no menu.",
        };

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[0]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("opens no flights");
    }
}
