using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Pressing fly puts a notification in the corner at once, and it turns until
/// the flight has a number.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-10-09: "when we're flying something it would be ideal if
/// the notification comes up immediately with some animation while they're
/// being submitted".</b> The corner said nothing until the door had accepted
/// the flight AND it had been projected - up to thirty seconds after a key that
/// looked as if it had done nothing.
/// </para>
/// <para>
/// <b>Three steps, so the first is on the screen before the second starts.</b>
/// The press asks (pure: a launch and its corner entry), the shell starts the
/// send on a task, and the screen folds what lands. One entry for the whole
/// opening: submitting, then accepted, then the number.
/// </para>
/// </remarks>
public class TheCornerSaysAFlightIsOnItsWayTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static AppState Picked() =>
        Reducer.Browsed(
            ConsoleDoubles.KnowsNoKinds(new AppState { BrowseVisible = true, ActiveTab = TabId.Intents }),
            "a-tracker",
            new BrowseOutcome.Listed(new WorkItemPage(
                [new WorkItemSummary("18398", "A draft job fails", "New", "", null)], null)));

    private static AppState Pressed(IConsoleActions actions) =>
        ConsoleLoop.FlewPicked(Picked(), actions);

    // ---- the press ----

    [Test]
    public async Task The_press_puts_the_corner_up_before_anything_is_sent()
    {
        var actions = new ConsoleDoubles.Records();

        var pressed = Pressed(actions);

        await Assert.That(actions.Flown).IsEmpty()
            .Because("the press asks; the send is the launcher's, beside the screen - so the "
                   + "corner can be up before the request has even started.");
        await Assert.That(pressed.Launches).Count().IsEqualTo(1);

        var corner = pressed.Notifications.Single();

        await Assert.That(corner.Kind).IsEqualTo(NotificationKind.Submitting);
        await Assert.That(corner.Launch).IsEqualTo(pressed.Launches[0].Token);
        await Assert.That(string.Join(" ", PaneText.NotificationLines(pressed)))
            .Contains("A draft job fails")
            .Because("there is no number yet, so it is called what the person picked.");
    }

    [Test]
    public async Task The_launch_carries_what_was_picked_not_what_is_picked_later()
    {
        var actions = new ConsoleDoubles.Records();
        var pressed = Pressed(actions);

        // THE CURSOR MOVES, AND THE LIST IS GONE, WHILE THE REQUEST IS OUT.
        var moved = pressed with { Browse = null, BrowseSelected = 4 };
        _ = Sent.Inline(moved, actions);

        await Assert.That(actions.Flown.Single()).IsEqualTo(("a-tracker", "18398"));
    }

    [Test]
    public async Task A_notification_in_motion_turns()
    {
        var pressed = Pressed(new ConsoleDoubles.Records());

        await Assert.That(Launches.InMotion(pressed.Notifications.Single())).IsTrue();

        var titles = Enumerable.Range(0, PaneText.SpinnerFrames.Count * 2)
            .Select(pulse => PaneText.NotificationTitle(pressed with { LoadingPulse = pulse }))
            .ToList();

        await Assert.That(titles.All(t => t.Contains("submitting", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(titles.Distinct().Count()).IsEqualTo(PaneText.SpinnerFrames.Count)
            .Because("every frame is shown, two pulses each - a spinner that does not move "
                   + "reads as a corner that froze.");
    }

    // ---- the answer ----

    [Test]
    public async Task An_accepted_flight_turns_on_in_the_same_entry_until_it_has_a_number()
    {
        var actions = new ConsoleDoubles.Records();

        var answered = Sent.Inline(Pressed(actions), actions);

        await Assert.That(answered.Launches).IsEmpty();

        var corner = answered.Notifications.Single();

        await Assert.That(corner.Kind).IsEqualTo(NotificationKind.Accepted)
            .Because("one flight, one entry: the submitting one becomes accepted where it "
                   + "stands rather than a second stacking under it.");
        await Assert.That(corner.FlightId).IsEqualTo(ConsoleDoubles.Records.Opened);
        await Assert.That(Launches.InMotion(corner)).IsTrue()
            .Because("there is still no number, so it is still on its way.");
        await Assert.That(answered.Expecting.Single().Id).IsEqualTo(ConsoleDoubles.Records.Opened);
    }

    [Test]
    public async Task Its_number_arriving_replaces_the_entry_rather_than_adding_one()
    {
        var actions = new ConsoleDoubles.Records();
        var answered = Sent.Inline(Pressed(actions), actions) with
        {
            Flights = new FlightList { Flights = [] },
        };

        var expectations = new Expectations(
            Expectations.Looks(_ => Task.FromResult<FlightSummary?>(AFlight(1042))), new Clock());

        var seen = expectations.Advance(expectations.Advance(answered));

        var corner = seen.Notifications.Single();

        await Assert.That(corner.Kind).IsEqualTo(NotificationKind.FlightOpened);
        await Assert.That(corner.FlightNumber).IsEqualTo("GG-1042");
        await Assert.That(Launches.InMotion(corner)).IsFalse()
            .Because("and the turning stops, because it has arrived.");
    }

    [Test]
    public async Task A_refusal_stops_turning_and_says_why_and_still_reads_again()
    {
        var actions = new ConsoleDoubles.Records(refusing: true);

        var answered = Sent.Inline(Pressed(actions), actions);
        var corner = answered.Notifications.Single();

        await Assert.That(corner.Kind).IsEqualTo(NotificationKind.NotOpened);
        await Assert.That(corner.Said).IsNotNull();
        await Assert.That(Launches.InMotion(corner)).IsFalse();
        await Assert.That(answered.Refresh.Wanted).IsTrue()
            .Because("a POST that failed on the way back may have opened a flight anyway, and "
                   + "a sentence cannot say which - so the console reads again.");
    }

    [Test]
    public async Task An_item_that_has_flown_before_is_asked_about_and_the_entry_goes()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: "GG-14 flew this last week");

        var answered = Sent.Inline(Pressed(actions), actions);

        await Assert.That(actions.Flown).IsEmpty();
        await Assert.That(answered.Mode).IsEqualTo(UiMode.ConfirmFlight);
        await Assert.That(answered.Notifications).IsEmpty()
            .Because("the question on the screen is the answer; a corner still turning beside "
                   + "it would say something is being sent while nothing is.");
    }

    // ---- the launcher ----

    [Test]
    public async Task The_launcher_sends_each_launch_once_however_often_it_is_started()
    {
        var sent = 0;
        var gate = new TaskCompletionSource();
        var launcher = new Launcher(_ =>
        {
            Interlocked.Increment(ref sent);
            gate.Task.Wait();
            return new Launches.Answered(new Opening("Opened.", "f-1"));
        });

        var pressed = Pressed(new ConsoleDoubles.Records());

        // A LAUNCH TO SEND, OR THE WAIT BELOW NEVER ENDS. A press that asked
        // for nothing starts nothing, and Landed would stay false for ever -
        // which is how this class once spun a test process at two cores.
        await Assert.That(pressed.Launches).Count().IsEqualTo(1);

        launcher.Start(pressed);
        launcher.Start(pressed);

        gate.SetResult();
        while (!launcher.Landed)
        {
            await Task.Yield();
        }

        var folded = launcher.Fold(pressed);

        await Assert.That(sent).IsEqualTo(1)
            .Because("a session rebuilt mid-send starts it again; the token is what stops a "
                   + "second flight.");
        await Assert.That(folded.Notifications.Single().Kind).IsEqualTo(NotificationKind.Accepted);
        await Assert.That(launcher.Fold(folded)).IsSameReferenceAs(folded)
            .Because("folded once, and then there is nothing left to fold.");
    }

    [Test]
    public async Task A_send_that_throws_is_a_sentence_in_the_corner()
    {
        var launcher = new Launcher(_ => throw new InvalidOperationException("the wire fell over"));
        var pressed = Pressed(new ConsoleDoubles.Records());

        // THE SAME GUARD, for the same never-ending wait.
        await Assert.That(pressed.Launches).Count().IsEqualTo(1);

        launcher.Start(pressed);
        while (!launcher.Landed)
        {
            await Task.Yield();
        }

        var corner = launcher.Fold(pressed).Notifications.Single();

        await Assert.That(corner.Kind).IsEqualTo(NotificationKind.NotOpened);
        await Assert.That(corner.Said!).Contains("the wire fell over");
    }

    [Test]
    public async Task With_a_launcher_the_loop_sends_nothing_itself()
    {
        var actions = new ConsoleDoubles.Records();
        var started = new List<Launch>();
        var launcher = new Launcher(launch =>
        {
            lock (started)
            {
                started.Add(launch);
            }

            return new Launches.Answered(new Opening("Opened.", "f-1"));
        });

        var ui = new ScriptedUi(
            _ => new UiOutcome(Command.FlyPicked, Picked()),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new ConsoleDoubles.Writes(""), actions: actions, launcher: launcher)
            .Run(new AppState());

        await Assert.That(actions.Flown).IsEmpty()
            .Because("the loop starts the send and goes straight back to the screen; it never "
                   + "waits on the door.");
        await Assert.That(ui.StatesSeen[1].Notifications.Single().Kind)
            .IsEqualTo(NotificationKind.Submitting)
            .Because("so the screen that comes back is already saying it is on its way.");
    }

    // ---- the corner keeps it ----

    [Test]
    public async Task Nothing_in_motion_ages_out_of_the_corner()
    {
        var clock = new Clock();
        var expectations = new Expectations(
            Expectations.Looks(_ => Task.FromResult<FlightSummary?>(null)), clock);

        var pressed = Pressed(new ConsoleDoubles.Records()) with { Expecting = [] };

        var state = expectations.Advance(pressed);
        clock.Now += Expectations.NotificationsLast * 3;
        state = expectations.Advance(state);

        await Assert.That(state.Notifications).Count().IsEqualTo(1)
            .Because("a corner that cleared itself halfway through a send would take away the "
                   + "one thing saying the key did something.");
    }

    private static FlightSummary AFlight(int number) => new()
    {
        FlightId = ConsoleDoubles.Records.Opened,
        FlightNumber = FlightRef.Format(number),
        Name = $"work {number}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = T0,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        internal List<AppState> StatesSeen { get; } = [];

        public UiOutcome Run(AppState state)
        {
            StatesSeen.Add(state);
            return _script.Dequeue()(state);
        }
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; set; } = T0;

        public DateTimeOffset UtcNow => Now;
    }
}
