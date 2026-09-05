using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// An arrow key is a reducer step and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 3, and the reason the boot loads what it loads.</b> No I/O inside a
/// UI session: reads happen in <c>ConsoleLoop</c>, between sessions, where the
/// writes already do. So detail for the selected row has to come from what boot
/// already has - and it does, because the boot fetches every flight's summary
/// in one list and every flight's log in the loop it was already running.
/// </para>
/// <para>
/// <b>Asserted over the real reducer with no ports at all.</b> There is nothing
/// here that could make a request; that is the point, and it is why this test
/// is pure while the panes' own are not.
/// </para>
/// </remarks>
public class SelectionMovesWithoutIoTests
{
    private static FlightSummary Flight(string id, int number, string name) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = name,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "why" },
        CreatedAt = new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    private static FlightLog Log(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Entries = [new FlightLogEntry
        {
            At = DateTimeOffset.UnixEpoch, Kind = "lease-granted", Detail = "{}",
        }],
    };

    private static AppState Loaded() => new()
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "a", FlightNumber = FlightRef.Format(1), Name = "first",
                Reason = QueueReason.AwaitingDecision,
                Since = new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.Zero),
            },
            new QueueRow
            {
                FlightId = "b", FlightNumber = FlightRef.Format(2), Name = "second",
                Reason = QueueReason.AwaitingDecision,
                Since = new DateTimeOffset(2026, 9, 5, 9, 30, 0, TimeSpan.Zero),
            },
        ],
        Flights = new FlightList
        {
            Flights = [Flight("a", 1, "first"), Flight("b", 2, "second")],
        },
        Logs = new Dictionary<string, FlightLog>(StringComparer.Ordinal)
        {
            ["a"] = Log("a", 1),
            ["b"] = Log("b", 2),
        },
        Flight = Flight("a", 1, "first"),
        FlightLog = Log("a", 1),
    };

    [Test]
    public async Task Moving_down_changes_which_flight_the_pane_shows()
    {
        var moved = Reducer.Reduce(Loaded(), Command.SelectNext);

        await Assert.That(moved.Selected!.FlightId).IsEqualTo("b");
        await Assert.That(moved.Flight!.FlightId).IsEqualTo("b")
            .Because("the pane follows the selection out of what boot loaded. Without this "
                   + "the row moves and the detail below it does not, which is worse than "
                   + "showing nothing.");
        await Assert.That(moved.FlightLog!.FlightId).IsEqualTo("b");
    }

    [Test]
    public async Task Moving_back_changes_it_back()
    {
        var there = Reducer.Reduce(Loaded(), Command.SelectNext);
        var back = Reducer.Reduce(there, Command.SelectPrevious);

        await Assert.That(back.Flight!.FlightId).IsEqualTo("a");
        await Assert.That(back.FlightLog!.FlightId).IsEqualTo("a");
    }

    [Test]
    public async Task A_row_the_boot_loaded_nothing_for_shows_nothing_rather_than_the_last_one()
    {
        // THE STALE-DETAIL TRAP. If the lookup misses, leaving the previous
        // flight in place would show one flight's detail under another flight's
        // name - which is the worst of the three options, because it is the one
        // a person cannot see is wrong.
        var thin = Loaded() with
        {
            Flights = new FlightList { Flights = [Flight("a", 1, "first")] },
            Logs = new Dictionary<string, FlightLog>(StringComparer.Ordinal) { ["a"] = Log("a", 1) },
        };

        var moved = Reducer.Reduce(thin, Command.SelectNext);

        await Assert.That(moved.Selected!.FlightId).IsEqualTo("b");
        await Assert.That(moved.Flight).IsNull()
            .Because("nothing was loaded for this row, and showing the previous row's flight "
                   + "under this row's name is a lie a person cannot catch.");
        await Assert.That(moved.FlightLog).IsNull();
    }
}
