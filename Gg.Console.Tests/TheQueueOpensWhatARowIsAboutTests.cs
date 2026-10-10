using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// `o` on a queue row opens what that row is about, and the pane that used to
/// sit beside the queue is gone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-10.</b> The flight half of the queue tab was one Label that
/// could not scroll: a flight with any history had what it was waiting on pushed
/// off the bottom. Rather than split it, reuse the flight modal - tabs, a log, a
/// scroll - "and have a shortcut to go there". Then: "we should use something
/// similar for nominations - anything to show details from queue".
/// </para>
/// <para>
/// <b>The modal reads the flights list's cursor</b>, so opening it from the
/// queue points that cursor at the queued flight first - and leaves the queue
/// the tab underneath, so escape comes back to it.
/// </para>
/// </remarks>
public class TheQueueOpensWhatARowIsAboutTests
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

    /// <summary>Three flights listed; the queue's cursor on the OLDEST.</summary>
    private static AppState OnTheQueue(params QueueRow[] rows) => new()
    {
        ActiveTab = TabId.Queue,
        Flights = new FlightList { Flights = [AFlight(1), AFlight(2), AFlight(3)] },
        FlightSelected = 0,
        Queue = rows.Length > 0 ? rows : [Queued(1)],
        SelectedRow = 0,
    };

    [Test]
    public async Task O_on_a_flight_row_opens_the_flight()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), KeymapContext.For(OnTheQueue())))
            .IsEqualTo(Command.ShowQueuedFlight);
        await Assert.That(Keymap.Hints(KeymapContext.For(OnTheQueue()))).Contains("open the flight");
    }

    [Test]
    public async Task It_opens_on_the_queued_flight_not_the_flights_tab_cursor()
    {
        // THE TWO CURSORS. The flights list's is on GG-3 (newest first), the
        // queue's on GG-1; the modal must be about the row somebody pressed on.
        var opened = Reducer.Reduce(OnTheQueue(), Command.ShowQueuedFlight);

        await Assert.That(opened.Mode).IsEqualTo(UiMode.FlightDetail);
        await Assert.That(FlightDetails.Title(opened)).StartsWith("GG-1");
        await Assert.That(opened.FlightTab).IsEqualTo(FlightTab.Details);
        await Assert.That(opened.ActiveTab).IsEqualTo(TabId.Queue)
            .Because("the queue stays underneath, so escape comes back to the row.");
    }

    [Test]
    public async Task Escape_comes_back_to_the_queue()
    {
        var back = Reducer.Reduce(Reducer.Reduce(OnTheQueue(), Command.ShowQueuedFlight), Command.CloseModal);

        await Assert.That(back.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(back.ActiveTab).IsEqualTo(TabId.Queue);
        await Assert.That(back.SelectedRow).IsEqualTo(0);
    }

    [Test]
    public async Task It_reads_the_story_as_opening_any_flight_does()
    {
        await Assert.That(ShellCommands.Reads).Contains(Command.ShowQueuedFlight)
            .Because("the modal's log is the flight's story, and opening it from the queue "
                   + "must fetch it the way the flights tab's enter does.");
    }

    [Test]
    public async Task A_row_whose_flight_is_not_listed_opens_nothing_and_says_why()
    {
        var unlisted = OnTheQueue(Queued(9));

        var pressed = Reducer.Reduce(unlisted, Command.ShowQueuedFlight);

        await Assert.That(pressed.Mode).IsEqualTo(UiMode.Normal)
            .Because("a modal titled for whatever the flights cursor was on would be a "
                   + "confident wrong answer.");
        await Assert.That(pressed.LastAction!).Contains("nothing to open");
    }

    [Test]
    public async Task O_on_a_nomination_opens_the_nomination()
    {
        var nomination = Guid.NewGuid();
        var state = OnTheQueue(new QueueRow
        {
            Key = nomination.ToString(),
            Reference = "work-item:18492",
            NominationId = nomination,
            Name = "a nomination",
            Reason = QueueReason.NominationStanding,
            Since = T0,
        });

        await Assert.That(Keymap.Resolve(KeyStroke.Char('o'), KeymapContext.For(state)))
            .IsEqualTo(Command.ShowQueueNomination)
            .Because("one key for 'show me this row', whichever kind of row it is.");
    }

    [Test]
    public async Task The_queue_has_no_flight_pane_beside_it()
    {
        // THE PANE IS GONE, and so is the work it cost: building its text was
        // measured at up to 1.9s for one Label assignment on a real tenant.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).DoesNotContain("PaneText.Flight(State)");
        await Assert.That(screen).Contains("queueTab.Add(_queuePane);");
    }
}
