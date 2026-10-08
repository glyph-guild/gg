using Gg.Console;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// The flights modal keeps the story it was opened on when a refresh folds.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "when refresh happens the modal is clearing
/// information."</b> The `why` and `awaiting` fields vanish on a tick, which is
/// every four seconds, while a person is reading them.
/// </para>
/// <para>
/// <b>Because two cursors share one slot.</b>
/// <c>Reducer.Detail</c> is about the QUEUE: it keys on <c>state.Selected</c>
/// and nulls <c>Story</c> and <c>Attribution</c> when that row is not a flight
/// — correctly, and <see cref="TheStoryBelongsToItsRowTests"/> is why. But the
/// flights modal reads its story through <c>PaneText.StoryOf</c> from the same
/// single slot, and its own cursor is <c>FlightSelected</c>. So a tenant whose
/// queue is empty — the healthy case — wipes the modal's story on every
/// refresh, and <c>ConsoleRefresh</c> calls <c>Detail</c> on every one.
/// </para>
/// <para>
/// <b>Not the cursor-anchoring defect, which was already fixed.</b>
/// <see cref="TheFlightCursorFollowsTheFlightTests"/> and <c>Anchored</c> keep
/// the flights cursor on its flight when the list reorders (2026-09-14). This
/// is the other half: the row is right and the story is gone.
/// </para>
/// <para>
/// <b>Fixed by holding stories PER FLIGHT</b>, as <c>Logs</c> already are,
/// rather than by making the queue's rule weaker. Then no pane can see a story
/// about another pane's row — which is the thing that must not happen — and
/// nothing has to be thrown away to guarantee it.
/// </para>
/// </remarks>
public class TheFlightModalKeepsItsStoryTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 7, 16, 42, 18, TimeSpan.Zero);

    private const string Said =
        "The loop used its whole wall-clock budget of 20m and stopped. This flight is "
      + "waiting for a person.";

    /// <summary>
    /// The state after the console has read this flight's story, built THROUGH
    /// the arm that reads it.
    /// </summary>
    /// <remarks>
    /// <b>Not assembled by hand.</b> A fixture that set the fields itself would
    /// pass while the arm that fills them did something else - which is the
    /// defect family this console keeps finding: a member declared in one place
    /// and never read in the other. <c>ConsoleProjection.Apply</c> is the door a
    /// story actually comes through.
    /// </remarks>
    private static AppState Reading() => ConsoleProjection.Apply(Listed(), new VerbResult.Story(AStory()));

    private static FlightStory AStory()
    {
        var owed = new StoryEntry
        {
            At = At,
            Kind = StoryKinds.LoopAsked,
            Stage = FlightStages.Of(StoryKinds.LoopAsked),
            Params = [],
            Said = Said,
        };

        return new FlightStory
        {
            FlightId = "a",
            FlightNumber = FlightRef.Format(991),
            WorkKind = "implement",
            Stage = FlightStoryStages.Reached([owed]),
            State = FlightStates.Open,
            Outstanding = [owed],
            Entries = [owed],
        };
    }

    private static AppState Listed() =>
        new AppState
        {
            Mode = UiMode.FlightDetail,

            // THE MODAL'S CURSOR, and the queue is EMPTY - which is the healthy
            // tenant and the case that reproduces this. Nothing is waiting for a
            // person in the queue, so `state.Selected` is null and Detail takes
            // the arm that clears all four fields.
            FlightSelected = 0,
            Flights = new FlightList
            {
                Flights =
                [
                    new FlightSummary
                    {
                        FlightId = "a",
                        FlightNumber = FlightRef.Format(991),
                        Name = "ITN-63 · Storybook 10 upgrade",
                        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "upgrade" },
                        CreatedAt = At.AddMinutes(-20),
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

    [Test]
    public async Task The_modal_shows_what_is_awaited_before_a_refresh()
    {
        // THE LIVENESS ANCHOR: the field is there to lose. Green before and
        // after, or the test below is measuring a fixture rather than a defect.
        var fields = FlightDetails.Fields(Reading());

        await Assert.That(fields.Any(f => f.Label == "awaiting")).IsTrue()
            .Because("this is the state a person is looking at when they open GG-991.");
    }

    [Test]
    public async Task A_refresh_with_an_empty_queue_does_not_take_it_away()
    {
        // WHAT THE TICK DOES. ConsoleRefresh folds the new queue and then calls
        // Detail, four times a second.
        var after = Reducer.Detail(Reading());

        await Assert.That(FlightDetails.Fields(after).Any(f => f.Label == "awaiting")).IsTrue()
            .Because("the queue's rule is about the queue's pane; a modal open on a flight is "
                   + "reading its own cursor, and a refresh must not empty it.");
    }

    [Test]
    public async Task A_story_about_another_flight_is_still_refused()
    {
        // THE RULE THAT MUST SURVIVE THE FIX, from TheStoryBelongsToItsRowTests:
        // an account of what happened, shown under a flight it is not about, is
        // the worst kind of wrong. Holding stories per flight is what lets both
        // be true at once - this one must stay green before and after.
        var elsewhere = ConsoleProjection.Apply(
            Listed(),
            new VerbResult.Story(AStory() with { FlightId = "b", FlightNumber = FlightRef.Format(993) }));

        await Assert.That(FlightDetails.Fields(elsewhere).Any(f => f.Label == "awaiting")).IsFalse()
            .Because("the modal is open on flight 'a'; a story read for another flight says "
                   + "nothing about it and must not be rendered under its name.");
    }
}
