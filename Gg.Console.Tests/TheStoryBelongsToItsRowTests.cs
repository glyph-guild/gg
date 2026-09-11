using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The flight pane's story belongs to the row it was read for, and goes when
/// that row does.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM A REAL TENANT: "the flight information on the right side
/// of that screen never clears."</b> Somebody approved the last waiting
/// decision, the queue emptied, and the pane beside it went on showing the
/// flight they had just dealt with.
/// </para>
/// <para>
/// <b>Because <c>Reducer.Detail</c> clears three fields and there are
/// four.</b> It nulls <c>Flight</c>, <c>FlightLog</c> and <c>Attribution</c>
/// when nothing is selected, and guards the attribution by flight number with
/// the reason written beside it — <i>"KEPT ONLY WHILE IT IS ABOUT THIS ROW"</i>.
/// <c>Story</c> is read for the selected row by the same boot, in the line
/// directly below the one that reads the attribution, and was left out of
/// both.
/// </para>
/// <para>
/// <b>So the pane's own guard could not fire.</b> It asks whether story AND
/// flight are both null before saying no flight is selected; a surviving story
/// answers that question wrongly, and the pane renders the old row rather than
/// the sentence it keeps for exactly this state.
/// </para>
/// <para>
/// <b>A stale story is worse than a stale log.</b> It is the account of what
/// happened to a flight — under a queue that no longer lists it, a person is
/// reading history that belongs to something they have finished with, with
/// nothing on screen saying so.
/// </para>
/// </remarks>
public class TheStoryBelongsToItsRowTests
{
    private static FlightStory AStory(string number) => new()
    {
        FlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8",
        FlightNumber = number,
        WorkKind = "register",
        Stage = FlightStages.Ended,
        State = FlightStates.Landed,
        Entries = [],
    };

    private static QueueRow ARow(string number) => new()
    {
        FlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8",
        FlightNumber = number,
        Name = "register names",
        Reason = QueueReason.AwaitingDecision,
        Since = new DateTimeOffset(2026, 9, 11, 22, 11, 0, TimeSpan.Zero),
    };

    [Test]
    public async Task An_emptied_queue_takes_the_story_with_it()
    {
        // WHAT APPROVING THE LAST DECISION LOOKS LIKE: the row is gone and the
        // pane beside it has to stop describing it.
        var after = Reducer.Detail(new AppState
        {
            Queue = [],
            SelectedRow = 0,
            Story = AStory("GG-88"),
        });

        await Assert.That(after.Story).IsNull()
            .Because("the story was read FOR a row, and there is no row. Flight, FlightLog "
                   + "and Attribution are already cleared here for that reason; this was "
                   + "the fourth field and it was missed.");

        await Assert.That(PaneText.Flight(after))
            .Contains("no flight selected", StringComparison.OrdinalIgnoreCase)
            .Because("and the pane's own sentence for this state can finally be reached - "
                   + "it asks whether story AND flight are null, so a surviving story "
                   + "answered that wrongly and the old row was drawn instead.");
    }

    [Test]
    public async Task A_story_read_for_another_row_is_not_shown_under_this_one()
    {
        // THE ATTRIBUTION'S OWN RULE, applied to the field beside it. Moving
        // the cursor to a row whose story has not been read must not leave the
        // previous row's account under the new row's name.
        var after = Reducer.Detail(new AppState
        {
            Queue = [ARow("GG-99")],
            SelectedRow = 0,
            Story = AStory("GG-88"),
        });

        await Assert.That(after.Story).IsNull()
            .Because("an account of what happened is the worst thing to leave under the "
                   + "wrong name - the same argument the attribution makes two lines above "
                   + "it, and it names a halt where this names a history.");
    }

    [Test]
    public async Task The_story_for_this_row_is_kept()
    {
        // THE POSITIVE CONTROL. Clearing too eagerly would make the pane blink
        // to "loading" on every refresh of a row whose story is in hand.
        var after = Reducer.Detail(new AppState
        {
            Queue = [ARow("GG-88")],
            SelectedRow = 0,
            Story = AStory("GG-88"),
        });

        await Assert.That(after.Story).IsNotNull()
            .Because("it is about this row, which is the whole test of whether it stays.");
    }
}
