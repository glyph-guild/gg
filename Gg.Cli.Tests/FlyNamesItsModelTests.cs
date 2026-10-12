namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg fly --model &lt;name&gt;</c> chooses the model a flight runs on, against
/// root's <c>models:</c> (slice seventy-six).
/// </summary>
/// <remarks>
/// <b>Every line gg fly already takes</b>, which is the rule the other flags
/// came in on: a flag that worked on one payload and not the others would be
/// a second way to open a flight wearing the first one's name.
/// </remarks>
public class FlyNamesItsModelTests
{
    private static CliAction.Fly Flown(params string[] args)
    {
        var action = CliArgs.Parse(args);
        return action as CliAction.Fly ?? throw new InvalidOperationException(
            $"'{string.Join(" ", args)}' did not parse as a flight but as "
          + $"{action.GetType().Name}"
          + (action is CliAction.Unknown unknown ? $": {unknown.Message}" : "."));
    }

    [Test]
    public async Task Text_a_uri_and_a_ticket_can_each_name_a_model()
    {
        var text = Flown("fly", "investigate this", "--model", "claude-sonnet-5-5");
        await Assert.That(text.Text).IsEqualTo("investigate this");
        await Assert.That(text.Model).IsEqualTo("claude-sonnet-5-5");

        var uri = Flown("fly", "--uri", "https://example.test/1", "--model", "claude-opus-5-5");
        await Assert.That(uri.Uri).IsEqualTo("https://example.test/1");
        await Assert.That(uri.Model).IsEqualTo("claude-opus-5-5");

        var ticket = Flown("fly", "--ticket", "ado#18492", "--work-kind", "investigate",
                           "--model", "claude-sonnet-5-5");
        await Assert.That(ticket.Id).IsEqualTo("18492");
        await Assert.That(ticket.WorkKind).IsEqualTo("investigate");
        await Assert.That(ticket.Model).IsEqualTo("claude-sonnet-5-5");
    }

    [Test]
    public async Task Nothing_named_is_null()
    {
        await Assert.That(Flown("fly", "investigate this").Model).IsNull()
            .Because("null is the loop's own choice, and a flight that chose nothing says nothing.");
    }

    [Test]
    public async Task A_trailing_flag_is_refused_rather_than_read_as_an_intent()
    {
        var parsed = CliArgs.Parse(["fly", "investigate this", "--model"]);
        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("--model");
    }

    [Test]
    public async Task On_any_other_verb_it_is_refused()
    {
        var parsed = CliArgs.Parse(["flights", "--model", "claude-sonnet-5-5"]);
        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>()
            .Because("stripped globally it would read as an instruction and do nothing.");
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains("--model");
    }
}
