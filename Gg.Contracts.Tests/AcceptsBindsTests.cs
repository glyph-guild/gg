using Gg.Contracts.Authoring;

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

        // MIRRORS `accepts:`, so this fixture stays about `accepts:`. Slice
        // seventeen made `produces:` required on a work kind and refused on
        // every other role - both being work-kind-only - so a document that
        // declares one declares the other, and a fixture that hard-coded either
        // answer would fail these assertions for a reason none of them is about.
        Produces = accepts is null ? null : [],
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
    public async Task The_field_survives_being_written_down_and_read_back()
    {
        // WHAT MY OWN FIRST RED MISSED, and it is the exact shape of the
        // failure this repository has spent the most words on. `evidence:` was
        // authorable, load-bearing at the gate, and emitted by neither render
        // path - so an edit round trip stripped it and the gate assembled from
        // an empty list. A field validated in memory and invisible to the
        // emitter is that failure with a new name.
        //
        // `[]` IS THE VALUE THAT HAS TO SURVIVE. Null renders as nothing and
        // parses back as null, which is easy; an empty list that renders as
        // nothing parses back as null too, and a subjectless kind silently
        // becomes a kind that never said.
        foreach (var accepts in (IReadOnlyList<string>[])[[], [SubjectKinds.Repository]])
        {
            var scope = accepts.Count == 0 ? EnvelopeScopes.None : "src/**";
            var written = EnvelopeText.Render(Kind(accepts, scope));
            var read = EnvelopeYaml.Parse(written);

            await Assert.That(read.Diagnosis).IsNull()
                .Because($"the emitter's own output must parse. Wrote:\n{written}");
            await Assert.That(read.Envelope!.Accepts).IsNotNull()
                .Because($"accepts: {(accepts.Count == 0 ? "[]" : "[repository]")} was written "
                       + "down and came back as nothing at all, which is a declaration turning "
                       + "into an absence in a round trip nobody watched.");
            await Assert.That(read.Envelope!.Accepts).IsEquivalentTo(accepts);
            await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written)
                .Because("parse(render(x)) == x is the property, and the second render is what "
                       + "catches a value that survived the parse in a form that renders back "
                       + "differently.");
        }
    }

    [Test]
    public async Task A_document_that_never_said_accepts_does_not_grow_the_line()
    {
        // THE POISON TWIN of the round trip above, and the reason the emitter
        // cannot simply always write the key. Every envelope written before
        // this field says nothing about subjects; emitting `accepts: []` for
        // them would rewrite every tenant's document on the next show, and a
        // diff nobody made is how a review practice gets abandoned.
        var written = EnvelopeText.Render(Kind(accepts: null, "src/**"));

        await Assert.That(written).DoesNotContain("accepts")
            .Because("absent stays absent, which is the same rule environment: and "
                   + "preserve-unadmitted are already held to.");
        await Assert.That(EnvelopeYaml.Parse(written).Envelope!.Accepts).IsNull();
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
