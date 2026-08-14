using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What a gate carries to the person answering it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stopping is machinery; the payload is the claim.</b> A gate that points at a
/// commit has offloaded the work rather than the decision - Article IV - so the payload is
/// the thing this product actually promises, and its shape is where the promise is kept or
/// lost.
/// </para>
/// <para>
/// <b>Truncation must be unrepresentable, not merely unused.</b> A half-item is a
/// different statement rather than a shorter one, which is the rule every inline item in
/// this contract already follows. An oversize item becomes a digest or a reference -
/// ADR-0006's three-way split, driven by measurement rather than by an author's judgement.
/// </para>
/// </remarks>
public class EvidencePayloadShapeTests
{
    [Test]
    public async Task An_obligation_declares_what_evidence_its_gate_needs()
    {
        // The envelope says what a decision requires, so the requirement is reviewed
        // configuration rather than whatever the payload assembler happened to have.
        var members = typeof(Obligation).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains(nameof(Obligation.Evidence));
    }

    [Test]
    public async Task A_payload_item_can_be_inline_a_digest_or_a_reference_and_nothing_else()
    {
        // THREE SHAPES, NOT TWO - and the fourth is the one that must not exist. A
        // 'truncated' disposition would make "the person saw part of it" a representable
        // state, and a decision made on part of an item is indistinguishable from one made
        // on all of it once the payload is filed.
        await Assert.That(EvidenceDispositions.All).IsEquivalentTo(
            new[] { EvidenceDispositions.Inline, EvidenceDispositions.Digest, EvidenceDispositions.Reference });

        foreach (var forbidden in (string[]) ["truncated", "partial", "elided", "summary"])
        {
            await Assert.That(EvidenceDispositions.All.Contains(forbidden, StringComparer.Ordinal))
                .IsFalse()
                .Because($"'{forbidden}' would make a half-item representable, and a half-item "
                       + "is a different statement rather than a shorter one.");
        }
    }

    [Test]
    public async Task A_payload_item_carries_exactly_one_of_the_three()
    {
        // Structural. An item holding both content and a reference would let two
        // descriptions of one thing disagree, and nothing would say which the person read.
        var members = typeof(GateEvidenceItem).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains(nameof(GateEvidenceItem.Inline));
        await Assert.That(members).Contains(nameof(GateEvidenceItem.Digest));
        await Assert.That(members).Contains(nameof(GateEvidenceItem.Reference));
        await Assert.That(members).Contains(nameof(GateEvidenceItem.Disposition));

        await Assert.That(GateEvidenceItem.Validate(new GateEvidenceItem
        {
            Item = EvidenceItems.ChangeManifest,
            Disposition = EvidenceDispositions.Inline,
            Voice = EvidenceVoices.Measured,
            Inline = "two files",
            Reference = new EvidenceReference
            {
                Commit = new string('a', 40),
                Path = "migrations/003.sql",
                ContentHash = new string('b', 64),
                ByteSize = 900,
                MediaType = "text/plain",
            },
        })).IsNotNull()
            .Because("one item, one description of it.");
    }

    [Test]
    public async Task A_reference_carries_what_somebody_needs_to_go_and_look()
    {
        // The answer to "I want to see the migration": not the content, but enough to
        // fetch it from their own systems authenticated as themselves.
        var members = typeof(EvidenceReference).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(EvidenceReference.Commit),
            nameof(EvidenceReference.Path),
            nameof(EvidenceReference.ContentHash),
            nameof(EvidenceReference.ByteSize),
            nameof(EvidenceReference.MediaType),
        });

        foreach (var forbidden in (string[]) ["Content", "Body", "Text", "Diff", "Patch"])
        {
            await Assert.That(members.Contains(forbidden)).IsFalse()
                .Because($"'{forbidden}' on a reference is the content crossing after all, "
                       + "which is the entire thing a reference exists to avoid.");
        }
    }

    [Test]
    public async Task The_budget_binds_on_the_payload_before_it_binds_on_the_count()
    {
        // Asserting a constant against itself proves nothing, so what is asserted is the
        // RELATIONSHIP between the three numbers - which is the part that could be wrong.
        //
        // Five items at the item limit is 10 KB and the payload limit is 8 KB, so the
        // payload cap is what actually routes. If the numbers were the other way round,
        // the item count would bind first and a payload could be under five items and
        // still too big to read in one sitting, which is the thing the budget is for.
        var budgets = new[]
        {
            GateEvidencePayload.MaxItemBytes * GateEvidencePayload.MaxItems,
            GateEvidencePayload.MaxPayloadBytes,
        };

        await Assert.That(budgets[0]).IsGreaterThan(budgets[1])
            .Because("the payload budget is the operative constraint, and the item budget "
                   + "routes individual pieces within it.");

        await Assert.That(budgets[1] / GateEvidencePayload.MaxItemBytes).IsGreaterThan(1)
            .Because("more than one item at the item limit fits in the payload, or a gate "
                   + "with one big item would present a case with nothing beside it")
            .And
            .IsLessThan(GateEvidencePayload.MaxItems)
            .Because("and fewer than the item cap, so the two budgets constrain different "
                   + "things rather than one of them being decorative.");
    }

    [Test]
    public async Task The_payload_says_when_the_loop_changed_nothing()
    {
        // ABSENCE AND SILENCE MUST NOT LOOK ALIKE. An empty delta rendered as a blank
        // section reads as a payload that failed to assemble; said out loud it is an
        // answer, and it is the answer to "did the loop act on my feedback".
        var members = typeof(GateEvidencePayload).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains(nameof(GateEvidencePayload.Delta));
        await Assert.That(members).Contains(nameof(GateEvidencePayload.DeltaNote));
    }
}
