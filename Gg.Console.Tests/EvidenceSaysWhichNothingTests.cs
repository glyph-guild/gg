using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// The evidence pane says which nothing it is showing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It has chosen on the wrong field TWICE, and the second time is why
/// these now read the flights list.</b> First it asked <c>state.Flight is
/// null</c> - <i>did this row's detail load</i> - to answer <i>has anybody
/// selected anything</i>. Then it asked <c>state.Selected</c>, the QUEUE's
/// cursor, after the pane had become a tab of the flight modal - and the modal
/// titles itself from <c>PaneText.Detailed</c>, the FLIGHTS list. So it said
/// "No flight selected" under a title naming a flight, for everybody whose
/// queue was empty, which is what a healthy tenant looks like.
/// </para>
/// <para>
/// <b>The subject is whatever the title is about, and that is the rule.</b> A
/// pane and the title above it that answer "which flight is this" from
/// different cursors will disagree, and the reader believes the title.
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
        // THE FLIGHT THE MODAL IS SHOWING, which is the flights list. The queue
        // is deliberately EMPTY here: that is the ordinary state, and keying on
        // it is the bug this class was reopened for.
        var state = new AppState
        {
            Flights = new FlightList { Flights = [Flight("a", 1)] },
            Flight = Flight("a", 1),
        };

        await Assert.That(PaneText.Evidence(state)).DoesNotContain("No flight selected")
            .Because("somebody has selected a flight, and telling them they have not is the "
                   + "pane's own rule inverted - it says something, and what it says is "
                   + "false.");

        // AND THE OTHER NOTHING MOVED, because this one could never know it.
        // Evidence reads AppState.Payload - the material BEHIND a gate - and
        // its absence says nothing about whether one is attached. Read as
        // "nothing is waiting on you", it was wrong on every flight with an
        // open gate, which is what somebody hit on a real tenant. PaneText.
        // Holding answers it now, from the attribution, and
        // TheGateTabIsAboutTheModalsFlightTests holds the sentence there.
        await Assert.That(PaneText.Evidence(state)).IsEmpty()
            .Because("an absent payload is not an answer about a gate, so this says "
                   + "nothing rather than guessing - and what the pane SHOWS is Holding "
                   + "plus this, which is where the true sentence comes from.");
    }

    [Test]
    public async Task A_selected_row_whose_detail_did_not_load_is_still_a_selection()
    {
        // STILL TRUE, AND NOW FOR A SECOND REASON. The reducer leaves Flight
        // null when nothing was loaded for a row - deliberately - so a pane
        // asking "is Flight null" gives the no-selection sentence to somebody
        // looking at a flight. Detailed reads the LIST rather than that
        // per-row detail, so it is right about this without having to know.
        var state = new AppState
        {
            Flights = new FlightList { Flights = [Flight("a", 1)] },
            Queue = [Row("a", 1)],
            Flight = null,
        };

        await Assert.That(PaneText.Evidence(state)).DoesNotContain("No flight selected")
            .Because("the row is selected; its detail is what is missing, and those are "
                   + "different sentences.");
    }
}
