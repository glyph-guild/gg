namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.5-02</b> - quitting gg with agents alive asks first, naming them, because quitting ends
/// them.
/// </summary>
public class QuittingWithAgentsAliveAsksTests
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

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }

    [Test]
    public async Task The_first_q_asks_naming_the_agents_and_the_second_ends_them()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("plan · console", fixture.Speaks("a", "never", "b", "never"));
        fixture.Agent("Claude Code", fixture.Speaks("a", "never", "b", "never"));
        _ = fixture.Mux.TakeWanted();

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.Quit, state),
            state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor(), mux: fixture.Mux).Run(new AppState());

        await Assert.That(ui.Seen).Count().IsEqualTo(2)
            .Because("the first q did not quit.");
        await Assert.That(ui.Seen[1].Diagnosis).IsNotNull();
        await Assert.That(ui.Seen[1].Diagnosis!).Contains("plan · console, Claude Code");
        await Assert.That(ui.Seen[1].Diagnosis!).Contains("Press q again to quit");
        await Assert.That(fixture.Mux.Any).IsFalse()
            .Because("the second q quit, and quitting ended them.");
    }

    [Test]
    public async Task Anything_between_the_two_qs_keeps_the_agents()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("Claude Code", fixture.Speaks("a", "never", "b", "never"));
        _ = fixture.Mux.TakeWanted();

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.Quit, state),
            state => new UiOutcome(Command.AgentsEnded, state),
            state => new UiOutcome(Command.Quit, state),
            state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor(), mux: fixture.Mux).Run(new AppState());

        await Assert.That(ui.Seen).Count().IsEqualTo(4)
            .Because("a q after something else is asked again, not taken as the answer.");
    }

    [Test]
    public async Task With_no_agent_q_quits_at_once()
    {
        using var fixture = new MuxFixture();
        var ui = new ScriptedUi(state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor(), mux: fixture.Mux).Run(new AppState());

        await Assert.That(ui.Seen).Count().IsEqualTo(1);
    }
}
