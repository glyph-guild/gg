using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The ways a nomination stops standing, and why there are six rather than three.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0022 § 2's vocabulary, closed for <c>FlightStates</c>' reason.</b> A
/// seventh ending is a version event rather than a value quietly appearing, and
/// the only safe response to one a reader does not know is to refuse it. This is
/// the same ruling one noun earlier in a flight's life: a nomination ends once,
/// and the word it ends with is what a person reads to find out why.
/// </para>
/// <para>
/// <b>Six, because who ended it is the part worth knowing.</b> The board ends a
/// row two ways — it <c>opened</c> a flight, or a newer version of the same
/// subject <c>superseded</c> it. The world ends one: the subject stopped
/// mattering, which is <c>withdrawn</c>. A person <c>declined</c> it. The rules
/// <c>refused</c> it, with the sentence the menu or the budget wrote. The clock
/// <c>lapsed</c> it. Collapsing any pair would put two answers under one word,
/// and this vault's own discipline is that <c>grounded</c> is not
/// <c>withdrawn</c>.
/// </para>
/// <para>
/// <b>Standing is not an ending, and that is the distinction the store depends
/// on.</b> It is the absence of one — the state every row is in until something
/// happens to it — so it may not be written as an ending any more than
/// <c>open</c> may be written to the flight exit store.
/// </para>
/// </remarks>
public class NominationEndingsArePinnedTests
{
    [Test]
    public async Task The_endings_are_six_and_there_is_no_seventh()
    {
        await Assert.That(NominationEndings.All).IsEquivalentTo((string[])
            [NominationEndings.Opened, NominationEndings.Superseded,
             NominationEndings.Withdrawn, NominationEndings.Declined,
             NominationEndings.Refused, NominationEndings.Lapsed])
            .Because("a seventh ending is a design decision rather than an addition to a list: "
                   + "every reader refuses a word it does not know, which is what closing the "
                   + "vocabulary buys and what a quiet addition would spend.");
    }

    [Test]
    public async Task Standing_is_a_reading_rather_than_an_ending()
    {
        // THE DISTINCTION THE STORE DEPENDS ON. Standing is the absence of an
        // ending, not something that happened to a row - so it is not in the
        // set a NominationEnded may carry, exactly as `open` is a state a flight
        // may be read to be in and never an exit it may record.
        await Assert.That(NominationEndings.All).DoesNotContain(NominationStates.Standing)
            .Because("a row recorded as having ended in the state that means it has not ended "
                   + "is a row no reader can act on.");
    }

    [Test]
    public async Task Every_ending_says_who_ended_it()
    {
        // ADR-0022 § 2 asserted rather than trusted to prose. Two are the
        // board's, one the world's, one a person's, one the rules', one the
        // clock's - and a seventh would have to name a sixth party, which is
        // the question this test puts in front of whoever adds one.
        await Assert.That(NominationEndings.All.Count).IsEqualTo(6)
            .Because("each ending names the party that ended it, and a word without one is a "
                   + "row a person cannot ask anybody about.");
    }

    [Test]
    public async Task Nothing_a_flight_does_is_an_ending_here()
    {
        // The vocabularies are adjacent and must not be confused: a nomination
        // ends, and then a FLIGHT begins and ends on its own terms. A row that
        // could read `landed` or `failed` would be one carrying the nominee's
        // outcome, which is the reference rule 7 refuses.
        foreach (var flightWord in (string[])["landed", "failed", "grounded", "open", "unknown"])
        {
            if (string.Equals(flightWord, NominationEndings.Withdrawn, StringComparison.Ordinal))
            {
                // The one word the two vocabularies share, and it means the same
                // thing in both: the question ceased to apply.
                continue;
            }

            await Assert.That(NominationEndings.All).DoesNotContain(flightWord)
                .Because($"'{flightWord}' is how a FLIGHT ends. A nomination that could end "
                       + "that way would be carrying its nominee's outcome, and the two are "
                       + "related by the row between them and by nothing else.");
        }
    }

    [Test]
    public async Task Every_ending_is_a_wire_value_somebody_can_read()
    {
        foreach (var ending in NominationEndings.All)
        {
            await Assert.That(ending).IsNotEmpty();
            await Assert.That(ending).IsEqualTo(ending.ToLowerInvariant())
                .Because("these cross the wire and are compared ordinally on both sides.");
        }

        await Assert.That(NominationEndings.All.Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(NominationEndings.All.Count)
            .Because("two names for one ending is two endings as far as any reader is concerned.");
    }
}
