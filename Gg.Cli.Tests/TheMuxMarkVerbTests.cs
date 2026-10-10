namespace Gg.Cli.Tests;

/// <summary><c>gg mux mark</c>: the command a mux agent's Claude Code hooks run.</summary>
public class TheMuxMarkVerbTests
{
    [Test]
    public async Task It_parses_and_is_on_the_usage()
    {
        await Assert.That(CliArgs.Parse(["mux", "mark", "working"])).IsEqualTo(new CliAction.MuxMark("working"));
        await Assert.That(((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message).Contains("gg mux mark");
    }
}
