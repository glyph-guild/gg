using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A loop that ran out of budget can hand the work to another loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>`executor` is a parameter of a loop, so this needs no new machinery.</b> An
/// agent resuming another agent's work is the same loop starting again, on a rung,
/// with the seed as declared context. What was missing was somewhere for
/// <c>on-exhaustion</c> to point and a way for the seed to reach a runner.
/// </para>
/// <para>
/// <b>And the seed reaches it on the LEASE, not from the route.</b>
/// <c>GET /v1/flights/{ref}/seed</c> answers a developer session, deliberately: a
/// runner that could read it could read what every flight in the tenant tried and
/// ruled out from a credential meant only to let it hold one lease. So a resuming
/// loop is handed its context rather than fetching it, which is also the shape
/// <c>ContextBinding</c> already has.
/// </para>
/// </remarks>
public class HandoffToAgentTests
{
    [Test]
    public async Task There_is_somewhere_for_an_exhausted_loop_to_go_besides_a_person()
    {
        await Assert.That(ExhaustionPolicies.All).Contains(ExhaustionPolicies.HandoffToAgent);
        await Assert.That(ExhaustionPolicies.All).Contains(ExhaustionPolicies.HandoffToHuman)
            .Because("the first value does not move. An envelope that named it means exactly what "
                   + "it meant.");
    }

    [Test]
    public async Task A_new_value_in_a_closed_vocabulary_is_a_break_and_the_ledger_records_it()
    {
        // THE COST, asserted rather than assumed. A value in a closed enumeration
        // makes the only safe response in a prior reader a HALT - which is the
        // behaviour, not a defect. So it moves the contract surface, and the
        // fingerprint over closed vocabularies is what forces this conversation.
        //
        // ClosedVocabularies discovers these by shape, so ExhaustionPolicies is
        // covered the day it gains a value. This asserts the membership that puts
        // it in the contract ledger rather than the fact ledger: an on-exhaustion
        // policy is read off an envelope and never travels inside a fact.
        var membership = typeof(ExhaustionPolicies)
            .GetCustomAttributes(typeof(VocabularyOfAttribute), inherit: false)
            .Cast<VocabularyOfAttribute>()
            .Single();

        await Assert.That(membership.Fingerprint).IsEqualTo(VocabularyFingerprints.Contract)
            .Because("a policy is read off an envelope and never travels inside a fact, so the "
                   + "FACT vocabulary must not move for it - and a type attributed by shape rather "
                   + "than declaration is how a gate payload once moved the wrong ledger.");
    }

    [Test]
    public async Task An_envelope_written_before_this_value_means_exactly_what_it_did()
    {
        // S7.5-02's other half, and the one that reassures a tenant. Adding a value
        // does not reinterpret an existing one: handoff-to-human still parses, still
        // validates, and still means a person is asked.
        var envelope = Governing(ExhaustionPolicies.HandoffToHuman);

        await Assert.That(Envelope.Validate(envelope)).IsNull();
        await Assert.That(EnvelopeText.Render(envelope)).Contains(
            $"on-exhaustion: {ExhaustionPolicies.HandoffToHuman}");
    }

    [Test]
    public async Task An_envelope_naming_the_new_value_validates()
    {
        await Assert.That(Envelope.Validate(Governing(ExhaustionPolicies.HandoffToAgent))).IsNull();
    }

    [Test]
    public async Task A_policy_this_version_does_not_know_is_still_refused()
    {
        // The poison twin for the whole vocabulary. A closed set that accepted a
        // third spelling would make the version cost pointless.
        var diagnosis = Envelope.Validate(Governing("handoff-to-whoever"));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("handoff-to-whoever");
    }

    [Test]
    public async Task A_resuming_loop_is_handed_what_the_last_one_ruled_out()
    {
        // The seed rides the LEASE. A member on LeaseLoop rather than a route the
        // runner calls, because the seed route answers a developer and a runner is
        // not one - and that audience is deliberate rather than incidental.
        var members = ProtocolSurface.JsonMembers[typeof(LeaseLoop)];

        await Assert.That(members).Contains("resumesFrom");

        var loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 1800,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
            ResumesFrom = "GG-42 — taking over\n\nread, not changed:\n  - src/util.py",
        };

        await Assert.That(loop.ResumesFrom).IsNotNull();

        // ABSENT ON A FIRST ATTEMPT, and that is the ordinary case: there is nothing
        // to resume from. A member that had to be present would make every lease
        // carry an empty document, and "no prior attempt" and "a prior attempt that
        // measured nothing" would read the same.
        await Assert.That((loop with { ResumesFrom = null }).ResumesFrom).IsNull();
    }

    private static Envelope Governing(string onExhaustion) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "scope-respected",
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
                Discharges = ["scope-respected"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = onExhaustion,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["scope-respected"],
            },
        ],
    };
}
