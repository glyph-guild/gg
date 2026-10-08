namespace Gg.Console.Tests;

/// <summary>
/// <c>n</c> then <c>c</c> starts a Claude Code agent beside gg and shows it, as <c>n</c> then
/// <c>l</c> starts a plan session (owner's call, 2026-10-07).
/// </summary>
/// <remarks>
/// <b><c>c</c>, because the column's "+ new agent" menu already spells it that way.</b> One
/// letter for one act in both places.
/// </remarks>
public class AnAgentStartsFromTheNewFlightMenuTests
{
    [Test]
    public async Task C_in_the_compose_choice_starts_an_agent()
    {
        var resolved = Keymap.Resolve(
            KeyStroke.Char('c'), new KeymapContext(UiMode.ComposeChoice, TabId.Flights));

        await Assert.That(resolved).IsEqualTo(Command.StartClaudeCode);
    }

    [Test]
    public async Task C_in_the_work_kind_picker_starts_one_too()
    {
        // `n` opens the work-kind picker first on a tenant with kinds, which is every tenant in
        // the field: a key only the compose choice knew would be a key nobody finds - `l`'s lesson.
        var resolved = Keymap.Resolve(
            KeyStroke.Char('c'), new KeymapContext(UiMode.WorkKindChoice, TabId.Flights));

        await Assert.That(resolved).IsEqualTo(Command.StartClaudeCode);
    }

    [Test]
    public async Task It_is_the_shells_to_run_and_the_reducers_to_leave()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.StartClaudeCode)
            .Because("it hands the terminal to a child, which a UI session may never do.");

        var before = new AppState { Mode = UiMode.ComposeChoice };
        await Assert.That(Reducer.Reduce(before, Command.StartClaudeCode)).IsEqualTo(before);
    }

    [Test]
    public async Task The_loop_starts_the_agent_and_shows_it()
    {
        using var fixture = new MuxFixture(agentCommand: "/bin/cat");

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.StartClaudeCode, state with { Mode = UiMode.ComposeChoice }),
            state => new UiOutcome(Command.Quit, state),
            state => new UiOutcome(Command.Quit, state));

        var running = Task.Factory.StartNew(
            () => new ConsoleLoop(ui, new NoEditor(), mux: fixture.Mux).Run(new AppState()),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("Claude Code", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the agent is started and put on screen, not left behind gg.");
        await Assert.That(fixture.Mux.Rows().Select(row => row.Label)).IsEquivalentTo(["Claude Code"]);

        // BACK TO GG, and the menu it came from is closed.
        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(MuxFixture.Until(() => ui.Seen.Count >= 2)).IsTrue();
        await Assert.That(ui.Seen[1].Mode).IsEqualTo(UiMode.Normal);
        fixture.Mux.EndAll();
        await running.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public List<AppState> Seen { get; } = [];

        public UiOutcome Run(AppState state)
        {
            lock (Seen)
            {
                Seen.Add(state);
            }

            return _script.Dequeue()(state);
        }
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
