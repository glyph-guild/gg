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
        var ui = new ScriptedUi(s => new UiOutcome(Command.Quit, s with { SelectedRow = 2 }));
        var final = new ConsoleLoop(ui, new RecordingEditor()).Run(new AppState());

        await Assert.That(final.SelectedRow).IsEqualTo(2);
        await Assert.That(ui.StatesSeen).Count().IsEqualTo(1);
    }

    [Test]
    public async Task TheUiIsDestroyedAndRebuiltAndNothingIsLost()
    {
        // The done criterion, against the FULL state rather than a token one.
        // The old version of this test carried three fields; the state now
        // carries four panes' worth, and "nothing is lost" is only worth
        // asserting over everything there is to lose.
        //
        // Compared as a document, so a field added later is covered by this
        // test on the day it is added rather than on the day somebody
        // remembers to extend the assertion.
        var full = StateGenerator.Next(new Random(31)) with { Notes = "before edit" };

        var ui = new ScriptedUi(
            _ => new UiOutcome(Command.OpenEditor, full),
            s => new UiOutcome(Command.Quit, s));
        var editor = new RecordingEditor();

        var final = new ConsoleLoop(ui, editor).Run(new AppState());

        // The editor received the model's text, not a view's.
        await Assert.That(editor.Received).IsEqualTo("before edit");

        await Assert.That(ui.StatesSeen).Count().IsEqualTo(2);
        var rebuilt = ui.StatesSeen[1];

        var expected = AppStateJson.Serialize(
            full with { Notes = "before edit\nappended while the UI was down" });
        await Assert.That(AppStateJson.Serialize(rebuilt)).IsEqualTo(expected)
            .Because("the second session is rebuilt from the surviving model and nothing else.");

        await Assert.That(final.Notes).IsEqualTo("before edit\nappended while the UI was down");
    }

    [Test]
    public async Task Every_generated_state_survives_a_child_process_running_over_the_terminal()
    {
        // The property form. One state surviving is an example; the claim is
        // that the model is the ONLY survivor, whatever it happens to hold.
        for (var seed = 0; seed < 100; seed++)
        {
            var state = StateGenerator.Next(new Random(seed));

            var ui = new ScriptedUi(
                _ => new UiOutcome(Command.OpenEditor, state),
                s => new UiOutcome(Command.Quit, s));

            new ConsoleLoop(ui, new PassThroughEditor()).Run(new AppState());

            await Assert.That(AppStateJson.Serialize(ui.StatesSeen[1])).IsEqualTo(AppStateJson.Serialize(state))
                .Because($"seed {seed} did not survive the UI being destroyed and rebuilt.");
        }
    }

    private sealed class PassThroughEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }
}
