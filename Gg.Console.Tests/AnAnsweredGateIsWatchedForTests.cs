using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// Answering a gate hands the console back at once, and the gate is looked for
/// until it closes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The screen was held while the console watched.</b> A decision is a 202;
/// the gate closes a moment later, when the decision's cascade reaches the
/// receptor that closes it. So the verb's own patience - submit, then watch the
/// gate list for up to thirty seconds - ran between one UI session and the
/// next, with nothing on the screen, and then a whole reload ran on top of it.
/// </para>
/// <para>
/// <b>Now it submits, looks once, and names the gate.</b> A refusal is still
/// the door's 409 and still said at once. Anything else is watched for the way
/// an opened flight is: the gate list read until the gate has gone, the queue
/// folded from it, and a notification saying so.
/// </para>
/// </remarks>
public class AnAnsweredGateIsWatchedForTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 18, 14, 0, 0, TimeSpan.Zero);

    private const string Obligation = "somebody-looks";

    private static PendingGate AGate(string number = "GG-1") => new()
    {
        FlightNumber = number,
        ObligationId = Obligation,
        Approver = "a-lead",
        Because = "the change touches migrations, and somebody has to look at that",
        AwaitingSince = T0,
        ManifestHash = new string('a', 64),
        Attempt = 1,
    };

    private static Expectation Answered(string number = "GG-1") => new()
    {
        Kind = ExpectationKind.GateAnswered,
        Id = number,
        Obligation = Obligation,
    };

    [Test]
    public async Task Answering_hands_the_console_back_and_names_the_gate()
    {
        var (data, plane) = AConsolePlane.Console();
        plane.Gates.Add(AGate());
        var actions = new VerbConsoleActions(data, new NeverAsked());

        var answered = actions.Decide("GG-1", Obligation, approved: true, reason: null);

        await Assert.That(answered.Expected).IsEqualTo(Answered())
            .Because("the gate the door took an answer for is what the console now looks for.");
        await Assert.That(plane.Paths.Count(p => p == "/v1/gates")).IsLessThanOrEqualTo(2)
            .Because("one read to find the gate and one look after the answer - not thirty "
                   + "seconds of looking with the screen held.");
    }

    [Test]
    public async Task A_refused_answer_names_nothing_and_says_so_at_once()
    {
        // THE ANCHOR. Nothing waiting on that obligation is the verb's own
        // refusal, before anything is sent.
        var (data, _) = AConsolePlane.Console();
        var actions = new VerbConsoleActions(data, new NeverAsked());

        var answered = actions.Decide("GG-1", Obligation, approved: true, reason: null);

        await Assert.That(answered.Expected).IsNull();
        await Assert.That(answered.Said).Contains("was not answered");
    }

    [Test]
    public async Task The_loop_looks_for_the_gate_rather_than_re_reading_under_it()
    {
        var reloads = 0;

        var final = new ConsoleLoop(
                new ConsoleDoubles.TypesKeys(Command.ApproveGate),
                new ConsoleDoubles.NoEditor(),
                actions: new ConsoleDoubles.Records(),
                reload: current =>
                {
                    reloads++;
                    return current;
                })
            .Run(WithAGate() with { Mode = UiMode.GateDecision });

        await Assert.That(final.Expecting).Contains(Answered());
        await Assert.That(reloads).IsEqualTo(0)
            .Because("a reload the moment the door answers runs before the gate has closed.");
    }

    [Test]
    public async Task A_gate_that_has_closed_leaves_the_queue_and_says_so()
    {
        var clock = new Clock();
        var expectations = new Expectations(
            Expectations.Looks(_ => Task.FromResult<FlightSummary?>(null),
                () => Task.FromResult<GateList?>(new GateList { Gates = [] })),
            clock);

        var state = expectations.Advance(WithAGate() with { Expecting = [Answered()] });

        await Assert.That(state.Gates!.Gates).IsEmpty();
        await Assert.That(state.Queue).IsEmpty()
            .Because("the queue is what needs somebody, and an answered gate no longer does.");
        await Assert.That(state.Expecting).IsEmpty();
        await Assert.That(state.Notifications.Select(n => (n.Kind, n.FlightNumber, n.FlightId)))
            .IsEquivalentTo(new[] { (NotificationKind.GateAnswered, (string?)"GG-1", "f-1") })
            .Because("named by the flight's id where the list has it, so going to it lands.");
    }

    [Test]
    public async Task A_gate_still_listed_is_looked_for_again()
    {
        var clock = new Clock();
        var looks = 0;
        var expectations = new Expectations(
            Expectations.Looks(_ => Task.FromResult<FlightSummary?>(null), () =>
            {
                looks++;
                return Task.FromResult<GateList?>(new GateList { Gates = [AGate()] });
            }),
            clock);

        var state = expectations.Advance(WithAGate() with { Expecting = [Answered()] });
        clock.Now += Expectations.FirstGap;
        state = expectations.Advance(state);

        await Assert.That(looks).IsEqualTo(2);
        await Assert.That(state.Expecting.Count).IsEqualTo(1);
        await Assert.That(state.Queue.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_gate_that_never_closes_is_said()
    {
        var clock = new Clock();
        var expectations = new Expectations(
            Expectations.Looks(_ => Task.FromResult<FlightSummary?>(null),
                () => Task.FromResult<GateList?>(new GateList { Gates = [AGate()] })),
            clock);

        var state = expectations.Advance(WithAGate() with { Expecting = [Answered()] });

        while (clock.Now - T0 <= Expectations.Patience + TimeSpan.FromSeconds(1))
        {
            clock.Now += TimeSpan.FromMilliseconds(250);
            state = expectations.Advance(state);
        }

        await Assert.That(state.Expecting).IsEmpty();
        await Assert.That(state.Notifications.Select(n => n.Kind)).IsEquivalentTo(
            new[] { NotificationKind.GateStillWaiting });
    }

    [Test]
    public async Task What_a_gate_notification_says()
    {
        var answered = new AppState
        {
            Notifications =
            [
                new Notification
                {
                    Kind = NotificationKind.GateAnswered,
                    FlightId = "f-1",
                    FlightNumber = "GG-1",
                    Name = Obligation,
                },
            ],
        };
        var waiting = answered with
        {
            Notifications = [answered.Notifications[0] with { Kind = NotificationKind.GateStillWaiting }],
        };

        await Assert.That(PaneText.NotificationTitle(answered)).IsEqualTo("gate answered");
        await Assert.That(PaneText.NotificationLines(answered)).IsEquivalentTo(
            new[] { "GG-1 is waiting on nobody now", Obligation });
        await Assert.That(PaneText.NotificationTitle(waiting)).IsEqualTo("gate still showing");
        await Assert.That(PaneText.NotificationLines(waiting)[0]).Contains("answered");
    }

    [Test]
    public async Task Going_to_a_gate_notification_opens_its_flight()
    {
        // BY NUMBER TOO, because a gate list names flights by number and a
        // console that had not listed the flight yet could only name it so.
        var state = WithAGate() with
        {
            Notifications =
            [
                new Notification
                {
                    Kind = NotificationKind.GateAnswered,
                    FlightId = "GG-1",
                    FlightNumber = "GG-1",
                    Name = Obligation,
                },
            ],
        };

        var after = Reducer.Reduce(state, Command.GoToNotification);

        await Assert.That(after.Mode).IsEqualTo(UiMode.FlightDetail);
        await Assert.That(PaneText.Detailed(after)!.FlightNumber).IsEqualTo("GG-1");
    }

    /// <summary>A console with one flight, its gate, and the queue row that gate makes.</summary>
    private static AppState WithAGate() => new()
    {
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "f-1",
                    FlightNumber = "GG-1",
                    Name = "work",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
                    CreatedAt = T0,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.25.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v6",
                    Attempts = 1,
                    State = FlightStates.Open,
                    Facts = [],
                },
            ],
        },
        Runners = new RunnerList { Runners = [] },
        Gates = new GateList { Gates = [AGate()] },
        Queue =
        [
            new QueueRow
            {
                Key = "f-1",
                Reference = "GG-1",
                FlightId = "f-1",
                FlightNumber = "GG-1",
                Name = "work",
                Reason = QueueReason.AwaitingDecision,
                Since = T0,
            },
        ],
    };

    private sealed class Clock : IClock
    {
        public DateTimeOffset Now { get; set; } = T0;

        public DateTimeOffset UtcNow => Now;
    }

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("answering a gate asks for no secret.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("answering a gate asks for no line.");
    }
}
