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
}
