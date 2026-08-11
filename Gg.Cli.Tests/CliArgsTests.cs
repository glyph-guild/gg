namespace Gg.Cli.Tests;

public class CliArgsTests
{
    [Test]
    public async Task NoArgumentsLaunchesTheConsole()
    {
        await Assert.That(CliArgs.Parse([])).IsTypeOf<CliAction.LaunchConsole>();
    }

    [Test]
    public async Task VersionFlagPrintsVersion()
    {
        await Assert.That(CliArgs.Parse(["--version"])).IsTypeOf<CliAction.PrintVersion>();
        await Assert.That(CliArgs.Parse(["-v"])).IsTypeOf<CliAction.PrintVersion>();
    }

    [Test]
    public async Task RunnerUpSpawnsTheRunner()
    {
        await Assert.That(CliArgs.Parse(["runner", "up"])).IsTypeOf<CliAction.RunnerUp>();
    }

    [Test]
    public async Task RunnerServeIsTheChildProcessEntry()
    {
        await Assert.That(CliArgs.Parse(["runner", "serve"])).IsTypeOf<CliAction.RunnerServe>();
    }

    [Test]
    public async Task AnythingElseIsUnknownWithUsage()
    {
        var action = CliArgs.Parse(["frobnicate"]);
        await Assert.That(action).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)action).Message).Contains("usage");
    }

    // ---- the verbs landing at step 4a ----

    [Test]
    public async Task FlyTakesWhatAPersonTyped()
    {
        var action = CliArgs.Parse(["fly", "fix the login bug"]);

        await Assert.That(action).IsTypeOf<CliAction.Fly>();
        await Assert.That(((CliAction.Fly)action).Text).IsEqualTo("fix the login bug");
        await Assert.That(((CliAction.Fly)action).Uri).IsNull();
    }

    [Test]
    public async Task FlyTakesAUriInstead()
    {
        // The typed reference the three-field intent exists for. One payload,
        // never both - the parser refuses rather than choosing.
        var action = CliArgs.Parse(["fly", "--uri", "https://example.invalid/issues/7"]);

        await Assert.That(((CliAction.Fly)action).Uri).IsEqualTo("https://example.invalid/issues/7");
        await Assert.That(((CliAction.Fly)action).Text).IsNull();
    }

    [Test]
    public async Task FlyRefusesToTakeBoth()
    {
        var action = CliArgs.Parse(["fly", "some words", "--uri", "https://example.invalid/x"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>()
            .Because("which one wins would be decided by whoever wrote the parser.");
    }

    [Test]
    public async Task FlyWithNothingToActOnIsRefused()
    {
        await Assert.That(CliArgs.Parse(["fly"])).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task ShowTakesEitherFormOfReference()
    {
        await Assert.That(((CliAction.Show)CliArgs.Parse(["show", "GG-42"])).Reference).IsEqualTo("GG-42");
        await Assert.That(((CliAction.Show)CliArgs.Parse(["show", "019fe815-6136-7518-bb57-b06d6d3f411a"])).Reference)
            .IsEqualTo("019fe815-6136-7518-bb57-b06d6d3f411a");
    }

    [Test]
    public async Task ShowWithoutAReferenceIsRefused()
    {
        await Assert.That(CliArgs.Parse(["show"])).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task LogTakesAReference()
    {
        await Assert.That(((CliAction.Log)CliArgs.Parse(["log", "GG-42"])).Reference).IsEqualTo("GG-42");
    }

    [Test]
    public async Task RunnersAndDoctorTakeNoArguments()
    {
        await Assert.That(CliArgs.Parse(["runners"])).IsTypeOf<CliAction.Runners>();
        await Assert.That(CliArgs.Parse(["doctor"])).IsTypeOf<CliAction.Doctor>();
    }

    [Test]
    public async Task FlightsListsThem()
    {
        await Assert.That(CliArgs.Parse(["flights"])).IsTypeOf<CliAction.Flights>();
    }

    // ---- --json, on every verb that produces a result ----

    [Test]
    public async Task EveryResultProducingVerbTakesJson()
    {
        // Not a flag on some of them. A person scripting against gg should
        // never have to find out which verbs learned it.
        foreach (var argv in (string[][])
                 [["flights"], ["show", "GG-42"], ["log", "GG-42"], ["runners"], ["doctor"],
                  ["fly", "words"]])
        {
            var action = CliArgs.Parse([.. argv, "--json"]);

            await Assert.That(action).IsAssignableTo<CliAction.IEmitsResult>()
                .Because($"'{string.Join(' ', argv)}' produces a structured result.");
            await Assert.That(((CliAction.IEmitsResult)action).Json).IsTrue();
        }
    }

    [Test]
    public async Task WithoutTheFlagTheSameVerbsRenderForAPerson()
    {
        foreach (var argv in (string[][])
                 [["flights"], ["show", "GG-42"], ["log", "GG-42"], ["runners"], ["doctor"],
                  ["fly", "words"]])
        {
            await Assert.That(((CliAction.IEmitsResult)CliArgs.Parse(argv)).Json).IsFalse();
        }
    }

    [Test]
    public async Task JsonIsPositionIndependent()
    {
        // gg --json show GG-42 and gg show GG-42 --json are the same command,
        // because a person will type both.
        await Assert.That(((CliAction.Show)CliArgs.Parse(["show", "--json", "GG-42"])).Json).IsTrue();
        await Assert.That(((CliAction.Show)CliArgs.Parse(["show", "GG-42", "--json"])).Json).IsTrue();
    }

    // ---- verbs whose feature does not exist yet do not exist yet ----

    [Test]
    public async Task CredentialAddTakesNoPositionalValue()
    {
        // This used to assert that the verb did not exist at all. It exists
        // now - and what survives from the original is the more important
        // half: a bare word after `credential add` is refused rather than read
        // as the secret. A positional value is in shell history and in ps
        // output before any code of ours has run.
        var action = CliArgs.Parse(["credential", "add", "A_PROVIDER_TOKEN"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>();
    }

    [Test]
    public async Task BundleIsAVerbNow()
    {
        // It graduated at step 9, which is what this pair of tests is for: a
        // verb moves from the forbidden list to the advertised one by being
        // built, and never the other way round without somebody noticing.
        await Assert.That(CliArgs.Parse(["bundle"])).IsTypeOf<CliAction.Bundle>();
        await Assert.That(((CliAction.Bundle)CliArgs.Parse(["bundle", "--json"])).Json).IsTrue();
    }

    [Test]
    public async Task AnUnknownVerbListsTheOnesThatAreReal()
    {
        var message = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        foreach (var verb in (string[])["fly", "show", "log", "runners", "doctor", "bundle",
                                        "login", "logout", "whoami", "version", "runner up",
                                        "credential add", "credential list", "credential rm"])
        {
            await Assert.That(message).Contains(verb)
                .Because($"'{verb}' works today and a person cannot be expected to guess it.");
        }
    }

    [Test]
    public async Task TheListOfRealVerbsAdvertisesNothingThatDoesNotWork()
    {
        // The other half. A usage string is a promise, and listing a verb that
        // is not implemented is the same lie as stubbing it.
        var message = ((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message;

        // 'credential' came off this list at step 5 and 'bundle' at step 9,
        // which is what the list is for: a verb graduates from here to the one
        // above by being built.
        foreach (var absent in (string[])["cancel", "approve"])
        {
            await Assert.That(message).DoesNotContain(absent)
                .Because($"'{absent}' does not exist yet, so advertising it is a promise gg cannot keep.");
        }
    }

    [Test]
    public async Task TheRefusalNamesWhatWasTyped()
    {
        // "unknown command" without saying which one makes a typo in a script
        // something you find by bisecting.
        await Assert.That(((CliAction.Unknown)CliArgs.Parse(["frobnicate"])).Message)
            .Contains("frobnicate");
    }
}
