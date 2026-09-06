namespace Gg.Cli.Tests;

/// <summary>
/// That <c>gg update</c> is a verb a person can actually type.
/// </summary>
/// <remarks>
/// Separate from <c>UpdateTests</c>, which is about what the advice SAYS. This
/// is about the verb existing and being advertised — the 426 is about to name
/// it as the remedy for being too old, and a refusal that points at a verb
/// nothing lists is the same shrug it replaced.
/// </remarks>
public class UpdateVerbTests
{
    [Test]
    public async Task Update_is_a_verb_a_person_can_type_and_the_usage_says_so()
    {
        await Assert.That(CliArgs.Parse(["update"])).IsTypeOf<CliAction.Update>();

        var usage = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        await Assert.That(usage).Contains("gg update")
            .Because("the remedy for being behind cannot be a verb a person has to be told about "
                   + "out of band. CliArgsTests holds the other direction: this list may "
                   + "advertise nothing that does not work.");
    }

    [Test]
    public async Task The_verb_reads_json_like_every_other_reporting_verb()
    {
        // Being behind is something a script checks, and the right command
        // differs per machine - so this is exactly the verb somebody automates.
        var action = CliArgs.Parse(["update", "--json"]);

        await Assert.That(action).IsTypeOf<CliAction.Update>();
        await Assert.That(((CliAction.Update)action).Json).IsTrue();
    }
}
