namespace Gg.Contracts.Tests;

/// <summary>
/// The two members the emitter dropped: <c>evidence:</c> and
/// <c>attempts:</c>, and where they land in the canonical bytes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fired live before this was written (slice nine, step 0).</b>
/// <c>Obligation.Evidence</c> was authorable and load-bearing, and the
/// emitter never wrote it - so <c>show → edit → apply</c> silently removed a
/// gate's evidence requirement, and the weakening minted an attributed
/// version with nothing marking the removal. <c>LoopBudget.Attempts</c> is
/// the same defect through the other door: stored via the wire, invisible in
/// the reviewed text, and additionally refused by the parser on the way back
/// in.
/// </para>
/// <para>
/// <b>Position is part of the contract.</b> <c>evidence:</c> lands LAST in
/// the obligation block and <c>attempts:</c> after <c>wall-clock:</c>, both
/// emitted only when declared - so every document that never declared them
/// keeps its exact bytes, and a diff nobody made never arrives in a
/// customer's review.
/// </para>
/// </remarks>
public class EvidenceCanonicalFormTests
{
    private static Envelope AnEnvelope(
        IReadOnlyList<string>? evidence = null, int? attempts = null) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "human-look",
                Check = ObligationChecks.Human,
                Approver = "lead",
                Evidence = evidence ?? [],
            },
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
                Moves = [LoopMoves.Edit, LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m", Attempts = attempts },
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
    public async Task An_evidence_declaration_lands_after_the_approver_in_the_bytes()
    {
        var text = EnvelopeText.Render(AnEnvelope(
            evidence: [EvidenceItems.AgentAccount, EvidenceItems.ChangeManifest]));

        await Assert.That(text).Contains(
            "    approver: lead\n"
          + "    evidence:\n"
          + "      - agent-account\n"
          + "      - change-manifest\n")
            .Because("the coda of the sentence: when this holds, this person answers, given "
                   + "this evidence - and last is what keeps every evidence-less document's "
                   + "bytes exactly where they were.");
    }

    [Test]
    public async Task An_obligation_without_evidence_keeps_its_exact_bytes()
    {
        // The preserve-unadmitted rule, applied here: absent stays absent, so a
        // fix for one member does not rewrite every tenant's document on the
        // next show.
        await Assert.That(EnvelopeText.Render(AnEnvelope())).DoesNotContain("evidence");
    }

    [Test]
    public async Task Adding_evidence_changes_the_bytes()
    {
        // The poison twin's other half: if the emitter dropped the member
        // again, these two renders would be equal and this would fail.
        await Assert.That(EnvelopeText.Render(AnEnvelope(evidence: [EvidenceItems.ChangeManifest])))
            .IsNotEqualTo(EnvelopeText.Render(AnEnvelope()));
    }

    [Test]
    public async Task An_attempts_budget_lands_after_the_wall_clock()
    {
        var text = EnvelopeText.Render(AnEnvelope(attempts: 3));

        await Assert.That(text).Contains(
            "      wall-clock: \"30m\"\n"
          + "      attempts: 3\n");
        await Assert.That(EnvelopeText.Render(AnEnvelope())).DoesNotContain("attempts");
    }
}
