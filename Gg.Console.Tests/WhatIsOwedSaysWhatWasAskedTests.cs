using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// What a flight is owed is shown with what was said, not just its kind's sentence (slice
/// sixty-seven, beside S67.4-02).
/// </summary>
/// <remarks>
/// <b>Found building GG-968's half.</b> The control plane now lists the agent's question and the
/// runner's release as outstanding. Both renderers drew each one as its kind's sentence alone -
/// "the agent asked for a decision it is not allowed to make" - and dropped the question, which is
/// the thing a person answers.
/// </remarks>
public class WhatIsOwedSaysWhatWasAskedTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 7, 0, 40, 35, TimeSpan.Zero);
    private const string Asked = "Was the repository supposed to be materialized before I started?";

    private static AppState Opened()
    {
        var asked = new StoryEntry
        {
            At = At,
            Kind = StoryKinds.LoopAsked,
            Stage = FlightStages.Of(StoryKinds.LoopAsked),
            Params = [],
            Said = Asked,
        };

        return new AppState
        {
            Mode = UiMode.FlightDetail,
            Story = new FlightStory
            {
                FlightId = "a",
                FlightNumber = FlightRef.Format(968),
                WorkKind = "implement",
                Stage = FlightStoryStages.Reached([asked]),
                State = FlightStates.Open,
                Outstanding = [asked],
                Entries = [asked],
            },
            Flights = new FlightList
            {
                Flights =
                [
                    new FlightSummary
                    {
                        FlightId = "a",
                        FlightNumber = FlightRef.Format(968),
                        Name = "ITN-60 · Storybook 10 upgrade",
                        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "upgrade" },
                        CreatedAt = At.AddMinutes(-2),
                        RunnerProtocolVersion = 1,
                        FactVocabularyVersion = "0.1.0",
                        ConstitutionVersion = "1.0.0",
                        EnvelopeVersion = "v18",
                        Attempts = 1,
                        State = FlightStates.Open,
                        Facts = [],
                    },
                ],
            },
        };
    }

    [Test]
    public async Task The_awaiting_field_carries_the_question()
    {
        var awaiting = FlightDetails.Fields(Opened()).Where(f => f.Label == "awaiting")
            .Select(f => f.Value).ToList();

        await Assert.That(awaiting.Count).IsEqualTo(1);
        await Assert.That(awaiting[0]).Contains(Asked);
    }

    [Test]
    public async Task The_flight_pane_carries_the_question()
    {
        var text = PaneText.Flight(Opened());
        // THE BLOCK ITSELF, up to the blank line that ends it: the history below it also
        // prints what the agent said, and a test reading past the block passes on that.
        var from = text.IndexOf("waiting on somebody", StringComparison.Ordinal);
        var to = text.IndexOf(Environment.NewLine + Environment.NewLine, from, StringComparison.Ordinal);
        var owed = text[from..(to < 0 ? text.Length : to)];

        await Assert.That(owed).Contains(Asked);
    }
}
