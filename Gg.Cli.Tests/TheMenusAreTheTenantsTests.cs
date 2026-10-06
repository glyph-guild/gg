using System.Text.Json;

namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.3-04</b> - <c>work_kind</c>, <c>repository</c> and <c>environment</c> are enums holding
/// the menu's lists; <c>subject</c> is required.
/// </summary>
/// <remarks>
/// <b>Constraints belong in the schema</b> (ADR-0038 Decision 8). GG-369 nominated <c>bugfix</c>,
/// a kind nobody declared, and GG-373 asked for the menu instead of being shown it. An enum is
/// shown, and a value outside it is refused here too, because not every client enforces one.
/// </remarks>
public class TheMenusAreTheTenantsTests
{
    private static async Task<JsonElement> DraftLegSchemaAsync(ItineraryServerHarness server)
    {
        var answers = await server.Method("tools/list").RunAsync();
        return answers[0].GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == "draft_leg")
            .GetProperty("inputSchema").GetProperty("properties");
    }

    private static IReadOnlyList<string?> Enum(JsonElement properties, string field) =>
        [.. properties.GetProperty(field).GetProperty("enum").EnumerateArray().Select(e => e.GetString())];

    [Test]
    public async Task The_three_menus_are_enums_from_the_menu_read()
    {
        using var server = new ItineraryServerHarness();

        var properties = await DraftLegSchemaAsync(server);

        await Assert.That(Enum(properties, "work_kind")).IsEquivalentTo((string?[])["implement", "triage"]);
        await Assert.That(Enum(properties, "repository")).IsEquivalentTo((string?[])["JDNext", "agile-cortex"]);
        await Assert.That(Enum(properties, "environment")).IsEquivalentTo((string?[])["dev"]);
        await Assert.That(server.Reads.Asked).Contains("menu plan")
            .Because("the menu is the planner's - a draft that names none is checked against 'plan'.");
    }

    [Test]
    public async Task A_choice_the_destination_permits_none_of_is_not_offered()
    {
        // NO BOUND IS NO CHOICE: SelectionBound refuses any environment a destination does not
        // bound, so a field with nothing in it would be a field that can only be refused.
        using var server = new ItineraryServerHarness();
        server.Reads.Menu = server.Reads.Menu with { Environments = [] };

        var properties = await DraftLegSchemaAsync(server);

        await Assert.That(properties.TryGetProperty("environment", out _)).IsFalse();
    }

    [Test]
    public async Task A_kind_outside_the_menu_is_refused_naming_the_menu()
    {
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "bugfix", reason = "GG-369's kind" });

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[0]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("'bugfix'");
        await Assert.That(text).Contains("implement");
        await Assert.That(File.Exists(server.Drafts.PathOf("draft"))).IsFalse()
            .Because("a refused leg writes nothing.");
    }

    [Test]
    public async Task A_repository_outside_the_menu_is_refused_too()
    {
        using var server = new ItineraryServerHarness()
            .Call("draft_leg", new { subject = "the icon", work_kind = "implement", reason = "x", repository = "Somebody/Else" });

        var answers = await server.RunAsync();

        var (text, isError) = ItineraryServerHarness.Result(answers[0]);
        await Assert.That(isError).IsTrue();
        await Assert.That(text).Contains("'Somebody/Else'");
    }
}
