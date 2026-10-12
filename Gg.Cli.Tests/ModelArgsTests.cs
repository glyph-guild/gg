namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg model &lt;flight&gt; &lt;name&gt;</c> moves a flight in the air to another
/// model root lists (slice seventy-six).
/// </summary>
public class ModelArgsTests
{
    [Test]
    public async Task A_flight_and_a_name_parse()
    {
        var parsed = CliArgs.Parse(["model", "GG-54", "claude-sonnet-5-5"]);
        await Assert.That(parsed).IsTypeOf<CliAction.ModelFlight>();
        var model = (CliAction.ModelFlight)parsed;
        await Assert.That(model.Reference).IsEqualTo("GG-54");
        await Assert.That(model.Name).IsEqualTo("claude-sonnet-5-5");
    }

    [Test]
    public async Task An_id_parses_too_because_the_wire_resolves_both()
    {
        var parsed = (CliAction.ModelFlight)CliArgs.Parse(
            ["model", "01a0792a-5e1f-7030-a5d8-52fd66e510b0", "claude-opus-5-5"]);
        await Assert.That(parsed.Reference).IsEqualTo("01a0792a-5e1f-7030-a5d8-52fd66e510b0");
    }

    [Test]
    public async Task A_flight_with_no_name_is_refused_before_it_is_sent()
    {
        var parsed = CliArgs.Parse(["model", "GG-54"]);
        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        var refusal = ((CliAction.Unknown)parsed).Message;
        await Assert.That(refusal).Contains("gg model <flight> <model>")
            .Because("the wire would refuse a blank with a 400 about a field; this side can say "
                   + "what the name is for and where the permitted ones are listed.");
        await Assert.That(refusal).Contains("models:");
    }

    [Test]
    public async Task It_is_discoverable()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;
        await Assert.That(usage).Contains("gg model <flight> <model>");
    }
}
