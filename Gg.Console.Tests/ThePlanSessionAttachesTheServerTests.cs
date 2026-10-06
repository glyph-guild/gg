using System.Text.Json;

namespace Gg.Console.Tests;

/// <summary>
/// <b>S66.2-02</b> - the session runs Claude Code with the <c>gg-itinerary</c> server named through
/// <c>SelfInvocation.Under</c>, drafting <c>console</c>, with its six tools granted by name.
/// </summary>
public class ThePlanSessionAttachesTheServerTests
{
    [Test]
    public async Task The_server_is_gg_itinerary_tools_drafting_console()
    {
        using var session = new PlanSessionFixture();

        session.Run();

        var args = session.Arguments.ToList();
        var flag = args.IndexOf("--mcp-config");
        await Assert.That(flag).IsGreaterThanOrEqualTo(0);

        using var config = JsonDocument.Parse(args[flag + 1]);
        var server = config.RootElement.GetProperty("mcpServers").GetProperty("gg-itinerary");
        await Assert.That(server.GetProperty("command").GetString()).IsEqualTo("/usr/local/bin/gg");
        await Assert.That(server.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToList())
            .IsEquivalentTo((string?[])["itinerary", "tools", "--draft", "console"]);
    }

    [Test]
    public async Task The_six_tools_are_granted_by_name()
    {
        using var session = new PlanSessionFixture();

        session.Run();

        foreach (var tool in (string[])["set_intent", "draft_leg", "revise_leg", "drop_leg", "show_plan", "propose"])
        {
            await Assert.That(session.Arguments).Contains($"mcp__gg-itinerary__{tool}")
                .Because("named, not granted by prefix: a prefix widens with every tool the server adds.");
        }
    }
}
