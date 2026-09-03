namespace Gg.Cli.Tests;

/// <summary>
/// A word starting with <c>--</c> is an option somebody got wrong, never a
/// sentence they meant to fly.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>gg fly --help</c> opened a flight called <c>--help</c>.</b> The parser's
/// free-text arm accepts any single token, so every unrecognised option became an
/// intent instead of an error. It happened for real: a live tenant has a flight
/// whose intent is the string <c>--help</c>, opened by somebody trying to find out
/// what the verb does.
/// </para>
/// <para>
/// <b>Why this is worth a refusal rather than a note in the docs.</b> <c>fly</c>
/// is the one verb with a side effect a person cannot undo from the CLI, and
/// <c>--help</c> is what somebody types when they are least sure what it does. The
/// safest-feeling input performed the least reversible action.
/// </para>
/// <para>
/// <b>The free-text arm stays.</b> A text intent is a real feature and
/// <c>gg fly "fix the login bug"</c> has to keep working; the rule is about the
/// shape of the token, not about removing the arm. A sentence that happens to
/// begin with a dash is not something anybody types, and an option that happens
/// to be a real intent does not exist.
/// </para>
/// </remarks>
public class OptionsAreNotIntentsTests
{
    [Test]
    public async Task Fly_help_is_refused_rather_than_flown()
    {
        var action = CliArgs.Parse(["fly", "--help"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>()
            .Because("this opened a real flight on a live tenant, named '--help', because "
                   + "somebody wanted to know what the verb does.");
    }

    [Test]
    public async Task The_refusal_names_what_fly_actually_takes()
    {
        // What the person was asking for in the first place. A refusal that
        // only says "no" makes them go and find the readme.
        var action = CliArgs.Parse(["fly", "--help"]);

        var message = ((CliAction.Unknown)action).Message;

        await Assert.That(message).Contains("--uri");
        await Assert.That(message).Contains("--ticket");
    }

    [Test]
    public async Task Any_unrecognised_option_is_refused_not_flown()
    {
        // --help is the one that got noticed, not the only one. A typo, a flag
        // from another tool, and a shell-mangled argument all took the same
        // path into a flight.
        foreach (var option in (string[])["--halp", "--json", "-h", "--uri="])
        {
            await Assert.That(CliArgs.Parse(["fly", option]))
                .IsTypeOf<CliAction.Unknown>()
                .Because($"'{option}' is somebody getting an option wrong, not an intent.");
        }
    }

    [Test]
    public async Task A_sentence_is_still_a_sentence()
    {
        // THE ANCHOR. The free-text intent is a shipped feature and this must
        // not be the change that quietly removes it.
        var action = CliArgs.Parse(["fly", "fix the login bug"]);

        await Assert.That(action).IsTypeOf<CliAction.Fly>();
        await Assert.That(((CliAction.Fly)action).Text).IsEqualTo("fix the login bug");
    }

    [Test]
    public async Task A_dash_inside_the_sentence_is_left_alone()
    {
        // Only a LEADING double dash is an option. "re-run the importer" and
        // "fix the drop-down" are ordinary things to ask for, and a rule that
        // read any dash would refuse them.
        foreach (var text in (string[])["re-run the importer", "fix the drop-down"])
        {
            await Assert.That(CliArgs.Parse(["fly", text])).IsTypeOf<CliAction.Fly>()
                .Because($"'{text}' is a sentence with a hyphen in it, which is most sentences.");
        }
    }

    [Test]
    public async Task The_real_options_still_parse()
    {
        // The other anchor: refusing options must not refuse the options.
        await Assert.That(CliArgs.Parse(["fly", "--uri", "https://forge.example.invalid/a/b/pull/1"]))
            .IsTypeOf<CliAction.Fly>();
        await Assert.That(CliArgs.Parse(["fly", "--ticket", "tracker#42"]))
            .IsTypeOf<CliAction.Fly>();
    }
}
