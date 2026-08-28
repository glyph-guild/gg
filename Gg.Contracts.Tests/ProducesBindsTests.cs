using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>produces:</c> — the fact families a work kind's work can yield, and the
/// relation ADR-0020 § 2 lost.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0014's relations table had two rows and ADR-0020 § 2 collapsed
/// them.</b> <i>work kind → subject kinds accepted</i> and <i>work kind → fact
/// families it produces</i> are different relations, and deriving the second
/// from the first cannot separate two kinds that share a subject. `implement`
/// and `review` are both about a repository and do not have the same fact
/// vocabulary.
/// </para>
/// <para>
/// <b>It is declared rather than derived, and the alternative was rejected on
/// a security ground.</b> A kind whose loops declare no <c>edit</c> or
/// <c>write</c> cannot produce a change manifest, which is mechanical and
/// impossible to under-declare — but <c>moves:</c> is not work-kind-only. It
/// composes across layers, so a narrowing, including one living in a customer's
/// own repository, could delete every scope gate on its own flights by
/// narrowing moves to <c>[read]</c>. A governance document that removes
/// governance by being obeyed.
/// </para>
/// <para>
/// <b>Absent is refused rather than defaulted, for <c>accepts:</c>' reason.</b>
/// The tempting default — <i>no <c>produces:</c> means everything the subject
/// allows</i> — is exactly today's behaviour, so it would make the correction
/// opt-in and leave the collision in place for every kind whose author did not
/// hear about the field.
/// </para>
/// <para>
/// <b>What a kind YIELDS, not what its runner POSTS.</b> A review flight
/// materializes a tree, so the runner ships it a manifest with no paths in it.
/// The kind still declares <c>produces: []</c>: an empty manifest arriving does
/// not make a scope rule applicable to work that cannot change anything. The
/// other reading leaves the vacuous pass exactly where it was found.
/// </para>
/// </remarks>
public class ProducesBindsTests
{
    private static Envelope Kind(
        IReadOnlyList<string>? accepts,
        IReadOnlyList<string>? produces,
        string scope = "src/**") => new()
    {
        Context = new ContextBinding { Scope = scope, Constitution = "1.0.0" },
        Accepts = accepts,
        Produces = produces,
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
                Id = "work",
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
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
    };

    // ---- the field ----

    [Test]
    public async Task A_work_kind_that_does_not_say_what_it_produces_is_refused()
    {
        var refusal = Envelope.Validate(
            Kind([SubjectKinds.Repository], produces: null), Roles.WorkKind);

        await Assert.That(refusal).IsNotNull()
            .Because("defaulting it to everything the subject allows IS today's behaviour, so a "
                   + "default would make the correction opt-in and leave the collision in place "
                   + "for every kind whose author never heard of the field.");
        await Assert.That(refusal!).Contains("produces");
    }

    [Test]
    public async Task A_document_that_is_not_a_work_kind_may_not_declare_it()
    {
        // The floor governs every kind at once, so it has no single answer to
        // give; a narrowing tightens whatever it attaches to without choosing a
        // kind. Same rule `accepts:` already carries, and the same words.
        var refusal = Envelope.Validate(
            Kind(accepts: null, produces: [FactKinds.ChangeManifest]), Roles.Root);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("produces");
        await Assert.That(refusal!).Contains(Roles.Root);
    }

    [Test]
    public async Task An_unknown_fact_family_is_refused_naming_the_vocabulary()
    {
        // A typo cannot become `produces everything`. The failure direction here
        // is permissive: a family nobody recognises, silently dropped, is a gate
        // that stops firing.
        var refusal = Envelope.Validate(
            Kind([SubjectKinds.Repository], ["change.manifests"]), Roles.WorkKind);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("change.manifests");
        await Assert.That(refusal!).Contains(FactKinds.ChangeManifest)
            .Because("naming the vocabulary is what turns a refusal into a correction somebody "
                   + "can make without opening the source.");
    }

