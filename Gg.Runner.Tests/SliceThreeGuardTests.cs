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
    /// <summary>
    /// Where the fact vocabulary stood when slice three opened.
    /// </summary>
    /// <remarks>
    /// It has moved since, to 0.10.0, and the reason is recorded on
    /// <see cref="FactVocabulary.Version"/>. Kept as the floor rather than as an
    /// equality, because a version that cannot move is a guard against progress.
    /// </remarks>
    private const string VocabularyAtSliceStart = "0.9.0";

    /// <summary>
    /// How many fact kinds cross.
    /// </summary>
    /// <remarks>
    /// Nine. Slice three opened at eight and step 3 added <c>destination.pushed</c>,
    /// which is a decision in a diff a reviewer can see - which is the whole
    /// mechanism. Changing this number is how adding a fact kind is admitted to.
    /// </remarks>
    /// <remarks>
    /// Eleven: <c>flight.nomination</c> at slice twenty-seven and
    /// <c>loop.question</c> at twenty-five. The set below names both, which is
    /// what this guard is for: adding one is a line in a diff beside a ledger
    /// row, and swapping one for another is caught as well as adding one.
    /// </remarks>
    /// <remarks>
    /// Twelve: <c>loop.attended</c> at slice twenty-six, step 6. It is the
    /// first whose subject is an ABSENCE - what a session could not measure,
    /// because a person held the terminal and there was no stream to read - and
    /// the argument for it being a fact at all is made where this guard's
    /// criterion lives, in <c>SliceTwelveGuardTests</c>.
    /// </remarks>
    private const int KindsThatCross = 12;

    [Test]
    public async Task A_moved_vocabulary_version_has_a_ledger_entry()
    {
        // THE GUARD, REWRITTEN. It used to assert the version string had not moved,
        // which halted this step - correctly, and then the halt had nothing to say
        // except "stop". A version that cannot move is a guard against progress; a
        // version that cannot move SILENTLY is the guard that was wanted.
        //
        // So: the version may move, and it may not move without a ledger row naming
        // what changed. Bumping without recording becomes impossible rather than
        // discouraged.
        var ledger = LedgerVersions();

        await Assert.That(ledger).Contains(FactVocabulary.Version)
            .Because($"the vocabulary declares {FactVocabulary.Version} and the ledger records "
                   + string.Join(", ", ledger)
                   + ". A version with no entry is a shape change nobody wrote down.");

        // PARSED, not compared as a string. This assertion read
        // string.CompareOrdinal until the version reached double digits, at which
        // point '1' sorted before '5' and 0.10.0 read as older than 0.5.0.
        await Assert.That(Version.Parse(FactVocabulary.Version))
            .IsGreaterThanOrEqualTo(Version.Parse(VocabularyAtSliceStart))
            .Because("the vocabulary only goes forward.");
    }

    [Test]
    public async Task The_ledger_entry_for_this_version_names_the_kinds_it_covers()
    {
        // The other half: an entry that exists and says nothing is a row somebody
        // added to get past the assertion above.
        var kinds = LedgerKinds(FactVocabulary.Version);

        await Assert.That(kinds).IsNotNull()
            .Because($"the ledger has no row for {FactVocabulary.Version}.");
        await Assert.That(kinds!)
            .IsEqualTo(string.Join(", ", FactKinds.All.Order(StringComparer.Ordinal)))
            .Because("the row names the kinds that actually cross at this version.");
    }

    /// <summary>Every version the ledger records.</summary>
    /// <remarks>
    /// Read from the file rather than from a constant, because the file is the thing
    /// being checked and a constant would be this test agreeing with itself.
    /// </remarks>
    private static List<string> LedgerVersions() =>
        [.. Ledger().Select(e => e.GetProperty("version").GetString()!)];

    private static string? LedgerKinds(string version) =>
        Ledger()
            .Where(e => e.GetProperty("version").GetString() == version)
            .Select(e => e.GetProperty("kinds").GetString())
            .FirstOrDefault();

    private static List<System.Text.Json.JsonElement> Ledger()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null
            && !File.Exists(Path.Combine(dir.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            dir = dir.Parent;
        }

        var path = Path.Combine(
            (dir ?? throw new InvalidOperationException("fact-vocabulary.json not found")).FullName,
            "Gg.Contracts", "fact-vocabulary.json");

        return [.. System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement
            .EnumerateArray()];
    }

    [Test]
    public async Task The_kinds_that_cross_are_named()
    {
        // The version and the count are two facts about the same thing, and a
        // version bumped for a rename would pass the assertion above.
        //
        // TWELVE. The tenth is flight.nomination, the eleventh is loop.question
        // - the question an agent could not answer from the work itself, which
        // belongs to the flight whose loop asked it - and the twelfth is
        // loop.attended, which belongs to the flight a person flew by hand.
        // This test
        // used to say "no fact
        // kind has been added" and the halt it produced is what sent the decision
        // back for a ruling rather than letting a fact quietly grow a member. What it
        // asserts now is that the set is NAMED: adding one is a line in a diff beside
        // a ledger row, and swapping one for another is caught as well as adding one.
        await Assert.That(FactKinds.All.Count).IsEqualTo(KindsThatCross);

        await Assert.That(FactKinds.All.Order(StringComparer.Ordinal).ToList())
            .IsEquivalentTo(new[]
            {
                FactKinds.ChangeManifest,
                FactKinds.DestinationLanded,
                FactKinds.DestinationPushed,
                FactKinds.EnvironmentIdentity,
                FactKinds.FlightNomination,
                FactKinds.HumanAccount,
                FactKinds.LoopAttended,
                FactKinds.LoopDigest,
                FactKinds.LoopOutcome,
                FactKinds.LoopQuestion,
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
