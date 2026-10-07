namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.3-01</b> - ctrl-g then <c>0</c>, <c>1</c>-<c>9</c>, <c>+</c> or <c>H</c>, and a click on a
/// row, switch to what they name; the agent shown is told the width beside the column.
/// </summary>
public class SwitchingBetweenAgentsTests
{
    [Test]
    public async Task The_keys_after_ctrl_g_name_tabs()
    {
        await Assert.That(MuxColumn.Key((byte)'0', 2)).IsEqualTo(MuxTab.Gg);
        await Assert.That(MuxColumn.Key((byte)'2', 2)).IsEqualTo(MuxTab.Agent(2));
        await Assert.That(MuxColumn.Key((byte)'3', 2)).IsNull()
            .Because("no agent is on row three, and a key that names nothing does nothing.");
        await Assert.That(MuxColumn.Key((byte)'+', 2)).IsEqualTo(MuxTab.New);
        await Assert.That(MuxColumn.Key((byte)'H', 2)).IsEqualTo(MuxTab.History);
        await Assert.That(MuxColumn.Key((byte)'h', 2)).IsEqualTo(MuxTab.History);
    }

    [Test]
    public async Task In_gg_ctrl_g_arms_the_switch_and_the_keymap_binds_what_follows()
    {
        var state = new AppState
        {
            Agents = [new(1, "A", TimeSpan.Zero, false), new(2, "B", TimeSpan.Zero, false)],
        };

        await Assert.That(Keymap.Resolve(KeyStroke.Control('g'), KeymapContext.For(state)))
            .IsEqualTo(Command.ArmSwitch);

        var armed = Reducer.Reduce(state, Command.ArmSwitch);
        var context = KeymapContext.For(armed);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('0'), context)).IsEqualTo(Command.ShowGg);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('2'), context)).IsEqualTo(Command.ShowAgent2);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('3'), context)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('+'), context)).IsEqualTo(Command.ShowNewAgent);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('h'), context)).IsEqualTo(Command.ShowHistory)
            .Because("the console's letters arrive without their shift, so history is h here and H or h beside an agent.");
        await Assert.That(Keymap.Resolve(KeyStroke.Control('g'), context)).IsEqualTo(Command.ShowScreensaver)
            .Because("ctrl-g was the mark's key, and pressed twice it still is.");

        await Assert.That(ShellCommands.Handled).Contains(Command.ShowAgent2)
            .Because("showing an agent hands the terminal to the mux, which only the shell can do.");
        await Assert.That(MuxCommands.Tab(Command.ShowAgent2)).IsEqualTo(MuxTab.Agent(2));
        await Assert.That(Reducer.Reduce(armed, Command.ShowGg).Switching).IsFalse();
    }

    [Test]
    public async Task Beside_an_agent_ctrl_g_and_a_number_switches_and_zero_goes_back_to_gg()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));
        fixture.Agent("B", fixture.Speaks("b-first", "b", "b-later", "end"));

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("bar A", StringComparison.Ordinal)))
            .IsTrue();

        var from = fixture.Terminal.Painted.Length;
        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("2");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted[from..].Contains("bar B", StringComparison.Ordinal)))
            .IsTrue()
            .Because("ctrl-g then 2 shows the agent on row two.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task A_click_on_a_row_switches_to_it()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));
        fixture.Agent("B", fixture.Speaks("b-first", "b", "b-later", "end"));

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("bar A", StringComparison.Ordinal)))
            .IsTrue();

        // ROW FOUR IS AGENT TWO: gg, a gap, agent one, agent two.
        var from = fixture.Terminal.Painted.Length;
        fixture.Terminal.Type("\u001b[<0;5;4M");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted[from..].Contains("bar B", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\u001b[<0;5;1M");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg)
            .Because("a click on gg's row is gg.");
    }

    [Test]
    public async Task The_agent_is_told_the_width_beside_the_column()
    {
        using var fixture = new MuxFixture(columns: 70, rows: 12);
        fixture.Agent("A", $"stty size; while [ ! -f '{fixture.Flag("end")}' ]; do sleep 0.02; done");

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("11 40", StringComparison.Ordinal)))
            .IsTrue()
            .Because("seventy columns less the thirty the column takes, and twelve rows less the bar's one.");
    }
}
