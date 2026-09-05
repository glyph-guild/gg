using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A flight waiting on a person is in the pane called "flights needing me".
/// </summary>
/// <remarks>
/// <para>
/// <b>The reason the pane exists, and it could not happen.</b>
/// <c>QueueReason.AwaitingDecision</c> was declared, rendered, and produced by
/// nothing - because <c>ConsoleProjection.Queue</c> was given flights, logs and
/// runners, and a gate is in none of them. Step 0 measured a tenant with three
/// flights and an envelope in force being told <i>nothing needs you</i>.
/// </para>
/// <para>
/// <b>The gates were already loaded, six lines too late.</b>
/// <c>ConsoleStart.LoadAsync</c> built the queue and THEN fetched them for the
/// modal, so the console has held the answer at boot the whole time and never
/// showed it. Nothing about this needs a request that was not already made.
/// </para>
/// <para>
/// <b>And it unblocks the rest of the slice.</b> Nothing below the queue is
/// reachable while nothing can be selected: the Flight pane, the evidence pane
/// and every read this slice adds hang off a selected row. This is the row that
/// makes the plan's other findings reachable, which is why it is step 2's first
/// job rather than its second.
/// </para>
/// </remarks>
public class TheQueueShowsADecisionTests
{
    private static readonly DateTimeOffset Asked =
        new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

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

    private static FlightLog Quiet(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Entries = [new FlightLogEntry
        {
            At = DateTimeOffset.UnixEpoch, Kind = "lease-granted", Detail = "{}",
        }],
    };

    private static PendingGate Gate(int number, string obligation = "somebody-decides") => new()
    {
        FlightNumber = FlightRef.Format(number),
        ObligationId = obligation,
        Approver = "platform-oncall",
        Branch = null,
        Commit = null,
        ManifestHash = new string('e', 64),
        Condition = "loop asked for a decision",
        Because = "the loop asked for a decision it is not allowed to make: which rule wins?",
        AwaitingSince = Asked,
        Attempt = 1,
    };

    [Test]
    public async Task A_flight_with_an_open_gate_is_a_row()
    {
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "waiting on somebody")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal) { ["a"] = Quiet("a", 1) },
            new RunnerList { Runners = [] },
            new GateList { Gates = [Gate(1)] });

        await Assert.That(queue.Count).IsEqualTo(1)
            .Because("a person is being asked something about this flight, which is the whole "
                   + "of what this pane is for.");
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.AwaitingDecision);
        await Assert.That(queue[0].Since).IsEqualTo(Asked)
            .Because("since WHEN somebody has been waiting, which is what the default sort "
                   + "orders on - not when the flight was opened.");
    }

    [Test]
    public async Task A_flight_nobody_is_waiting_on_is_still_not_a_row()
    {
        // THE LIVENESS TWIN, and the rule the pane turns on. "Flights needing
        // me. Not a flight list." A healthy flight with no gate is countable,
        // not readable - get this backwards and the console becomes the
        // dashboard the usage model rejects.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "running fine")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal) { ["a"] = Quiet("a", 1) },
            new RunnerList { Runners = [] },
            new GateList { Gates = [] });

        await Assert.That(queue).IsEmpty();
    }

    [Test]
    public async Task Two_gates_on_one_flight_are_one_row()
    {
        // A FLIGHT CAN WAIT ON MORE THAN ONE PERSON, and the queue is a list of
        // flights rather than a list of gates - so two open gates on one flight
        // is one row, since when the FIRST of them started waiting. Listing it
        // twice would make a person answer one and still see it.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "doubly gated")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal) { ["a"] = Quiet("a", 1) },
            new RunnerList { Runners = [] },
            new GateList
            {
                Gates =
                [
                    Gate(1, "reversibility-plan") with { AwaitingSince = Asked.AddHours(2) },
                    Gate(1, "somebody-decides"),
                ],
            });

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue[0].Since).IsEqualTo(Asked)
            .Because("waiting since the first of them, because that is how long somebody has "
                   + "been waiting on this flight.");
    }

    [Test]
    public async Task A_gate_naming_a_flight_this_tenant_cannot_see_is_not_a_row()
    {
        // A ROW HAS TO NAME A FLIGHT. The gate list and the flight list are two
        // reads, so they can disagree - and a row built from a gate alone would
        // have no name, no id and nothing to select. Skipped rather than
        // half-built.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal),
            new RunnerList { Runners = [] },
            new GateList { Gates = [Gate(99)] });

        await Assert.That(queue).IsEmpty();
    }

    [Test]
    public async Task A_decision_outranks_trouble_on_the_same_flight()
    {
        // ONE ROW PER FLIGHT, and when a flight is both gated and stranded the
        // reason shown is the one a person can DO something about. Two rows for
        // one flight is the same flight answered twice.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "gated and stranded")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = new FlightLog
                {
                    FlightId = "a",
                    FlightNumber = FlightRef.Format(1),
                    Entries =
                    [
                        new FlightLogEntry { At = Asked, Kind = "lease-expired", Detail = "{}" },
                        new FlightLogEntry { At = Asked, Kind = "lease-expired", Detail = "{}" },
                    ],
                },
            },
            new RunnerList { Runners = [] },
            new GateList { Gates = [Gate(1)] });

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.AwaitingDecision)
            .Because("somebody being asked something is actionable; a lease that expired "
                   + "twice is a diagnosis. The row says the thing the reader can answer.");
    }
}
