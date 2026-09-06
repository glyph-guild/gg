using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A machine with no runner running is offered one, above the table and on a
/// key.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tab answers "is my runner up" and then leaves you in the terminal it
/// is drawing on.</b> The remedy is <c>gg runner up</c>, which is a command a
/// person cannot type while this console owns the screen - the same dead end
/// the sign-in modal exists to remove. So the console offers it, and starting
/// it is the shell's, because it spawns a child.
/// </para>
/// <para>
/// <b>Offered only when it applies.</b> Article XI: a key that appears to work
/// is worse than one that is not offered. A machine whose runner is up and
/// waiting has nothing to start, and a second one registered from the same
/// machine is litter in the fleet.
/// </para>
/// <para>
/// <b>What counts as not running is three cases, not one.</b> Nothing
/// registered here; registered and never heard from, which is what `gg runner
/// up' has been run once and the process is gone looks like; and registered
/// with a heartbeat that has gone stale. All three are answered by the same
/// command, which is why they are one predicate.
/// </para>
/// </remarks>
public class StartingTheRunnerFromTheConsoleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Mine = "01a06572-a784-72ae-b951-f147553cd48e";

    private static AppState Fleet(string? local, string? state, string? flight = null) => new()
    {
        ActiveTab = TabId.Runners,
        LocalRunnerId = local,
        Runners = new RunnerList
        {
            Runners = state is null
                ? []
                : [
                    new RunnerSummary
                    {
                        RunnerId = local ?? "someone-else",
                        Label = "this laptop",
                        State = state,
                        CurrentFlightNumber = flight,
                        LastHeartbeatAt = T0,
                    },
                  ],
        },
    };

    [Test]
    public async Task Nothing_registered_here_nothing_heard_from_and_gone_stale_are_all_offered()
    {
        var nothing = Fleet(local: null, state: null);
        var never = Fleet(Mine, state: null);
        var stale = Fleet(Mine, RunnerStates.Offline);

        foreach (var (what, state) in new[]
        {
            ("nothing registered here", nothing),
            ("registered and never heard from", never),
            ("registered and gone stale", stale),
        })
        {
            await Assert.That(Rows.NoRunnerHere(state)).IsTrue().Because(what);

            await Assert.That(Keymap.Resolve(KeyStroke.Char('s'), Keymap.For(state)))
                .IsEqualTo(Command.StartRunner)
                .Because($"{what}, so the key is live.");
        }
    }

    [Test]
    public async Task A_runner_that_is_up_is_not_offered_a_second()
    {
        foreach (var running in new[] { RunnerStates.Idle, RunnerStates.Busy })
        {
            var state = Fleet(Mine, running, flight: running == RunnerStates.Busy ? "GG-9" : null);

            await Assert.That(Rows.NoRunnerHere(state)).IsFalse();
            await Assert.That(Keymap.Resolve(KeyStroke.Char('s'), Keymap.For(state))).IsNull()
                .Because("a second runner registered from one machine is litter in the fleet, "
                       + "and a key that appears to work is worse than one not offered.");
        }
    }

    [Test]
    public async Task The_notice_is_above_the_table_and_names_the_key()
    {
        var pane = PaneText.Runners(Fleet(local: null, state: null));
        var notice = PaneText.RunnerNotice(Fleet(local: null, state: null));

        await Assert.That(notice).IsNotEmpty();
        await Assert.That(notice).Contains("s ")
            .Because("the remedy is a key on this screen, not a command a person cannot type "
                   + "while gg owns the terminal.");
        await Assert.That(pane.StartsWith(notice, StringComparison.Ordinal)).IsTrue()
            .Because("above the table, which is where somebody looks before they read a list "
                   + "of runners that does not have theirs in it.");

        await Assert.That(PaneText.RunnerNotice(Fleet(Mine, RunnerStates.Idle))).IsEmpty()
            .Because("nothing to say when it is up.");
    }

    [Test]
    public async Task Starting_it_is_the_shells_because_it_spawns_a_child()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.StartRunner);

        await Assert.That(Reducer.Reduce(
            Fleet(local: null, state: null), Command.StartRunner).Mode)
            .IsEqualTo(UiMode.Normal)
            .Because("a shell command with a second local effect would happen whether or not "
                   + "the child did.");
    }

    [Test]
    public async Task The_loop_starts_it_and_what_happened_reaches_the_model()
    {
        var asked = 0;

        var ui = new ScriptedUi(
            state => new UiOutcome(Command.StartRunner, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(
            ui,
            new NoEditor(),
            startRunner: state =>
            {
                asked++;
                return state with { LastRunner = "A runner is starting on this machine." };
            })
            .Run(Fleet(local: null, state: null));

        await Assert.That(asked).IsEqualTo(1);
        await Assert.That(final.LastRunner).IsNotNull();
        await Assert.That(ConsoleLoop.Said(new AppState(), final)).IsEqualTo(final.LastRunner)
            .Because("each arm records its outcome in its own field and Said takes whichever "
                   + "changed, so a new arm cannot forget to say anything.");
    }

    [Test]
    public async Task A_console_that_cannot_start_one_says_so_rather_than_blinking()
    {
        var ui = new ScriptedUi(
            state => new UiOutcome(Command.StartRunner, state),
            state => new UiOutcome(Command.Quit, state));

        var final = new ConsoleLoop(ui, new NoEditor()).Run(Fleet(local: null, state: null));

        await Assert.That(final.LastRunner).IsNotNull()
            .Because("the port not being passed is what `y' looked like for two slices, and "
                   + "EveryPortIsPassedTests is the guard - this is the sentence for the day "
                   + "somebody builds a loop without it.");
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
