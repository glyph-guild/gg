namespace Gg.Console.Tests;

/// <summary>
/// A click on the column's background folds it to one text column, and another click unfolds it
/// (owner, 2026-10-11: "it should take up just a single text column").
/// </summary>
/// <remarks>
/// <para>
/// <b>The background, not a row.</b> A click on gg, an agent, "+ new agent" or history still
/// switches to it; a click anywhere else in the column - the gap under gg, the blank rows, the
/// hint - folds or unfolds it.
/// </para>
/// <para>
/// <b>Folded, it still switches.</b> Each row keeps one cell of what it was: <c>g</c> for gg,
/// an agent's number, <c>+</c> and <c>H</c>, on the same rows, so a click lands where it did.
/// </para>
/// <para>
/// <b>One fold for both painters.</b> The mux holds it, the console's column reads it on its
/// tick, and the room an agent is given follows it - so gg and an agent agree on where the
/// column ends.
/// </para>
/// </remarks>
public class TheColumnFoldsToOneCellTests
{
    private static readonly MuxRow[] TwoAgents =
    [
        new(1, "Claude Code", TimeSpan.FromMinutes(3), Changed: false),
        new(2, "plan · console", TimeSpan.FromSeconds(9), Changed: true),
    ];

    [Test]
    public async Task Folded_every_row_is_one_cell_and_keeps_its_tab()
    {
        var open = MuxColumn.Lines(TwoAgents, MuxTab.Gg, height: 12);
        var folded = MuxColumn.Lines(TwoAgents, MuxTab.Gg, height: 12, folded: true);

        await Assert.That(folded.Select(l => l.Text.Length).Distinct()).IsEquivalentTo([1]);
        await Assert.That(folded.Select(l => l.Tab)).IsEquivalentTo(open.Select(l => l.Tab))
            .Because("a click lands on the same row folded or not.");
        await Assert.That(folded.Select(l => l.Text))
            .IsEquivalentTo((string[])["g", " ", "1", "2", "+", " ", " ", " ", " ", " ", " ", "H"]);
    }

    [Test]
    public async Task The_room_the_column_takes_follows_the_fold()
    {
        await Assert.That(MuxColumn.Room(new AppState())).IsEqualTo(MuxColumn.Width);
        await Assert.That(MuxColumn.Room(new AppState { MuxFolded = true })).IsEqualTo(1);
    }

    [Test]
    public async Task The_mux_tells_the_console_its_fold()
    {
        var mux = new Mux(terminal: () => null);

        mux.ToggleFolded();
        var state = mux.Fold(new AppState(), out _);

        await Assert.That(state.MuxFolded).IsTrue();
        await Assert.That(mux.PaneSize().Columns).IsEqualTo(120 - 1);
    }

    [Test]
    public async Task A_click_on_the_background_beside_an_agent_folds_it_and_the_agent_gets_the_room()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Agent("A",
            $"printf ready; while [ ! -f '{fixture.Flag("size")}' ]; do sleep 0.02; done; stty size; "
            + $"while [ ! -f '{fixture.Flag("end")}' ]; do sleep 0.02; done");

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("ready", StringComparison.Ordinal)))
            .IsTrue();

        // ROW SIX IS BLANK: gg, a gap, agent 1, "+ new agent", then nothing until the hint.
        fixture.Terminal.Type("\u001b[<0;10;6M");
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Folded, TimeSpan.FromMilliseconds(300))).IsFalse()
            .Because("a press alone folds nothing: the release would land on whatever the fold moved under it.");
        fixture.Terminal.Type("\u001b[<0;10;6m");

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Folded)).IsTrue();
        fixture.Raise("size");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains(" 119", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the agent is told the width the column gave back.");

        // AND BACK: the folded column is column one, and row six is still background.
        fixture.Terminal.Type("\u001b[<0;1;6M\u001b[<0;1;6m");
        await Assert.That(MuxFixture.Until(() => !fixture.Mux.Folded)).IsTrue();

        fixture.Raise("end");
        await showing.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Test]
    public async Task A_click_on_a_row_still_switches_and_does_not_fold()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Agent("A", $"printf ready; while [ ! -f '{fixture.Flag("end")}' ]; do sleep 0.02; done");

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("ready", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\u001b[<0;3;1M");

        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
        await Assert.That(fixture.Mux.Folded).IsFalse();
        fixture.Raise("end");
    }

    // --- found by the owner: a press folded the column, and the release landed on gg's content ---

    [Test]
    [Arguments(true, false, false, MuxColumnAct.None)]
    [Arguments(false, true, false, MuxColumnAct.Fold)]
    [Arguments(true, false, true, MuxColumnAct.Switch)]
    [Arguments(false, true, true, MuxColumnAct.None)]
    public async Task Gg_folds_on_the_finished_click_and_switches_on_the_press(
        bool pressed, bool clicked, bool overARow, MuxColumnAct act)
    {
        // TERMINAL.GUI MAKES A CLICK OF A PRESS AND A RELEASE, and sends it to the view under the
        // release. Folded on the press, that view was gg's content, which took focus mid-change and
        // threw "SetHasFocusFalse new focused view does not have focus".
        await Assert.That(MuxColumn.Mouse(pressed, clicked, overARow ? MuxTab.Gg : null)).IsEqualTo(act);
    }
}
