using Gg.Client;
using Gg.Console;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A refresh that brings a new flight does not move the one you are reading.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "while looking at the flight modal - randomly it
/// resets to 'No story was fetched for this flight'."</b> Random because it
/// depends on somebody else: the fleet creating a flight while you have one
/// open.
/// </para>
/// <para>
/// <b><c>FlightSelected</c> is a row number, and the rows are sorted newest
/// first.</b> <c>AutoRefresh</c> re-reads the flights list every few seconds
/// and does not stop for an open modal — <c>Reads(TabId.Flights)</c> is true —
/// so a flight created between two ticks lands at index 0 and shifts every
/// other row down one. The cursor does not move, which means it is now
/// pointing at a DIFFERENT FLIGHT.
/// </para>
/// <para>
/// <b>And the story is checked against the flight it is about</b>, which is
/// what turns a silent swap into a visible one: <c>PaneText.StoryOf</c> refuses
/// to caption one flight's history with another's name, finds the ids no longer
/// match, and the modal says no story was fetched. That guard is working
/// correctly and is the only reason this was ever noticeable — without it the
/// modal would have quietly shown the wrong flight's log under the right
/// flight's title.
/// </para>
/// <para>
/// <b>So the cursor is re-anchored where the list is replaced</b>, which is the
/// one place it can be: <c>ConsoleProjection.Apply</c>. A person pointing at a flight
/// is pointing at a FLIGHT, not at a row number — the same thing
/// <c>AppState.FlightSelected</c>'s own remark says about indexing the list as
/// shown, taken one step further.
/// </para>
/// </remarks>
public class TheFlightCursorFollowsTheFlightTests
{
    private static FlightSummary Flight(string id, int minutesAgo) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(id.Length + minutesAgo),
        Name = "flight " + id,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "flight " + id },
        CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1000 - minutesAgo),
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.30.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    /// <summary>Two flights, newest first, with the cursor on the older one.</summary>
    private static AppState Reading() => new()
    {
        ActiveTab = TabId.Flights,
        Mode = UiMode.FlightDetail,
        Flights = new FlightList { Flights = [Flight("a", 1), Flight("b", 2)] },

        // ROW ONE IS 'b', because the list is shown newest first.
        FlightSelected = 1,
    };

    [Test]
    public async Task A_flight_created_elsewhere_does_not_move_the_cursor_off_mine()
    {
        var reading = Reading();

        await Assert.That(PaneText.Detailed(reading)!.FlightId).IsEqualTo("b")
            .Because("the premise: row one of a newest-first list of two is the older one.");

        // THE TICK. AutoRefresh re-read the list and somebody's flight had
        // started in between, which puts it at the top and pushes everything
        // down one.
        var after = ConsoleProjection.Apply(
            reading,
            new VerbResult.Flights(new FlightList
            {
                Flights = [Flight("c", 0), Flight("a", 1), Flight("b", 2)],
            }));

        await Assert.That(PaneText.Detailed(after)!.FlightId).IsEqualTo("b")
            .Because("a person pointing at a flight is pointing at a FLIGHT. The row it "
                   + "happens to be on is the list's business, and the list just changed "
                   + "under them.");

        await Assert.That(after.FlightSelected).IsEqualTo(2)
            .Because("and the cursor moved to where that flight now is, rather than staying "
                   + "on a number.");
    }

    [Test]
    public async Task And_the_story_it_is_reading_still_belongs_to_it()
    {
        // THE SYMPTOM, ASSERTED WHERE IT WAS SEEN. StoryOf checks the story
        // against the flight it is about, so a cursor that slid onto the wrong
        // flight makes the modal claim nothing was ever fetched.
        var reading = Reading() with
        {
            Story = new FlightStory
            {
                FlightId = "b",
                FlightNumber = FlightRef.Format(3),
                Stage = FlightStoryStages.Reached([]),
                State = FlightStates.Open,
                Entries = [],
            },
        };

        var after = ConsoleProjection.Apply(
            reading,
            new VerbResult.Flights(new FlightList
            {
                Flights = [Flight("c", 0), Flight("a", 1), Flight("b", 2)],
            }));

        await Assert.That(FlightDetails.LogAbsence(after))
            .DoesNotContain("No story was fetched")
            .Because("that sentence is what the owner saw, and it was a statement about a "
                   + "flight they had not opened.");
    }

    [Test]
    public async Task A_flight_that_goes_away_leaves_the_cursor_somewhere_real()
    {
        // THE OTHER DIRECTION, and it must not throw or point past the end. A
        // flight can leave the list - pruned, or filtered out - and the cursor
        // has to land somewhere that exists.
        var after = ConsoleProjection.Apply(
            Reading(),
            new VerbResult.Flights(new FlightList { Flights = [Flight("a", 1)] }));

        await Assert.That(after.FlightSelected).IsEqualTo(0);
        await Assert.That(PaneText.Detailed(after)!.FlightId).IsEqualTo("a");
    }

    [Test]
    public async Task An_empty_list_puts_the_cursor_at_the_top()
    {
        var after = ConsoleProjection.Apply(
            Reading(),
            new VerbResult.Flights(new FlightList { Flights = [] }));

        await Assert.That(after.FlightSelected).IsEqualTo(0);
        await Assert.That(PaneText.Detailed(after)).IsNull();
    }

    [Test]
    public async Task A_list_that_did_not_move_leaves_the_cursor_exactly_where_it_was()
    {
        // THE CONTROL. Most ticks bring the same flights back, and re-anchoring
        // must be a no-op then - a cursor that shifted on an unchanged list
        // would be this defect with the sign flipped.
        var after = ConsoleProjection.Apply(
            Reading(),
            new VerbResult.Flights(new FlightList
            {
                Flights = [Flight("a", 1), Flight("b", 2)],
            }));

        await Assert.That(after.FlightSelected).IsEqualTo(1);
    }
}
