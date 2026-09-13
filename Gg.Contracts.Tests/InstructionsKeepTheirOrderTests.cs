using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Instructions are the one sequence in an envelope whose order is what it
/// says, and the canonical form has to keep it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This repository has already decided that instruction order is
/// meaningful.</b> <c>InstructionCompositionTests</c> is the argument, in its
/// own words: <c>MergeOperators</c> claims order-freedom for what it has and
/// <i>"text does not have it: root's guidance then the work kind's reads
/// differently from the reverse, and a person writing the second is writing it
/// to be read after the first."</i> <c>Append</c> exists as a separate
/// operator for exactly that reason. Then the canonical emitter sorts the
/// lines ordinally inside each layer, which destroys within a document the
/// property the operator was added to protect between documents.
/// </para>
/// <para>
/// <b>The rule it was borrowing does not transfer.</b>
/// <c>EnvelopeCanonicalOrderTests</c> sorts obligations by id so that <i>"a
/// version derived from these bytes has to mean the rules changed, not that
/// two lines were swapped in a text editor"</i> — true of a set of named
/// rules, and false here, because for instructions the lines ARE the rules and
/// an agent reads them in the order given. <c>EnvelopeText.RenderInstructions</c>,
/// which is what actually reaches an agent, iterates the model and sorts
/// nothing.
/// </para>
/// <para>
/// <b>What it cost, measured on a real airspace (2026-09-12).</b> An operator
/// added an instruction as the fourth of eight in
/// <c>work-kinds/score-hal.yaml</c>, applied it, and pulled: the line came
/// back eighth, and the file was in ordinal order start to finish. The
/// document the control plane holds is unaffected — apply sends the parsed
/// list and the control plane serializes it verbatim — so the working copy
/// disagreed with what governs, and nothing said so.
/// </para>
/// <para>
/// <b>And the diff cannot see it, which is why it was found by accident.</b>
/// <c>AirspaceTree.Changed</c> compares renders, so a pure reorder renders
/// identically, reports "no changes", and is never sent: **instructions
/// currently cannot be reordered at all.** The operator found this by reading
/// a git diff after a pull, which is the only place it was visible.
/// </para>
/// </remarks>
public class InstructionsKeepTheirOrderTests
{
    /// <summary>An envelope that will render, carrying the instructions given.</summary>
    private static Envelope With(params string[] instructions) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Instructions = [.. instructions.Select(text => new EnvelopeInstruction { Text = text })],
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
                Moves = [LoopMoves.Read, LoopMoves.Edit],
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
    };

    /// <summary>The instruction lines of a rendered document, in the order emitted.</summary>
    private static IReadOnlyList<string> Emitted(string rendered)
    {
        var lines = rendered.Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("instructions:", StringComparison.Ordinal));

        return start < 0
            ? []
            :
            [
                .. lines.Skip(start + 1)
                    .TakeWhile(l => l.StartsWith("  - ", StringComparison.Ordinal))
                    .Select(l => l["  - ".Length..].Trim('"')),
            ];
    }

    [Test]
    public async Task The_canonical_form_emits_instructions_as_they_were_written()
    {
        // THE DEFECT, IN THE SHAPE IT WAS FOUND. Authored fourth of four, and
        // ordinally last of four - so a sorted emitter and an authored one
        // disagree, which the single-instruction case could never show.
        // AUTHORED NON-ALPHABETICALLY, DELIBERATELY. Written in the obvious
        // order - the real document's - every line happened to be in ordinal
        // order already, so sorted and authored agreed and the assertion could
        // not fail. The scoping rule is written first here because it decides
        // WHAT is being scored, which is what a person would put first.
        var envelope = With(
            "When this flight names a work item, score that item and no other.",
            "HAL is the maximum of the five rubric dimensions, never the average.",
            "Score only items on the Agentic backlog.",
            "If two or more dimensions are unanswerable, return the question instead of a score.");

        // JOINED, BECAUSE A COLLECTION ASSERTION HERE PROVES NOTHING.
        // IsEquivalentTo compares membership and not order, so all three of
        // these read green against the sorted emitter - the assertion would
        // have been blind to the only thing being asserted.
        await Assert.That(string.Join(" | ", Emitted(EnvelopeText.Render(envelope))))
            .IsEqualTo(
                "When this flight names a work item, score that item and no other. | "
              + "HAL is the maximum of the five rubric dimensions, never the average. | "
              + "Score only items on the Agentic backlog. | "
              + "If two or more dimensions are unanswerable, return the question instead of a score.")
            .Because("an agent reads these in the order given - RenderInstructions iterates the "
                   + "model and sorts nothing - so a canonical form that reorders them shows a "
                   + "person something other than what governs their work.");
    }

    [Test]
    public async Task An_authored_order_survives_being_written_and_read_again()
    {
        // THE ROUND TRIP IS THE PRODUCT. Pull renders the estate into the
        // working copy and apply parses it back; an order that does not
        // survive both halves is an order nobody can hold.
        var envelope = With("zebra goes first, deliberately", "alpha goes second");

        var written = EnvelopeText.Render(envelope);
        var reread = Authoring.EnvelopeYaml.Parse(written);

        await Assert.That(reread.Diagnosis).IsNull()
            .Because($"the emitter's own output must parse. Wrote:\n{written}");
        await Assert.That(string.Join(" | ", reread.Envelope!.Instructions.Select(i => i.Text)))
            .IsEqualTo("zebra goes first, deliberately | alpha goes second")
            .Because("the working copy is the document a person edits, and a pull that reorders "
                   + "it hands back something they did not write.");
    }

    [Test]
    public async Task Two_documents_differing_only_in_order_are_different_documents()
    {
        // WHAT MAKES A REORDER APPLIABLE. AirspaceTree.Changed compares these
        // renders, so while they are equal a reorder is not a change, is never
        // sent, and cannot be expressed at all. gg airspace diff answered "no
        // changes: the working copy matches the airspace" for a file whose
        // instructions were in a different order from the estate's.
        var written = EnvelopeText.Render(With("second in the file", "first in the file"));
        var swapped = EnvelopeText.Render(With("first in the file", "second in the file"));

        await Assert.That(written).IsNotEqualTo(swapped)
            .Because("for instructions the lines ARE the rules, so swapping two of them changes "
                   + "what an agent is told and must read as a change. That is the opposite of "
                   + "the obligation rule, and the difference is that an obligation has an id.");
    }

    [Test]
    public async Task Everything_else_in_an_envelope_is_still_a_set_and_still_sorts()
    {
        // THE CONTROL, AND THE BOUND ON THE FIX. Every other sequence an
        // envelope carries is a set of names - accepts, produces, moves,
        // discharges, requires, may-write - where two documents naming the same
        // things are the same document, and sorting is what makes their bytes
        // agree. Turning canonical ordering off wholesale would pass the three
        // assertions above and give up that property everywhere.
        var envelope = With("only instruction") with
        {
            Loops =
            [
                new Loop
                {
                    Id = "implement",
                    Executor = ExecutorRungs.Frontier,
                    Discharges = ["in-scope"],
                    Moves = [LoopMoves.Edit, LoopMoves.Read],
                    Budget = new LoopBudget { WallClock = "30m" },
                    OnExhaustion = ExhaustionPolicies.HandoffToHuman,
                },
            ],
        };

        var rendered = EnvelopeText.Render(envelope);
        var moves = rendered[rendered.IndexOf("moves:", StringComparison.Ordinal)..];

        await Assert.That(moves.IndexOf(LoopMoves.Edit, StringComparison.Ordinal))
            .IsLessThan(moves.IndexOf(LoopMoves.Read, StringComparison.Ordinal))
            .Because("a loop declaring read and edit is the same loop whichever way round they "
                   + "were typed, so those still sort - the exemption is for the one field whose "
                   + "order a reader consumes.");
    }
}
