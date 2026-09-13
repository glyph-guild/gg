using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A flight that stopped for want of a credential is in the pane called
/// "flights needing me".
/// </summary>
/// <remarks>
/// <para>
/// <b>The queue's own remark said this was coming and it never came.</b>
/// <c>QueueReason</c>: <i>Every value here is a condition step 3 actually
/// produces; nothing is here in anticipation. Credential resolution joins at
/// step 5 and is deliberately absent rather than stubbed.</i> Step 5 shipped.
/// The runner resolves credentials, the control plane records
/// <c>credential-unresolved</c> on the flight log, and the one pane a person
/// reads still says nothing needs them.
/// </para>
/// <para>
/// <b>It reaches a person through the queue rather than beside it.</b>
/// <c>Envelope</c>'s rule for the loop's question is the rule here: <i>what
/// routes a flight to a person is a gate; the gate list is fed by gates and the
/// queue a person reads is fed by the gate list. A surface beside all three
/// would be a second way for a flight to need somebody, and only one of them
/// has a reader.</i> So this is a row in the queue that already exists, read
/// off the log the console already loads - no new read, no fourth surface.
/// </para>
/// <para>
/// <b>Above the two below it, because it is the cause and they are
/// symptoms.</b> A flight nobody can resolve a credential for is taken,
/// refused and handed back by every runner that tries. The queue's own rule for
/// a flight that is two things at once is that the reason shown is <i>the one a
/// person can DO something about - the other is a diagnosis</i>, and "register
/// a credential" is the act. "Expired twice" is what it looks like from
/// outside.
/// </para>
/// </remarks>
public class AFlightWaitingOnACredentialIsARowTests
{
    private static readonly DateTimeOffset Failed =
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

    private static FlightLog Log(string id, int number, params FlightLogEntry[] entries) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Entries = entries,
    };

    private static FlightLogEntry Entry(string kind, DateTimeOffset at) => new()
    {
        At = at,
        Kind = kind,
        Detail = "local:acme/widgets",
    };

    [Test]
    public async Task A_flight_whose_credential_would_not_resolve_is_a_row()
    {
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "needs a token nobody gave it")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = Log("a", 1,
                    Entry("lease-granted", Failed.AddMinutes(-1)),
                    Entry(StoryKinds.CredentialUnresolved, Failed)),
            },
            new RunnerList { Runners = [] },
            new GateList { Gates = [] });

        await Assert.That(queue.Count).IsEqualTo(1)
            .Because("this flight cannot run until a person registers a credential, which is "
                   + "exactly what this pane is for.");
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.CredentialUnresolved);
    }

    [Test]
    public async Task It_has_been_waiting_since_the_first_time_it_could_not_resolve()
    {
        // SINCE THE FIRST, like a gate and unlike two expiries. Two expiries is
        // a PATTERN that became true at the second; a missing credential became
        // true at the first and has been true ever since. The default sort puts
        // the longest wait on top, and a flight blocked since yesterday
        // outranking one blocked a minute ago is the whole point of ordering on
        // this field.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "tried three times")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = Log("a", 1,
                    Entry(StoryKinds.CredentialUnresolved, Failed),
                    Entry(StoryKinds.CredentialUnresolved, Failed.AddHours(1)),
                    Entry(StoryKinds.CredentialUnresolved, Failed.AddHours(2))),
            },
            new RunnerList { Runners = [] },
            new GateList { Gates = [] });

        await Assert.That(queue.Count).IsEqualTo(1)
            .Because("three refusals of one flight is one row. Listing it three times would "
                   + "make a person register a credential and still see two.");
        await Assert.That(queue[0].Since).IsEqualTo(Failed);
    }

    [Test]
    public async Task The_cause_is_shown_rather_than_the_symptom()
    {
        // A CREDENTIAL-BLOCKED FLIGHT IS HANDED BACK BY EVERY RUNNER THAT TAKES
        // IT, so it accumulates the shapes of trouble the queue already knows.
        // Shown as "expired twice", a person goes and looks at runners. Shown as
        // what it is, they run one command.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "both at once")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = Log("a", 1,
                    Entry("lease-expired", Failed.AddMinutes(-2)),
                    Entry("lease-expired", Failed.AddMinutes(-1)),
                    Entry(StoryKinds.CredentialUnresolved, Failed)),
            },
            new RunnerList { Runners = [] },
            new GateList { Gates = [] });

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.CredentialUnresolved)
            .Because("register a credential is the act; expired twice is what it looks like "
                   + "from outside.");
    }

    [Test]
    public async Task A_decision_still_outranks_it()
    {
        // THE ONE THING ABOVE IT. A gate is a person being asked a question
        // directly, and the queue has always shown that first. A credential is
        // an act somebody performs; a gate is an answer only they can give.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "gated and blocked")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = Log("a", 1, Entry(StoryKinds.CredentialUnresolved, Failed)),
            },
            new RunnerList { Runners = [] },
            new GateList
            {
                Gates =
                [
                    new PendingGate
                    {
                        FlightNumber = FlightRef.Format(1),
                        ObligationId = "somebody-decides",
                        Approver = "platform-oncall",
                        Branch = null,
                        Commit = null,
                        ManifestHash = new string('e', 64),
                        Condition = "loop asked for a decision",
                        Because = "the loop asked for a decision it is not allowed to make",
                        AwaitingSince = Failed,
                        Attempt = 1,
                    },
                ],
            });

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue[0].Reason).IsEqualTo(QueueReason.AwaitingDecision);
    }

    [Test]
    public async Task A_flight_that_resolved_its_credentials_is_still_not_a_row()
    {
        // THE LIVENESS TWIN. "Flights needing me. Not a flight list." A flight
        // whose log says a lease was granted and nothing failed is countable,
        // not readable - and a projection that answered this one with a row
        // would be matching on the word rather than on the kind.
        var queue = ConsoleProjection.Queue(
            new FlightList { Flights = [Flight("a", 1, "running fine")] },
            new Dictionary<string, FlightLog>(StringComparer.Ordinal)
            {
                ["a"] = Log("a", 1, Entry("lease-granted", Failed)),
            },
            new RunnerList { Runners = [] },
            new GateList { Gates = [] });

        await Assert.That(queue).IsEmpty();
    }

    [Test]
    public async Task The_row_renders_in_words_rather_than_an_enum_name()
    {
        // Article XI, as PaneText.Reason already holds it: a reason nothing can
        // render THROWS rather than showing a blank cell that reads as "nothing
        // wrong". A new value added to the enum and not to the switch is a row
        // that halts the pane, so this is the half of the pair that has to
        // arrive with it.
        var words = PaneText.Reason(QueueReason.CredentialUnresolved);

        await Assert.That(words).IsNotEmpty();
        await Assert.That(words).Contains("credential");
    }
}