    [Test]
    public async Task The_declarations_that_agree_are_not_refused()
    {
        // THE LIVENESS HALF. Refusals with nothing passing between them are a
        // validator that refuses everything, and every assertion above would
        // still be green.
        await Assert.That(Envelope.Validate(
            Kind([SubjectKinds.Repository], [FactKinds.ChangeManifest]), Roles.WorkKind)).IsNull()
            .Because("a kind over a tree that changes it is the ordinary case.");

        await Assert.That(Envelope.Validate(
            Kind([SubjectKinds.Repository], []), Roles.WorkKind)).IsNull()
            .Because("REVIEW. A kind over a tree that yields no tree fact is the whole point of "
                   + "the second relation - and its runner still ships an empty manifest, which "
                   + "is a thing the runner did rather than a thing the kind can yield.");

        await Assert.That(Envelope.Validate(
            Kind([], [], EnvelopeScopes.None), Roles.WorkKind)).IsNull()
            .Because("research: no subject, no bound, nothing yielded.");
    }

    [Test]
    public async Task An_empty_produces_is_a_declaration_and_an_absent_one_is_not()
    {
        await Assert.That(Envelope.Validate(
            Kind([SubjectKinds.Repository], []), Roles.WorkKind)).IsNull();

        await Assert.That(Envelope.Validate(
            Kind([SubjectKinds.Repository], produces: null), Roles.WorkKind)).IsNotNull()
            .Because("`[]` is somebody writing down that this kind yields nothing. Null is "
                   + "somebody not saying, and the two must not mean the same thing.");
    }

    // ---- the round trip ----

    [Test]
    public async Task Produces_survives_both_render_paths_and_keeps_empty_apart_from_absent()
    {
        // SLICE SIXTEEN'S OWN LESSON, PAID FORWARD. Its first `accepts:` red
        // asserted the field EXISTED, so the green validated it in memory while
        // neither render path emitted it - and `evidence:` had gone missing the
        // same way three contract versions earlier. A field that does not
        // round-trip is a field that silently disappears on the way to storage.
        foreach (var produces in (IReadOnlyList<string>?[])
                 [[], [FactKinds.ChangeManifest], [FactKinds.ChangeManifest, FactKinds.SourceProvenance]])
        {
            var written = EnvelopeText.Render(Kind([SubjectKinds.Repository], produces));
            var read = EnvelopeYaml.Parse(written);

            await Assert.That(read.Diagnosis).IsNull()
                .Because($"what this library renders it must parse. Wrote:\n{written}");
            await Assert.That(read.Envelope!.Produces).IsEquivalentTo(produces!);
            await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written);
        }
    }

    [Test]
    public async Task An_absent_produces_renders_no_line_and_parses_back_as_absent()
    {
        var written = EnvelopeText.Render(Kind(accepts: null, produces: null));

        await Assert.That(written).DoesNotContain("produces:");
        await Assert.That(EnvelopeYaml.Parse(written).Envelope!.Produces).IsNull();
    }

    // ---- composition ----

    [Test]
    public async Task Produces_is_supplied_by_the_work_kind_and_no_narrowing_can_touch_it()
    {
        // THE SECURITY PROPERTY THAT CHOSE THIS DESIGN OVER DERIVING IT FROM
        // `moves:`. A narrowing has no member to express it, and composition
        // reads it off the base document alone - so a repository-resident
        // narrowing cannot reach it however it is written.
        await Assert.That(typeof(EnvelopeNarrowing).GetProperty("Produces")).IsNull()
            .Because("the strongest form of the rule is one a document cannot express.");

        // A DISTINCT OBLIGATION ID, because two layers declaring one name is
        // refused by composition and has nothing to do with what is under test.
        var basis = Kind([SubjectKinds.Repository], []);
        var review = basis with
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "a-person-read-it",
                    Check = ObligationChecks.Human,
                    Approver = "a-reviewer",
                },
            ],
            Loops = [basis.Loops[0] with { Discharges = [] }],
            Destinations = [basis.Destinations[0] with { Requires = ["a-person-read-it"] }],
        };

        var composed = EnvelopeComposition.Compose(
        [
            new EnvelopeLayer
            {
                Role = Roles.Root, Name = "root", Version = "v1",
                Document = Kind(null, null),
            },
            new EnvelopeLayer
            {
                Role = Roles.WorkKind,
                Name = "review",
                Parent = "root",
                Version = "v1",
                Document = review,
            },
        ]);

        await Assert.That(composed.Refused).IsNull();
        await Assert.That(composed.Composed!.Produces).IsEquivalentTo((string[])[])
            .Because("one layer supplies the sets, and it is the work kind's.");
    }
}
