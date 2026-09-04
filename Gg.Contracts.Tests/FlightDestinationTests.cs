namespace Gg.Contracts.Tests;

/// <summary>
/// <c>flight</c> — the destination kind whose act is that a flight exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>First category, third instance.</b> <see cref="DestinationKinds.EnvelopeChange"/>
/// and <see cref="DestinationKinds.AirspaceRegistration"/> are performed by
/// admission itself and land inside the tenant's own stream and registries;
/// <see cref="DestinationKinds.PullRequest"/> leaves on the customer's
/// credential and <see cref="DestinationKinds.CheckRun"/> leaves on the control
/// plane's. This one is performed by admission and nothing leaves at all, which
/// makes it the safest of the four and the reason a classifier can be governed
/// rather than trusted.
/// </para>
/// <para>
/// <b>Why the kind needs a second member beside it.</b> A work kind is not a
/// label on a flight, it is the selection of a governance regime — the loop,
/// the moves, the budget, the destinations and which obligations apply. So a
/// destination able to open a flight is a destination able to hand out a regime,
/// and <c>opens:</c> is what bounds which ones. Without it an agent chooses its
/// own moves with two extra flights and an audit trail that makes it look
/// governed.
/// </para>
/// <para>
/// <b>What this file does NOT hold.</b> Whether an opened kind is declared in
/// the tenant's topology, whether it plays the work-kind role, and whether it
/// itself opens a flight are all questions about the tenant's OTHER documents.
/// <see cref="Envelope.Validate(Envelope)"/> is pure over one document and is
/// not even told this document's own name, so those refusals live at the
/// control plane's apply door, where the rest of the chain is readable. Trying
/// to express them here is how a rule ends up half-enforced.
/// </para>
/// </remarks>
public class FlightDestinationTests
{
    private static Envelope Classifying(Destination destination) => new()
    {
        // NO SUBJECT AND NO SCOPE. A classifier reads a work item, and a work
        // item is not an admitted subject kind - so `accepts: []` is the honest
        // answer and `scope: none` is what ADR-0020 section 1 requires of it.
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [],
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
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "10m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    private static Destination Opening(
        IReadOnlyList<string>? opens = null, bool? preserve = null) => new()
    {
        Id = "open-the-flight",
        Kind = DestinationKinds.Flight,
        Requires = ["human-look"],
        Opens = opens ?? ["research", "implement"],
        PreserveUnadmitted = preserve,
    };

    [Test]
    public async Task The_kind_is_a_member_and_an_envelope_declaring_it_validates()
    {
        await Assert.That(DestinationKinds.All).Contains(DestinationKinds.Flight);
        await Assert.That(Envelope.Validate(Classifying(Opening()))).IsNull()
            .Because("slice twelve found AirspaceRegistration declared but absent from All, so "
                   + "an envelope declaring it was refused by the very vocabulary that "
                   + "declared it. Both halves, every time.");
    }

    [Test]
    public async Task An_unknown_kind_is_still_refused_and_lists_the_legal_ones()
    {
        // LIVENESS. A vocabulary that accepted anything would satisfy the
        // assertion above without being a vocabulary.
        var refusal = Envelope.Validate(Classifying(Opening() with { Kind = "flght" }));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("flght");
        await Assert.That(refusal!).Contains(DestinationKinds.Flight);
    }

    [Test]
    public async Task Opens_is_refused_on_every_kind_that_is_not_a_flight()
    {
        // A SWEEP RATHER THAN A CASE, so the fifth kind inherits the refusal on
        // the day it joins instead of on the day somebody remembers. This is
        // the shape preserve-unadmitted's own refusal has, three lines away in
        // the same loop, and for the same stated reason: a knob that silently
        // does nothing on one kind of destination is a governance permission
        // somebody sets and believes they granted.
        foreach (var kind in DestinationKinds.All.Where(k =>
            !string.Equals(k, DestinationKinds.Flight, StringComparison.Ordinal)))
        {
            var declaring = Classifying(Opening() with
            {
                Kind = kind,
                // preserve-unadmitted rides only pull-request, and requires must
                // still name a real obligation - so the ONLY thing wrong with
                // this document is `opens:`.
                PreserveUnadmitted = null,
            });

            var refusal = Envelope.Validate(declaring);

            await Assert.That(refusal).IsNotNull()
                .Because($"'{kind}' cannot open a flight, so `opens:` on it names nothing.");
            await Assert.That(refusal!).Contains("opens");
            await Assert.That(refusal!).Contains(kind)
                .Because("the diagnosis names the kind, so somebody who put the key on the "
                       + "wrong destination can see which one it was.");
        }
    }

