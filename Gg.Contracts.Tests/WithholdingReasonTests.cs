using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A flight nobody can claim because the fleet was WITHHELD says so, and says
/// which of the three ways it happened.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three kinds, not one, and the split is the same one
/// <see cref="ReasonKinds.DeclaredAndAbsent"/> and
/// <see cref="ReasonKinds.ForgeUnreachable"/> already make.</b> Somebody
/// DECLARED a reservation and somebody DECLARED a parking; NOBODY declared a
/// laptop being shut. Collapsing them sends an operator whose machine is simply
/// closed off to go and check a configuration that is perfectly correct, and
/// sends the person whose flight is queued behind a colleague's reservation off
/// to bring up capacity they already have.
/// </para>
/// <para>
/// <b>Every one of them is <c>failed</c>, never <c>refused</c>.</b> Nothing was
/// refused: the flight was admitted, it is in the air, and it is waiting. A
/// withholding filed under <c>refused</c> would put a flight that is going to
/// run in the same bucket as a document somebody was told no about.
/// </para>
/// <para>
/// <b>Reserving and parking are not capability gaps, and that is the whole
/// point of naming them.</b> "No runner advertises environment=dev" means bring
/// one up; "the only runner advertising environment=dev is reserved to somebody"
/// means the capacity is there and a person is holding it. Same silence,
/// opposite remedy — <see cref="ReasonKinds.NoRunnerAdvertises"/> would send
/// somebody to buy a machine they own.
/// </para>
/// </remarks>
public class WithholdingReasonTests
{
    [Test]
    public async Task The_three_withholdings_are_three_kinds()
    {
        // THE CRITERION, as membership. A kind absent from All is a kind no
        // fingerprint covers and no totality test can see.
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.RunnerReserved);
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.RunnerParked);
        await Assert.That(ReasonKinds.All).Contains(ReasonKinds.DirectedRunnerAbsent);
    }

    [Test]
    public async Task A_withheld_flight_is_waiting_and_never_refused()
    {
        // A HALT IS NOT A REFUSAL - the rule NoRunnerAdvertises, PoolWarming,
        // DeclaredAndAbsent and ForgeUnreachable are all already filed under.
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.RunnerReserved))
            .IsEqualTo(ReasonFamilies.Failed);
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.RunnerParked))
            .IsEqualTo(ReasonFamilies.Failed);
        await Assert.That(ReasonKinds.FamilyOf(ReasonKinds.DirectedRunnerAbsent))
            .IsEqualTo(ReasonFamilies.Failed);
    }

    [Test]
    public async Task A_reservation_names_the_person_holding_it()
    {
        // BY DISPLAY, NEVER BY ID - TakeoverHeld.By settled it: "somebody else
        // has this" sends a person nowhere and a uuid sends them somewhere
        // worse. The label is named too, because a flight requiring two is
        // waiting on ONE of them and a count is not a name.
        var sentence = Reason.Sentence(ReasonKinds.RunnerReserved, ["environment=dev", "Dana"]);

        await Assert.That(sentence).Contains("environment=dev");
        await Assert.That(sentence).Contains("Dana");
        await Assert.That(sentence).DoesNotContain("no runner advertises")
            .Because("the capacity exists and a person is holding it. Sending somebody to bring "
                   + "up a runner they already own is the wrong remedy, told confidently.");
    }

    [Test]
    public async Task A_reservation_whose_holder_has_left_says_so_rather_than_naming_nobody()
    {
        // S24.7-05, AND THE OWNER'S RULING: leave it, report it, let a person
        // release it. A reservation held by a departed principal releases on
        // nothing - no expiry, no cleanup - so a runner that mysteriously takes
        // no work forever is exactly what this prevents. One param instead of
        // two, the way a schedule clearing carries an eta or does not.
        var sentence = Reason.Sentence(ReasonKinds.RunnerReserved, ["environment=dev"]);

        await Assert.That(sentence).Contains("environment=dev");
        await Assert.That(sentence).Contains("no longer");
        await Assert.That(sentence).DoesNotContain("(unnamed)")
            .Because("a placeholder where a person's name goes reads as a rendering bug, and the "
                   + "reader learns nothing about why the runner takes nothing.");
    }

    [Test]
    public async Task A_parking_quotes_the_reason_the_person_gave()
    {
        // THE 0.94.0 NOTE'S OWN PROMISE: the reason is a member because "a
        // runner taking nothing for a fortnight with no reason attached is the
        // failure mode this most likely produces, and it is the sentence a
        // withheld flight quotes back". This is the quoting back.
        var sentence = Reason.Sentence(ReasonKinds.RunnerParked, ["environment=dev", "draining for 26.4"]);

        await Assert.That(sentence).Contains("environment=dev");
        await Assert.That(sentence).Contains("draining for 26.4");
    }

    [Test]
    public async Task A_parking_with_no_reason_given_still_says_it_was_parked()
    {
        // The reason is nullable at its source, so a sentence that required one
        // would throw on the parking somebody made in a hurry - turning a
        // governed wait into a 500 on the surface meant to explain it.
        var sentence = Reason.Sentence(ReasonKinds.RunnerParked, ["environment=dev"]);

        await Assert.That(sentence).Contains("environment=dev");
        await Assert.That(sentence).Contains("parked");
    }

    [Test]
    public async Task A_flight_directed_at_a_runner_that_is_not_beating_blames_nobody()
    {
        // THE ForgeUnreachable HALF. Nothing is wrong with what was set up: the
        // person named a machine and the machine is not currently answering.
        // The remedy is to start it, and a sentence that implied a
        // misconfiguration would send them to re-read a correct one.
        var sentence = Reason.Sentence(ReasonKinds.DirectedRunnerAbsent, ["dana-laptop"]);

        await Assert.That(sentence).Contains("dana-laptop");
        await Assert.That(sentence).DoesNotContain("reserved");
    }

    [Test]
    public async Task Withholding_and_a_capability_gap_do_not_word_themselves_alike()
    {
        // The distinction has to survive a person SKIMMING. Two sentences that
        // differ by one clause get read as the same sentence, and then the
        // split that cost three reason kinds buys nothing.
        var gap = Reason.Sentence(ReasonKinds.NoRunnerAdvertises, ["environment=dev"]);
        var reserved = Reason.Sentence(ReasonKinds.RunnerReserved, ["environment=dev", "Dana"]);
        var parked = Reason.Sentence(ReasonKinds.RunnerParked, ["environment=dev"]);

        await Assert.That(reserved).IsNotEqualTo(gap);
        await Assert.That(parked).IsNotEqualTo(gap);
        await Assert.That(parked).IsNotEqualTo(reserved);
    }
}
