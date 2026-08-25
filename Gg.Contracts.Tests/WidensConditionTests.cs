namespace Gg.Contracts.Tests;

using Gg.Contracts.Description;

/// <summary>
/// The reserved condition that designates a widening gate, and the deadlock
/// the validator refuses to build.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0016 § 6, with no new primitive: the gate is an ordinary
/// <c>check: human</c> obligation, designated by <c>when: envelope widens</c>
/// — what is new is that its attachment is computed from the recorded
/// direction of the change rather than from a fact about the work. The
/// closure-set semantics ride the constant: registrations widen the
/// envelope's reachable estate, so the same form gates them.
/// </para>
/// <para>
/// <b>A machine check on the widens form is refused where an author can
/// still act.</b> A machine predicate reads facts, and an envelope-change
/// flight ships none — accepting the pair is accepting a gate no evaluation
/// can ever open, which is the deadlock rule 5 exists to keep out of the
/// estate.
/// </para>
/// </remarks>
public class WidensConditionTests
{
    private static Envelope WithWideningGate(string check) => new()
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
                Id = "widen-root",
                Check = check,
                Rule = check == ObligationChecks.Machine ? ObligationPredicates.NoFileOutsideScope : null,
                Approver = check == ObligationChecks.Human ? "platform-owner" : null,
                When = AttachmentConditions.Widens,
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

    [Test]
    public async Task The_widens_form_is_a_condition_this_contract_knows()
    {
        await Assert.That(AttachmentConditions.Widens).IsEqualTo("envelope widens");
        await Assert.That(AttachmentConditions.IsKnown(AttachmentConditions.Widens)).IsTrue()
            .Because("an unknown condition halts rather than attaching, so a form the engine "
                   + "must act on has to be one the contract declares.");
        await Assert.That(AttachmentConditions.Forms).Contains(AttachmentConditions.Widens);
        await Assert.That(AttachmentConditions.GlobOf(AttachmentConditions.Widens)).IsNull()
            .Because("it reads a recorded direction, never a path.");
    }

    [Test]
    public async Task A_human_widening_gate_validates()
    {
        await Assert.That(Envelope.Validate(WithWideningGate(ObligationChecks.Human))).IsNull()
            .Because("the gate is an ordinary human obligation; only its attachment is new.");
    }

    [Test]
    public async Task A_machine_check_on_the_widens_form_is_refused_naming_the_deadlock()
    {
        var diagnosis = Envelope.Validate(WithWideningGate(ObligationChecks.Machine));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("widen-root");
        await Assert.That(diagnosis).Contains(AttachmentConditions.Widens);
        await Assert.That(diagnosis.ToLowerInvariant()).Contains("machine")
            .Because("a machine predicate reads facts an envelope-change flight never ships, "
                   + "so the pair is a gate no evaluation can open - refused where the author "
                   + "can still do something about it.");
    }

    [Test]
    public async Task The_applied_answer_can_say_a_widening_was_diverted()
    {
        // Member additions on EnvelopeApplied: null on every answer today's
        // control planes give, carried when the widening path diverts - so an
        // older reader keeps reading applies and a newer one learns where the
        // gate went.
        var diverted = new EnvelopeApplied
        {
            Version = "v4",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Changed = false,
            Widens = "context.scope",
            Flight = "GG-7",
            Awaiting = "platform-owner",
        };

        await Assert.That(diverted.Widens).IsEqualTo("context.scope");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvelopeApplied)])
            .Contains("widens");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvelopeApplied)])
            .Contains("flight");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnvelopeApplied)])
            .Contains("awaiting");
    }
}
