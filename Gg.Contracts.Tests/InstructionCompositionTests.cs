using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Standing instructions compose by appending, in layer order.
/// </summary>
/// <remarks>
/// <para>
/// <b>S30.1-01 and -02. `Append` is a new operator because union's argument
/// does not transfer.</b> <c>MergeOperators</c> claims order-freedom for what
/// it has — <i>"intersect, min and union are commutative and associative"</i> —
/// and that claim is what makes composition safe to run in any order. Text does
/// not have it: root's guidance then the work kind's reads differently from the
/// reverse, and a person writing the second is writing it to be read after the
/// first.
/// </para>
/// <para>
/// <b>So the order-dependence is the feature, and it is asserted as one.</b> A
/// test that passed under union's commutativity would prove nothing about
/// append; this one has to fail if somebody quietly reuses union.
/// </para>
/// <para>
/// <b>Provenance is the composer's, never the author's</b> — the discipline
/// <c>Obligation.Provenance</c> already follows, and the reason is rule 4:
/// guidance whose source a person cannot find is guidance nobody can change.
/// </para>
/// </remarks>
public class InstructionCompositionTests
{
    [Test]
    public async Task Append_is_a_declared_operator()
    {
        await Assert.That(MergeOperators.All).Contains(MergeOperators.Append);
    }

    [Test]
    public async Task Instructions_compose_by_append_rather_than_by_union()
    {
        // THE DECLARATION IS THE BINDING. Composition is generic over the
        // operator table, so this is what says instructions accrete rather than
        // being deduplicated into a set.
        await Assert.That(EnvelopeComposition.Operators["Envelope.Instructions"])
            .IsEqualTo(MergeOperators.Append)
            .Because("union would deduplicate two layers that happened to say the same "
                   + "sentence, and drop the one a person wrote second on purpose.");
    }

    [Test]
    public async Task Root_and_a_work_kind_both_contribute_in_that_order()
    {
        var composed = Compose(
            root: ["read the ADRs before proposing a schema change"],
            workKind: ["for a bug, reproduce it in a test first"]);

        await Assert.That(composed.Select(i => i.Text)).IsEquivalentTo((string[])
        [
            "read the ADRs before proposing a schema change",
            "for a bug, reproduce it in a test first",
        ]);
    }

    [Test]
    public async Task The_order_is_the_layer_order_and_reversing_it_changes_the_result()
    {
        // WHAT UNION COULD NOT FAIL. Commutativity would make these two equal;
        // append must not.
        var forward = Compose(root: ["first"], workKind: ["second"]);
        var backward = Compose(root: ["second"], workKind: ["first"]);

        await Assert.That(string.Join("|", forward.Select(i => i.Text)))
            .IsNotEqualTo(string.Join("|", backward.Select(i => i.Text)))
            .Because("append is order-dependent, and a test that passes either way is a test "
                   + "that would also pass for union.");
    }

    [Test]
    public async Task Every_block_carries_the_document_it_came_from()
    {
        var composed = Compose(root: ["from the floor"], workKind: ["from the kind"]);

        await Assert.That(composed[0].Provenance.Role).IsEqualTo(Roles.Root);
        await Assert.That(composed[1].Provenance.Role).IsEqualTo(Roles.WorkKind)
            .Because("guidance whose source a person cannot find is guidance nobody can change.");
    }

    [Test]
    public async Task An_envelope_declaring_none_composes_to_none()
    {
        // THE ANCHOR, and the compatibility claim: every envelope that exists
        // today reads back with an empty list rather than a null or a block.
        await Assert.That(Compose(root: [], workKind: [])).IsEmpty();
    }

    /// <summary>A minimal envelope carrying the instructions a layer declares.</summary>
    private static Envelope Document(params string[] instructions) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = [],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [],
        Instructions = [.. instructions.Select(text => new EnvelopeInstruction { Text = text })],
    };

    private static IReadOnlyList<EnvelopeInstruction> Compose(string[] root, string[] workKind)
    {
        var composition = EnvelopeComposition.Compose(
        [
            new EnvelopeLayer
            {
                Role = Roles.Root,
                Name = "root",
                Parent = null,
                Document = Document(root),
                Version = "v1",
            },
            new EnvelopeLayer
            {
                Role = Roles.WorkKind,
                Name = "bug",
                Parent = "root",
                Document = Document(workKind),
                Version = "v1",
            },
        ]);

        return (composition.Composed
            ?? throw new InvalidOperationException(
                "These layers were meant to compose. Refused: " + composition.Refused))
            .Instructions;
    }
}

/// <summary>
/// Instructions survive the text form a person edits them in.
/// </summary>
/// <remarks>
/// <para>
/// <b>A field with no text form is a field nobody can author</b>, and
/// <c>EnvelopeModelRoundTripTests</c> refuses to let one ship — its ratchet
/// fired on <c>Envelope.Instructions</c> the moment the member landed, because
/// <i>"parsed and never rendered shipped twice before nothing forced that
/// decision"</i>.
/// </para>
/// <para>
/// <b>One line per block, and that is the decision the ratchet forced.</b> A
/// block is a sentence or two; an author with more to say writes another block.
/// That reads better in a prompt, diffs better in review, attributes on its own
/// — and keeps the round trip exact, where a multi-line scalar in a hand-rolled
/// emitter is where that guarantee usually dies.
/// </para>
/// </remarks>
public class InstructionTextFormTests
{
    private static Envelope With(params string[] instructions) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
        Instructions = [.. instructions.Select(text => new EnvelopeInstruction { Text = text })],
    };

    [Test]
    public async Task Blocks_round_trip_through_the_text_form_in_order()
    {
        var envelope = With("read the ADRs before proposing a schema change",
                            "for a bug, reproduce it in a test first");

        var read = Authoring.EnvelopeYaml.Parse(EnvelopeText.Render(envelope));

        await Assert.That(read.Envelope).IsNotNull();
        await Assert.That(read.Envelope!.Instructions.Select(i => i.Text))
            .IsEquivalentTo(envelope.Instructions.Select(i => i.Text).ToList());
    }

    [Test]
    public async Task An_envelope_with_none_renders_exactly_as_it_did_before()
    {
        // THE COMPATIBILITY CLAIM, and the reason empty collapses into absent
        // here rather than being written as `instructions: []` the way
        // `accepts: []` is. There is no such thing as a document declaring "no
        // instructions on purpose", so nothing is emitted and every envelope
        // written before this field is byte-for-byte unchanged.
        await Assert.That(EnvelopeText.Render(With())).DoesNotContain("instructions");
    }

    [Test]
    public async Task A_block_spanning_two_lines_is_refused_rather_than_folded()
    {
        // The parser would have to invent a spelling for it, and whatever it
        // invented would round-trip to something the author did not type.
        await Assert.That(Envelope.Validate(With("first line\nsecond line"))).IsNotNull();
    }

    [Test]
    public async Task Rendering_twice_gives_identical_bytes()
    {
        // The canonical-form rule the emitter already follows for obligations.
        var envelope = With("one", "two");

        await Assert.That(EnvelopeText.Render(envelope)).IsEqualTo(EnvelopeText.Render(envelope));
    }
}
