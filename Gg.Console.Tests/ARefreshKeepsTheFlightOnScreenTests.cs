using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The automatic refresh re-reads the flight a person is looking at.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported from the live console: the flight modal empties every thirty
/// seconds.</b> The reload fetches the story, the reason and the takeover seed
/// for the QUEUE's selected row - and the modal shows the flights list's row,
/// which is a different cursor. On a healthy tenant the queue is empty, so the
/// reload asked for nothing, and the modal a person was reading went blank
/// while they read it.
/// </para>
/// <para>
/// <b>Two cursors, and this is the third thing that has confused them.</b>
/// <c>Selected</c> is the queue's and is null on a tenant with nothing waiting;
/// <c>Detailed</c> is the flights list's, and is what a modal draws. What the
/// refresh has to re-read is whatever is ON THE SCREEN, which is the modal's
/// flight while one is open and the queue's row otherwise.
/// </para>
/// </remarks>
public class ARefreshKeepsTheFlightOnScreenTests
{
    private static FlightSummary Flight(string number) => new()
    {
        FlightId = $"019fe815-6136-7518-bb57-b06d6d3f41{number.Length:00}",
        FlightNumber = number,
        Name = $"flight {number}",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "something" },
        CreatedAt = DateTimeOffset.Parse("2026-09-18T04:00:00Z"),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.31.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v7",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    private static QueueRow AQueueRow(string number) => new()
    {
        FlightId = "01a092f2-fba6-73a6-91e0-6b7f8f278991",
        FlightNumber = number,
        Key = "01a092f2-fba6-73a6-91e0-6b7f8f278991",
        Reference = number,
        Name = "waiting on somebody",
        Reason = QueueReason.AwaitingDecision,
        Since = DateTimeOffset.UnixEpoch,
    };

    private static AppState Reading(params string[] flights) => new()
    {
        Mode = UiMode.FlightDetail,
        ActiveTab = TabId.Flights,
        Flights = new FlightList { Flights = [.. flights.Select(Flight)] },
    };

    [Test]
    public async Task The_modals_flight_is_what_a_refresh_re_reads()
    {
        // THE DEFECT, IN ONE LINE. The queue is empty - the ordinary state of a
        // tenant with nothing waiting - and the modal is open on a flight.
        await Assert.That(ConsoleStart.OnTheScreen(Reading("GG-153"))).IsEqualTo("GG-153")
            .Because("a refresh that asks about nothing answers with nothing, and the modal "
                   + "somebody is reading goes blank while they read it.");
    }

    [Test]
    public async Task And_it_beats_the_queues_own_cursor()
    {
        // BOTH CURSORS AT ONCE, which is the case that says which one wins: a
        // queue row is selected AND a modal is open over it. What is on the
        // screen is the modal.
        var both = Reading("GG-153") with
        {
            Queue = [AQueueRow("GG-9")],
            SelectedRow = 0,
        };

        await Assert.That(ConsoleStart.OnTheScreen(both)).IsEqualTo("GG-153");
    }

    [Test]
    public async Task With_no_modal_the_queues_row_is_what_is_on_the_screen()
    {
        // UNCHANGED, and it has to be: the queue pane draws the reason and the
        // story for its own selected row, and this is what feeds them.
        var queue = new AppState
        {
            Queue = [AQueueRow("GG-9")],
            SelectedRow = 0,
        };

        await Assert.That(ConsoleStart.OnTheScreen(queue)).IsEqualTo("GG-9");
    }

    [Test]
    public async Task And_nothing_on_the_screen_asks_for_nothing()
    {
        await Assert.That(ConsoleStart.OnTheScreen(new AppState())).IsNull()
            .Because("a console with an empty queue and no modal has no flight to re-read, "
                   + "and asking for one would be a request for a row that is not there.");
    }

    [Test]
    public async Task A_modal_over_an_empty_flights_list_asks_for_nothing_either()
    {
        var empty = new AppState
        {
            Mode = UiMode.FlightDetail,
            Flights = new FlightList { Flights = [] },
        };

        await Assert.That(ConsoleStart.OnTheScreen(empty)).IsNull();
    }
}
