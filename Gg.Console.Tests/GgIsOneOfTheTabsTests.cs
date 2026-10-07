namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.4-01</b> - choosing gg shows the console with the column while agents live, and the agents
/// keep running behind it.
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
    public async Task The_console_draws_the_column_only_while_an_agent_lives()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("new MuxColumnView(");
        await Assert.That(screen).Contains("var alive = State.Agents.Count > 0;")
            .Because("with no agent there is no column at all, so a person who never starts one sees no change.");
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
