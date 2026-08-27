namespace Gg.Contracts.Tests;

/// <summary>
/// <c>accepts:</c> — what subject kinds a work kind takes, and the floor both
/// halves of ADR-0020's schema compute from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The field decides which <c>scope</c> values are legal.</b> A kind that
/// accepts a repository is working over a tree, so a path bound means
/// something and <c>none</c> does not. A kind that accepts nothing has no
/// tree, so a path bound means nothing and <c>none</c> is the only honest
/// answer. Those are the same rule read in two directions, and both are
/// refused where an author can still act.
/// </para>
/// <para>
/// <b>The agreement is checkable without knowing the role, and the absence is
/// not.</b> A document carrying <c>accepts:</c> is claiming to be a work kind,
/// so <c>Validate</c> can check that its two fields agree with no topology in
/// front of it. Whether a work kind was ALLOWED to leave the field out is a
/// question about the role the document was applied to, which only the caller
/// holding the topology can answer - so it takes the role-aware overload, and
/// <c>gg envelope validate</c> supplies the role from the directory the file
/// sits in.
/// </para>
/// <para>
/// <b>Absent is refused rather than defaulted.</b> The default that suggests
/// itself is <i>a work kind with no <c>accepts:</c> accepts a repository</i>,
/// because that is what every kind before this field meant. It is refused
/// anyway: a subjectless kind would then be one keystroke from a kind that
/// takes a tree, with nothing on the page saying which was meant. The whole
/// point of § 1 is that <i>nothing was written</i> and <i>nothing is bounded</i>
/// must not look alike.
/// </para>
/// </remarks>
public class AcceptsBindsTests
{
    private static Envelope Kind(IReadOnlyList<string>? accepts, string scope) => new()
    {
        Context = new ContextBinding { Scope = scope, Constitution = "1.0.0" },
        Accepts = accepts,
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

    [Test]
    public async Task A_kind_that_takes_a_repository_may_not_say_its_scope_is_none()
    {
        var refusal = Envelope.Validate(Kind([SubjectKinds.Repository], EnvelopeScopes.None));

        await Assert.That(refusal).IsNotNull()
            .Because("a kind working over a tree that bounds no path is not a subjectless kind - "
                   + "it is a kind whose author wrote the wrong word, and the flight it opens "
                   + "would be unbounded over a repository.");
        await Assert.That(refusal!).Contains("accepts");
        await Assert.That(refusal!).Contains(EnvelopeScopes.None);
        await Assert.That(refusal!).Contains(SubjectKinds.Repository)
            .Because("naming the field, the value and the subject kind is what lets somebody "
                   + "reading their own file work out which of the two lines to change. "
                   + "'Invalid envelope' sends them reading nine.");
    }

    [Test]
    public async Task A_kind_that_takes_no_subject_may_not_bound_a_path()
    {
        var refusal = Envelope.Validate(Kind([], "src/**"));

        await Assert.That(refusal).IsNotNull()
            .Because("a path bound over work that has no tree is a rule nothing can ever read, "
                   + "which is a gate that reports satisfied by never running.");
        await Assert.That(refusal!).Contains("accepts");
        await Assert.That(refusal!).Contains("src/**");
    }

    [Test]
    public async Task The_two_legal_pairings_are_not_refused()
    {
        // THE LIVENESS HALF. Two refusals with nothing passing between them is
        // a validator that refuses everything, and both assertions above would
        // still be green.
        await Assert.That(Envelope.Validate(Kind([SubjectKinds.Repository], "src/**"))).IsNull()
            .Because("a kind over a tree, bounded to a path, is the ordinary case and the only "
                   + "one that existed before this field.");
        await Assert.That(Envelope.Validate(Kind([], EnvelopeScopes.None))).IsNull()
            .Because("a kind with no subject and no bound is what research looks like, and it "
                   + "is the pairing this whole slice exists to make writable.");
    }

    [Test]
    public async Task An_empty_accepts_is_a_declaration_and_an_absent_one_is_not()
    {
        // The distinction the whole field rests on: `[]` is somebody writing
        // down that this kind takes no subject. Null is somebody not saying.
        await Assert.That(Envelope.Validate(Kind([], EnvelopeScopes.None), Roles.WorkKind)).IsNull()
            .Because("`accepts: []` is a declaration and the pairing is legal.");

        var refusal = Envelope.Validate(Kind(accepts: null, "src/**"), Roles.WorkKind);

        await Assert.That(refusal).IsNotNull()
            .Because("a work kind that does not say what it accepts is one keystroke from a "
                   + "subjectless kind with nothing on the page saying which was meant, and "
                   + "both halves of ADR-0020's schema compute from the field.");
        await Assert.That(refusal!).Contains("accepts");
    }

    [Test]
    public async Task Only_a_work_kind_owes_the_field()
    {
        // ROOT DOES NOT DECLARE IT, and neither does a narrowing. `accepts:`
        // says what a KIND OF WORK takes; the floor applies to every kind at
        // once and has nothing to answer. Refusing root for its absence would
        // make the floor undeclarable.
        await Assert.That(Envelope.Validate(Kind(accepts: null, "src/**"), Roles.Root)).IsNull()
            .Because("the floor governs every kind, so there is no single answer for it to give.");
        await Assert.That(Envelope.Validate(Kind(accepts: null, "src/**"), Roles.Narrowing)).IsNull()
            .Because("a narrowing tightens whatever it attaches to and does not choose a kind.");
    }

    [Test]
    public async Task A_document_that_is_not_a_work_kind_may_not_declare_it_either()
    {
        // The other direction, and it is the one that costs something if it is
        // missing: `accepts:` on root would read as a floor-wide claim about
        // subjects that composition has nowhere to put, and a field that
        // parses and is never read is a promise standing where a control was
        // needed.
        var refusal = Envelope.Validate(Kind([SubjectKinds.Repository], "src/**"), Roles.Root);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("accepts");
        await Assert.That(refusal!).Contains(Roles.Root);
    }
}
