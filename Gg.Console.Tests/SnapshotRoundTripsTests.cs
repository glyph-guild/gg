using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The model still survives being written down.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is serialized for real, twice.</b> <c>GG_STATE_DUMP</c> writes it to
/// disk when the console exits, and the bundle reads it - so a field this slice
/// adds that cannot be written is a crash on the way out, in the hook whose
/// whole purpose is proving what the surviving model was.
/// </para>
/// <para>
/// <b>Source-generated, which is the part that bites.</b>
/// <c>AppStateJsonContext</c> is AOT-safe and knows only the shapes it was told
/// about; a dictionary or a list of something new is exactly the kind of member
/// that compiles, runs in a test that never serializes, and throws in the
/// published binary.
/// </para>
/// </remarks>
public class SnapshotRoundTripsTests
{
    private static FlightSummary Flight(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = "a flight",
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

    private static AppState Full() => new()
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "a", FlightNumber = FlightRef.Format(1), Name = "waiting",
                Reason = QueueReason.AwaitingDecision,
                Since = new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.Zero),
            },
        ],
        Flights = new FlightList { Flights = [Flight("a", 1)] },
        Logs = new Dictionary<string, FlightLog>(StringComparer.Ordinal) { ["a"] = Log("a", 1) },
        Flight = Flight("a", 1),
        FlightLog = Log("a", 1),
        Runners = new RunnerList { Runners = [] },
        Credentials = new CredentialList { Credentials = [] },
        Principal = "somebody",
    };

    [Test]
    public async Task Everything_this_step_added_survives_the_dump()
    {
        var there = AppStateJson.Deserialize(AppStateJson.Serialize(Full()));

        await Assert.That(there.Flights!.Flights.Count).IsEqualTo(1)
            .Because("the flight list is what makes an arrow key free, and a model that "
                   + "cannot be written down loses it on the way out.");
        await Assert.That(there.Logs.Count).IsEqualTo(1)
            .Because("a dictionary is exactly the member shape a source-generated context "
                   + "will refuse if it was not told - and it would refuse it in the "
                   + "published binary, not here.");
        await Assert.That(there.Logs["a"].FlightNumber).IsEqualTo(FlightRef.Format(1));
        await Assert.That(there.Flight!.FlightId).IsEqualTo("a");
        await Assert.That(there.FlightLog!.FlightId).IsEqualTo("a");
        await Assert.That(there.Credentials).IsNotNull();
    }

    [Test]
    public async Task An_empty_model_still_round_trips()
    {
        // THE BOOT THAT GOT NOTHING. A console that failed to load must still be
        // dumpable, because that is the state somebody would most want to see.
        var there = AppStateJson.Deserialize(AppStateJson.Serialize(new AppState()));

        await Assert.That(there.Queue).IsEmpty();
        await Assert.That(there.Logs).IsEmpty()
            .Because("empty rather than null, so a reader never has to ask which nothing it "
                   + "is - the same rule the panes follow.");
        await Assert.That(there.Flights).IsNull();
    }
}
