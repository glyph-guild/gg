namespace Gg.Console.Tests;

/// <summary>
/// Two defects found driving the real console with slice sixty-nine built: the new-agent menu's
/// <c>l</c> did nothing, and an agent started from the menu was shown again the moment the person
/// went back to gg.
/// </summary>
public class TheNewAgentMenuGoesWhereItSaysTests
{
    [Test]
    public async Task Plan_asks_the_shell_for_a_plan_agent()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 16);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("New agent", StringComparison.Ordinal)))
            .IsTrue();

        await Assert.That(fixture.Choose("plan several flights")).IsTrue();
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Plan)
            .Because("the menu says it plans with an agent, and only the shell can start one: "
                   + "leaving for it has no next tab, which the menu read as staying.");
    }

    [Test]
    public async Task Going_back_to_gg_does_not_show_the_agent_started_from_the_menu_again()
    {
        var bin = Directory.CreateTempSubdirectory("gg-claude-").FullName;
        var agent = Path.Combine(bin, "agent");
        File.WriteAllText(agent, "#!/bin/sh\necho started\nwhile :; do sleep 1; done\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(agent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        using var fixture = new MuxFixture(columns: 120, rows: 16, agentCommand: agent);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("New agent", StringComparison.Ordinal)))
            .IsTrue();

        await Assert.That(fixture.Choose("Claude Code here")).IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("started", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);

        await Assert.That(fixture.Mux.TakeWanted()).IsNull()
            .Because("the agent asked to be shown when it started, and it was: left standing, the "
                   + "shell showed it again at once, so ctrl-g 0 came straight back to it.");
    }
}
