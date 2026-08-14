using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every closed vocabulary declares which ledger it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes declaring safe.</b> Without it, "declared" degrades into
/// "declared by whoever remembered", which is where attribution-by-shape started - and a
/// vocabulary that forgot would silently drop out of both fingerprints, which is worse
/// than being in the wrong one.
/// </para>
/// <para>
/// The counted-list pattern, fourth use: enumerate everything matching the shape and
/// assert each one is accounted for, so an omission fails the build rather than narrowing
/// a control quietly.
/// </para>
/// </remarks>
public class VocabularyMembershipTests
{
    [Test]
    public async Task Every_closed_vocabulary_declares_a_fingerprint()
    {
        var undeclared = ClosedVocabularies.Discovered()
            .Where(t => t.GetCustomAttribute<VocabularyOfAttribute>() is null)
            .Select(t => t.Name)
            .ToList();

        await Assert.That(undeclared).IsEmpty()
            .Because("a closed vocabulary that declares nothing is in NEITHER fingerprint, "
                   + "so a value added to it moves no ledger and nobody is asked to think "
                   + "about it - which is the defect this whole mechanism exists to close. "
                   + "Found: " + string.Join(", ", undeclared));
    }

    [Test]
    public async Task Every_declaration_names_a_fingerprint_that_exists()
    {
        var unknown = ClosedVocabularies.Discovered()
            .Select(t => (t.Name, t.GetCustomAttribute<VocabularyOfAttribute>()?.Fingerprint))
            .Where(x => x.Fingerprint is not null
                     && !VocabularyFingerprints.All.Contains(x.Fingerprint, StringComparer.Ordinal))
            .Select(x => $"{x.Name}: '{x.Fingerprint}'")
            .ToList();

        await Assert.That(unknown).IsEmpty()
            .Because("there are two ledgers and no third. A vocabulary belonging to neither "
                   + "is worth a conversation rather than an escape hatch. Found: "
                   + string.Join(", ", unknown));
    }

    [Test]
    public async Task The_scan_finds_a_vocabulary_that_does_not_call_its_list_All()
    {
        // THE HOLE THIS CLOSED. Requiring the name meant Classifications - whose list is
        // called Ordered - was invisible to a mechanism built to make closed vocabularies
        // visible, while its values sit inside every change manifest that crosses.
        await Assert.That(ClosedVocabularies.Discovered().Select(t => t.Name))
            .Contains(nameof(Classifications));
    }

    [Test]
    public async Task A_vocabulary_on_both_sides_is_counted_as_a_fact()
    {
        // Moves and executor rungs are declared in an envelope AND reported inside
        // loop.outcome. A change to either changes what a FACT can say, which is the
        // stricter of the two obligations, so that is the one they answer to.
        foreach (var both in (Type[]) [typeof(LoopMoves), typeof(ExecutorRungs)])
        {
            await Assert.That(both.GetCustomAttribute<VocabularyOfAttribute>()!.Fingerprint)
                .IsEqualTo(VocabularyFingerprints.Fact)
                .Because($"{both.Name} appears inside loop.outcome.");
        }
    }

    [Test]
    public async Task A_gate_payload_vocabulary_is_not_part_of_the_fact_fingerprint()
    {
        // The case that produced the halt: the payload is assembled control-plane-side
        // from evidence that has already crossed, and none of its vocabularies can appear
        // inside a fact.
        foreach (var payload in (Type[])
                 [typeof(EvidenceItems), typeof(EvidenceDispositions), typeof(EvidenceVoices)])
        {
            await Assert.That(payload.GetCustomAttribute<VocabularyOfAttribute>()!.Fingerprint)
                .IsEqualTo(VocabularyFingerprints.Contract);
        }

        await Assert.That(ClosedVocabularies.Lines(VocabularyFingerprints.Fact)
                .Any(l => l.Contains("Evidence", StringComparison.Ordinal)))
            .IsFalse()
            .Because("no fact kind changed, so the fact fingerprint must not have moved.");
    }

    [Test]
    public async Task A_ranking_is_hashed_in_its_own_order()
    {
        // THE SILENT CHANGE THIS CLOSES. Whether a fact may leave a customer's network is
        // computed from whether its classification sits at or below a ceiling, and that
        // comparison reads the ORDER of the levels. Sorted before hashing, reordering
        // them would change what may cross and move no ledger - a silent change to an
        // egress control, inside the ledger built to make silent changes impossible.
        var declared = typeof(Classifications).GetCustomAttribute<VocabularyOfAttribute>()!;

        await Assert.That(declared.Ordered).IsTrue()
            .Because("Classifications is a ranking, not a set.");

        var line = ClosedVocabularies.Lines(VocabularyFingerprints.Fact)
            .Single(l => l.StartsWith("vocabulary Classifications ", StringComparison.Ordinal));

        await Assert.That(line).Contains(
            string.Join(",", Classifications.Ordered))
            .Because("hashed in the order that decides what may cross.");

        await Assert.That(line).DoesNotContain(
            string.Join(",", Classifications.Ordered.OrderBy(v => v, StringComparer.Ordinal)))
            .Because("and not in the sorted order, which is the whole difference - a "
                   + "normalisation that discards something meaningful misses in a way that "
                   + "looks like coverage.");
    }

    [Test]
    public async Task A_set_is_still_sorted()
    {
        // The other half, so 'ordered' is a distinction rather than a switch that turned
        // sorting off everywhere. Reordering a set is not a wire change and must not read
        // as one.
        var line = ClosedVocabularies.Lines(VocabularyFingerprints.Fact)
            .Single(l => l.StartsWith("vocabulary DiffBasis ", StringComparison.Ordinal));

        await Assert.That(line).Contains(
            string.Join(",", DiffBasis.All.OrderBy(v => v, StringComparer.Ordinal)));
    }
}
