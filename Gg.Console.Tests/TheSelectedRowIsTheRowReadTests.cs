using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The detail under the queue is the selected row's, wherever the cursor is.
/// </summary>
/// <remarks>
/// <para>
/// <b>The boot read <c>queue[0]</c> and the arrow key read the selection.</b>
/// Two copies of one rule, and they disagreed the moment the boot became the
/// refresh: press a key on the fourth row and the flight pane showed the first
/// row's flight, under the fourth row's name. That is the answer
/// <c>Reducer.Select</c> calls the worst of the three, because it is the one a
/// person cannot see is wrong.
/// </para>
/// <para>
/// <b>So there is one rule now.</b> <c>Reducer.Detail</c> is what the arrow key
/// has always done, and the loader ends by applying it - which is also what
/// makes a refresh legible: the cursor stays, and the detail under it is
/// re-read rather than reset to the top.
/// </para>
/// </remarks>
public class TheSelectedRowIsTheRowReadTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task The_detail_follows_the_cursor_rather_than_the_top_of_the_queue()
    {
        var state = Loaded() with { SelectedRow = 2 };

        var detailed = Reducer.Detail(state);

        await Assert.That(detailed.Flight!.FlightId).IsEqualTo("f-2")
            .Because("the third row is selected, so the third row's flight is the detail.");
        await Assert.That(detailed.FlightLog!.FlightNumber).IsEqualTo(FlightRef.Format(3))
            .Because("and its log, out of the logs the boot already fetched.");
    }

    [Test]
    public async Task A_row_nothing_was_loaded_for_shows_nothing_rather_than_the_last_one()
    {
        // The rule Select already states: one flight's detail under another
        // flight's name is worse than an empty pane, because a person cannot
        // see that it is wrong.
        var state = Loaded() with
        {
            SelectedRow = 1,
            Flights = new FlightList { Flights = [] },
            Logs = new Dictionary<string, FlightLog>(StringComparer.Ordinal),
        };

        var detailed = Reducer.Detail(state);

        await Assert.That(detailed.Flight).IsNull();
        await Assert.That(detailed.FlightLog).IsNull();
    }

    [Test]
    public async Task An_empty_queue_has_no_detail_and_does_not_throw()
    {
        var detailed = Reducer.Detail(new AppState { SelectedRow = 4 });

        await Assert.That(detailed.Flight).IsNull();
        await Assert.That(detailed.FlightLog).IsNull();
    }

    private static AppState Loaded()
    {
        var rows = Enumerable.Range(0, 4).Select(at => new QueueRow
        {
            FlightId = $"f-{at}",
            FlightNumber = FlightRef.Format(at + 1),
            Name = $"flight {at}",
            Reason = QueueReason.AwaitingDecision,
            Since = T0,
        }).ToList();

        return new AppState
        {
            Queue = rows,
            Flights = new FlightList { Flights = [.. rows.Select(Summary)] },
            Logs = rows.ToDictionary(r => r.FlightId, Log, StringComparer.Ordinal),
        };
    }

    private static FlightSummary Summary(QueueRow row) => new()
    {
        FlightId = row.FlightId,
        FlightNumber = row.FlightNumber,
        Name = row.Name,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "why" },
        CreatedAt = T0,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    private static FlightLog Log(QueueRow row) => new()
    {
        FlightId = row.FlightId,
        FlightNumber = row.FlightNumber,
        Entries = [],
    };
}
