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
        await Assert.That(EnvelopeComposition.Operators["instructions"])
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
