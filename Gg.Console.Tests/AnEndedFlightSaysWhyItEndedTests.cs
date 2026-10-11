using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A flight that ended says how and why on its details tab, and the tab does not read the list's
/// absences as answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"GG-1074 shows no details in the TUI (it also shows failed
/// state)."</i> The flight failed because its runner could not resolve the sonarqube server's
/// credential, and <c>gg show</c> said so. The details tab said <c>state failed</c>,
/// <c>attempts 0</c> and <c>facts (none yet)</c>.
/// </para>
/// <para>
/// <b>Two of those were the flight list's absences.</b> The list leaves attempts and facts off
/// every row on purpose - loading every flight's evidence would make the cheapest read the most
/// expensive - and the tab drew its <c>0</c> and its empty list as the flight's own. The story,
/// which the modal reads on opening, has the attempt and what the runner said.
/// </para>
/// </remarks>
public class AnEndedFlightSaysWhyItEndedTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 11, 7, 38, 11, TimeSpan.Zero);

    private const string Said =
        "This loop uses the MCP server 'sonarqube', which needs the credential "
      + "'local:analysis/sonarcloud', and this machine cannot resolve it.";

    private static StoryEntry Entry(int seconds, string kind, string[] parameters, int? attempt = null, string? said = null) =>
        new()
        {
            At = At.AddSeconds(seconds),
            Kind = kind,
            Stage = FlightStages.Of(kind),
            Params = parameters,
            Attempt = attempt,
            Said = said,
        };

    private static AppState Opened(string state, params StoryEntry[] entries) => new()
    {
        Mode = UiMode.FlightDetail,
        Story = new FlightStory
        {
            FlightId = "a",
            FlightNumber = FlightRef.Format(1074),
            WorkKind = "investigate",
            Stage = FlightStoryStages.Reached(entries),
            State = state,
            Entries = entries,
        },
        Flights = new FlightList
        {
            Flights =
            [
                // AS THE LIST SENDS IT: no attempts and no facts, for every flight.
                new FlightSummary
                {
                    FlightId = "a",
                    FlightNumber = FlightRef.Format(1074),
                    Name = "ado#18492",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Ticket, Provider = "ado", Id = "18492" },
                    CreatedAt = At,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.1.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v20",
                    Attempts = 0,
                    State = state,
                    Facts = [],
                },
            ],
        },
    };

    private static AppState Failed() => Opened(
        FlightStates.Failed,
        Entry(0, StoryKinds.Created, ["Kevin Deenanauth"]),
        Entry(3, StoryKinds.LeaseGranted, ["01a0bca3"], attempt: 1),
        Entry(17, StoryKinds.LoopRan, ["failed", "investigate"], said: Said),
        Entry(29, StoryKinds.LeaseReleased, ["failed"], attempt: 1, said: Said),
        Entry(29, StoryKinds.Ended, ["failed"]));

    private static string? Field(AppState state, string label) =>
        FlightDetails.Fields(state).FirstOrDefault(f => f.Label == label)?.Value;

    [Test]
    public async Task An_ended_flight_says_why_on_its_details()
    {
        var ended = Field(Failed(), "ended");

        await Assert.That(ended).IsNotNull()
            .Because("a failed flight whose details do not say why is GG-1074: the reason was "
                   + "in its story and only gg show drew it.");
        await Assert.That(ended!).Contains("failed").And.Contains(Said);
    }

    [Test]
    public async Task Attempts_come_from_the_story_when_the_list_left_them_off()
    {
        await Assert.That(Field(Failed(), "attempts")).IsEqualTo("1")
            .Because("the list sends 0 for every flight, and a flight handed to a runner was "
                   + "attempted.");
    }

    [Test]
    public async Task Facts_the_list_left_off_are_not_drawn_as_none()
    {
        await Assert.That(Field(Failed(), "facts")).DoesNotContain("none yet")
            .Because("GG-1074 recorded two facts; the list carries no flight's facts at all.");
    }

    [Test]
    public async Task A_flight_still_in_the_air_has_no_ending()
    {
        var flying = Opened(
            FlightStates.Open,
            Entry(0, StoryKinds.Created, ["Kevin Deenanauth"]),
            Entry(3, StoryKinds.LeaseGranted, ["01a0bca3"], attempt: 1));

        await Assert.That(Field(flying, "ended")).IsNull();
    }
}
