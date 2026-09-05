using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The instructions a lease carries, rendered once by the contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>S30.2-01, and the rule it follows is already on the type beside it.</b>
/// <c>LeaseLoop.ResumesFrom</c> says <i>"the rendered seed, not the model …
/// rendering it here would be a second implementation of a document the
/// contract already renders once"</i>. Instructions take the same shape for the
/// same reason: the control plane composes the envelope and renders once, the
/// runner inserts a string, and there is exactly one wording in the product.
/// </para>
/// <para>
/// <b>Which is also what makes the fencing testable from the runner's side.</b>
/// If each end rendered its own, a prompt test in this repository would be
/// asserting a second implementation nobody ships.
/// </para>
/// </remarks>
public class LeaseInstructionTests
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
        Destinations = [],
        Instructions =
        [
            .. instructions.Select(text => new EnvelopeInstruction
            {
                Text = text,
                Provenance = new ObligationProvenance { Role = Roles.WorkKind, Name = "bug" },
            }),
        ],
    };

    [Test]
    public async Task An_envelope_with_no_instructions_renders_nothing_at_all()
    {
        // NULL, NOT AN EMPTY BLOCK. "No standing instructions" and "standing
        // instructions that say nothing" would read the same in a prompt, which
        // is this project's most repeated defect and the reason ResumesFrom is
        // absent on a first attempt rather than empty.
        await Assert.That(EnvelopeText.RenderInstructions(With())).IsNull();
    }

    [Test]
    public async Task Each_block_is_attributed_to_the_document_that_wrote_it()
    {
        var rendered = EnvelopeText.RenderInstructions(With("reproduce a bug in a test first"))!;

        await Assert.That(rendered).Contains("reproduce a bug in a test first");
        await Assert.That(rendered).Contains("bug")
            .Because("guidance whose source a person cannot find is guidance nobody can change.");
    }

    [Test]
    public async Task It_says_the_instructions_are_the_operators_rather_than_advice()
    {
        // THE WORDING IS THE CONTROL. Four kinds of text reach an agent and
        // this is the only reviewed one; an agent that cannot tell it from a
        // rejection reason gains nothing from the review having happened.
        var rendered = EnvelopeText.RenderInstructions(With("prefer small commits"))!;

        await Assert.That(rendered.Contains("standing", StringComparison.OrdinalIgnoreCase))
            .IsTrue();
    }

    [Test]
    public async Task It_says_an_instruction_cannot_widen_what_the_envelope_bounds()
    {
        // RULE 5, IN THE SAME BLOCK. Instructions cannot contradict the
        // structured fields beside them, and the manifest check is what
        // decides - so the agent is told, in the words Feedback and Resumption
        // already use, rather than left to infer it.
        var rendered = EnvelopeText.RenderInstructions(With("edit whatever you like"))!;

        await Assert.That(rendered.Contains("cannot", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(rendered.Contains("scope", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task Blocks_appear_in_the_order_they_composed()
    {
        var rendered = EnvelopeText.RenderInstructions(With("first", "second"))!;

        await Assert.That(rendered.IndexOf("first", StringComparison.Ordinal))
            .IsLessThan(rendered.IndexOf("second", StringComparison.Ordinal))
            .Because("append is order-dependent and the rendering is where that becomes "
                   + "something a person can read.");
    }

    [Test]
    public async Task Rendering_is_the_contracts_and_a_lease_carries_the_result()
    {
        // The member exists and takes a rendered string, so no consumer has a
        // reason to render its own.
        var loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 1800,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Instructions = EnvelopeText.RenderInstructions(With("read the ADRs")),
        };

        await Assert.That(loop.Instructions).IsNotNull();
        await Assert.That(loop.Instructions!).Contains("read the ADRs");
    }
}
