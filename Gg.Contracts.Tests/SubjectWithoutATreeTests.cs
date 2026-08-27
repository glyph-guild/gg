namespace Gg.Contracts.Tests;

/// <summary>
/// ADR-0020 § 1's table has three rows, and the third is a subject that is not
/// a tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule shipped as <c>none</c> ⟺ <c>accepts: []</c>, and that is right
/// for a one-member vocabulary and wrong the moment there are two.</b> § 1's
/// table says <c>[repo]</c> takes a path glob, <c>[]</c> takes <c>none</c>, and
/// <c>[envelope]</c> takes <c>none</c> — <i>the subject is the bound</i>. So
/// <c>none</c> is not the value for <i>no subject</i>. It is the value for
/// <b>no tree</b>, and having no subject is one way to have no tree.
/// </para>
/// <para>
/// <b>This is not a hypothetical member.</b> An envelope-change flight is a
/// shipped work kind whose subject is a document, not a repository — it has no
/// tree, produces no manifest, and could not be declared honestly while
/// <c>repository</c> was the only subject kind: <c>[]</c> would say it is about
/// nothing, and <c>[repository]</c> would say it is about a tree.
/// </para>
/// <para>
/// <b>So the rule is re-expressed rather than extended.</b> A path glob is
/// legal exactly when the kind accepts a subject that HAS a tree; everything
/// else writes <c>none</c>. Adding a fourth subject kind then costs one
/// property on the kind rather than an edit to a refusal — which is the
/// difference between a vocabulary and a list of special cases.
/// </para>
/// </remarks>
public class SubjectWithoutATreeTests
{
    private static Envelope Kind(IReadOnlyList<string>? accepts, string scope) => new()
    {
        Context = new ContextBinding { Scope = scope, Constitution = "1.0.0" },
        Accepts = accepts,
        Obligations =
        [
            new Obligation
            {
                Id = "loop-not-exhausted",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.LoopNotExhausted,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "work",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["loop-not-exhausted"],
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
                Requires = ["loop-not-exhausted"],
            },
        ],
    };

    [Test]
    public async Task An_envelope_is_a_subject_kind_and_it_has_no_tree()
    {
        await Assert.That(SubjectKinds.All).Contains(SubjectKinds.Envelope);
        await Assert.That(SubjectKinds.HasTree(SubjectKinds.Envelope)).IsFalse()
            .Because("the subject IS the bound - there is no path inside a document for a "
                   + "glob to select.");
        await Assert.That(SubjectKinds.HasTree(SubjectKinds.Repository)).IsTrue();
    }

    [Test]
    public async Task A_kind_whose_subject_has_no_tree_writes_none()
    {
        await Assert.That(Envelope.Validate(Kind([SubjectKinds.Envelope], EnvelopeScopes.None)))
            .IsNull()
            .Because("ADR-0020 section 1's third row, which the one-member vocabulary could "
                   + "not express: a kind that IS about something, and still has no tree.");

        var refused = Envelope.Validate(Kind([SubjectKinds.Envelope], "src/**"));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(SubjectKinds.Envelope);
        await Assert.That(refused!).Contains("src/**");
    }

    [Test]
    public async Task The_two_rows_that_already_worked_still_do()
    {
        // LIVENESS. Re-expressing the rule in terms of trees rather than
        // emptiness must not move either answer it already gave.
        await Assert.That(Envelope.Validate(Kind([SubjectKinds.Repository], "src/**"))).IsNull();
        await Assert.That(Envelope.Validate(Kind([], EnvelopeScopes.None))).IsNull();
        await Assert.That(Envelope.Validate(Kind([SubjectKinds.Repository], EnvelopeScopes.None)))
            .IsNotNull();
        await Assert.That(Envelope.Validate(Kind([], "src/**"))).IsNotNull();
    }

    [Test]
    public async Task A_kind_accepting_both_takes_a_glob_because_one_of_them_is_a_tree()
    {
        // The case the emptiness rule could not have answered at all. A glob is
        // legal when ANY accepted subject has a tree, because the bound has
        // something to select from - and refusing it would leave a kind that
        // works over both unable to bound the half that has paths.
        await Assert.That(
                Envelope.Validate(Kind([SubjectKinds.Envelope, SubjectKinds.Repository], "src/**")))
            .IsNull();
        await Assert.That(
                Envelope.Validate(
                    Kind([SubjectKinds.Envelope, SubjectKinds.Repository], EnvelopeScopes.None)))
            .IsNotNull()
            .Because("something in the set has a tree, so `none` would leave it unbounded.");
    }

    [Test]
    public async Task Every_subject_kind_answers_whether_it_has_a_tree()
    {
        // TOTALITY, and the anchor beside it. A fourth subject kind that
        // nobody classified would fall through HasTree to some default, and
        // whichever default it fell to would silently decide what scope its
        // kinds may write.
        await Assert.That(SubjectKinds.All).IsNotEmpty();

        foreach (var kind in SubjectKinds.All)
        {
            await Assert.That(() => SubjectKinds.HasTree(kind)).ThrowsNothing();
        }

        await Assert.That(() => SubjectKinds.HasTree("no-such-kind"))
            .Throws<ArgumentOutOfRangeException>()
            .Because("a kind nobody classified must halt rather than take a default that "
                   + "decides what its documents may say.");
    }
}
