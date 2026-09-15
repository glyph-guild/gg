using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A queue row can be something that is not a flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE EXCAVATION IS IN THE ROW, AND S37.0-04 IS WHAT FOUND THAT.</b>
/// S37.4-01 was written expecting a new <c>IQueueSort</c> to be enough — the
/// seam <c>QueueSort</c> says it left for exactly this. It is not:
/// <c>QueueRow</c> makes <c>FlightId</c> and <c>FlightNumber</c> required,
/// <c>QueueReason</c> has no member for a nomination, and <c>OldestFirst</c>
/// breaks its ties on both required members. A standing nomination has
/// neither — having no flight yet is the whole point of one.
/// </para>
/// <para>
/// <b>And the sort must still not know what a nomination is.</b> That is the
/// half worth guarding rather than the half that is easy: if ordering the
/// queue required a special case per kind of row, <c>QueueSort</c>'s promise —
/// that replacing it is a new class rather than an excavation — would be spent
/// on the first real urgency signal it was left for. So the row carries a key
/// and a reference that every kind of row has, and the ordering reads those.
/// </para>
/// <para>
/// <b>A <c>gated</c> row asks and an <c>auto</c> one does not.</b>
/// <c>hold-expired</c>'s argument, one noun over: a row nobody need answer,
/// sitting in a list of rows that need answering, is how the ones that do get
/// missed. An <c>auto</c> nomination is visible on the board without anybody
/// being asked about it.
/// </para>
/// </remarks>
public class TheBoardEntersTheQueueTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    private static NominationSummary ANomination(
        string mode = DestinationOpening.Gated,
        string? ending = null,
        DateTimeOffset? madeAt = null) => new()
    {
        NominationId = Guid.Parse("01a0792a-5e1f-7030-a5d8-52fd66e510b0"),
        Nominator = "flight:019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
        Subject = "flight:019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
        Version = "an-idempotency-key",
        WorkKind = "research-27",
        Mode = mode,
        State = ending ?? NominationStates.Standing,
        Ending = ending,
        MadeAt = madeAt ?? T0,
    };

    private static BoardPage ABoard(params NominationSummary[] rows) => new()
    {
        Nominations = rows,
        IncludedEnded = false,
    };

    private static IReadOnlyList<QueueRow> QueueOf(BoardPage board) =>
        ConsoleProjection.Queue(
            new FlightList { Flights = [] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal),
            new RunnerList { Runners = [] },
            gates: null,
            board: board);

    [Test]
    public async Task A_standing_gated_nomination_is_a_queue_row()
    {
        var queue = QueueOf(ABoard(ANomination()));

        await Assert.That(queue).Count().IsEqualTo(1)
            .Because("a nomination waiting for somebody is the queue's own subject - work "
                   + "that has not started, beside flights that have stopped.");
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.NominationStanding);
        await Assert.That(queue[0].Since).IsEqualTo(T0)
            .Because("how long it has stood is what the default sort orders on, and it is the "
                   + "only urgency signal a nomination has.");
    }

    [Test]
    public async Task The_row_is_not_a_flight_and_does_not_pretend_to_be_one()
    {
        var row = QueueOf(ABoard(ANomination()))[0];

        await Assert.That(row.FlightId).IsNull();
        await Assert.That(row.FlightNumber).IsNull()
            .Because("a blank number would make 'no flight yet' and 'a flight called nothing' "
                   + "the same value, and every pane that reads one would draw the second.");
        await Assert.That(row.NominationId).IsEqualTo(Guid.Parse(
            "01a0792a-5e1f-7030-a5d8-52fd66e510b0"))
            .Because("it is what `gg board open` takes, and what the modal will send.");
    }

    [Test]
    public async Task An_auto_nomination_is_on_the_board_and_not_in_the_queue()
    {
        var queue = QueueOf(ABoard(ANomination(mode: DestinationOpening.Auto)));

        await Assert.That(queue).IsEmpty()
            .Because("nobody need answer an auto row - admission opens it. A row nobody need "
                   + "answer sitting in a list of rows that need answering is how the ones "
                   + "that do get missed, which is hold-expired's argument one noun over.");
    }

    [Test]
    public async Task An_ended_nomination_is_not_in_the_queue_either()
    {
        var queue = QueueOf(ABoard(ANomination(ending: NominationEndings.Declined)));

        await Assert.That(queue).IsEmpty()
            .Because("it was answered. A queue of things that have been decided is a list of "
                   + "things somebody would have to read to discover they need nothing.");
    }

    [Test]
    public async Task The_ordering_is_total_over_rows_of_both_kinds()
    {
        // THE SAME KEYS, WHATEVER THE ROW IS ABOUT. Ties used to fall through
        // to the flight number and then the flight id, so a row with neither
        // sorted against null - which is not an ordering, it is whichever way
        // the comparer happened to answer.
        var flight = new QueueRow
        {
            Key = "019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
            Reference = "GG-42",
            Name = "a flight that stopped",
            Reason = QueueReason.AwaitingDecision,
            Since = T0,
            FlightId = "019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
            FlightNumber = "GG-42",
        };

        var nomination = QueueOf(ABoard(ANomination()))[0];

        var forwards = QueueSort.Default.Order([flight, nomination]);
        var backwards = QueueSort.Default.Order([nomination, flight]);

        await Assert.That(forwards.Select(r => r.Key))
            .IsEquivalentTo(backwards.Select(r => r.Key).ToList())
            .Because("a sort that is unstable under equal keys makes the cursor appear to "
                   + "move on its own, and these two rows share a Since by construction.");
    }

    [Test]
    public async Task The_ordering_does_not_know_what_a_nomination_is()
    {
        // THE HALF WORTH GUARDING. If ordering the queue needed a special case
        // per kind of row, QueueSort's promise - that replacing it is a new
        // class rather than an excavation - would be spent on the first real
        // urgency signal it was left for. The row carries a key and a reference
        // every kind of row has, and the ordering reads those.
        var source = ConsoleSource.Text("Gg.Console", "State/QueueSort.cs");

        await Assert.That(source).DoesNotContain("Nomination", StringComparison.Ordinal);
        await Assert.That(source).DoesNotContain("FlightNumber", StringComparison.Ordinal)
            .Because("a tie broken on a member only some rows have is a tie broken against "
                   + "null, which is not an ordering.");
        await Assert.That(source).Contains("Key", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_flight_row_still_carries_its_flight()
    {
        // THE OTHER DIRECTION, because the excavation must not cost the thing
        // the queue already did. Every pane that reads a flight off the
        // selected row still gets one when the row is about a flight.
        var queue = ConsoleProjection.Queue(
            new FlightList
            {
                Flights =
                [
                    new FlightSummary
                    {
                        FlightId = "019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
                        FlightNumber = "GG-42",
                        Name = "a flight that stopped",
                        Intent = new FlightIntent
                        {
                            Kind = FlightIntentKinds.Text,
                            Text = "a flight that stopped",
                        },
                        CreatedAt = T0,
                        RunnerProtocolVersion = 1,
                        FactVocabularyVersion = "0.1.0",
                        ConstitutionVersion = "1.0.0",
                        EnvelopeVersion = "none",
                        Attempts = 1,
                        Facts = [],
                    },
                ],
            },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal),
            new RunnerList { Runners = [] },
            gates: new GateList
            {
                Gates =
                [
                    new PendingGate
                    {
                        FlightNumber = "GG-42",
                        ObligationId = "a-person-looks",
                        Approver = "a-lead",
                        ManifestHash = "sha256:whatever",
                        AwaitingSince = T0,
                        Attempt = 1,
                        Because = "somebody has to look at it",
                    },
                ],
            },
            board: null);

        await Assert.That(queue).Count().IsEqualTo(1);
        await Assert.That(queue[0].FlightNumber).IsEqualTo("GG-42");
        await Assert.That(queue[0].Reference).IsEqualTo("GG-42")
            .Because("the reference is what a person types, and for a flight that is its "
                   + "number - so nothing about the flight rows changes.");
    }
}
