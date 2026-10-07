using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The console's flight detail on a leg held behind another says what it waits on, in `gg why`'s
/// sentence, once the why has been read (slice sixty-seven, S67.3-02).
/// </summary>
/// <remarks>
/// <b>Found on ITN-60</b>: GG-969 waited for GG-968 and the console's "why" said only "nothing is
/// holding this flight" - true of its obligations, false of the flight.
/// </remarks>
public class TheConsoleShowsALegsHoldTests
{
    private static readonly LegHold Held = new()
    {
        Follows = FlightRef.Format(968),
        Subject = "Storybook 10 upgrade",
    };

    private static AppState Detailed(LegHold? held) => new()
    {
        Mode = UiMode.FlightDetail,
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "a",
                    FlightNumber = FlightRef.Format(969),
                    Name = "ITN-60 · Storybook 10 visual check",
                    Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "check it" },
                    CreatedAt = new DateTimeOffset(2026, 10, 7, 0, 38, 38, TimeSpan.Zero),
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.1.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v18",
                    Attempts = 0,
                    State = FlightStates.Open,
                    Facts = [],
                },
            ],
        },
        Story = new FlightStory
        {
            FlightId = "a",
            FlightNumber = FlightRef.Format(969),
            WorkKind = "ui-preview",
            Stage = FlightStoryStages.Reached([]),
            State = FlightStates.Open,
            Entries = [],
        },
                Attribution = new FlightAttribution
        {
            FlightNumber = FlightRef.Format(969),
            EnvelopeVersion = "v18",
            Obligations = [],
            Held = held,
        },
    };

    [Test]
    public async Task The_why_says_what_the_leg_waits_on()
    {
        var text = PaneText.Flight(Detailed(Held));

        await Assert.That(text).Contains("NOT STARTED: this leg follows GG-968 (Storybook 10 upgrade)");
    }

    [Test]
    public async Task A_flight_held_behind_nothing_says_nothing_of_it()
    {
        var text = PaneText.Flight(Detailed(held: null));

        await Assert.That(text).DoesNotContain("NOT STARTED");
    }
}