    [Test]
    public async Task A_flight_destination_that_opens_nothing_is_refused()
    {
        // THE UNREACHABLE DESTINATION, refused at authoring for the first time.
        // ADR-0019 section 3 has asked for this since it was written - "the
        // engine should refuse an envelope with an unreachable destination, the
        // way a build system rejects a missing rule" - and nothing has ever
        // implemented it. A flight destination that may open nothing is a
        // destination whose admission can never act, which is a flight that
        // can never end.
        var empty = Envelope.Validate(Classifying(Opening(opens: [])));

        await Assert.That(empty).IsNotNull();
        await Assert.That(empty!).Contains("opens");
        await Assert.That(empty!).Contains("open-the-flight");
    }

    [Test]
    public async Task A_flight_destination_that_omits_opens_is_refused()
    {
        // ABSENT AND EMPTY ARE ONE ANSWER HERE, and that is the opposite of
        // `accepts:` on purpose. `accepts:` distinguishes them because silence
        // is a legal state for a document that is not a work kind; a flight
        // destination is never silent about this, because the key is the whole
        // of what its admission does.
        var absent = Envelope.Validate(Classifying(new Destination
        {
            Id = "open-the-flight",
            Kind = DestinationKinds.Flight,
            Requires = ["human-look"],
        }));

        await Assert.That(absent).IsNotNull();
        await Assert.That(absent!).Contains("opens");
    }

    [Test]
    public async Task A_blank_or_repeated_opened_name_is_refused()
    {
        var blank = Envelope.Validate(Classifying(Opening(opens: ["research", " "])));

        await Assert.That(blank).IsNotNull()
            .Because("a blank entry is a line somebody meant to fill in, and reading it as a "
                   + "work-kind name would send the refusal to the topology instead.");

        var repeated = Envelope.Validate(Classifying(Opening(opens: ["research", "research"])));

        await Assert.That(repeated).IsNotNull();
        await Assert.That(repeated!).Contains("research");
    }

    [Test]
    public async Task Preserve_unadmitted_is_refused_on_it_naming_the_key()
    {
        // ALREADY TRUE, AND ASSERTED RATHER THAN RE-IMPLEMENTED - the same debt
        // check-run paid. There is no half-opened flight.
        var refusal = Envelope.Validate(Classifying(Opening(preserve: true)));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("preserve-unadmitted");
        await Assert.That(refusal!).Contains(DestinationKinds.Flight);
    }

    [Test]
    public async Task Requires_still_names_a_real_obligation()
    {
        // The per-kind opt-in is `requires:`, so it has to keep working here.
        // A tenant gates flight-opening behind an obligation or does not, and a
        // requires naming nothing would be a gate nobody can answer.
        var refusal = Envelope.Validate(Classifying(Opening() with
        {
            Requires = ["a-gate-nobody-declared"],
        }));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("a-gate-nobody-declared");
    }

    [Test]
    public async Task A_flight_destination_round_trips()
    {
        var written = EnvelopeText.Render(Classifying(Opening()));
        var read = Authoring.EnvelopeYaml.Parse(written);

        await Assert.That(read.Diagnosis).IsNull()
            .Because($"the emitter's own output must parse. Wrote:\n{written}");

        var destination = read.Envelope!.Destinations.Single();

        await Assert.That(destination.Kind).IsEqualTo(DestinationKinds.Flight);
        await Assert.That(destination.Opens).IsNotNull();
        await Assert.That(destination.Opens!).Contains("research");
        await Assert.That(destination.Opens!).Contains("implement");

        // AND THE SECOND RENDER IS THE FIRST. `envelope show` after
        // `envelope apply` must not produce a diff nobody made.
        await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written);
    }

    [Test]
    public async Task A_destination_that_omits_opens_round_trips_unchanged()
    {
        // ABSENT STAYS ABSENT on every other kind, which is what stops this
        // member rewriting every tenant's document on their next show.
        var pullRequest = new Envelope
        {
            Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
            Accepts = [SubjectKinds.Repository],
            Produces = [FactKinds.ChangeManifest],
            Obligations =
            [
                new Obligation
                {
                    Id = "human-look", Check = ObligationChecks.Human, Approver = "lead",
                },
            ],
            Loops =
            [
                new Loop
                {
                    Id = "build",
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
                    Id = "the-branch",
                    Kind = DestinationKinds.PullRequest,
                    Requires = ["human-look"],
                },
            ],
        };

        var written = EnvelopeText.Render(pullRequest);

        await Assert.That(written).DoesNotContain("opens");

        var read = Authoring.EnvelopeYaml.Parse(written);

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Envelope!.Destinations.Single().Opens).IsNull();
    }
}
