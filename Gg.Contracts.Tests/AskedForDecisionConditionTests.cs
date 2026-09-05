using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A fourth attachment condition, so an envelope can gate on a question.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 3 is what this is for: the question decides nothing.</b> Recording it
/// is the runner's; whether it opens a gate is the ENVELOPE's to say, through
/// an ordinary obligation with an ordinary condition. An agent that could open
/// a gate by asking could stall a tenant's work at will, and one that could
/// close one could unstick itself.
/// </para>
/// <para>
/// <b>A condition, not a new primitive</b> — the disposition `envelope widens`
/// already established. What routes a flight to a person is a gate, the gate
/// list is fed by gates, and the queue a person reads is fed by the gate list.
/// Inventing a surface beside all three would be a second way for a flight to
/// need somebody.
/// </para>
/// <para>
/// <b>And a machine check may not carry it</b>, for the reason `envelope
/// widens` is refused the same pairing: a machine predicate computes a verdict
/// from facts, and this condition is about whether a PERSON is needed. The pair
/// would be a gate no evaluation can ever open.
/// </para>
/// </remarks>
public class AskedForDecisionConditionTests
{
    private static Obligation Obligation(string check, string when) => new()
    {
        Id = "somebody-decides",
        Check = check,
        Approver = string.Equals(check, ObligationChecks.Human, StringComparison.Ordinal)
            ? "a-lead"
            : null,
        // A MACHINE OBLIGATION CARRIES A RULE, and this fixture has to give it
        // one or the refusal that fires is "unknown rule" - a different
        // complaint about a different field, reached before the pairing is
        // looked at. The first draft of this test refused for that reason and
        // read as though the pairing had been caught.
        Rule = string.Equals(check, ObligationChecks.Machine, StringComparison.Ordinal)
            ? ObligationPredicates.LoopNotExhausted
            : null,
        When = when,
    };

    private static Envelope Governing(Obligation obligation) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = [obligation],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
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
                Requires = ["somebody-decides"],
            },
        ],
    };

    [Test]
    public async Task The_condition_is_one_the_contract_understands()
    {
        await Assert.That(AttachmentConditions.Forms)
            .Contains(AttachmentConditions.AskedForDecision)
            .Because("a condition nothing recognises cannot be treated as false - false is "
                   + "the answer that removes the obligation, and nothing would be recorded.");

        await Assert.That(Envelope.Validate(Governing(
                Obligation(ObligationChecks.Human, AttachmentConditions.AskedForDecision))))
            .IsNull()
            .Because("an ordinary human obligation carrying it is the whole mechanism: no new "
                   + "primitive, and the gate list already feeds the queue a person reads.");
    }

    [Test]
    public async Task A_machine_check_may_not_carry_it()
    {
        // THE SAME PAIRING `envelope widens` REFUSES, and the same argument: a
        // machine predicate computes a verdict from facts, and this condition
        // is about whether a PERSON is needed. The pair is a gate no evaluation
        // can open, which is the permanent deadlock the no-break-glass rule
        // refuses to build.
        var refusal = Envelope.Validate(Governing(
            Obligation(ObligationChecks.Machine, AttachmentConditions.AskedForDecision)));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains(AttachmentConditions.AskedForDecision)
            .Because("the refusal names the condition, so an author knows which of their two "
                   + "fields to change.");
        await Assert.That(refusal!.Contains("human", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("and says which way to change it. A refusal that only says no leaves "
                   + "somebody guessing between the check and the condition.");
    }

    [Test]
    public async Task It_is_refused_where_the_author_can_still_act()
    {
        // AT APPLY, not at evaluation. A pairing that could never open a gate,
        // discovered when a flight is already waiting on it, is discovered by
        // the person the flight was waiting for.
        var refusal = Envelope.Validate(Governing(
            Obligation(ObligationChecks.Machine, AttachmentConditions.AskedForDecision)));

        await Assert.That(refusal).IsNotNull()
            .Because("Envelope.Validate is the apply door's own check, and this is where an "
                   + "author is still holding the document.");
    }

    [Test]
    public async Task The_measured_tier_is_a_condition_too_and_refuses_the_same_pairing()
    {
        // THE TIER THAT HOLDS WHEN THE DECLARED ONE IS IGNORED. An agent that
        // is stuck and does not say so still produces a run that touched
        // nothing, and that is knowable without its cooperation - which is what
        // a tenant reaches for when they do not want to depend on an agent
        // choosing to ask.
        await Assert.That(AttachmentConditions.Forms)
            .Contains(AttachmentConditions.ChangedNothing);

        await Assert.That(Envelope.Validate(Governing(
                Obligation(ObligationChecks.Human, AttachmentConditions.ChangedNothing))))
            .IsNull();

        // AND THE SAME PAIRING, refused for the same reason one condition over:
        // this says a person is needed and a machine predicate is what runs
        // when one is not.
        var refusal = Envelope.Validate(Governing(
            Obligation(ObligationChecks.Machine, AttachmentConditions.ChangedNothing)));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains(AttachmentConditions.ChangedNothing)
            .Because("the refusal names the condition the author wrote, not the other one - "
                   + "a shared arm that named a fixed condition would send somebody looking "
                   + "at a line they did not write.");
        await Assert.That(refusal!.Contains("human", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task The_conditions_that_were_there_are_still_there()
    {
        // A CLOSED VOCABULARY GROWS; it does not get rewritten. A condition
        // removed here would make every envelope carrying it unreadable.
        await Assert.That(AttachmentConditions.Forms.Count).IsEqualTo(5)
            .Because("five forms, and a sixth is a contract version move rather than an edit.");

        foreach (var still in (string[])
            [AttachmentConditions.Widens,
             AttachmentConditions.TouchesPrefix + "<glob>",
             AttachmentConditions.MovesUsedPrefix + "<move>"])
        {
            await Assert.That(AttachmentConditions.Forms).Contains(still);
        }
    }
}
