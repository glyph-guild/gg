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
    /// <summary>Where the fact vocabulary stood when slice twelve opened.</summary>
    private const string VocabularyAtSliceStart = "0.16.0";

    /// <summary>How many fact kinds cross. Nine, and staying nine is the point.</summary>
    private const int KindsThatCross = 9;

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
                   + "and this literal moving in a diff a reviewer sees.");
    }
}
