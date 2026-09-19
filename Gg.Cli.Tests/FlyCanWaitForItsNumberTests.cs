using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>`gg fly --wait` is read wherever a person types it.</summary>
public class FlyCanWaitForItsNumberTests
{
    [Test]
    [Arguments("fly|--wait|do the thing")]
    [Arguments("fly|do the thing|--wait")]
    [Arguments("fly|--ticket|ado#42|--wait")]
    public async Task The_flag_is_read_before_or_after_the_intent(string typed)
    {
        var action = CliArgs.Parse(typed.Split('|'));

        await Assert.That(action).IsTypeOf<CliAction.Fly>();
        await Assert.That(((CliAction.Fly)action).Wait).IsTrue()
            .Because("a trailing flag and a leading one are the same request.");
    }

    [Test]
    public async Task Without_it_nothing_waits()
    {
        var action = CliArgs.Parse(["fly", "do the thing"]);

        await Assert.That(((CliAction.Fly)action).Wait).IsFalse();
    }

    [Test]
    public async Task The_usage_says_it_is_there()
    {
        // THE USAGE RIDES ON A REFUSAL, which is where somebody reads it.
        var refused = CliArgs.Parse(["fly"]);

        await Assert.That(refused).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)refused).Message).Contains("--wait");
    }
}
