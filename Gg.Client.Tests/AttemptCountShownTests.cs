using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// How many times this flight's loop has run, where somebody deciding will see it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rejection is unbounded, still.</b> <c>budget.attempts</c> was never built, so
/// reject → run → reject → run has no termination condition and each cycle costs a real
/// agent run. A person is the rate limiter, because every cycle needs a decision - and a
/// person can only play that part if they can see how many cycles there have been.
/// </para>
/// <para>
/// <b>Shown so that no limit and a limit nobody wrote down stop looking identical.</b>
/// The number is not enforced anywhere; making it visible is what turns "this has been
/// round four times" from something only the event stream knows into something the person
/// answering the gate knows.
/// </para>
/// </remarks>
public class AttemptCountShownTests
{
    [Test]
    public async Task Gg_show_says_how_many_attempts_there_have_been()
    {
        var rendered = VerbOutput.ToText(new VerbResult.Flight(AFlight(attempts: 3)));

        await Assert.That(rendered).Contains("attempts")
            .Because("labelled, because a bare number beside an envelope version is a number "
                   + "nobody can read.");
        await Assert.That(rendered).Contains("3");
    }

    [Test]
    public async Task A_flight_that_has_never_run_says_none_rather_than_a_number()
    {
        // Article XI, at the smallest scale that still matters. Zero and one are
        // different states here - a flight nobody has run yet is not a flight that ran
        // once - and "0" beside a label reads like a count that failed to increment.
        var rendered = VerbOutput.ToText(new VerbResult.Flight(AFlight(attempts: 0)));

        await Assert.That(rendered).Contains("attempts    none")
            .Because("nothing has run, and saying so is different from reporting a zero.");
    }

    [Test]
    public async Task The_json_carries_it_too()
    {
        // The rendering and the JSON are two views of one document, which is the property
        // every verb here holds: a pane that showed something --json could not is a pane
        // nobody can script against.
        var json = VerbOutput.ToJson(new VerbResult.Flight(AFlight(attempts: 2)));

        await Assert.That(json).Contains("\"attempts\":2");
    }

    private static FlightSummary AFlight(int attempts) => new()
    {
        FlightId = "flight-1",
        FlightNumber = "GG-42",
        Name = "add a down step",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "add a down step" },
        CreatedAt = new DateTimeOffset(2026, 8, 13, 9, 0, 0, TimeSpan.Zero),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.11.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v1",
        Attempts = attempts,
        Facts = [],
    };
}
