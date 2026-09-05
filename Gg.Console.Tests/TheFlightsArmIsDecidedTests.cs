using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The arm that looked like an oversight and was one.
/// </summary>
/// <remarks>
/// <para>
/// <b>S28.6-03 predicted a shape and got a defect.</b> The plan expected
/// `ConsoleProjection.Apply`'s <c>Flights</c> arm to be a correct no-op needing
/// an exemption with its reason - it cleared the diagnosis, dropped the list,
/// and had a comment explaining that the queue is derived rather than fetched.
/// The comment was right about the queue and wrong about the list: dropping it
/// left the detail under a selected row with nowhere to come from but a second
/// request.
/// </para>
/// <para>
/// So the answer was not an exemption. Step 2 made the arm assign
/// <c>Flights</c>, which is what makes an arrow key free, and this asserts the
/// property rather than the wording of a comment.
/// </para>
/// </remarks>
public class TheFlightsArmIsDecidedTests
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

    [Test]
    public async Task The_arm_keeps_the_list_rather_than_deriving_and_dropping_it()
    {
        var listed = new FlightList { Flights = [Flight("a", 1), Flight("b", 2)] };

        var state = ConsoleProjection.Apply(new AppState(), new VerbResult.Flights(listed));

        await Assert.That(state.Flights).IsNotNull()
            .Because("the queue is DERIVED from this list, which is why nothing renders the "
                   + "list directly - and is not a reason to throw it away. Holding it is "
                   + "what makes moving the selection a reducer step and nothing else.");
        await Assert.That(state.Flights!.Flights.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Every_projection_arm_assigns_something()
    {
        // THE GENERAL CASE, so the next arm somebody adds cannot be a no-op
        // that looks like a decision. An arm that only cleared the diagnosis
        // was indistinguishable from one nobody had finished.
        var kinds = new VerbResult[]
        {
            new VerbResult.Flights(new FlightList { Flights = [] }),
            new VerbResult.Flight(Flight("a", 1)),
            new VerbResult.Runners(new RunnerList { Runners = [] }),
        };

        foreach (var result in kinds)
        {
            var before = new AppState { Diagnosis = "something older" };
            var after = ConsoleProjection.Apply(before, result);

            await Assert.That(after).IsNotEqualTo(before)
                .Because($"{result.Kind}'s arm changed nothing but the diagnosis, which is "
                       + "the shape a half-written arm has.");
        }
    }
}
