using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A bundle taken from a model that is no longer mostly empty.
/// </summary>
/// <remarks>
/// <para>
/// <b>The redaction tests passed against a thin object.</b> Nine
/// <c>AppState</c> fields were at their defaults in the running product, so
/// every proof that a bundle carries nothing it should not was a proof about a
/// model with almost nothing in it. Step 2 fills it. This asks the same question
/// of the fuller one.
/// </para>
/// <para>
/// <b>And the answer is structural rather than lucky.</b>
/// <c>ConsoleData.BundleFrom</c> takes the whole state and reads almost none of
/// it - the flight log it carries comes from a verb, never from the console's
/// own possibly-stale copy. So filling the model cannot change what a bundle
/// contains, and this test is what makes that a property rather than an
/// observation.
/// </para>
/// </remarks>
public class BundleOfAFullModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private const string Needle = "sk-live-do-not-ship-this";

    private static AppState Full() => new()
    {
        Queue =
        [
            new QueueRow
            {
                FlightId = "a", FlightNumber = FlightRef.Format(1), Name = "waiting",
                Reason = QueueReason.AwaitingDecision, Since = T0,
            },
        ],
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "a",
                    FlightNumber = FlightRef.Format(1),
                    // THE NEEDLE, IN A FIELD STEP 2 ADDED. A flight's name is
                    // whatever somebody typed, and this asks whether the new
                    // collections became a route out.
                    Name = Needle,
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = Needle },
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
        Logs = new Dictionary<string, FlightLog>(StringComparer.Ordinal)
        {
            ["a"] = new FlightLog
            {
                FlightId = "a",
                FlightNumber = FlightRef.Format(1),
                Entries = [new FlightLogEntry { At = T0, Kind = "lease-granted", Detail = Needle }],
            },
        },
        Credentials = new CredentialList { Credentials = [] },
        Runners = new RunnerList { Runners = [] },
    };

    [Test]
    public async Task A_fuller_model_does_not_make_the_bundle_wider()
    {
        var state = Full();

        // THE PLANT HAS TO HAVE WORKED, or the absence below is vacuous.
        await Assert.That(state.Flights!.Flights[0].Name).IsEqualTo(Needle);
        await Assert.That(state.Logs["a"].Entries[0].Detail).IsEqualTo(Needle);

        var bundle = ConsoleData.BundleFrom(
            state, T0, BundleRedactionTests.AnEnvironment(), BundleRedactionTests.AReport(),
            flightLog: null);

        var json = VerbOutput.ToJson(new VerbResult.Bundle(bundle));
        var text = VerbOutput.ToText(new VerbResult.Bundle(bundle));

        await Assert.That(json).DoesNotContain(Needle)
            .Because("the collections step 2 added are in scope at the call and must not be "
                   + "a route out of it. BundleFrom reads almost none of the state, and this "
                   + "is what keeps that true when the state grows.");
        await Assert.That(text).DoesNotContain(Needle);
    }
}
