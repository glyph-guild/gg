namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.1-01</b> - the mux holds several agents, and each one's emulator keeps receiving what its
/// child writes while another agent, or gg, is on screen.
/// </summary>
/// <remarks>
/// <b>Real children on real pseudo-terminals.</b> What is being proven is that a reader feeds an
/// emulator nobody is looking at, and only a child writing to a pty can show that.
/// </remarks>
public class AgentsOutliveTheScreenTests
{
    [Test]
    public async Task Two_agents_keep_writing_while_gg_is_on_screen()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));
        fixture.Agent("B", fixture.Speaks("b-first", "b", "b-later", "end"));

        await Assert.That(fixture.Mux.Rows().Select(row => row.Label)).IsEquivalentTo(["A", "B"]);

        // NOTHING IS SHOWN: this is gg on screen, with both agents behind it.
        fixture.Raise("a");
        fixture.Raise("b");

        await Assert.That(MuxFixture.Until(() =>
                fixture.Mux.Screen(1).Contains("a-later", StringComparison.Ordinal)
                && fixture.Mux.Screen(2).Contains("b-later", StringComparison.Ordinal)))
            .IsTrue()
            .Because("an agent's screen is current when somebody switches to it, so its reader "
                   + "keeps feeding its emulator whether or not it is shown.");
    }

    [Test]
    public async Task An_agent_keeps_writing_while_another_is_shown()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));
        fixture.Agent("B", fixture.Speaks("b-first", "b", "b-later", "end"));

        var showing = fixture.Showing(MuxTab.Agent(2));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("bar B", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Raise("a");
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("a-later", StringComparison.Ordinal)))
            .IsTrue()
            .Because("A is hidden behind B, and what it wrote meanwhile is on its screen.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }
}
