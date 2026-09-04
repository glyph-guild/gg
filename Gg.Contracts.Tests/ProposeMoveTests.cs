namespace Gg.Contracts.Tests;

/// <summary>
/// <c>propose</c> — the move that grants a loop the channel it declares a value
/// through.
/// </summary>
/// <remarks>
/// <para>
/// <b>A move rather than a new gating mechanism, and that is what makes it
/// cheap.</b> The grant then arrives through the <c>--allowedTools</c> path
/// that already exists, and the work kind never has to reach the runner.
/// <c>LeaseLoop</c> carries four values and its own doc forbids more -
/// <i>"the runner needs four things to run a loop and must not be handed the
/// document that decides what it is allowed to do - policy arriving at a runner
/// is Article IX's failure wearing a convenience"</i> - so keying the tool on
/// the work kind would mean putting the work kind on the lease, which is that
/// failure.
/// </para>
/// <para>
/// <b>And the envelope decides, which is the right answer anyway.</b> A loop
/// that may nominate is a loop whose document says so. Unlike a help tool -
/// always granted, because an envelope able to withhold it would make a stuck
/// agent silent - this one is the whole output of one kind of work, and a loop
/// that is not classifying has no business proposing a kind.
/// </para>
/// <para>
/// <b>Record-only, on the criterion MoveKinds states.</b> Its product is a
/// fact, and a destination still gates whether that fact becomes a flight. It
/// is not an outward act: nothing leaves, nobody is messaged, and admission can
/// refuse it.
/// </para>
/// </remarks>
public class ProposeMoveTests
{
    private static Envelope Classifying(params string[] moves) => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [FactKinds.FlightNomination],
        Obligations =
        [
            new Obligation { Id = "human-look", Check = ObligationChecks.Human, Approver = "lead" },
        ],
        Loops =
        [
            new Loop
            {
                Id = "classify",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = moves,
                Budget = new LoopBudget { WallClock = "10m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "open-the-flight",
                Kind = DestinationKinds.Flight,
                Requires = ["human-look"],
                Opens = ["research"],
            },
        ],
    };

    [Test]
    public async Task The_move_is_a_member_and_a_loop_declaring_it_validates()
    {
        await Assert.That(LoopMoves.All).Contains(LoopMoves.Propose);
        await Assert.That(Envelope.Validate(
            Classifying(LoopMoves.Read, LoopMoves.Propose))).IsNull();
    }

    [Test]
    public async Task It_is_record_only_because_a_destination_still_gates_its_product()
    {
        await Assert.That(MoveKinds.Of(LoopMoves.Propose)).IsEqualTo(MoveKinds.RecordOnly)
            .Because("nominating produces a fact, and whether that fact becomes a flight is "
                   + "admission's answer - so nothing has left and nothing is unrecallable.");
    }

    [Test]
    public async Task Every_move_still_has_a_classification()
    {
        // The totality the table exists for, re-asserted because this is the
        // commit that adds a member to it.
        foreach (var move in LoopMoves.All)
        {
            await Assert.That(MoveKinds.All).Contains(MoveKinds.Of(move));
        }
    }

    [Test]
    public async Task A_loop_that_does_not_declare_it_is_a_loop_that_may_not_nominate()
    {
        // ASSERTED AS AN ABSENCE, because the grant is the interesting half and
        // it is asserted at the launch. What belongs here is that the envelope
        // can express withholding it at all - a move every loop had would be a
        // permission nobody granted.
        var withheld = Classifying(LoopMoves.Read);

        await Assert.That(Envelope.Validate(withheld)).IsNull();
        await Assert.That(withheld.Loops[0].Moves).DoesNotContain(LoopMoves.Propose);
    }
}
