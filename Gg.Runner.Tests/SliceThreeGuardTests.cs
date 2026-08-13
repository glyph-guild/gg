using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// Slice three declares no new fact and gives the runner no new capability.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than beside the contract</b>, because this is the one test
/// assembly that can see both the wire vocabulary and the runner - and the guard
/// is about the two of them not moving together.
/// </para>
/// <para>
/// <b>The hardest guard in the slice, and it is a guard against building the
/// wrong thing.</b> A gate reads facts that already cross, and gates are
/// evaluated control-plane-side by Article IX. So if a step in this slice needs a
/// seventh — or in fact a ninth — fact kind, something is being built that is not
/// part of this slice, and the version moving is the first place that shows.
/// </para>
/// <para>
/// Asserted as literals rather than against the ledger's own last row, because a
/// test that reads the thing it is checking cannot notice it moving. The numbers
/// here are what the slice started at; changing them is a decision somebody makes
/// in a diff a reviewer can see.
/// </para>
/// </remarks>
public class SliceThreeGuardTests
{
    /// <summary>Where the fact vocabulary stood when slice three opened.</summary>
    private const string VocabularyAtSliceStart = "0.9.0";

    /// <summary>How many fact kinds crossed when slice three opened.</summary>
    private const int KindsAtSliceStart = 8;

    [Test]
    public async Task The_fact_vocabulary_version_has_not_moved()
    {
        await Assert.That(FactVocabulary.Version).IsEqualTo(VocabularyAtSliceStart)
            .Because("a gate reads facts that already cross. If this moved, something outside this "
                   + "slice is being built - stop and say why before continuing.");
    }

    [Test]
    public async Task No_fact_kind_has_been_added()
    {
        // The version and the count are two facts about the same thing, and a
        // version bumped for a rename would pass the assertion above.
        await Assert.That(FactKinds.All.Count).IsEqualTo(KindsAtSliceStart);

        await Assert.That(FactKinds.All.Order(StringComparer.Ordinal).ToList())
            .IsEquivalentTo(new[]
            {
                FactKinds.ChangeManifest,
                FactKinds.DestinationLanded,
                FactKinds.EnvironmentIdentity,
                FactKinds.HumanAccount,
                FactKinds.LoopDigest,
                FactKinds.LoopOutcome,
                FactKinds.LoopTranscript,
                FactKinds.SourceProvenance,
            }.Order(StringComparer.Ordinal).ToList())
            .Because("named, so a kind swapped for another is caught as well as a kind added.");
    }

    [Test]
    public async Task The_second_obligation_reads_a_fact_that_already_crossed()
    {
        // The guard's positive half: the new predicate is only allowed BECAUSE it
        // reads something that was already crossing. loop.outcome has crossed
        // since slice two step 3.
        await Assert.That(FactKinds.All).Contains(FactKinds.LoopOutcome);
        await Assert.That(ObligationPredicates.All).Contains(ObligationPredicates.LoopNotExhausted);

        await Assert.That(ObligationPredicates.All.Count).IsEqualTo(2)
            .Because("two predicates over two existing facts. A third is the next slice.");
    }

    [Test]
    public async Task The_runners_declared_capabilities_have_not_moved()
    {
        // A gate that needed the runner to do something new would be a gate on
        // the wrong side of the boundary. The capability type is where a new
        // ability would have to be declared, so its shape is what is checked.
        var declared = typeof(Gg.Runner.Execution.ExecutorCapabilities)
            .GetProperties()
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(declared).IsEquivalentTo(new[]
        {
            "Rung", "ReportsAttempts", "ReportsDuration", "ReportsMovesUsed", "ReportsTokens",
            "EnforcesMoves", "AttributesEditsToTools", "Gaps",
        }.Order(StringComparer.Ordinal).ToList())
            .Because("a new capability would appear here first, and appearing here is how a slice "
                   + "quietly becomes a different slice.");
    }

    [Test]
    public async Task The_guard_can_see_a_change()
    {
        // Liveness. Every assertion above is satisfied by a test that reads
        // nothing, and a guard that cannot fail is not a guard.
        await Assert.That(FactKinds.All).DoesNotContain("gate.decision")
            .Because("nothing like this exists yet, and the day it does this file is what says so.");

        await Assert.That(VocabularyAtSliceStart).IsNotEqualTo("0.10.0")
            .Because("the literal really is a literal, not a read of the value it checks.");
    }
}
