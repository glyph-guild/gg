using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A preview is the KIND's, not the machine's — and until this, the hold and the
/// fact were both the machine's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-327, a learning rehearsal that had nothing to do with
/// previews.</b> It ran on vmlinux002, which holds jdapp slot 01 with its
/// connector permanently up, and two things followed that nobody asked for: it
/// shipped <c>preview.url: https://jdapp-01.goodgrief.dev</c> having served
/// nothing, and its runner held its lease for the twelve-hour preview hold,
/// taking no other work. Grounding the flight did not release it, because the
/// hold is the runner's own state. Only a restart did.
/// </para>
/// <para>
/// <b><see cref="TreeRetention.HoldsItsMachine"/>'s own remark already said what
/// it should do</b> — <i>"a flight gated on a preview stays alive, and its runner
/// takes no work"</i> — and what it did was hold for every flight on a machine
/// that serves an address. <c>_served</c> is set once, on the beat, when the
/// MACHINE learns its slot; nothing about it is about this flight.
/// </para>
/// <para>
/// <b>And the fact was exceeding <c>produces:</c></b>, which is the field that
/// exists to bound exactly this: <i>"what the kind can YIELD, not what its runner
/// POSTS."</i> A runner posting a fact the kind never declared is the permissive
/// failure that field was added to stop, arriving from the other side.
/// </para>
/// <para>
/// <b>So the kind's own declaration is the test.</b> Not a new flag: a work kind
/// that wants a preview already says so, and one that does not already says
/// nothing.
/// </para>
/// </remarks>
public class APreviewBelongsToItsKindTests
{
    private static ExposureServed Serving() => new()
    {
        Url = "https://jdapp-01.goodgrief.dev",
        Exposure = "jdapp",
        Slot = "01",
    };

    private static readonly string[] Previewing = [FactKinds.LoopOutcome, FactKinds.PreviewUrl];
    private static readonly string[] NotPreviewing = [FactKinds.LoopOutcome, FactKinds.ChangeManifest];

    [Test]
    public async Task A_kind_that_produces_a_preview_holds_its_machine()
    {
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), Previewing)).IsTrue();
    }

    /// <summary>
    /// And a kind that does not, does not — which is GG-327.
    /// </summary>
    /// <remarks>
    /// The machine still serves an address: the tunnel is up and stays up, which
    /// is what the exposure work was for. What changed is that serving is no
    /// longer a reason for THIS flight to pin the machine.
    /// </remarks>
    [Test]
    public async Task A_kind_that_does_not_leaves_the_machine_free()
    {
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), NotPreviewing)).IsFalse()
            .Because("a learning rehearsal on a slot-holding machine made it a "
                   + "one-flight-per-twelve-hours machine, and nothing it did was about a "
                   + "preview.");
    }

    /// <summary>
    /// A document that said nothing about what it produces holds nothing.
    /// </summary>
    /// <remarks>
    /// Null is silence, not emptiness, and silence is not a claim to a preview.
    /// Every envelope written before <c>produces:</c> existed says nothing, and
    /// none of them wanted a machine held.
    /// </remarks>
    [Test]
    public async Task A_kind_that_said_nothing_holds_nothing()
    {
        await Assert.That(TreeRetention.HoldsItsMachine(Serving(), null)).IsFalse();
    }

    [Test]
    public async Task The_tree_is_kept_on_the_same_terms_as_the_machine()
    {
        // THE SAME FACT, deliberately: two rules that could disagree would leave
        // a held machine serving from a released tree, which is the state GG-303
        // was found in.
        await Assert.That(TreeRetention.MustKeep(landed: true, Serving(), Previewing)).IsTrue();

        await Assert.That(TreeRetention.MustKeep(landed: true, Serving(), NotPreviewing)).IsFalse()
            .Because("a tree kept for a preview nobody asked for is disk nothing reclaims, "
                   + "and nothing reaps a kept preview tree.");
    }

    [Test]
    public async Task An_unlanded_flight_keeps_its_tree_whatever_its_kind_produces()
    {
        await Assert.That(TreeRetention.MustKeep(landed: false, null, NotPreviewing)).IsTrue()
            .Because("a flight that has not landed is already held, and that is not about "
                   + "previews at all.");
    }

    /// <summary>The runner is told what the kind produces, because nothing told it before.</summary>
    [Test]
    public async Task The_lease_carries_what_the_kind_produces()
    {
        var loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = "frontier",
            Moves = [LoopMoves.Read],
            WallClockSeconds = 60,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Produces = Previewing,
        };

        await Assert.That(loop.Produces).IsNotNull();
        await Assert.That(loop.Produces).Contains(FactKinds.PreviewUrl);
    }
}
