using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A flight the runner gave back as <c>outstanding</c> is a queue row until something happens
/// to it (slice sixty-seven, S67.4-01).
/// </summary>
/// <remarks>
/// <b>Found on GG-968.</b> Its agent found no repository, asked how to reach the code, and the
/// runner released it <c>outstanding</c> - "somebody owes the flight an answer". Nothing said
/// so: it was not ready, had no gate and no ending, and the queue showed "nothing needs you".
/// The owner's decision: never ground it automatically, but show it.
/// </remarks>
public class AGivenBackFlightIsInTheQueueTests
{
    private static readonly DateTimeOffset Released = new(2026, 10, 7, 0, 40, 48, TimeSpan.Zero);

    private static FlightSummary Flight(string state = FlightStates.Open) => new()
    {
        FlightId = "a",
        FlightNumber = FlightRef.Format(968),
        Name = "ITN-60 · Storybook 10 upgrade",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "upgrade storybook" },
        CreatedAt = Released.AddMinutes(-2),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.1.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
        State = state,
    };

    private static FlightLogEntry Entry(string kind, DateTimeOffset at, string detail = "{}") => new()
    {
        At = at,
        Kind = kind,
        Detail = detail,
    };

    private static FlightLogEntry GivenBack(DateTimeOffset at, string disposition = "outstanding") =>
        Entry("lease-released", at,
            $$"""{"leaseId":"01a1","generation":"1","runnerId":"r","disposition":"{{disposition}}","detail":"I couldn't start: my working directory isn't a git repository"}""");

    private static IReadOnlyList<QueueRow> Queue(
        FlightSummary flight, GateList? gates = null, params FlightLogEntry[] entries) =>
        ConsoleProjection.Queue(
            new FlightList { Flights = [flight] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = new FlightLog { FlightId = "a", FlightNumber = flight.FlightNumber, Entries = entries },
            },
            new RunnerList { Runners = [] },
            gates ?? new GateList { Gates = [] });

    [Test]
    public async Task A_flight_given_back_outstanding_is_a_row_from_the_release()
    {
        var queue = Queue(Flight(), null,
            Entry("lease-granted", Released.AddMinutes(-1)),
            GivenBack(Released));

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.GivenBack);
        await Assert.That(queue[0].Since).IsEqualTo(Released);
        await Assert.That(PaneText.Reason(QueueReason.GivenBack)).IsEqualTo("given back · needs you");
    }

    [Test]
    public async Task A_release_that_owes_nobody_is_no_row()
    {
        var queue = Queue(Flight(), null,
            Entry("lease-granted", Released.AddMinutes(-1)),
            GivenBack(Released, disposition: "completed"));

        await Assert.That(queue.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_later_lease_clears_it()
    {
        var queue = Queue(Flight(), null,
            GivenBack(Released),
            Entry("lease-granted", Released.AddMinutes(5)));

        await Assert.That(queue.Count).IsEqualTo(0)
            .Because("somebody took it again, so what was owed is being worked on.");
    }

    [Test]
    public async Task An_ending_clears_it()
    {
        var queue = Queue(Flight(FlightStates.Grounded), null, GivenBack(Released));

        await Assert.That(queue.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_gate_on_the_same_flight_outranks_it()
    {
        var gates = new GateList
        {
            Gates =
            [
                new PendingGate
                {
                    FlightNumber = FlightRef.Format(968),
                    ObligationId = "somebody-decides",
                    Approver = "platform-oncall",
                    Branch = null,
                    Commit = null,
                    ManifestHash = new string('e', 64),
                    Condition = "loop asked for a decision",
                    Because = "the loop asked for a decision it is not allowed to make",
                    AwaitingSince = Released.AddMinutes(1),
                    Attempt = 1,
                },
            ],
        };

        var queue = Queue(Flight(), gates, GivenBack(Released));

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.AwaitingDecision);
    }
}
