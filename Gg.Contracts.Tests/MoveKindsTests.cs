namespace Gg.Contracts.Tests;

/// <summary>
/// Every move bears a kind - outward act or record-only - and a move
/// classified as an outward act, whose enforcement nothing can confirm, is
/// refused at authoring naming the move.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0014's third resolution: <i>enforce a move whose use is itself the
/// outward act; record-only a move whose product a destination still
/// gates.</i> All five moves today are record-only - each produces something
/// a destination gate still faces - so <b>the enforced set being empty is
/// the correct answer</b>, asserted here as the ratchet's anchor. What ships
/// is the classification and its refusal, exercisable with zero outward
/// moves, which is what keeps this from being a mechanism waiting for a
/// member: the lock is installed before the door.
/// </para>
/// <para>
/// <b>The refusal is driven by the classification alone.</b> At authoring
/// there is no runner and no probe; what Validate can honestly answer is
/// whether anything this product has lets a probe confirm enforcement of a
/// GRANTED move's use - and nothing does: the probe confirms withholding of
/// tools that were NOT granted, which is the opposite bound. The day a
/// confirmable enforcement exists is the day the first outward move ships
/// (`send`, `power-on` - ADR-0015 section 10's verbs), and that commit adds
/// the second input.
/// </para>
/// </remarks>
public class MoveKindsTests
{
    private static Envelope Declaring(params string[] moves) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
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
                Moves = moves,
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

    [Test]
    public async Task Every_move_bears_a_kind_and_the_table_is_total()
    {
        // THE DRIFT GUARD: a sixth move added to the vocabulary without a
        // classification goes red here, so the decision is taken where the
        // move is minted rather than discovered when an envelope grants it.
        await Assert.That(MoveKinds.Table.Keys.Order(StringComparer.Ordinal).ToList())
            .IsEquivalentTo(LoopMoves.All.Order(StringComparer.Ordinal).ToList())
            .Because("a move nobody classified would be granted by an envelope with "
                   + "nobody having decided whether granting it is itself the act.");
    }

    [Test]
    public async Task An_unclassified_move_poisons_rather_than_passing()
    {
        // run-migrations is the move the laundering attack arrives with, and
        // send and power-on are ADR-0015 section 10's; none exists yet, and
        // when one does, Of must throw until somebody classifies it.
        var refused = Assert.Throws<InvalidOperationException>(() =>
            MoveKinds.Of("run-migrations"));

        await Assert.That(refused.Message).Contains("run-migrations")
            .Because("the poison names the move somebody has to classify.");
        await Assert.That(refused.Message).Contains(MoveKinds.OutwardAct)
            .Because("and the choices, so the fix is in the message.");
    }

    [Test]
    public async Task The_enforced_set_is_correctly_empty_which_is_the_ratchets_anchor()
    {
        // EXPECTED TO BE EDITED the day a real outward move arrives - that
        // edit is the deliberate act this anchor exists to force. Every move
        // today produces something a destination gate still faces: read and
        // search produce knowledge, edit and write produce tree state the
        // manifest measures, run-tests produces outcomes the verdicts read.
        await Assert.That(MoveKinds.Table.Values.All(k => k == MoveKinds.RecordOnly))
            .IsTrue()
            .Because("an empty enforced set is the CORRECT answer today, not a gap: the "
                   + "first outward move arrives with maintain-environment's verbs, and "
                   + "this row is where its arrival becomes a decision.");
    }

    [Test]
    public async Task An_outward_act_nothing_can_confirm_is_refused_at_authoring_naming_the_move()
    {
        // THE LIVENESS PROOF, by planted classification: the real table is
        // correctly empty of outward acts, and a fake move never reaches the
        // branch (the unknown-move refusal fires first), so the branch is
        // unreachable with honest inputs. The overload exists exactly for
        // this - the honest alternative to reflection - and production
        // callers never pass it.
        var refused = Envelope.Validate(
            Declaring(LoopMoves.Read, LoopMoves.Edit),
            move => move == LoopMoves.Read ? MoveKinds.OutwardAct : MoveKinds.Of(move));

        await Assert.That(refused).IsNotNull()
            .Because("a declared outward act nothing can confirm enforcement of would be "
                   + "granted by the envelope and withheld by nothing.");
        await Assert.That(refused!).Contains("'read'")
            .Because("the refusal names the move.");
        await Assert.That(refused!).Contains("implement")
            .Because("and the loop that declared it.");
    }

    [Test]
    public async Task The_default_wiring_classifies_by_the_real_table()
    {
        // All five real moves are record-only, so the one-argument Validate
        // accepts them - and an unknown move still dies at the unknown-move
        // refusal rather than reaching Of's poison, ordering intact.
        await Assert.That(Envelope.Validate(Declaring([.. LoopMoves.All]))).IsNull();

        var unknown = Envelope.Validate(Declaring("run-migrations"));
        await Assert.That(unknown).IsNotNull();
        await Assert.That(unknown!).Contains("Unknown move")
            .Because("an unknown move is refused as unknown, not as unclassified - the "
                   + "vocabulary gate stands in front of the kind gate.");
    }
}
