namespace Gg.Contracts.Tests;

/// <summary>
/// S53.1-04. A leg a revision no longer names ends <c>dropped</c>, and the
/// vocabulary is still closed against an eighth.
/// </summary>
/// <remarks>
/// <para>
/// <b>A VERSION EVENT, WHICH IS WHY THIS FILE EXISTS.</b>
/// <c>NominationEndings</c> says it out loud: <i>"a seventh ending is a version
/// event rather than a value quietly appearing"</i>. So this is not a list
/// growing by one - it is the contract moving, and the argument for the word is
/// recorded here rather than in a commit message nobody reads again.
/// </para>
/// <para>
/// <b>None of the six fitted, and that was checked one at a time.</b>
/// <c>withdrawn</c> is the subject ending - <i>the world's, not ours</i> - and a
/// dropped leg's subject is untouched. <c>superseded</c> requires a newer
/// version of the same subject, and a dropped leg has none, because the
/// revision that ended it stopped naming that subject at all. <c>declined</c>
/// and <c>refused</c> are a person's answer and a rule's, kept apart from each
/// other on purpose, and neither happened. <c>lapsed</c> is the clock's.
/// <c>opened</c> is the one ending that leaves something behind.
/// </para>
/// <para>
/// <b>Its own sentence is the discriminator a reader needs:</b> the plan that
/// named it no longer does - <b>ours, not the world's</b>. That is the exact
/// inverse of <c>withdrawn</c>'s, and the pair is only readable because both
/// are written down.
/// </para>
/// <para>
/// <b>The seventh party.</b> <c>Every_ending_says_who_ended_it</c> puts the
/// question in front of whoever adds one: the board, the world, a person, the
/// rules and the clock were five. This one is the PLANNER - the pass that
/// proposed the leg, revising itself - and it is a party none of the six named.
/// </para>
/// </remarks>
public class ASeventhEndingIsDeclaredTests
{
    [Test]
    public async Task A_leg_a_revision_no_longer_names_ends_dropped()
    {
        await Assert.That(NominationEndings.Dropped).IsEqualTo("dropped");
        await Assert.That(NominationEndings.All).Contains(NominationEndings.Dropped);
    }

    [Test]
    public async Task The_endings_are_seven_and_there_is_no_eighth()
    {
        // The closure itself, restated at the new count. Every reader refuses a
        // word it does not know, which is what closing the vocabulary buys -
        // and an eighth is the same design decision this one was, taken again.
        await Assert.That(NominationEndings.All).IsEquivalentTo((string[])
            [NominationEndings.Opened, NominationEndings.Superseded,
             NominationEndings.Withdrawn, NominationEndings.Declined,
             NominationEndings.Refused, NominationEndings.Lapsed,
             NominationEndings.Dropped]);

        await Assert.That(NominationEndings.All.Count).IsEqualTo(7)
            .Because("each ending names the party that ended it, and an eighth would have to "
                   + "name a seventh party - which is the question this test puts in front of "
                   + "whoever adds one.");
    }

    [Test]
    public async Task Dropped_is_distinct_from_the_six_it_joined()
    {
        // Not a synonym for any of them. The one that would actually be reached
        // for is `withdrawn`, and the two mean opposite halves of the same
        // silence.
        await Assert.That(NominationEndings.Dropped).IsNotEqualTo(NominationEndings.Withdrawn);
        await Assert.That(NominationEndings.Dropped).IsNotEqualTo(NominationEndings.Superseded);

        await Assert.That(NominationEndings.All.Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(NominationEndings.All.Count)
            .Because("two names for one ending is two endings as far as any reader is concerned.");
    }

    [Test]
    public async Task Dropping_is_nobody_to_type()
    {
        // The planner's, like superseding is the board's and lapsing is the
        // clock's. A door that accepted it would let a person record that a
        // revision did what they did - and there is no revision.
        await Assert.That(NominationDecisions.All).DoesNotContain(NominationEndings.Dropped);

        await Assert.That(NominationDecisions.All).IsEquivalentTo((string[])
            [NominationEndings.Opened, NominationEndings.Declined])
            .Because("two of the seven are a person's to type, and the seventh is not one of "
                   + "them.");
    }

    [Test]
    public async Task Standing_is_still_not_an_ending()
    {
        // The distinction the store depends on, re-asserted at seven: a row
        // recorded as having ended in the state that means it has not ended is
        // a row no reader can act on.
        await Assert.That(NominationEndings.All).DoesNotContain(NominationStates.Standing);
    }

    [Test]
    public async Task It_is_a_wire_value_read_the_way_the_other_six_are()
    {
        await Assert.That(NominationEndings.Dropped).IsNotEmpty();
        await Assert.That(NominationEndings.Dropped)
            .IsEqualTo(NominationEndings.Dropped.ToLowerInvariant())
            .Because("these cross the wire and are compared ordinally on both sides.");
    }

    [Test]
    public async Task Nothing_a_flight_does_is_this_ending()
    {
        // The adjacent vocabulary, checked against the new word: a flight has
        // no `dropped`, so this one carries no flight's outcome.
        foreach (var flightWord in (string[])["landed", "failed", "grounded", "open", "unknown"])
        {
            await Assert.That(NominationEndings.Dropped).IsNotEqualTo(flightWord);
        }
    }
}
