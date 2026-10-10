using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The flight modal's gate tab shows the gate as the decision modal does, from
/// the gates list the console already holds, and answers it - for the flight
/// the modal is showing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-10:</b> the gate tab read "Why this flight is held has not
/// been read - press g to read it", and it should show the gate and the
/// decision options. That sentence came from the why read, made only on a key
/// press and for the queue's row; the gates list is read at boot and on every
/// refresh, and is what the decision modal renders.
/// </para>
/// <para>
/// <b>TWO CURSORS, and the answer must follow the one being read.</b> The modal
/// titles itself from the flights list; the decision used to answer the queue
/// row's gate. Opened from the Flights tab while the queue sat on another
/// flight, approving would have answered that other flight.
/// </para>
/// </remarks>
public class TheGateTabAnswersItsGateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static FlightSummary AFlight(int n) => new()
    {
        FlightId = AConsolePlane.Id(n),
        FlightNumber = FlightRef.Format(n),
        Name = $"work {n}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = T0.AddMinutes(n),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.42.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v7",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    private static PendingGate AGate(int n, string obligation) => new()
    {
        FlightNumber = FlightRef.Format(n),
        ObligationId = obligation,
        Approver = "platform-owner",
        ManifestHash = new string('a', 64),
        Because = $"{obligation} is decided by a person",
        AwaitingSince = T0,
        Attempt = 1,
    };

    private static QueueRow Queued(int n) => new()
    {
        Key = AConsolePlane.Id(n),
        Reference = FlightRef.Format(n),
        FlightId = AConsolePlane.Id(n),
        FlightNumber = FlightRef.Format(n),
        Name = $"work {n}",
        Reason = QueueReason.AwaitingDecision,
        Since = T0,
    };

    /// <summary>
    /// The modal open on GG-1 (row 1 of two, newest first), while the queue's
    /// cursor is on GG-2 - each with a gate of its own.
    /// </summary>
    private static AppState OnGg1WhileTheQueueIsOnGg2(GateList? gates = null) => new()
    {
        Mode = UiMode.FlightDetail,
        FlightTab = FlightTab.Gate,
        ActiveTab = TabId.Flights,
        Flights = new FlightList { Flights = [AFlight(1), AFlight(2)] },
        FlightSelected = 1,
        Queue = [Queued(2)],
        SelectedRow = 0,
        Gates = gates ?? new GateList { Gates = [AGate(1, "plan-reviewed"), AGate(2, "widen-root")] },
    };

    [Test]
    public async Task The_tab_shows_this_flights_gate_without_asking_anybody()
    {
        var said = FlightDetails.Gate(OnGg1WhileTheQueueIsOnGg2());

        await Assert.That(said).Contains("plan-reviewed");
        await Assert.That(said).DoesNotContain("widen-root")
            .Because("that is the queue row's flight, not the one this modal is about.");
        await Assert.That(said).DoesNotContain("press g")
            .Because("the gates list is already held; nothing has to be read to say this.");
    }

    [Test]
    public async Task The_tab_names_the_keys_that_answer_it_and_they_resolve()
    {
        var state = OnGg1WhileTheQueueIsOnGg2();
        var context = KeymapContext.For(state);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('a'), context)).IsEqualTo(Command.ApproveGate);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('r'), context)).IsEqualTo(Command.RejectGate);
        await Assert.That(FlightDetails.Gate(state)).Contains("a approve")
            .Because("named from the bindings that resolve them, so the tab cannot advertise a "
                   + "key the modal would not answer.");
    }

    [Test]
    public async Task Approving_answers_the_flight_on_screen_not_the_queue_row()
    {
        var actions = new ConsoleDoubles.Records();
        var ui = new ScriptedUi(
            _ => new UiOutcome(Command.ApproveGate, OnGg1WhileTheQueueIsOnGg2()),
            s => new UiOutcome(Command.Quit, s));

        new ConsoleLoop(ui, new ConsoleDoubles.Writes(""), actions: actions).Run(new AppState());

        await Assert.That(actions.Decided.Single().Flight).IsEqualTo("GG-1");
        await Assert.That(actions.Decided.Single().Obligation).IsEqualTo("plan-reviewed")
            .Because("the queue's cursor was on GG-2, and answering that from a modal about "
                   + "GG-1 is the two-cursor defect with a write behind it.");
    }

    [Test]
    public async Task With_no_gate_on_this_flight_it_says_so_and_offers_no_answer()
    {
        var state = OnGg1WhileTheQueueIsOnGg2(new GateList { Gates = [AGate(2, "widen-root")] });

        await Assert.That(FlightDetails.Gate(state)).Contains("Nothing is waiting on you");
        await Assert.That(Keymap.Resolve(KeyStroke.Char('a'), KeymapContext.For(state))).IsNull()
            .Because("an approve key over a flight with nothing to approve is a dead key.");
    }

    [Test]
    public async Task With_nothing_read_it_says_that_rather_than_nothing_is_waiting()
    {
        var state = OnGg1WhileTheQueueIsOnGg2() with { Gates = null };

        var said = FlightDetails.Gate(state);

        await Assert.That(said).DoesNotContain("Nothing is waiting");
        await Assert.That(said).Contains("not been read");
    }

    private sealed class ScriptedUi(params Func<AppState, UiOutcome>[] script) : IUiSession
    {
        private readonly Queue<Func<AppState, UiOutcome>> _script = new(script);

        public UiOutcome Run(AppState state) => _script.Dequeue()(state);
    }
}
