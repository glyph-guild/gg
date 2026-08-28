namespace Gg.Contracts.Tests;

/// <summary>
/// A work kind may not claim to yield a fact its own subjects make impossible.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0021 § 3's veto, refused where an author can still act.</b> The work
/// kind declares what its work yields and the subject rules out what cannot
/// exist, and a document where the two disagree is a contradiction rather than a
/// permission. <c>produces: [change.manifest]</c> with <c>accepts: []</c> is
/// work with no tree claiming to measure one.
/// </para>
/// <para>
/// <b>The same refusal <c>Accepting</c> already ships, not a second one that
/// reads differently.</b> § 1 already refuses <c>accepts: [repository]</c> with
/// <c>scope: none</c>, and it is the same shape: two fields on one document that
/// have to agree, checked without a role in front of them, naming both halves so
/// somebody reading their own file knows which of two lines to change.
/// </para>
/// <para>
/// <b>The classification has to be here for that to be possible.</b> Whether a
/// family needs a tree is a property of the fact vocabulary, and the vocabulary
/// is declared in this package. Leaving the classification control-plane-side
/// would mean either a second copy of it here — two computations of one
/// question, in the place where being wrong removes a gate — or a refusal an
/// author cannot get until they apply.
/// </para>
/// </remarks>
public class ProducesContradictionTests
{
    private static Envelope Kind(
        IReadOnlyList<string> accepts, IReadOnlyList<string> produces, string scope) => new()
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
                Requires = ["in-scope"],
            },
        ],
    };

    [Test]
    public async Task A_kind_with_no_tree_may_not_claim_to_produce_a_tree_fact()
    {
        var refusal = Envelope.Validate(
            Kind([], [FactKinds.ChangeManifest], EnvelopeScopes.None));

        await Assert.That(refusal).IsNotNull()
            .Because("work with no tree claiming to measure one is a contradiction, and if it "
                   + "is not caught here it is caught by a flight that halts.");
        await Assert.That(refusal!).Contains("produces");
        await Assert.That(refusal!).Contains(FactKinds.ChangeManifest);
        await Assert.That(refusal!).Contains("accepts")
            .Because("NAMING BOTH HALVES is what lets somebody reading their own file work out "
                   + "which of the two lines to change. 'Invalid envelope' sends them reading "
                   + "nine.");
    }

    [Test]
    public async Task A_kind_that_takes_only_a_subject_with_no_tree_is_refused_the_same_way()
    {
        // `envelope` is a real subject kind and has no tree. The rule is about
        // TREES rather than about emptiness, which is the correction slice
        // sixteen made to itself when SubjectKinds gained its second member.
        var refusal = Envelope.Validate(
            Kind([SubjectKinds.Envelope], [FactKinds.SourceProvenance], EnvelopeScopes.None));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains(FactKinds.SourceProvenance);
    }

    [Test]
    public async Task A_kind_with_a_tree_may_claim_a_tree_fact()
    {
        // THE LIVENESS HALF. A refusal with nothing passing it is a validator
        // that refuses everything, and both assertions above would be green.
        await Assert.That(Envelope.Validate(
                Kind([SubjectKinds.Repository], [FactKinds.ChangeManifest], "src/**")))
            .IsNull();
    }

    [Test]
    public async Task A_kind_with_no_tree_may_claim_a_flight_fact()
    {
        // THE OTHER LIVENESS HALF, and it is the one that matters most. A rule
        // about the running applies perfectly to work with no tree, so a
        // refusal that swept up every family would make `research` unwritable -
        // which is the permissive failure's mirror image and just as wrong.
        await Assert.That(Envelope.Validate(
                Kind([], [FactKinds.LoopOutcome], EnvelopeScopes.None)))
            .IsNull()
            .Because("a subjectless flight still runs a loop, and every work kind that runs one "
                   + "produces its outcome.");
    }

    [Test]
    public async Task Every_fact_family_is_classified_so_the_refusal_can_be_computed()
    {
        // The classification has to be TOTAL for the check above to be
        // decidable, and it is guarded here rather than assumed - a family
        // nobody classified would make the refusal silently skip it, which is
        // the permissive direction.
        var unclassified = FactKinds.All.Where(f => !FactCategories.IsClassified(f)).ToList();

        await Assert.That(unclassified).IsEmpty()
            .Because("Found: " + string.Join(", ", unclassified));

        await Assert.That(FactCategories.IsClassified("no.such.family")).IsFalse()
            .Because("a classifier that answers yes to an invented family has stopped being one, "
                   + "and the sweep above would still pass over it.");
    }
}
