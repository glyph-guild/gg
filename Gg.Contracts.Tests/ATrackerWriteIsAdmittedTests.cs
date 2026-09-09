using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What the control plane hands back when a tracker destination is admitted,
/// and what its absence means.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.5-01, the contract half.</b> Absent means no. A facts response with no
/// tracker admission writes nothing, and the runner never derives one from a
/// verdict it can see — the rule <see cref="LandingDecision.Admission"/> and
/// <see cref="LandingDecision.Push"/> already hold for the repository path, where
/// their own remark says "two permissions, two fields, each refused by its own
/// absence".
/// </para>
/// <para>
/// <b>A sibling rather than a widening, and that is the whole design decision.</b>
/// <see cref="DestinationAdmission"/> requires a branch, a base ref and a slug.
/// A tracker has none of the three, so carrying a tracker on it would mean
/// making three required members optional — which would silently weaken the
/// repository path, where their being required is what stops a runner pushing
/// somewhere nobody named.
/// </para>
/// <para>
/// <b>Each proposal, not the batch.</b> A triage ships one fact per change so a
/// person can take the re-field and refuse the link. An admission that said
/// only <i>yes</i> would hand the runner a decision nobody made, so what comes
/// back is the set of proposals admitted — named by the idempotency key their
/// facts already carry, because inventing a second identity for something that
/// has one is how two identifiers drift.
/// </para>
/// </remarks>
public class ATrackerWriteIsAdmittedTests
{
    [Test]
    public async Task Absent_means_nothing_is_written()
    {
        var settled = new LandingDecision { Settled = true };

        await Assert.That(settled.Tracker).IsNull()
            .Because("a response that says nothing about a tracker is a response that "
                   + "admits nothing to one, for every reason at once: no destination "
                   + "declared, obligations unmet, or a control plane too old to answer.");
    }

    [Test]
    public async Task It_names_each_proposal_rather_than_saying_yes()
    {
        var admitted = new TrackerAdmission
        {
            DestinationId = "backlog",
            Reason = "two of the three were admitted; the link was not",
            Proposals = ["01a0776a-cacb-76dc", "01a0776a-cacb-76dd"],
        };

        await Assert.That(TrackerAdmission.Validate(admitted)).IsNull();
        await Assert.That(admitted.Proposals.Count).IsEqualTo(2)
            .Because("an admission that said only yes would hand the runner a decision "
                   + "nobody made - the third proposal was refused, and the runner has to "
                   + "be able to tell which.");
    }

    [Test]
    public async Task An_admission_that_names_no_proposal_is_refused()
    {
        // NOT AN EMPTY LIST MEANING NOTHING. A destination that admitted none of
        // them is an ABSENT tracker admission, which the first test pins; an
        // admission object naming nothing is a control plane that decided and
        // then failed to say what, and a runner cannot tell that from "all of
        // them" without guessing.
        var empty = new TrackerAdmission
        {
            DestinationId = "backlog",
            Reason = "admitted",
            Proposals = [],
        };

        await Assert.That(TrackerAdmission.Validate(empty)).IsNotNull()
            .Because("absence is how nothing is said; an empty list is a sentence with the "
                   + "subject missing.");
    }

    [Test]
    public async Task It_is_registered_the_way_every_wire_type_is()
    {
        // The four-way rule, which this inherits rather than restates: a type on
        // the wire carries a pinned id and appears in the vocabulary, and
        // ContractSurfaceTests fingerprints its shape. Named here only so a
        // reader of this file knows the registration is not optional.
        await Assert.That(Vocabulary.Types).Contains(typeof(TrackerAdmission));
    }
}

/// <summary>
/// What a tracker destination permits, and why it is not <c>opens</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.5-02, and the slice named the wrong mechanism.</b> It said the
/// destination's <c>Opens</c> conditions would be evaluated against the
/// proposal's named members. <see cref="Destination.Opens"/> is a menu of WORK
/// KINDS on a flight destination and is refused outright on every other kind,
/// for a reason stated where it is validated: a list that bounds nothing is
/// worse than no list, because somebody wrote it believing they had bounded
/// something. So a tracker gets its own menu on the same terms.
/// </para>
/// <para>
/// <b>And there is no condition language, which settles the other half by
/// construction.</b> That criterion also asks that a condition over the opaque
/// member be refused at composition rather than at flight time. A menu of
/// operations cannot express one at all — the opaque member is not something a
/// destination can mention — so it is inexpressible rather than refused, which
/// is the stronger of the two and needs no check to stay true.
/// </para>
/// </remarks>
public class ATrackerDestinationSaysWhatItPermitsTests
{
    private static Envelope With(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [SubjectKinds.Tracker],
        Produces = [FactKinds.WorkItemProposal],
        Obligations =
        [
            new Obligation
            {
                Id = "loop-ran",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.LoopNotExhausted,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "triage",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["loop-ran"],
                Moves = [LoopMoves.Read, LoopMoves.ProposeWorkItem],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    private static Destination Tracker(IReadOnlyList<string>? mayPerform) => new()
    {
        Id = "backlog",
        Kind = DestinationKinds.WorkItemTracker,
        Requires = ["loop-ran"],
        MayPerform = mayPerform,
    };

    [Test]
    public async Task A_tracker_destination_says_which_operations_it_permits()
    {
        await Assert.That(Envelope.Validate(With(Tracker(
            [WorkItemOperations.Field, WorkItemOperations.Score])))).IsNull();
    }

    [Test]
    public async Task One_that_permits_nothing_can_never_act_and_is_refused()
    {
        // `Opens`' rule, one kind over: absent and empty are ONE answer here,
        // because a tracker destination that may perform nothing is one whose
        // admission can never do anything - ADR-0019 section 3's unreachable
        // destination, refused at authoring rather than discovered in flight.
        foreach (var (what, menu) in ((string, IReadOnlyList<string>?)[])
            [("absent", null), ("empty", [])])
        {
            await Assert.That(Envelope.Validate(With(Tracker(menu)))).IsNotNull()
                .Because($"a tracker destination with {what} may-perform is one whose "
                       + "admission can never act, and somebody wrote it expecting it to.");
        }
    }

    [Test]
    public async Task An_operation_nobody_declared_is_refused_at_authoring()
    {
        var refused = Envelope.Validate(With(Tracker(["delete"])));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("delete", StringComparison.Ordinal);

        foreach (var operation in WorkItemOperations.All)
        {
            await Assert.That(refused).Contains(operation, StringComparison.Ordinal)
                .Because("the diagnosis names the menu, or whoever hit it goes to read the "
                       + "contract to find out what they were allowed to write.");
        }
    }

    [Test]
    public async Task It_bounds_nothing_on_any_other_kind_so_it_is_refused_there()
    {
        // The PreserveUnadmitted precedent, which Opens and MaySelect both
        // follow: a knob that silently does nothing reads as configuration.
        var onAPullRequest = With(new Destination
        {
            Id = "forge",
            Kind = DestinationKinds.PullRequest,
            Requires = ["loop-ran"],
            MayPerform = [WorkItemOperations.Score],
        });

        await Assert.That(Envelope.Validate(onAPullRequest)).IsNotNull()
            .Because("only a tracker performs operations, so on any other kind this list "
                   + "bounds nothing and somebody believes they granted a permission.");
    }
}
