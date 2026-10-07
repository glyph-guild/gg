using System.Text.Json;

namespace Gg.Cli.Tests;

/// <summary>
/// `draft_leg` and `revise_leg` describe `repository` as what a leg works in, not as optional
/// (slice sixty-seven, S67.2-01).
/// </summary>
/// <remarks>
/// <b>Found on ITN-60.</b> The field read "the registered repository this leg works in, when not
/// the plan's own". A plan a person proposes has no repository of its own, so the agent left it
/// out of every leg - correctly, by the words it was given - and GG-968 opened with no tree.
/// </remarks>
public class TheRepositoryArgumentSaysItIsNeededTests
{
    private static async Task<string> DescriptionAsync(ItineraryServerHarness server, string tool)
    {
        var answers = await server.Method("tools/list").RunAsync();
        return answers[0].GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == tool)
            .GetProperty("inputSchema").GetProperty("properties")
            .GetProperty("repository").GetProperty("description").GetString()!;
    }

    [Test]
    [Arguments("draft_leg")]
    [Arguments("revise_leg")]
    public async Task It_says_a_leg_names_one(string tool)
    {
        using var server = new ItineraryServerHarness();

        var described = await DescriptionAsync(server, tool);

        await Assert.That(described).DoesNotContain("plan's own")
            .Because("a person's plan has none, so the phrase told the agent to leave the field out.");
        await Assert.That(described).Contains("required")
            .Because("a leg whose kind works in a repository is refused without one, and the agent "
                   + "should know before the refusal rather than from it.");
    }
}
