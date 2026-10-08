using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg itineraries</c> lists the tenant's plans, and <c>gg itinerary show ITN-n</c> reads one -
/// the reads the console's itineraries tab and history screen already had, on the command line
/// and so for an agent (owner, 2026-10-08: "we definitely need a command to list itineraries").
/// </summary>
public class TheItinerariesVerbListsPlansTests
{
    private static NominationSummary Leg(
        string plan, string number, string kind, string reason, int minute,
        string? ending = null, string? flight = null) => new()
    {
        NominationId = Guid.NewGuid(),
        Nominator = $"itinerary:{plan}",
        Subject = $"leg:{kind}@0011223344556677",
        Reason = reason,
        Version = "v",
        WorkKind = kind,
        Mode = "auto",
        State = ending ?? NominationStates.Standing,
        Ending = ending,
        FlightNumber = flight,
        ItineraryNumber = number,
        IntentKey = "Bring the Node packages up to date",
        MadeAt = new DateTimeOffset(2026, 10, 7, 15, minute, 0, TimeSpan.Zero),
    };

    private static BoardPage TwoPlans() => new()
    {
        IncludedEnded = true,
        // AS THE CONTROL PLANE SENDS THEM: newest first.
        Nominations =
        [
            Leg("b", "ITN-64", "implement", "a later plan", 30),
            Leg("a", "ITN-63", "review", "Storybook 10 upgrade review", 22, "opened", "GG-992"),
            Leg("a", "ITN-63", "ui-preview", "Storybook 10 visual check", 21, "opened", "GG-990"),
            Leg("a", "ITN-63", "implement", "Storybook 10 upgrade", 20, "opened", "GG-991"),
        ],
    };

    [Test]
    public async Task The_verbs_parse()
    {
        await Assert.That(CliArgs.Parse(["itineraries"])).IsEqualTo(new CliAction.Itineraries(false));
        await Assert.That(CliArgs.Parse(["itineraries", "--json"])).IsEqualTo(new CliAction.Itineraries(true));
        await Assert.That(CliArgs.Parse(["itineraries", "--limit", "5"])).IsEqualTo(new CliAction.Itineraries(false, 5));
        await Assert.That(CliArgs.Parse(["itinerary", "show", "ITN-63"]))
            .IsEqualTo(new CliAction.ItineraryShow("ITN-63", false));
    }

    [Test]
    public async Task Both_are_on_the_usage()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg itineraries");
        await Assert.That(usage).Contains("gg itinerary show");
    }

    [Test]
    public async Task A_listing_is_one_block_per_plan_newest_first_its_legs_in_plan_order()
    {
        var text = VerbOutput.ToText(new VerbResult.Itineraries(TwoPlans()));

        await Assert.That(text.IndexOf("ITN-64", StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("ITN-63", StringComparison.Ordinal))
            .Because("the newest plan first, as the console's tab reads. Text:\n" + text);

        var upgrade = text.IndexOf("Storybook 10 upgrade", StringComparison.Ordinal);
        var check = text.IndexOf("Storybook 10 visual check", StringComparison.Ordinal);
        var review = text.IndexOf("Storybook 10 upgrade review", StringComparison.Ordinal);
        await Assert.That(upgrade).IsLessThan(check)
            .Because("a plan's legs read in the order it wrote them. Text:\n" + text);
        await Assert.That(check).IsLessThan(review);

        await Assert.That(text).Contains("Bring the Node packages up to date");
        await Assert.That(text).Contains("GG-991");
        await Assert.That(text).Contains("ui-preview");
        await Assert.That(text).Contains("opened");
    }

    [Test]
    public async Task No_plans_is_said_rather_than_an_empty_table()
    {
        var text = VerbOutput.ToText(new VerbResult.Itineraries(new BoardPage { Nominations = [], IncludedEnded = true }));

        await Assert.That(text).Contains("No plans");
    }

    [Test]
    public async Task The_json_is_the_page_itself()
    {
        var json = VerbOutput.ToJson(new VerbResult.Itineraries(TwoPlans()));

        await Assert.That(json).Contains("\"itineraryNumber\":\"ITN-63\"");
    }
}
