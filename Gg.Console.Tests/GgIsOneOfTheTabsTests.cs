namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.4-01</b> - choosing gg shows the console with the column, and the agents keep running
/// behind it. Since 2026-10-07 the column is there from launch, before any agent.
/// </summary>
public class GgIsOneOfTheTabsTests
{
    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public List<AppState> Seen { get; } = [];

        public UiOutcome Run(AppState state)
        {
            Seen.Add(state);
            return _script.Dequeue()(state);
        }
    }

    [Test]
    public async Task The_console_is_given_the_agents_and_they_run_while_it_is_up()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));
        _ = fixture.Mux.TakeWanted();

        var spoke = false;
        var ui = new ScriptedUi(
            state =>
            {
                // WHILE GG IS UP: the agent speaks, and the console carries on.
                fixture.Raise("a");
                spoke = MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("a-later", StringComparison.Ordinal));
                return new UiOutcome(Command.Quit, state);
            },
            state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor(), mux: fixture.Mux).Run(new AppState());

        await Assert.That(ui.Seen[0].Agents.Select(row => row.Label)).IsEquivalentTo(["A"])
            .Because("the column is drawn from the model, and the model is given the live agents.");
        await Assert.That(spoke).IsTrue()
            .Because("an agent keeps running while gg is the tab on screen.");
    }

    [Test]
    public async Task The_console_opens_in_the_mux_view_with_no_agents()
    {
        // OWNER'S CALL, 2026-10-07: gg launches into the mux view - the column, gg chosen, no
        // agents - rather than hiding the column until the first agent starts. Slice sixty-nine
        // drew it only while an agent lived.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("new MuxColumnView(");
        await Assert.That(screen).DoesNotContain("State.Agents.Count > 0")
            .Because("the column does not wait for an agent: it is there from launch.");
        // ITS ROOM COMES FROM MuxColumn.Room SINCE 2026-10-10, which is the full width at all
        // times but one: the screensaver, which takes the whole terminal. An agent never
        // enters into it - AScreensaverCoversAnIdleConsoleTests holds the width.
        await Assert.That(screen).Contains("var left = MuxColumn.Room(State);")
            .Because("the console sits beside the column whether or not an agent lives.");
        await Assert.That(MuxColumn.Room(new AppState())).IsEqualTo(MuxColumn.Width);
    }

    [Test]
    public async Task The_column_with_no_agents_offers_gg_a_new_agent_and_history()
    {
        var lines = MuxColumn.Lines([], MuxTab.Gg, 12).Select(line => line.Text.Trim()).ToList();

        await Assert.That(lines).Contains("gg");
        await Assert.That(lines).Contains("+ new agent");
        await Assert.That(lines).Contains("history");
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
