namespace Gg.Contracts.Tests;

/// <summary>
/// <c>check-run</c> — the first destination kind whose act leaves this system.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0020 § 4 names <c>comment</c>, and this is <c>check-run</c>, on
/// evidence.</b> § 4's table puts all three kinds on ports it says
/// <i>Platform Architecture already designs</i>. There is no Notify port in
/// any form, and Intent is a URI rendered into a prompt that nothing resolves
/// in either direction — so of the three, none has machinery. What does have
/// machinery is the check run: a command, a receptor that returns the event,
/// an adapter, and the one write the client is permitted to make.
/// </para>
/// <para>
/// <b>And the permission decides it.</b> The installation holds <c>checks</c>
/// and no write on issues or pull requests, so <c>comment</c> costs a re-grant
/// every installation must approve and nobody can un-ask. A check run is
/// visible on the pull request, attributable, durable, and costs nothing —
/// which is strictly better than this slice's own pre-committed cut, where the
/// act would have been admitted and recorded and never performed.
/// </para>
/// <para>
/// <b><c>preserve-unadmitted</c> is refused on it by a rule that already
/// generalises</b>, and that is asserted here rather than re-implemented. What
/// this slice owes § 5 is a test, not a check.
/// </para>
/// </remarks>
public class CheckRunDestinationTests
{
    private static Envelope Posting(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Accepts = [SubjectKinds.Repository],
        Obligations =
        [
            new Obligation { Id = "human-look", Check = ObligationChecks.Human, Approver = "lead" },
        ],
        Loops =
        [
            new Loop
            {
                Id = "review",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    private static Destination CheckRun(bool? preserve = null) => new()
    {
        Id = "post-the-review",
        Kind = DestinationKinds.CheckRun,
        Requires = ["human-look"],
        PreserveUnadmitted = preserve,
    };

    [Test]
    public async Task The_kind_is_a_member_and_an_envelope_declaring_it_validates()
    {
        await Assert.That(DestinationKinds.All).Contains(DestinationKinds.CheckRun);
        await Assert.That(Envelope.Validate(Posting(CheckRun()))).IsNull()
            .Because("slice twelve found AirspaceRegistration declared but absent from All, so "
                   + "an envelope declaring it was refused by the very vocabulary that "
                   + "declared it. Both halves, every time.");
    }

    [Test]
    public async Task An_unknown_kind_is_still_refused_and_lists_the_legal_ones()
    {
        // LIVENESS. A vocabulary that accepted anything would satisfy the
        // assertion above without being a vocabulary.
        var refusal = Envelope.Validate(Posting(CheckRun() with { Kind = "cheque-run" }));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("cheque-run");
        await Assert.That(refusal!).Contains(DestinationKinds.CheckRun)
            .Because("the diagnosis lists what was expected, so somebody who mistyped it can "
                   + "see the spelling rather than go looking for the vocabulary.");
    }

    [Test]
    public async Task Preserve_unadmitted_is_refused_on_it_naming_the_key()
    {
        // ALREADY TRUE, AND ASSERTED RATHER THAN RE-IMPLEMENTED. Validate
        // refuses the key on EVERY kind that is not pull-request, so a new kind
        // inherits the refusal the moment it joins the vocabulary. What section
        // 5 asks for is that the key not parse and do nothing, and it does not.
        var refusal = Envelope.Validate(Posting(CheckRun(preserve: true)));

        await Assert.That(refusal).IsNotNull()
            .Because("there is no half-posted check run, so the key means nothing here - and a "
                   + "key that parses and does nothing is a promise standing where a control "
                   + "was needed.");
        await Assert.That(refusal!).Contains("preserve-unadmitted");
        await Assert.That(refusal!).Contains(DestinationKinds.CheckRun);
    }

    [Test]
    public async Task Preserve_unadmitted_is_still_allowed_where_there_is_a_branch()
    {
        // The other half, so the refusal above is about THIS kind rather than
        // about the key having been retired.
        var onABranch = Posting(CheckRun() with
        {
            Kind = DestinationKinds.PullRequest,
            PreserveUnadmitted = true,
        });

        await Assert.That(Envelope.Validate(onABranch)).IsNull();
    }

    [Test]
    public async Task A_check_run_destination_round_trips()
    {
        var written = EnvelopeText.Render(Posting(CheckRun()));
        var read = Authoring.EnvelopeYaml.Parse(written);

        await Assert.That(read.Diagnosis).IsNull()
            .Because($"the emitter's own output must parse. Wrote:\n{written}");
        await Assert.That(read.Envelope!.Destinations.Single().Kind)
            .IsEqualTo(DestinationKinds.CheckRun);
    }
}
