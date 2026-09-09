using Gg.Cli;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// Which of the three sources answered, and what it is overriding.
/// </summary>
/// <remarks>
/// <para>
/// <b>Precedence is only safe if it is visible.</b> A person edits the file,
/// nothing changes, and there is nowhere on any screen that says an environment
/// variable answered first — that is the silent no-op this codebase refuses
/// everywhere else, arriving through a feature meant to help.
/// </para>
/// <para>
/// <b>Environment, then file, then default.</b> The environment wins so that
/// nothing configured today stops working and so a one-off override keeps
/// working; the file is what an operator keeps; the default is what a machine
/// nobody configured uses. Measured before choosing this order: four variables
/// are set anywhere at all, so the file is mostly filling a blank rather than
/// fighting a variable.
/// </para>
/// </remarks>
public class EveryValueSaysWhereItCameFromTests
{
    private static Configuration AFileSaying(string controlPlane) =>
        new() { ControlPlane = controlPlane };

    [Test]
    public async Task The_file_answers_when_the_environment_is_silent()
    {
        var resolved = Settings.Resolve(
            "GG_CONTROL_PLANE",
            AFileSaying("https://from-the-file.invalid"),
            environment: _ => null);

        await Assert.That(resolved.Value).IsEqualTo("https://from-the-file.invalid");
        await Assert.That(resolved.Source).IsEqualTo(SettingSources.File);
        await Assert.That(resolved.Shadowed).IsNull();
    }

    [Test]
    public async Task The_environment_answers_over_the_file_and_says_what_it_is_shadowing()
    {
        // THE ONE THAT MATTERS. Without Shadowed there is no way for a person
        // to find out why their edit did nothing.
        var resolved = Settings.Resolve(
            "GG_CONTROL_PLANE",
            AFileSaying("https://from-the-file.invalid"),
            environment: _ => "https://from-the-environment.invalid");

        await Assert.That(resolved.Value).IsEqualTo("https://from-the-environment.invalid");
        await Assert.That(resolved.Source).IsEqualTo(SettingSources.Environment);
        await Assert.That(resolved.Shadowed).IsEqualTo("https://from-the-file.invalid")
            .Because("the file's value is being ignored, and the person who wrote it is "
                   + "the one who needs to know.");
    }

    [Test]
    public async Task The_default_answers_when_nobody_said_anything()
    {
        var resolved = Settings.Resolve(
            "GG_CONTROL_PLANE", file: null, environment: _ => null);

        await Assert.That(resolved.Value).IsEqualTo("http://localhost:5199");
        await Assert.That(resolved.Source).IsEqualTo(SettingSources.Default);
    }

    [Test]
    public async Task A_variable_with_no_default_is_unset_rather_than_blank()
    {
        // Unset and "set to nothing" are different answers, and most of these
        // have no default at all - an unset GG_VCS_HOSTS means no adapters,
        // which is a real state rather than a missing one.
        var resolved = Settings.Resolve("GG_VCS_HOSTS", file: null, environment: _ => null);

        await Assert.That(resolved.Value).IsNull();
        await Assert.That(resolved.Source).IsEqualTo(SettingSources.Unset);
    }

    [Test]
    public async Task Every_setting_the_file_can_carry_resolves_from_it()
    {
        // ONE AT A TIME, so a member added to Configuration without a row in
        // the table is the one setting the file silently cannot carry.
        foreach (var member in Configuration.Members)
        {
            // A number for the one number, so `With` has something it can parse.
            var written = member.Variable == "GG_RUNNER_HOLD_SECONDS" ? "30" : "a-value";

            var carried = Settings.Resolve(
                member.Variable,
                Settings.With(new Configuration(), member.Variable, written),
                environment: _ => null);

            await Assert.That(carried.Source).IsEqualTo(SettingSources.File)
                .Because($"'{member.Variable}' is a member of Configuration, so the file "
                       + "has somewhere to put it and the resolution must read it.");
            await Assert.That(carried.Value).IsEqualTo(written)
                .Because($"'{member.Variable}' reads back as something other than what "
                       + "With wrote, so the pair disagree about which member they are.");
        }
    }

    [Test]
    public async Task A_path_root_or_a_signal_is_environment_only_and_says_so()
    {
        // Not an omission to be discovered. XDG_CONFIG_HOME decides where the
        // file itself lives, so a value inside it could not be read before it
        // was needed; GG_STATE_DUMP is one process telling another something.
        foreach (var variable in (string[])
                 ["XDG_CONFIG_HOME", "XDG_STATE_HOME", "XDG_CACHE_HOME", "GG_STATE_DUMP"])
        {
            var setting = ConsoleEnvironment.Read().SingleOrDefault(s => s.Name == variable);

            await Assert.That(setting).IsNotNull()
                .Because($"'{variable}' is read in production, so it belongs on the page.");
            await Assert.That(setting!.EnvironmentOnly).IsTrue()
                .Because($"'{variable}' cannot be put in the file, and a reader hunting for "
                       + "it needs the page to say so rather than to leave it out.");
        }
    }

    [Test]
    public async Task Every_setting_on_the_page_says_which_source_answered()
    {
        foreach (var setting in ConsoleEnvironment.Read())
        {
            await Assert.That(SettingSources.All).Contains(setting.Source)
                .Because($"'{setting.Name}' reports source '{setting.Source}', which is not "
                       + "one of the four.");
        }
    }

    [Test]
    public async Task Every_gg_variable_read_in_production_is_named_on_the_page()
    {
        // THE RATCHET. The page exists to say what this binary reads, and it
        // named ten of them while production read twenty - so the half a person
        // most needed (the runner's) was the half nobody could see.
        var named = ConsoleEnvironment.Read().Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        var found = Sources.EveryGgVariable();

        await Assert.That(found).IsNotEmpty()
            .Because("a scan that found nothing would make the assertion below vacuous.");

        var unnamed = found
            .Where(v => !named.Contains(v))
            .Where(v => !Sources.NotOnThePage.ContainsKey(v))
            .ToList();

        await Assert.That(unnamed).IsEmpty()
            .Because("these are read in production and appear on no surface: "
                   + string.Join(", ", unnamed));
    }

    [Test]
    public async Task The_scan_would_notice_a_variable_that_was_not_on_the_page()
    {
        // THE LIVENESS ANCHOR, for the reason EveryVerbIsDiscoverableTests
        // gives: a regex that matched nothing would make the walk above pass
        // for a page naming none of them.
        await Assert.That(Sources.EveryGgVariable()).Contains("GG_CONTROL_PLANE");
        await Assert.That(Sources.EveryGgVariable()).Contains("GG_POOL_ENDPOINT")
            .Because("this one is read through a constant rather than a literal, which is "
                   + "the shape the scan is most likely to miss.");
    }

    [Test]
    public async Task Every_exemption_still_names_a_variable_that_exists()
    {
        // A stale exemption is a hole held open for whatever is written next
        // under that name - VocabularyTests' argument for the same shape.
        var found = Sources.EveryGgVariable();

        foreach (var (variable, why) in Sources.NotOnThePage)
        {
            await Assert.That(found).Contains(variable)
                .Because($"'{variable}' is exempted for '{why}' and is no longer read "
                       + "anywhere, so the exemption should go.");
        }
    }
}
