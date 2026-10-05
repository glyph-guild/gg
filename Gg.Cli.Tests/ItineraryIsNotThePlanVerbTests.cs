namespace Gg.Cli.Tests;

/// <summary>
/// <b>S61.6-03</b> - <c>gg itinerary check</c> parses to the new verb, and <c>gg plan</c> and
/// <c>gg plan &lt;flight&gt;</c> still mean the checklist.
/// </summary>
/// <remarks>
/// <b>Why the noun is <c>itinerary</c>.</b> ADR-0038 first named this <c>gg plan check</c>, and
/// <c>gg plan</c> is the ADR-0015 checklist: <c>["plan", var flight]</c> would read
/// <c>gg plan check</c> as the checklist of a flight called "check". The two words sit one apart
/// on a keyboard and nowhere near each other in meaning, so both directions are held here.
/// </remarks>
public class ItineraryIsNotThePlanVerbTests
{
    [Test]
    public async Task Itinerary_check_is_the_new_verb()
    {
        var parsed = CliArgs.Parse(["itinerary", "check", "plan.yaml"]);

        await Assert.That(parsed).IsTypeOf<CliAction.ItineraryCheck>();
        await Assert.That(((CliAction.ItineraryCheck)parsed).Path).IsEqualTo("plan.yaml");
    }

    [Test]
    public async Task Itinerary_check_takes_json()
    {
        var parsed = (CliAction.ItineraryCheck)CliArgs.Parse(["itinerary", "check", "plan.yaml", "--json"]);

        await Assert.That(parsed.Json).IsTrue();
    }

    [Test]
    public async Task Plan_is_still_the_checklist()
    {
        await Assert.That(CliArgs.Parse(["plan"])).IsTypeOf<CliAction.Plan>();
        await Assert.That(((CliAction.Plan)CliArgs.Parse(["plan", "GG-12"])).Flight).IsEqualTo("GG-12");
    }
}
