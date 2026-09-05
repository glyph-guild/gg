using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The evidence pane says which nothing it is showing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It chose between its two sentences on the wrong field.</b>
/// <c>state.Flight is null</c> asks <i>did this row's detail load</i>, and the
/// question the sentence answers is <i>has anybody selected anything</i>. While
/// nothing assigned <c>Flight</c> those were the same, so the pane told a person
/// who had selected a flight that no flight was selected.
/// </para>
/// <para>
/// <b>Step 2 makes them different rather than fixing them.</b> A row whose
/// detail did not load now has a selection and no flight - the case the reducer
/// makes deliberately, because showing the previous row's flight under this
/// row's name is worse. On that row the old condition says "No flight
/// selected", which is the same wrong sentence for a new reason.
/// </para>
/// <para>
/// <b>Rule 5: three nothings, three sentences.</b> Not loaded, loaded and
/// empty, and failed to load are different facts about the same blank pane, and
/// the person reading it is deciding whether to wait, act, or go and look.
/// </para>
/// </remarks>
public class EvidenceSaysWhichNothingTests
{
    private static QueueRow Row(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = "waiting",
        Reason = QueueReason.AwaitingDecision,
        Since = new DateTimeOffset(2026, 9, 5, 9, 0, 0, TimeSpan.Zero),
    };

    private static FlightSummary Flight(string id, int number) => new()
    {
        FlightId = id,
        FlightNumber = FlightRef.Format(number),
        Name = "waiting",
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
    public async Task With_nothing_selected_it_says_so()
    {
        var pane = PaneText.Evidence(new AppState());

        await Assert.That(pane).Contains("No flight selected")
            .Because("an empty queue is a real state and the sentence for it is true.");
    }

    [Test]
    public async Task With_a_flight_selected_it_does_not_claim_otherwise()
    {
        // THE PLAN'S HEADLINE, reachable for the first time. Until the queue
        // could fill this could not be staged at all.
        var state = new AppState
        {
            Queue = [Row("a", 1)],
            Flight = Flight("a", 1),
        };

        await Assert.That(PaneText.Evidence(state)).DoesNotContain("No flight selected")
            .Because("somebody has selected a flight, and telling them they have not is the "
                   + "pane's own rule inverted - it says something, and what it says is "
                   + "false.");
        await Assert.That(PaneText.Evidence(state)).Contains("Nothing is waiting on you")
            .Because("which is the other nothing, and the true one here.");
    }

    [Test]
    public async Task A_selected_row_whose_detail_did_not_load_is_still_a_selection()
    {
        // THE NEW CASE, and the reason keying on Flight is wrong rather than
        // merely coincidental. The reducer leaves Flight null when nothing was
        // loaded for a row - deliberately - so a pane asking "is Flight null"
        // gives the no-selection sentence to somebody with a row highlighted.
        var state = new AppState { Queue = [Row("a", 1)], Flight = null };

        await Assert.That(PaneText.Evidence(state)).DoesNotContain("No flight selected")
            .Because("the row is selected; its detail is what is missing, and those are "
                   + "different sentences.");
    }
}
