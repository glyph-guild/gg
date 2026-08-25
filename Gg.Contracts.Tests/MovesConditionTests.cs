namespace Gg.Contracts.Tests;

/// <summary>
/// The condition that reads an act: `moves used include &lt;move&gt;`, over the
/// flight-wide union of every loop's recorded moves.
/// </summary>
/// <remarks>
/// <para>
/// The first form whose subject is what was DONE rather than what the work
/// contains. The union is monotone by construction - an act cannot be
/// un-happened - so a gate attached by it does not detach when the evidence
/// of the act is deleted, which is the laundering attack ADR-0014 names,
/// closed before the move that makes it live (`run-migrations`) exists.
/// </para>
/// <para>
/// <b>Unlike the glob, the move value is a closed vocabulary</b>, so a typo
/// is refused at parse rather than shipping as a condition that silently
/// never attaches - the one hazard the touches form cannot avoid.
/// </para>
/// </remarks>
public class MovesConditionTests
{
    private static Envelope WithMovesGate(string condition) => new()
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
            new Obligation
            {
                Id = "paper-trail",
                Check = ObligationChecks.Human,
                Approver = "platform-oncall",
                When = condition,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit, LoopMoves.Write],
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
    public async Task The_moves_form_is_a_condition_this_contract_knows()
    {
        await Assert.That(AttachmentConditions.MovesUsedPrefix)
            .IsEqualTo("moves used include ");
        await Assert.That(AttachmentConditions.IsKnown("moves used include write")).IsTrue();
        await Assert.That(AttachmentConditions.Forms)
            .Contains(AttachmentConditions.MovesUsedPrefix + "<move>");
        await Assert.That(AttachmentConditions.GlobOf("moves used include write")).IsNull()
            .Because("it reads recorded moves, never a path.");
    }

    [Test]
    public async Task The_move_it_names_reads_back_out()
    {
        await Assert.That(AttachmentConditions.MoveOf("moves used include write"))
            .IsEqualTo("write");
        await Assert.That(AttachmentConditions.MoveOf("moves used include run-tests"))
            .IsEqualTo("run-tests");
        await Assert.That(AttachmentConditions.MoveOf(AttachmentConditions.Widens)).IsNull()
            .Because("a form that is not this one answers null, the same contract GlobOf keeps.");
        await Assert.That(AttachmentConditions.MoveOf(AttachmentConditions.MovesUsedPrefix))
            .IsNull()
            .Because("a prefix with nothing after it names no move.");
    }

    [Test]
    public async Task A_move_outside_the_closed_vocabulary_is_refused_at_parse()
    {
        // THE HAZARD THE GLOB CANNOT AVOID, avoided here: 'writes' is a typo of
        // 'write', and a condition over a move nobody records would silently
        // never attach - a gate that looks authored and asks nobody, ever.
        var refused = Envelope.Validate(WithMovesGate("moves used include writes"));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("writes")
            .Because("the refusal names the value somebody typed.");
        await Assert.That(refused!).Contains(LoopMoves.Write)
            .Because("and the vocabulary, so the fix is in the message.");
        await Assert.That(refused!).Contains("paper-trail")
            .Because("and which obligation it came from.");
    }

    [Test]
    public async Task A_known_move_validates_on_either_check_kind()
    {
        // Unlike the widens form, this one reads a MEASURED fact - loop.outcome
        // crosses on every loop - so a machine check can carry it without the
        // deadlock that bans machine-check widens gates.
        await Assert.That(Envelope.Validate(WithMovesGate("moves used include write")))
            .IsNull();

        var machine = WithMovesGate("moves used include write");
        machine = machine with
        {
            Obligations =
            [
                machine.Obligations[0],
                machine.Obligations[1] with
                {
                    Check = ObligationChecks.Machine,
                    Approver = null,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                },
            ],
        };

        await Assert.That(Envelope.Validate(machine)).IsNull();
    }

    [Test]
    public async Task An_empty_move_is_not_a_condition()
    {
        var refused = Envelope.Validate(WithMovesGate("moves used include "));

        await Assert.That(refused).IsNotNull()
            .Because("a prefix with nothing after it is not a form, and IsKnown says so.");
    }
}
