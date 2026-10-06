namespace Gg.Cli.Tests;

/// <summary>The command line half of S65.5-01: the verb parses, and the usage names it.</summary>
public class ProposingAPlanFileVerbTests
{
    [Test]
    public async Task The_verb_takes_a_plan_file()
    {
        var action = CliArgs.Parse(["itinerary", "propose", "plan.yaml"]);

        var propose = await Assert.That(action).IsTypeOf<CliAction.ItineraryPropose>();
        await Assert.That(propose!.Path).IsEqualTo("plan.yaml");
    }

    [Test]
    public async Task The_usage_names_it()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg itinerary propose");
    }
}
