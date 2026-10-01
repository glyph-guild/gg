using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight holding its machine for a preview keeps the instance that preview is
/// served from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-522, the first flight ever granted an environment instance,
/// and this slice wrote the defect.</b> The agent did everything asked: it built
/// an image in the daemon it was given, ran it with
/// <c>-p 127.0.0.1:8080:80</c>, confirmed the port mapping in <c>docker ps</c>,
/// and fetched both <c>/</c> and <c>/assets/build-info.json</c> to check the
/// change it had just baked was being served. The preview WORKED.
/// </para>
/// <para>
/// <b>Then gg destroyed it, one second later, and asked a person to review it.</b>
/// <c>BringDownAsync</c> runs immediately after the agent and before the facts,
/// the push and the hold — so the order was: serve, empty the instance, announce
/// the address, hold the machine out of service for twelve hours waiting for
/// somebody to look at an address that now returns 502. The container was gone
/// from <c>docker ps -a</c> entirely, which is how we know it was removed rather
/// than crashed.
/// </para>
/// <para>
/// <b>Two correct halves, built a slice apart.</b> Slice fifty-four's preview
/// holds the tree and the machine because what they carry is somebody's
/// unreviewed work; slice fifty-six added an instance and taught the departure to
/// empty it. Neither is wrong on its own. Nothing told the second about the
/// first, and the composition root is where that shows — the same shape this
/// slice family keeps producing.
/// </para>
/// <para>
/// <b>The existing predicate decides it, rather than a new flag.</b>
/// <c>TreeRetention.HoldsItsMachine</c> already means "this flight's environment
/// must survive, because a person was asked to look at it". An instance is part
/// of that environment. A second predicate spelling the same condition would be
/// two names for one fact, and the one nobody updated would be the one that ran.
/// </para>
/// </remarks>
public class ATearDownThatDestroysThePreviewTests
{
    [Test]
    public async Task A_flight_holding_for_a_preview_keeps_its_instance()
    {
        // THE DEFECT, STATED. Everything about this departure says "empty it" -
        // an ordinary flight hold, nothing inherited - and it must not, because
        // the address gg is about to publish is served from inside it.
        await Assert.That(InstanceDeparture.BringsDown(
                InstanceHolds.Flight, holdsItsMachine: true)).IsFalse()
            .Because("GG-522 served its change, had the container removed a second later, and "
                   + "then held its machine for twelve hours waiting for a person to review a "
                   + "502.");
    }

    [Test]
    public async Task A_flight_that_serves_nothing_still_gives_its_instance_back()
    {
        // THE POISON TWIN, and rule 3's whole point. Most flights host nothing
        // and must keep giving the instance back: a tear-down that stopped
        // happening would leave every flight's containers for the next one, and
        // the reclaim on the way in would be carrying the entire burden.
        await Assert.That(InstanceDeparture.BringsDown(
                InstanceHolds.Flight, holdsItsMachine: false)).IsTrue()
            .Because("this is every flight in the field, and the courtesy rule 3 describes.");
    }

    [Test]
    public async Task An_itinerary_keeps_its_instance_whether_it_serves_or_not()
    {
        // UNCHANGED, AND CHECKED BOTH WAYS. A leg cannot know whether a sibling
        // is coming, so guessing wrong destroys the environment the next leg was
        // granted - which is true regardless of whether this leg served anything.
        foreach (var hold in (string[])[InstanceHolds.Itinerary, InstanceHolds.Inherited])
        {
            await Assert.That(InstanceDeparture.BringsDown(hold, holdsItsMachine: false)).IsFalse()
                .Because($"'{hold}' was already exempt and this change must not narrow it.");
            await Assert.That(InstanceDeparture.BringsDown(hold, holdsItsMachine: true)).IsFalse();
        }
    }

    [Test]
    public async Task It_agrees_with_the_hold_vocabulary_it_extends()
    {
        // ONE LAW, NOT TWO. Without this the new function could disagree with
        // InstanceHolds about an ordinary departure and both would look right in
        // isolation - which is the two-correct-halves shape that caused this.
        foreach (var hold in (string?[])[InstanceHolds.Flight, InstanceHolds.Itinerary,
                                         InstanceHolds.Inherited, null, "something-new"])
        {
            await Assert.That(InstanceDeparture.BringsDown(hold, holdsItsMachine: false))
                .IsEqualTo(InstanceHolds.BringsDownOnDeparture(hold))
                .Because($"for '{hold}', a flight serving no preview must depart exactly as the "
                       + "hold vocabulary already says. This adds a reason to KEEP an instance "
                       + "and takes none away.");
        }
    }
}
