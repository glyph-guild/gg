using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The evidence a flight modal shows is the modal's flight's.
/// </summary>
/// <remarks>
/// <para>
/// <b>TWO CURSORS, AND THE MODAL FOLLOWED THE WRONG ONE.</b> This console has
/// a queue cursor and a flights cursor. <c>AppState.Selected</c> is derived
/// from the QUEUE - <c>Queue[SelectedRow]</c>, null whenever the queue is
/// empty - while the flight modal titles itself from
/// <c>PaneText.Detailed</c>, which reads the FLIGHTS list. The evidence
/// renderer keyed on the first.
/// </para>
/// <para>
/// <b>So the modal said "No flight selected" under a title naming a
/// flight.</b> Open any flight from the Flights tab while nothing needs you -
/// the ordinary case, and the one a person is most likely to be in when they
/// go looking at a flight - and the queue is empty, so the pane reported that
/// nothing was selected about a flight it was at that moment displaying.
/// </para>
/// <para>
/// <b>The two sentences it must tell apart.</b> "No flight selected" is about
/// the CONSOLE and "nothing is waiting on you for this flight" is about the
/// FLIGHT; a person reading the first one under a flight's name learns
/// something false about their console, and stops trusting the pane rather
/// than the flight.
/// </para>
/// </remarks>
public class TheEvidenceTabIsAboutTheModalsFlightTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A flight in the list, and NOTHING in the queue: the ordinary case.</summary>
    private static AppState AFlightNothingWaitingOn() => new()
    {
        Mode = UiMode.FlightDetail,
        FlightTab = FlightTab.Evidence,
        FlightSelected = 0,

        // THE WHOLE POINT OF THE FIXTURE. An empty queue is not an unusual
        // state - it is what a healthy tenant looks like.
        Queue = [],
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8",
                    FlightNumber = "GG-81",
                    Name = "a flight nobody is being asked about",
                    Intent = new FlightIntent
                    {
                        Kind = FlightIntentKinds.Text,
                        Text = "count slowly from one to forty",
                    },
                    CreatedAt = T0,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.25.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v6",
                    Attempts = 1,
                    Facts = [],
                },
            ],
        },
    };

    [Test]
    public async Task A_flight_is_open_so_the_tab_does_not_say_none_is_selected()
    {
        var state = AFlightNothingWaitingOn();

        // The premise, asserted rather than assumed: the modal IS showing a
        // flight, which is what makes the sentence below wrong.
        await Assert.That(PaneText.Detailed(state)).IsNotNull();
        await Assert.That(FlightDetails.Title(state)).Contains("GG-81");

        await Assert.That(FlightDetails.Evidence(state)).DoesNotContain("No flight selected")
            .Because("the title names the flight, so a pane underneath saying nothing is "
                   + "selected is telling a person something false about their console.");
    }

    [Test]
    public async Task It_says_nothing_is_waiting_on_this_flight_instead()
    {
        await Assert.That(FlightDetails.Evidence(AFlightNothingWaitingOn()))
            .Contains("Nothing is waiting on you for this flight")
            .Because("that is the true sentence, and it is about the FLIGHT rather than "
                   + "about the console - which is the distinction the wrong one lost.");
    }

    [Test]
    public async Task With_no_flight_at_all_it_still_says_so()
    {
        // THE SENTENCE IS NOT DELETED, only moved off the wrong condition.
        // Reached through the modal with no flights list at all, which is the
        // state a failed boot leaves.
        var empty = new AppState { Mode = UiMode.FlightDetail, FlightTab = FlightTab.Evidence };

        await Assert.That(FlightDetails.Evidence(empty)).Contains("No flight")
            .Because("a modal with nothing behind it still has to say which of the two "
                   + "kinds of nothing this is.");
    }
}
