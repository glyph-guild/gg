using System.Text.Json.Nodes;

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
        var full = StateGenerator.Next(new Random(31));

        var ui = new ScriptedUi(
            _ => new UiOutcome(Command.OpenFlight, full),
            s => new UiOutcome(Command.Quit, s));
        var editor = new RecordingEditor();

        var final = new ConsoleLoop(ui, editor, actions: new SilentActions()).Run(new AppState());

        // The editor really was handed the terminal.
        await Assert.That(editor.Received).IsNotNull();

        await Assert.That(ui.StatesSeen).Count().IsEqualTo(2);
        var rebuilt = ui.StatesSeen[1];

        // The state the loop itself moved on, so the comparison is against what
        // the handover was supposed to produce rather than against the input.
        await Assert.That(Carried(rebuilt)).IsEqualTo(Carried(full))
            .Because("the second session is rebuilt from the surviving model and nothing else.");

        await Assert.That(final.LastFlightOpened).IsNotNull()
            .Because("the child process ran and its result crossed back in the model.");
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
                _ => new UiOutcome(Command.OpenFlight, state),
                s => new UiOutcome(Command.Quit, s));

            // MOVED OFF THE NOTES SCRATCHPAD, which was removed. `new flight` is
            // the better vehicle anyway: it is a real feature that really hands
            // the terminal to $EDITOR, rather than a field kept alive so this
            // property had something to carry.
            new ConsoleLoop(ui, new PassThroughEditor(), actions: new SilentActions())
                .Run(new AppState());

            await Assert.That(Carried(ui.StatesSeen[1])).IsEqualTo(Carried(state))
                .Because($"seed {seed} did not survive the UI being destroyed and rebuilt.");
        }
    }

    /// <summary>
    /// The model as a document, minus what this handover is SUPPOSED to write.
    /// </summary>
    /// <remarks>
    /// <b>Still a whole-document comparison, minus two named keys.</b> The
    /// scratchpad this test used to ride on wrote nothing, so the two states were
    /// byte-identical and the claim was easy to state. A real handover produces a
    /// result - that is what it is for - so the claim becomes "everything the
    /// command did not write survived, whatever it happens to hold". Excluding
    /// two keys by name keeps the part that mattered: a field added later is
    /// covered by this test on the day it is added, not the day somebody
    /// remembers to extend it.
    /// </remarks>
    private static string Carried(AppState state)
    {
        var document = JsonNode.Parse(AppStateJson.Serialize(state))!.AsObject();

        document.Remove(nameof(AppState.LastFlightOpened));
        document.Remove(nameof(AppState.LastAction));

        return document.ToJsonString();
    }

    private sealed class PassThroughEditor : IEditorSession
    {
        public string Edit(string initialText) => initialText;
    }

    /// <summary>Answers without reaching a control plane.</summary>
    private sealed class SilentActions : IConsoleActions
    {
        public string Fly(string intent) => "opened";

        public string FlyTicket(string provider, string id) =>
            Fly($"{provider}#{id}");
        public string Decide(string flight, string obligation, bool approved, string? reason) => "decided";
        public string AddCredential() => "added";
        public string Invite() => "invited";
    }
}
