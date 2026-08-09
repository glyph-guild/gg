namespace Gg.Console.Tests;

/// <summary>
/// The terminal-release invariant, at the seam: a UI session ends completely,
/// something else owns the terminal, a new session starts from the surviving
/// model — and nothing is lost.
/// </summary>
public class ConsoleLoopTests
{
    private sealed class ScriptedUi : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script;
        public List<AppState> StatesSeen { get; } = [];

        public ScriptedUi(params Func<AppState, UiOutcome>[] script) => _script = new(script);

        public UiOutcome Run(AppState state)
        {
            StatesSeen.Add(state);
            return _script.Dequeue()(state);
        }
    }

    private sealed class RecordingEditor : IEditorSession
    {
        public string? Received { get; private set; }

        public string Edit(string initialText)
        {
            Received = initialText;
            return initialText + "\nappended while the UI was down";
        }
    }

    [Test]
    public async Task QuitReturnsTheFinalState()
    {
        var ui = new ScriptedUi(s => new UiOutcome(Command.Quit, s with { SelectedFlight = 2 }));
        var final = new ConsoleLoop(ui, new RecordingEditor()).Run(new AppState());

        await Assert.That(final.SelectedFlight).IsEqualTo(2);
        await Assert.That(ui.StatesSeen).Count().IsEqualTo(1);
    }

    [Test]
    public async Task EditorRunsBetweenUiSessionsAndNothingIsLost()
    {
        var stateInsideFirstSession = new AppState
        {
            FocusedPane = PaneId.Notes,
            Flights = ["flight-1", "flight-2"],
            SelectedFlight = 1,
            Notes = "before edit",
        };

        var ui = new ScriptedUi(
            _ => new UiOutcome(Command.OpenEditor, stateInsideFirstSession),
            s => new UiOutcome(Command.Quit, s));
        var editor = new RecordingEditor();

        var final = new ConsoleLoop(ui, editor).Run(new AppState());

        // The editor received the model's text, not a view's.
        await Assert.That(editor.Received).IsEqualTo("before edit");

        // The second UI session was rebuilt from the surviving model:
        // everything except the edited notes is exactly what session one held.
        await Assert.That(ui.StatesSeen).Count().IsEqualTo(2);
        var rebuilt = ui.StatesSeen[1];
        await Assert.That(rebuilt.FocusedPane).IsEqualTo(PaneId.Notes);
        await Assert.That(rebuilt.Flights).IsEquivalentTo(stateInsideFirstSession.Flights);
        await Assert.That(rebuilt.SelectedFlight).IsEqualTo(1);
        await Assert.That(rebuilt.Notes).IsEqualTo("before edit\nappended while the UI was down");

        await Assert.That(final.Notes).IsEqualTo("before edit\nappended while the UI was down");
    }
}
