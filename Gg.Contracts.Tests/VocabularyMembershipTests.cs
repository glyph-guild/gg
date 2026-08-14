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
}
