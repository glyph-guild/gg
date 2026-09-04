using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The declined family's first kind arrives, and a decline never wears a
/// gap's clothes: the bound names itself, its clearing, and — for a schedule
/// — when it opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice ten reserved exactly this:</b> "its first kind arrives with the
/// producer that mints it, because a constant nothing produces is a promise
/// nobody has to keep." The producer is the control-plane decider, which
/// declines by not deciding; the read models render the bound from the same
/// inputs, and this is the sentence they derive.
/// </para>
/// <para>
/// <b>An unknown clearing THROWS, one param deeper than an unknown kind.</b>
/// The sentence branches on the clearing; a branch that blanked on a value
/// nobody declared would read as health, which is the exact defect the
/// kind-level poison already kills.
/// </para>
/// </remarks>
public class BoundReasonTests
{
    [Test]
    public async Task Blocked_by_bound_is_the_declined_familys_first_kind()
    {
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.BlockedByBound))
            .IsEqualTo(ReasonFamilies.Declined)
            .Because("a bound the tenant declared is a decision they already made - "
                   + "declined, never failed: the fleet is fine, the number is theirs.");
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.BlockedByBound);
    }

    [Test]
    public async Task Pool_warming_is_failed_because_the_world_cannot_satisfy_it_yet()
    {
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.PoolWarming))
            .IsEqualTo(ReasonFamilies.Failed)
            .Because("warming is neither a gap nor a bound - without its own kind it "
                   + "would have to wear one of their clothes, and both remedies would "
                   + "be wrong.");
    }

    [Test]
    public async Task The_capacity_clearing_names_the_bound_and_what_clears_it()
    {
        var sentence = Reason.Sentence(
            ReasonKinds.BlockedByBound, ["pool-maximum", BoundClearings.Capacity]);

        await Assert.That(sentence).Contains("declined by your own bound");
        await Assert.That(sentence).Contains("pool-maximum");
        await Assert.That(sentence).Contains("peer flight releases")
            .Because("the remedy is the sentence's other half: a capacity bound clears "
                   + "itself, and a person who reads this buys no hardware.");
    }

    [Test]
    public async Task The_schedule_clearing_carries_when_it_opens()
    {
        var sentence = Reason.Sentence(
            ReasonKinds.BlockedByBound, ["active-hours", BoundClearings.Schedule, "08:00Z"]);

        await Assert.That(sentence).Contains("active-hours");
        await Assert.That(sentence).Contains("opens 08:00Z");
    }

    [Test]
    public async Task A_schedule_clearing_without_an_eta_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() =>
            Reason.Sentence(ReasonKinds.BlockedByBound, ["active-hours", BoundClearings.Schedule]));

        await Assert.That(refused!.Message).Contains("eta")
            .Because("a schedule that cannot say when it opens is a wait with no end a "
                   + "reader can plan around.");
    }

    [Test]
    public async Task An_unknown_clearing_poisons_rather_than_blanking()
    {
        var poisoned = Assert.Throws<InvalidOperationException>(() =>
            Reason.Sentence(ReasonKinds.BlockedByBound, ["spend-ceiling", "authority"]));

        await Assert.That(poisoned!.Message).Contains("authority")
            .Because("the authority clearing arrives with the first metered strategy "
                   + "row - a render that blanked on it today would read as health.");
    }

    [Test]
    public async Task The_warming_sentence_names_the_pool_and_what_clears_it()
    {
        var sentence = Reason.Sentence(ReasonKinds.PoolWarming, ["payments-pool"]);

        await Assert.That(sentence).Contains("payments-pool");
        await Assert.That(sentence).Contains("warming");
    }

    [Test]
    public async Task The_checklists_third_satisfier_is_the_prophesied_design_event()
    {
        // THE SUBJECT IS THE THIRD VALUE, not the length of the list. This
        // asserted the whole closure once, which made it a SECOND exhaustive
        // pin on a vocabulary ChecklistSurfaceTests already pins - so every
        // later design event failed in two files and the reader of either had
        // to go and find the other. The closure lives there; what lives here is
        // that a bound's own satisfier exists and is spelled the way the
        // producer spells it.
        await Assert.That(ChecklistSatisfiers.All)
            .Contains(ChecklistSatisfiers.DeclinedByBound)
            .Because("the closure comment said it in slice eight: a third value here "
                   + "means a strategy exists - a design event that arrives as a "
                   + "deliberate contract change. This is that change.");
        await Assert.That(ChecklistSatisfiers.DeclinedByBound).IsEqualTo("declined-by-bound");
    }

    [Test]
    public async Task The_clearings_close_at_two_and_authority_is_deliberately_absent()
    {
        await Assert.That(BoundClearings.All)
            .IsEquivalentTo((string[])[BoundClearings.Capacity, BoundClearings.Schedule])
            .Because("docker-host meters no spend; the authority clearing arrives with "
                   + "the first metered strategy row, not as a constant nothing produces.");
    }
}
