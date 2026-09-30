using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// A leg that received its instance from a sibling leaves the stack alone, at
/// both ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six step 4, and the runner cannot work any of this out for
/// itself.</b> Whether a sibling leg still stands is knowable only where the legs
/// are recorded, so the lease says <b>whose the hold is</b> and this side reads
/// it. Two behaviours hang off that one value — whether to empty on the way in
/// and whether to bring the stack down on the way out — and they are decided in
/// one place here, because a rule spelled twice is a rule that can disagree with
/// itself.
/// </para>
/// <para>
/// <b>Whose the hold is, NEVER whether the stack is running</b> (rule 13). A hold
/// is a fact about the grant, written by the transaction that took it; it stays
/// true until the hold lapses. A liveness flag would be read as authority and
/// would be wrong the moment a container died — so the agent still asks the
/// daemon, and S56.4-06 asserts nothing on the wire answers for it.
/// </para>
/// <para>
/// <b>A leg that TOOK the hold still empties.</b> It is the first of its
/// itinerary, so nothing of this plan has run yet — but the instance may hold
/// what a dead flight left, which is the case the reclaim exists for.
/// </para>
/// <para>
/// <b>No itinerary hold is brought down at the end</b>, including the first leg's.
/// A leg cannot know whether a sibling is coming, and guessing wrong destroys the
/// environment the next leg was given. The hold lapsing is what frees it, and the
/// next taker empties it.
/// </para>
/// </remarks>
public class ALegDoesNotEmptyWhatItInheritedTests
{
    [Test]
    public async Task A_leg_that_inherited_its_hold_does_not_empty_the_instance()
    {
        await Assert.That(InstanceHolds.EmptiesOnArrival(InstanceHolds.Inherited)).IsFalse()
            .Because("a sibling leg brought this stack up and is the reason the itinerary kept "
                   + "the instance at all; emptying it here is the defect reuse exists to "
                   + "avoid, and it would look like a plan that rebuilds its world per leg.");
    }

    [Test]
    public async Task A_leg_that_took_the_hold_still_empties_it()
    {
        // FIRST OF ITS ITINERARY: nothing of this plan has run, but the instance
        // may hold what a dead flight left, which is the case the reclaim exists
        // for. "An itinerary holds it" is not "somebody's stack is in it".
        await Assert.That(InstanceHolds.EmptiesOnArrival(InstanceHolds.Itinerary)).IsTrue();
    }

    [Test]
    public async Task A_flight_holding_alone_empties_as_it_always_did()
    {
        await Assert.That(InstanceHolds.EmptiesOnArrival(InstanceHolds.Flight)).IsTrue();
        await Assert.That(InstanceHolds.EmptiesOnArrival(null)).IsTrue()
            .Because("absent is every flight in the field and every lease from a control plane "
                   + "older than this member, and they must behave exactly as before.");
    }

    [Test]
    public async Task No_itinerary_hold_is_brought_down_at_the_end()
    {
        // INCLUDING THE FIRST LEG'S. A leg cannot know whether a sibling is
        // coming, and guessing wrong destroys the environment the next leg was
        // granted. The hold lapsing frees it; the next taker empties it.
        await Assert.That(InstanceHolds.BringsDownOnDeparture(InstanceHolds.Itinerary)).IsFalse();
        await Assert.That(InstanceHolds.BringsDownOnDeparture(InstanceHolds.Inherited)).IsFalse();
    }

    [Test]
    public async Task A_flight_holding_alone_brings_its_stack_down()
    {
        await Assert.That(InstanceHolds.BringsDownOnDeparture(InstanceHolds.Flight)).IsTrue();
        await Assert.That(InstanceHolds.BringsDownOnDeparture(null)).IsTrue();
    }

    [Test]
    public async Task The_lease_carries_whose_the_hold_is()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseLoop)]).Contains("instanceHold")
            .Because("the runner has no way to work it out: whether a sibling leg still stands "
                   + "is knowable only where the legs are recorded.");
    }

    [Test]
    public async Task And_says_nothing_about_whether_the_stack_is_running()
    {
        // RULE 13, AND THE DISTINCTION THIS WHOLE STEP RESTS ON. A hold cannot go
        // stale - it is written by the transaction that took it and stays true
        // until it lapses. A liveness flag would be wrong the moment a container
        // died, and would be read as authority anyway.
        var members = ProtocolSurface.JsonMembers[typeof(LeaseLoop)];

        foreach (var forbidden in (string[])["running", "stackUp", "up", "live", "alive"])
        {
            await Assert.That(members.Any(m =>
                string.Equals(m, forbidden, StringComparison.OrdinalIgnoreCase))).IsFalse()
                .Because($"'{forbidden}' would answer for the daemon, which is the one thing "
                       + "rule 13 forbids. The agent asks it.");
        }
    }

    [Test]
    public async Task An_unknown_hold_is_treated_as_a_flights_own()
    {
        // A CONTROL PLANE AHEAD OF THIS BINARY can name a hold this one has never
        // heard of, and the safe reading is the conservative one: empty before
        // use and bring down after. Reuse is an optimisation; a dirty instance
        // is a defect.
        await Assert.That(InstanceHolds.EmptiesOnArrival("something-new")).IsTrue();
        await Assert.That(InstanceHolds.BringsDownOnDeparture("something-new")).IsTrue();
    }
}
