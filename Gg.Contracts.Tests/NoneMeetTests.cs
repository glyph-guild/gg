namespace Gg.Contracts.Tests;

/// <summary>
/// <c>none ⊓ glob</c> — the question ADR-0020 does not list, and the first
/// research flight computes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Root is not a work kind, and composition is a meet.</b> The floor always
/// applies and always carries a <c>scope</c>, so the first flight for a
/// subjectless kind computes <c>"src/**" ⊓ none</c> — and before this, nothing
/// anywhere defined that. § 2 does not rescue it: § 2 is about rules reading
/// fact families the kind cannot produce, and the scope FIELD is not a rule and
/// is not read from a fact. So § 2 saves the obligations and leaves the field
/// it was written to protect.
/// </para>
/// <para>
/// <b>Decided as a domain mismatch, which is the ruling § 2 already makes one
/// noun over.</b> A path bound is a statement about a tree. A kind that accepts
/// no subject has no tree, so the floor's bound is not <i>narrowed away</i> and
/// is not <i>in conflict</i> — it is inapplicable, structurally, from the
/// documents alone and without evaluating anything.
/// </para>
/// <para>
/// <b>The computed value is the same as <i>none absorbs</i>, and saying so is
/// the honest part.</b> What differs is not the string: it is that the answer
/// is derived from a declaration rather than from a rule about which of two
/// bounds is smaller, and that nothing has to decide whether nothing is
/// narrower than something. The permissive reading — a floor's bound silently
/// ceasing to apply — is the same bytes and a different argument, and the
/// argument is what the next person reads.
/// </para>
/// <para>
/// <b>And the value may only appear where it is declared.</b> Root writing
/// <c>scope: none</c> would bound every path-taking kind to nothing, which is
/// the trap this rule closes: <c>none</c> is legal exactly where
/// <c>accepts: []</c> is, and refused everywhere else.
/// </para>
/// </remarks>
public class NoneMeetTests
{
    private static Envelope Document(
        string scope, IReadOnlyList<string>? accepts, string obligationId = "in-scope") => new()
    {
        Context = new ContextBinding { Scope = scope, Constitution = "1.0.0" },
        Accepts = accepts,
        Obligations =
        [
            new Obligation
            {
                Id = obligationId,
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
                Discharges = [obligationId],
                Moves = [LoopMoves.Read],
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
                Requires = [obligationId],
            },
        ],
    };

    private static EnvelopeLayer Layer(string role, string name, Envelope document) => new()
    {
        Role = role,
        Name = name,
        Parent = string.Equals(role, Roles.Root, StringComparison.Ordinal) ? null : Roles.Root,
        Document = document,
        Version = $"{name}@v1",
    };

    [Test]
    public async Task A_floors_path_bound_meets_a_subjectless_kind_as_none()
    {
        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, Roles.Root, Document("src/**", accepts: null, "floor-scope")),
            Layer(Roles.WorkKind, "research", Document(EnvelopeScopes.None, accepts: [])),
        ]);

        await Assert.That(composed.Refused).IsNull()
            .Because("before this rule the two scopes fell through to 'neither contains the "
                   + "other', so the FIRST research flight a tenant opened was a refusal - the "
                   + "ADR's section 1 shipping the failure section 2 exists to prevent.");
        await Assert.That(composed.Composed!.Context.Scope).IsEqualTo(EnvelopeScopes.None)
            .Because("work with no tree is bounded to no tree. The floor's bound is not "
                   + "narrowed away and is not in conflict - it is inapplicable, and that is "
                   + "decided from the documents rather than from any fact.");
    }

    [Test]
    public async Task The_ordinary_meet_is_untouched()
    {
        // LIVENESS. A rule that answered `none` for every pairing would satisfy
        // the assertion above and quietly stop bounding every flight in the
        // estate.
        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, Roles.Root, Document("**", accepts: null, "floor-scope")),
            Layer(Roles.WorkKind, "implement",
                  Document("src/**", accepts: [SubjectKinds.Repository])),
        ]);

        await Assert.That(composed.Refused).IsNull();
        await Assert.That(composed.Composed!.Context.Scope).IsEqualTo("src/**");
    }

    [Test]
    public async Task Two_bounds_that_do_not_nest_are_still_refused_naming_both()
    {
        // The other liveness half: `none` is a new value in the meet and not a
        // new way out of it. An undecidable pair of GLOBS is still a refusal.
        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, Roles.Root, Document("src/**", accepts: null, "floor-scope")),
            Layer(Roles.WorkKind, "implement",
                  Document("docs/**", accepts: [SubjectKinds.Repository])),
        ]);

        await Assert.That(composed.Refused).IsNotNull();
        await Assert.That(composed.Refused!).Contains("src/**");
        await Assert.That(composed.Refused!).Contains("docs/**");
    }

    [Test]
    public async Task A_document_that_is_not_a_subjectless_kind_may_not_say_none()
    {
        // THE TRAP THIS CLOSES. A floor saying `scope: none` would bound every
        // path-taking kind in the tenant to nothing - and with the meet rule
        // above in place it would do it QUIETLY, which is worse than the
        // refusal it used to produce.
        var refusal = Envelope.Validate(Document(EnvelopeScopes.None, accepts: null));

        await Assert.That(refusal).IsNotNull()
            .Because("`none` says there is no tree, and only a kind that declared it accepts "
                   + "no subject has standing to say so.");
        await Assert.That(refusal!).Contains(EnvelopeScopes.None);
        await Assert.That(refusal!).Contains("accepts");
    }

    [Test]
    public async Task The_meet_is_decided_by_the_declaration_and_not_by_the_string()
    {
        // The distinction the whole ruling rests on, made checkable: a work
        // kind that says `none` while accepting a repository never reaches the
        // meet at all, because Validate refuses it first. So the composer's
        // `none` branch can only ever be reached by a document that DECLARED it
        // takes no subject - which is what makes this a domain rule rather than
        // a claim that nothing is narrower than anything.
        var lying = Document(EnvelopeScopes.None, accepts: [SubjectKinds.Repository]);

        await Assert.That(Envelope.Validate(lying)).IsNotNull()
            .Because("if this pairing were authorable, the meet below would be absorbing a "
                   + "bound for work that does have a tree.");

        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, Roles.Root, Document("src/**", accepts: null, "floor-scope")),
            Layer(Roles.WorkKind, "research", Document(EnvelopeScopes.None, accepts: [])),
        ]);

        await Assert.That(composed.Composed!.Accepts).IsNotNull()
            .Because("the composed document carries the declaration the meet was decided from, "
                   + "so a reader can check the reasoning rather than take the scope on trust.");
        await Assert.That(composed.Composed!.Accepts).IsEmpty();
    }
}
