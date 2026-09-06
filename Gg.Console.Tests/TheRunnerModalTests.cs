using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// One modal for the runner on this machine: what it is doing, what it has
/// said, and the two things that can be done to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The sentence was on the activity line and it was cut off.</b> "A runner
/// is starting on this machine. Its output is in /Users/.../runner.log, and it
/// appears in this tab a beat after it registers" is three clauses on a strip
/// one line tall, and the path - the half somebody would go and read - was the
/// part that ran off the edge. The same fix the hand-flight refusal got.
/// </para>
/// <para>
/// <b>And a runner that is starting is not a runner that is missing.</b> The
/// notice above the table said nothing is registered here, which was true a
/// second ago and is the wrong thing to say to somebody who just pressed the
/// key that starts one.
/// </para>
/// <para>
/// <b>The log is a local file, which is the one thing a UI session may read.</b>
/// The exception is already written down for the live pane and is scoped to
/// exactly this: a file whose path the console holds. Nothing here spawns,
/// asks the control plane, or touches a credential - the starting and the
/// stopping are the loop's, between sessions, like every other write.
/// </para>
/// </remarks>
public class TheRunnerModalTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Mine = "01a06572-a784-72ae-b951-f147553cd48e";

    private static AppState Starting() => new()
    {
        ActiveTab = TabId.Runners,
        Here = new RunnerHere
        {
            Pid = 4242,
            LogPath = "/tmp/gg/runner.log",
            Log = ["listening on the pool", "registered as 01a06572"],
        },
    };

    private static AppState Running() => new()
    {
        ActiveTab = TabId.Runners,
        LocalRunnerId = Mine,
        Here = new RunnerHere { Pid = 4242, LogPath = "/tmp/gg/runner.log" },
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = Mine,
                    Label = "this laptop",
                    State = RunnerStates.Idle,
                    LastHeartbeatAt = T0,
                },
            ],
        },
    };

    [Test]
    public async Task Starting_it_opens_the_modal_rather_than_a_strip()
    {
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.StartRunner, state),
            state => new UiOutcome(Command.Quit, state));

        new ConsoleLoop(ui, new NoEditor(), startRunner: _ => Starting()).Run(new AppState());

        await Assert.That(ui.StatesSeen[1].Mode).IsEqualTo(UiMode.Runner);

        var modal = PaneText.Modal(ui.StatesSeen[1]);

        await Assert.That(modal).Contains("/tmp/gg/runner.log")
            .Because("the path is the half somebody goes and reads, and it is what ran off "
                   + $"the edge of the activity line. Modal:\n{modal}");
    }

    [Test]
    public async Task The_modal_shows_what_the_runner_has_said()
    {
        var modal = PaneText.Modal(Starting() with { Mode = UiMode.Runner });

        await Assert.That(modal).Contains("registered as 01a06572")
            .Because("watching it start is the reason to open this rather than a sentence "
                   + "saying it was asked to.");
    }

    [Test]
    public async Task A_runner_that_is_starting_is_not_reported_missing()
    {
        var notice = PaneText.RunnerNotice(Starting());

        await Assert.That(notice).DoesNotContain("No runner is registered")
            .Because("it is the wrong thing to say to somebody who pressed the key that "
                   + $"starts one a second ago. Notice: '{notice}'");
        await Assert.That(Rows.NoRunnerHere(Starting())).IsFalse()
            .Because("and the start key goes away while one is coming up, or a second press "
                   + "is a second runner.");
    }

    [Test]
    public async Task The_local_runner_opens_the_same_modal()
    {
        // ENTER ON THE RUNNERS TAB, which is where the cursor already rests on
        // this machine's row. The same modal, so there is one place that says
        // what the runner is doing and one place the two actions live.
        await Assert.That(Keymap.Resolve(
            KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, TabId.Runners)))
            .IsEqualTo(Command.ShowRunner);

        await Assert.That(Keymap.Resolve(
            KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, TabId.Flights)))
            .IsEqualTo(Command.ShowFlight)
            .Because("and enter still opens a flight everywhere else.");

        await Assert.That(Reducer.RunnerShown(Running()).Mode).IsEqualTo(UiMode.Runner);
    }

    [Test]
    public async Task The_modal_offers_a_restart_and_a_shutdown_and_one_way_out()
    {
        var context = new KeymapContext(UiMode.Runner);

        await Assert.That(Keymap.EscapeHatch(context)).IsEqualTo(KeyStroke.Esc);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('r'), context))
            .IsEqualTo(Command.RestartRunner);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), context))
            .IsEqualTo(Command.StopRunner);

        await Assert.That(ShellCommands.Handled).Contains(Command.RestartRunner);
        await Assert.That(ShellCommands.Handled).Contains(Command.StopRunner);
    }

    [Test]
    public async Task Stopping_and_restarting_reach_the_ports_and_say_so()
    {
        var did = new List<string>();

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.RestartRunner, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(
            ui,
            new NoEditor(),
            startRunner: state => { did.Add("start"); return state with { LastRunner = "starting" }; },
            stopRunner: state => { did.Add("stop"); return state with { LastRunner = "stopped" }; })
            .Run(Running());

        await Assert.That(did).IsEquivalentTo(new[] { "stop", "start" })
            .Because("a restart is a stop and a start, in that order, and doing it with one "
                   + "port would be a second way to spawn the same child.");
        await Assert.That(final.LastRunner).IsEqualTo("starting")
            .Because("what a person is told is what the second half of it did.");

        var stopped = new ScriptedUi(
            state => new UiOutcome(Command.StopRunner, state),
            state => new UiOutcome(Command.Quit, state));

        did.Clear();

        new ConsoleLoop(
            stopped,
            new NoEditor(),
            startRunner: state => { did.Add("start"); return state; },
            stopRunner: state => { did.Add("stop"); return state; })
            .Run(Running());

        await Assert.That(did).IsEquivalentTo(new[] { "stop" })
            .Because("and a shutdown is only the first half.");
    }

    [Test]
    public async Task What_the_log_reaches_is_a_local_file_and_nothing_else()
    {
        // THE EXCEPTION, HELD. The live pane's guard names the files a UI
        // session may reach; the runner's log is read on the same tick and has
        // to be in that list and clean, or the scope of the exception is a
        // comment again.
        var guard = Sources.Read("Gg.Console.Tests", "LiveStreamingTests.cs");

        await Assert.That(guard).Contains("RunnerLog.cs")
            .Because("a second thing the session reads is a second thing the scan has to "
                   + "cover, or it covers less than it says.");
    }

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public List<AppState> StatesSeen { get; } = [];

        public UiOutcome Run(AppState state)
        {
            StatesSeen.Add(state);
            return _script.Dequeue()(state);
        }
    }

    private sealed class NoEditor : IEditorSession
    {
        public string Edit(string initialText) => "";
    }
}
