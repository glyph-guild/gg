using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The gate tab says what is holding the flight and who may decide it, and the
/// modal offers the way to answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM A REAL TENANT: an awaiting decision that could not be
/// acted on.</b> GG-88 has <c>widen-root</c> attached, awaiting
/// <c>platform-owner</c> — <c>gg why</c> says so at a terminal — and the
/// flight modal's gate tab showed nothing at all.
/// </para>
/// <para>
/// <b>Because the tab renders a field nothing fills.</b>
/// <c>FlightDetails.Gate</c> answers <c>PaneText.Evidence</c>, which reads
/// <c>AppState.Payload</c> — and no production path assigns it, only tests.
/// So the tab could only ever reach its fallback, and the fallback said
/// <i>"Nothing is waiting on you for this flight"</i> about a flight with an
/// open gate. The one sentence it must never say.
/// </para>
/// <para>
/// <b>While the answer was already in hand.</b> <c>ConsoleStart</c> calls the
/// <c>why</c> verb at boot and puts a <c>FlightAttribution</c> on the model;
/// it is rendered in the QUEUE's detail pane and nowhere near the gate tab.
/// Assembled, held, and not shown where it was wanted.
/// </para>
/// <para>
/// <b>TWO CURSORS, AND THE ATTRIBUTION BELONGS TO ONE OF THEM.</b> It is
/// fetched for the queue's selected row; the modal titles itself from a
/// different cursor. Rendering it unguarded would put one flight's gate under
/// another flight's name — which is the defect this pane was fixed for once
/// already, arriving through a different door. So it is shown only when
/// <c>FlightNumber</c> matches the flight on screen.
/// </para>
/// <para>
/// <b>And there was no way to answer from here.</b> `a` and `r` exist only in
/// <c>UiMode.GateDecision</c>, which only `d` opens — and `d` is
/// <c>OffTheHintLine</c> in Normal mode, deliberately, so that nobody answers
/// a question they have not read. The place where they HAVE read it offered
/// nothing.
/// </para>
/// </remarks>
public class TheGateTabSaysWhoDecidesTests
{
    private static FlightSummary AFlight(string number) => new()
    {
        FlightId = "01a0776a-cacb-76dc-b444-2b7031e840d8",
        FlightNumber = number,
        Name = "register names",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "register names" },
        CreatedAt = DateTimeOffset.UnixEpoch,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.29.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Open,
        Facts = [],
    };

    private static FlightAttribution Gated(string number) => new()
    {
        FlightNumber = number,
        EnvelopeVersion = "v6",
        Halt = "waiting on a person",
        Obligations =
        [
            new ObligationAttribution
            {
                ObligationId = "widen-root",
                Attachment = Attachments.Attached,
                Condition = "envelope widens",
                Outcome = null,
                Because = "the proposed change widens airspace.names, and a widening is "
                        + "gated by the obligation the widened document declares",
                Diagnosis = "This obligation is decided by a person. platform-owner may "
                          + "decide it.",
            },
        ],
    };

    /// <summary>The modal open on one flight, with the why for another.</summary>
    private static AppState Showing(string flight, FlightAttribution? why) => new()
    {
        Mode = UiMode.FlightDetail,
        FlightTab = FlightTab.Gate,
        Flights = new FlightList { Flights = [AFlight(flight)] },
        FlightSelected = 0,
        Attribution = why,
    };

    [Test]
    public async Task It_names_the_obligation_and_who_may_decide_it()
    {
        var said = FlightDetails.Gate(Showing("GG-88", Gated("GG-88")));

        await Assert.That(said).Contains("widen-root", StringComparison.Ordinal)
            .Because("which obligation is holding it, because that is what a person "
                   + $"argues about. Said: {said}");

        await Assert.That(said).Contains("platform-owner", StringComparison.Ordinal)
            .Because("and WHO may answer, because the commonest reason somebody cannot act "
                   + $"on a gate is that it is not theirs to answer. Said: {said}");
    }

    [Test]
    public async Task It_never_says_nothing_is_waiting_when_something_is()
    {
        var said = FlightDetails.Gate(Showing("GG-88", Gated("GG-88")));

        await Assert.That(said)
            .DoesNotContain("Nothing is waiting", StringComparison.OrdinalIgnoreCase)
            .Because("this is the sentence a person read on a flight with an open gate, "
                   + "and it is the one thing the pane must never say while one is "
                   + $"attached. Said: {said}");
    }

    [Test]
    public async Task An_attribution_for_another_flight_is_not_shown_under_this_one()
    {
        // THE TWO-CURSOR TRAP. The why is fetched for the QUEUE's selected row
        // and the modal titles itself from the flights list, so an unguarded
        // render puts one flight's gate under another flight's name.
        var said = FlightDetails.Gate(Showing("GG-99", Gated("GG-88")));

        await Assert.That(said).DoesNotContain("widen-root", StringComparison.Ordinal)
            .Because("a gate belonging to GG-88 shown under GG-99 is worse than a blank "
                   + $"pane: it is a confident wrong answer. Said: {said}");

        await Assert.That(said).Contains("GG-88", StringComparison.Ordinal)
            .Because("and it says WHOSE it is rather than going quiet, because 'not read "
                   + "for this flight' and 'read for a different one' are different facts "
                   + $"with different next moves. Said: {said}");
    }

    [Test]
    public async Task Not_read_at_all_says_which_absence_it_is()
    {
        var said = FlightDetails.Gate(Showing("GG-88", null));

        await Assert.That(said).DoesNotContain("Nothing is waiting", StringComparison.OrdinalIgnoreCase)
            .Because("nobody asked, which says nothing about whether a gate is open.");

        await Assert.That(said).Contains("not", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task It_says_where_the_gate_is_answered_rather_than_naming_a_dead_key()
    {
        // THE MODAL BINDS NOTHING THAT ACTS ON ITS FLIGHT, deliberately -
        // EnterOpensWhatTheCursorIsOnTests: "a person reading a log has not
        // asked to decide anything". So this pane must not tell somebody to
        // press a key that does not resolve here; it says where the answer
        // lives instead.
        var state = Showing("GG-88", Gated("GG-88"));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('d'), KeymapContext.For(state)))
            .IsNull()
            .Because("the flight modal owns the keyboard and reading it acts on nothing.");

        await Assert.That(FlightDetails.Gate(state))
            .Contains("Queue", StringComparison.Ordinal)
            .Because("and the pane points at where a gate IS answered, because a person "
                   + "who has just read one is about to look for how.");
    }
}
