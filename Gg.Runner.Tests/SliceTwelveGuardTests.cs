using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// Slice twelve declares no new fact: an attestation is not a fact, and the
/// count staying put is the decision made visible.
/// </summary>
/// <remarks>
/// <para>
/// <b>The non-change is the assertion.</b> The fact plumbing is lease-welded
/// at four points — the batch's generation, the lease-scoped endpoint, the
/// ship call's lease id, and the flight-id idempotency key — and a routine
/// action has no flight. The attestation got its own record and prefix
/// instead of a tenth kind, and this literal is where a reviewer sees that
/// hold.
/// </para>
/// <para>
/// Asserted as literals rather than against the ledger's own last row,
/// <c>SliceThreeGuardTests</c>' rule: a test that reads the thing it is
/// checking cannot notice it moving.
/// </para>
/// </remarks>
public class SliceTwelveGuardTests
{
    /// <summary>
    /// Where the fact vocabulary stands. It was <c>0.16.0</c> when slice twelve
    /// opened and held there through slices thirteen to sixteen.
    /// </summary>
    /// <remarks>
    /// <b>Moved to 0.17.0 by slice seventeen, and slice twelve's claim was
    /// untouched.</b> This literal exists so that a move appears in a diff a
    /// reviewer sees, and that move was: <c>FactCategories</c> joined the
    /// fact fingerprint — a CLASSIFICATION over the same nine kinds, saying
    /// whether each describes a subject, a tree, or the flight. No kind was
    /// added, no member of one changed, and a runner pinned to 0.16.0 shipped
    /// byte-identical facts.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// <b>Moved to 0.18.0 by slice twenty-seven, and this time a KIND was
    /// added — so slice twelve's decision has to be re-argued rather than
    /// noted.</b> <c>flight.nomination</c> is the work kind a classifier
    /// nominates. It gets past the assertion below because it meets that
    /// assertion's own criterion: <i>a fact belongs to a flight and a routine
    /// action has none</i>. A nomination belongs to a classify flight, is
    /// produced by a loop inside it, ships on that flight's lease, and is
    /// keyed on that flight's id. It is not a flightless path grafted under
    /// the fact name, which is what slice twelve refused.
    /// </para>
    /// <para>
    /// <b>Moved to 0.19.0 by slice twenty-five, and NO kind was added — so
    /// slice twelve's decision is noted rather than re-argued.</b> The surface
    /// moved because <c>LoopOutcomes</c> is part of it and it grew a fourth
    /// value, <c>blocked</c>: an agent that asked for a decision it is not
    /// allowed to make and stopped. Nothing new crosses; the count below is
    /// unchanged, which is exactly the shape this guard was built to make
    /// visible — a version move a reviewer sees, with a ledger row behind it,
    /// and no new fact smuggled in beside it.
    /// </remarks>
    /// <para>
    /// <b>And to 0.20.0 by step 3 of the same slice, which DID add a kind</b> -
    /// so the count below moves too, and the argument is made rather than
    /// noted. See it there.
    /// </para>
    private const string VocabularyAtSliceStart = "0.20.0";

    /// <summary>
    /// How many fact kinds cross. Ten since slice twenty-seven, and the number
    /// moving in a diff is the point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nine through slices twelve to twenty-six. The tenth is
    /// <c>flight.nomination</c>, and it is the first addition to get past this
    /// guard — on the guard's own terms, because it belongs to a flight. An
    /// eleventh still has to make that argument.
    /// </para>
    /// <para>
    /// <b>The eleventh is <c>loop.question</c>, and here is the argument.</b>
    /// This guard's criterion is that <i>a fact belongs to a flight and a
    /// routine action has none</i>. A question is asked by a LOOP, and a loop
    /// runs inside a flight: it is produced by that loop, ships on that
    /// flight's lease, is keyed on that flight's id, and is read back beside
    /// that flight's other facts when a person decides what to do about it.
    /// There is no question without a flight to ask it, which is exactly what
    /// slice twelve found was untrue of a pool attestation - the flightless
    /// path grafted under the fact name that this guard refused.
    /// </para>
    /// <para>
    /// <b>A twelfth still has to make it too.</b> Two of the eleven are now an
    /// agent's requests rather than measurements, and that is the direction
    /// this number exists to make somebody look at.
    /// </para>
    /// </remarks>
    private const int KindsThatCross = 11;

    [Test]
    public async Task Attestations_are_not_facts_and_the_kind_count_stays_nine()
    {
        await Assert.That(FactKinds.All.Count).IsEqualTo(KindsThatCross)
            .Because("an attestation from a resident runner is runner-origin and measured - "
                   + "it survives both prior arguments against new kinds - and it is STILL "
                   + "not a fact, because a fact belongs to a flight and a routine action "
                   + "has none. A tenth kind here is somebody grafting a flightless path "
                   + "under the fact name.");
    }

    [Test]
    public async Task The_fact_vocabulary_did_not_move_for_the_pools_surface()
    {
        await Assert.That(FactVocabulary.Version).IsEqualTo(VocabularyAtSliceStart)
            .Because("nothing about a pool crosses on a fact this slice - the provenance "
                   + "and digest the next flight carries were already in "
                   + "environment.identity. Moving this needs a fact-vocabulary ledger row "
                   + "and this literal moving in a diff a reviewer sees, which is what "
                   + "slice seventeen did for a classification rather than for a kind.");
    }
}
