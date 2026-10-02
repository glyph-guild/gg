using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg why` on a leg that has not started names the leg it follows, and what
/// happened to that leg.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-seven step 5, and ADR-0035 Decision 3 says this ADR is wrong
/// without it.</b> The decision to let a leg wait was taken on the condition
/// that a held leg is legible: <i>"the warning is not that a flight waits — it
/// is that it waits AND LOOKS PATIENT"</i>. Step 4 made legs wait. This is the
/// other half, and until it exists the slice has added a silent stall — the
/// failure this project meets most often.
/// </para>
/// <para>
/// <b>What it is replacing is a sentence that is RIGHT about two cases and
/// wrong about this one.</b> A held leg has no obligations evaluated, so today
/// it renders the empty-list reading: <i>"Nothing has been judged against this
/// flight. Either its envelope declares no obligation, or nothing has been
/// evaluated yet."</i> That sentence was carefully written to name two absences
/// rather than guess between them — and a held leg is a THIRD absence it does
/// not know about, which reads as the ordinary "not started yet". S57.5-02 is
/// that distinction and nothing else.
/// </para>
/// <para>
/// <b>Rendered, never derived, like everything else in this verb.</b> The
/// client is told which flight is being waited on and how it ended; it does not
/// look up a predecessor, and it does not decide whether an ending counts. A
/// client that worked out for itself whether a leg was releasable would be
/// explaining a decision the claim made — the same drift the rest of this verb
/// refuses.
/// </para>
/// <para>
/// <b>A PERMANENT hold is a different sentence from a temporary one</b>, which
/// is the third point of Decision 3: a leg behind a grounded predecessor will
/// never be offered, nothing expires it, and the system's duty is to say so
/// rather than let it be discovered. So the ending is not decoration — it is
/// what tells a person whether to wait or to end the itinerary.
/// </para>
/// </remarks>
public class AHeldLegSaysWhatItWaitsForTests
{
    /// <summary>A leg held behind a predecessor, with nothing else to say.</summary>
    /// <remarks>
    /// NO OBLIGATIONS, because that is the real shape: a leg that was never
    /// offered has run no loop, so nothing has been evaluated against it. If
    /// this fixture carried obligations the tests below would pass against a
    /// rendering that only works for flights that have already been judged.
    /// </remarks>
    private static FlightAttribution Held(string? ending) => new()
    {
        FlightNumber = "GG-88",
        EnvelopeVersion = "v3",
        Obligations = [],
        Held = new LegHold
        {
            Follows = "GG-87",
            Subject = "the-schema",
            Ending = ending,
        },
    };

    [Test]
    public async Task A_held_leg_names_the_flight_it_follows_and_the_subject_it_declared()
    {
        // S57.5-01. BOTH, because they answer different questions. The number is
        // what a person types next; the subject is what the agent wrote, and it
        // is the only thing that says whether the order declared was the one
        // intended.
        var text = VerbOutput.ToText(new VerbResult.Why(Held(ending: null)));

        await Assert.That(text).Contains("GG-87")
            .Because("a person asking why this has not started needs the flight to go and "
                   + "look at, and a hold that does not name it leaves them to find it.");
        await Assert.That(text).Contains("the-schema")
            .Because("the subject is what the agent declared it was following, and it is the "
                   + "only thing that says whether the order it chose was the right one.");
    }

    [Test]
    public async Task A_held_leg_does_not_read_as_a_flight_nothing_has_judged()
    {
        // S57.5-02, THE WHOLE OF IT. This is the sentence a held leg renders
        // today, and it is true of two other states - an envelope with no
        // obligations, and a flight between opening and its first gate. A
        // reader given it would conclude the leg is simply early.
        var text = VerbOutput.ToText(new VerbResult.Why(Held(ending: null)));

        await Assert.That(text).DoesNotContain("Nothing has been judged")
            .Because("this leg has not been judged BECAUSE it has not been offered, and "
                   + "reporting the absence without the reason is the silent stall rule 4 "
                   + "forbids.");
    }

    [Test]
    public async Task A_leg_behind_a_grounded_predecessor_says_it_will_never_start()
    {
        // ADR-0035 DECISION 3, POINT 3. A grounded predecessor leaves this leg's
        // premise false for ever: nothing expires it, nothing retries it, and
        // "waiting" is the wrong thing for a person to do. The two renderings
        // must differ, or a permanent hold reads as patience.
        var waiting = VerbOutput.ToText(new VerbResult.Why(Held(ending: null)));
        var stranded = VerbOutput.ToText(
            new VerbResult.Why(Held(ending: FlightStates.Grounded)));

        await Assert.That(stranded).IsNotEqualTo(waiting)
            .Because("one of these resolves itself and the other never will. Rendering them "
                   + "alike is the patient-looking wait ADR-0019 warned about.");
        await Assert.That(stranded).Contains(FlightStates.Grounded)
            .Because("how it ended is what tells a person whether to wait, and the ending is "
                   + "sent rather than inferred.");
        await Assert.That(stranded).Contains("never")
            .Because("a leg that can never be offered has to say so in words. A reader who "
                   + "has to work that out from an ending is the discovery Decision 3 "
                   + "refuses.");
    }

    [Test]
    public async Task A_flight_that_follows_nothing_renders_exactly_as_it_did()
    {
        // THE POISON TWIN, and it is every flight in the field. `Held` is null
        // for all of them, and the two-absence sentence is RIGHT about them -
        // so this step must add a reading rather than replace one.
        var text = VerbOutput.ToText(new VerbResult.Why(new FlightAttribution
        {
            FlightNumber = "GG-42",
            EnvelopeVersion = "v3",
            Obligations = [],
        }));

        await Assert.That(text).Contains("Nothing has been judged against this flight.")
            .Because("a flight nothing has judged and that follows nothing is the ordinary "
                   + "case, and the sentence naming its two absences is the one that was "
                   + "argued for - good-grief#418's other half.");
        await Assert.That(text).DoesNotContain("follows")
            .Because("silence is not a claim to anything, and a hold reported for a flight "
                   + "that declared no order would be this verb inventing one.");
    }

    [Test]
    public async Task A_hold_is_reported_even_on_a_leg_something_has_judged()
    {
        // BECAUSE THE TWO ARE INDEPENDENT, and the easy mistake is to render the
        // hold only inside the empty-obligations branch - where it was first
        // noticed. A leg can be held AFTER a loop of its own was exhausted and
        // requeued, and then it has both an attribution and a reason it is not
        // being offered.
        var text = VerbOutput.ToText(new VerbResult.Why(new FlightAttribution
        {
            FlightNumber = "GG-88",
            EnvelopeVersion = "v3",
            Obligations =
            [
                new ObligationAttribution
                {
                    ObligationId = "in-scope",
                    Attachment = Attachments.Attached,
                    Because = "this obligation declares no condition, so it always applies",
                    Outcome = "satisfied",
                    Diagnosis = "Every path this flight touched is inside 'src/**'.",
                },
            ],
            Held = new LegHold { Follows = "GG-87", Subject = "the-schema" },
        }));

        await Assert.That(text).Contains("GG-87")
            .Because("a flight with obligations can still be waiting to be offered, and a "
                   + "hold rendered only where the list is empty would be invisible there.");
        await Assert.That(text).Contains("in-scope: attached")
            .Because("the hold is an addition to what this verb says, never a replacement "
                   + "for it.");
    }
}
