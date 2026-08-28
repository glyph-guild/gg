namespace Gg.Contracts.Tests;

/// <summary>
/// Narrowing what a work kind takes, or what it yields, is a widening of the
/// estate — and one half of that was never computed at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this fixes was found by slice seventeen's step 0, in shipped
/// code.</b> <c>Accepts</c> carries a <c>[Composes]</c> operator and therefore
/// passes the drift guard in <c>EnvelopeDirection</c>'s static constructor —
/// but <c>Widening</c> never compared it. A work kind that narrowed
/// <c>accepts:</c> between versions computed <i>tighter-or-equal</i> and walked
/// straight through the gate a floor exists to hold.
/// </para>
/// <para>
/// <b>Both fields remove gates by being reduced, which is why they run
/// together.</b> Dropping a subject kind from <c>accepts:</c> makes every rule
/// reading that subject's facts structurally inapplicable. Dropping a family
/// from <c>produces:</c> does it directly. Adding <c>produces:</c> without this
/// would have shipped a second field whose removal silently deletes gates —
/// this slice's central danger, through the door it forgot to lock.
/// </para>
/// <para>
/// <b>Reduction is the widening, and the sets are unordered otherwise.</b>
/// <c>EnvelopeDirection</c> answers two questions and never mints a third, so a
/// move that cannot be shown to tighten takes the widening path rather than
/// falling through as unchanged.
/// </para>
/// </remarks>
public class AcceptsAndProducesDirectionTests
{
    private static Envelope Kind(IReadOnlyList<string>? accepts, IReadOnlyList<string>? produces) =>
        new()
        {
            Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
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

    private static readonly IReadOnlyList<string> Both =
        [FactKinds.ChangeManifest, FactKinds.SourceProvenance];

    // ---- produces ----

    [Test]
    public async Task Removing_a_fact_family_from_produces_is_a_widening()
    {
        var widening = EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository], Both),
            Kind([SubjectKinds.Repository], [FactKinds.SourceProvenance]));

        await Assert.That(widening).IsNotNull()
            .Because("a family this kind no longer claims to produce makes every rule reading it "
                   + "structurally inapplicable - a gate that stops firing, for every flight of "
                   + "this kind, for ever.");
        await Assert.That(widening!.Field).Contains("produces");
        await Assert.That(widening.Because).Contains(FactKinds.ChangeManifest)
            .Because("naming the family is what tells a reviewer which gates they are being "
                   + "asked to give up.");
    }

    [Test]
    public async Task Adding_a_fact_family_to_produces_is_not_a_widening()
    {
        // The liveness half on this field. A rule that starts applying is a
        // gate that starts firing, which is safe and possibly noisy.
        await Assert.That(EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository], [FactKinds.SourceProvenance]),
            Kind([SubjectKinds.Repository], Both))).IsNull();

        await Assert.That(EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository], Both),
            Kind([SubjectKinds.Repository], Both))).IsNull()
            .Because("identical documents are not a widening, and a comparator that said they "
                   + "were would send every unchanged apply to a gate.");
    }

    // ---- accepts, which was never compared ----

    [Test]
    public async Task Removing_a_subject_kind_from_accepts_is_a_widening()
    {
        // THE DEFECT STEP 0 FOUND. This assertion fails against the code as it
        // shipped: Accepts is in the operator table and was never in the
        // comparison, so this move computed tighter-or-equal and took no gate.
        var widening = EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository, SubjectKinds.Envelope], []),
            Kind([SubjectKinds.Repository], []));

        await Assert.That(widening).IsNotNull()
            .Because("a subject kind this work no longer takes is every fact about that subject "
                   + "becoming unproducible, which removes every rule that reads one.");
        await Assert.That(widening!.Field).Contains("accepts");
        await Assert.That(widening.Because).Contains(SubjectKinds.Envelope);
    }

    [Test]
    public async Task Adding_a_subject_kind_to_accepts_is_not_a_widening()
    {
        await Assert.That(EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository], []),
            Kind([SubjectKinds.Repository, SubjectKinds.Envelope], []))).IsNull();
    }

    [Test]
    public async Task Declaring_either_field_where_there_was_none_is_not_a_widening_by_itself()
    {
        // Absent means the document never said - root, a narrowing, or a
        // document from before the field existed. An author filling it in for
        // the first time is not giving anything up, and treating it as a
        // widening would send every migrating work kind to a gate for the
        // privilege of becoming legible.
        await Assert.That(EnvelopeDirection.Widening(
            Kind(accepts: null, produces: null),
            Kind([SubjectKinds.Repository], Both))).IsNull();
    }

    [Test]
    public async Task Withdrawing_either_field_entirely_is_a_widening()
    {
        // The other direction, and it is not symmetric with the one above. A
        // document that stops declaring what it produces has withdrawn every
        // claim in it at once, which is the maximal reduction rather than a
        // return to innocence.
        await Assert.That(EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository], Both),
            Kind([SubjectKinds.Repository], produces: null))).IsNotNull();

        await Assert.That(EnvelopeDirection.Widening(
            Kind([SubjectKinds.Repository], Both),
            Kind(accepts: null, produces: Both))).IsNotNull();
    }
}
