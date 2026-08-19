namespace Gg.Contracts.Tests;

/// <summary>
/// Whether a flight that stopped may leave its work on the remote.
/// </summary>
/// <remarks>
/// <para>
/// <b>The contested half of slice seven, and the reason it needs a knob.</b> A
/// flight that halted, violated or exhausted has no branch anywhere -
/// <c>HandoffRoot</c>'s own comment names it: <i>"a landed flight has a branch and
/// a proposal… a violated or exhausted one has neither, and the work exists only
/// here, which is precisely the flight somebody wants to take over."</i> Inverting
/// that makes handoff portable and is not free: unadmitted work now exists on the
/// customer's remote, and a violated flight's code reaching the forge is a
/// governance question rather than a plumbing one.
/// </para>
/// <para>
/// <b>So absence must mean no.</b> The failure mode is a tenant discovering that
/// every abandoned agent attempt is a branch on their default remote. Article XI,
/// and the same reason an unknown predicate halts rather than evaluating false.
/// </para>
/// <para>
/// <b>A member rather than a value, which is why this costs no vocabulary.</b>
/// Contracts says a member may be added freely and a value may not - a value in a
/// closed enumeration makes every prior reader halt. <c>LoopBudget.Attempts</c>
/// carries the same note.
/// </para>
/// </remarks>
public class PreserveUnadmittedTests
{
    private static Envelope Governing(Destination destination) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "scope-respected",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["scope-respected"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [destination],
    };

    private static Destination ARepository(bool? preserve) => new()
    {
        Id = "forge",
        Kind = DestinationKinds.PullRequest,
        Requires = ["scope-respected"],
        PreserveUnadmitted = preserve,
    };

    [Test]
    public async Task Absence_and_false_both_mean_no()
    {
        // NOT nullable-for-the-sake-of-it. An envelope written before this member
        // existed has to keep meaning exactly what it meant, and what it meant is
        // that nothing unadmitted is pushed. So null and false are one answer, and
        // the type says which by having no third state that could be read as yes.
        await Assert.That(ARepository(null).PreserveUnadmitted is true).IsFalse();
        await Assert.That(ARepository(false).PreserveUnadmitted is true).IsFalse();
        await Assert.That(ARepository(true).PreserveUnadmitted is true).IsTrue();
    }

    [Test]
    public async Task A_destination_that_is_not_a_repository_may_not_declare_it()
    {
        // An envelope-change destination has no branch and no repository, so
        // "preserve the work there" names nothing. Refused with a diagnosis rather
        // than ignored: a knob that silently does nothing on one kind of
        // destination is a knob somebody will set and believe.
        var envelope = Governing(new Destination
        {
            Id = "policy",
            Kind = DestinationKinds.EnvelopeChange,
            Requires = ["scope-respected"],
            PreserveUnadmitted = true,
        });

        var diagnosis = Envelope.Validate(envelope);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("preserve-unadmitted");
        await Assert.That(diagnosis).Contains(DestinationKinds.EnvelopeChange);
    }

    [Test]
    public async Task The_same_envelope_without_it_is_accepted()
    {
        // The poison twin. Without it the refusal above could be about anything
        // else in the fixture.
        var envelope = Governing(new Destination
        {
            Id = "policy",
            Kind = DestinationKinds.EnvelopeChange,
            Requires = ["scope-respected"],
        });

        await Assert.That(Envelope.Validate(envelope)).IsNull();
    }

    [Test]
    public async Task A_preserved_branch_is_not_where_an_admitted_one_goes()
    {
        // TWO NAMES, because they are two facts. A flight preserved for handoff and
        // the same flight later admitted must not fight over one ref - the second
        // push would either be refused as an existing branch or would overwrite the
        // thing somebody was about to take over.
        await Assert.That(DestinationBranch.ForHandoff("GG-42"))
            .IsNotEqualTo(DestinationBranch.For("GG-42"));

        await Assert.That(DestinationBranch.IsOurs(DestinationBranch.ForHandoff("GG-42"))).IsTrue()
            .Because("it is still a branch this platform created, so whatever cleans those up has "
                   + "to see it.");

        // AND IT IS RECOGNISABLE AS ONE, so a runner can report which kind of push
        // it made without inferring a governance answer from a string it matched
        // itself. The control plane chooses the branch; the runner says what it did.
        await Assert.That(DestinationBranch.IsHandoff(DestinationBranch.ForHandoff("GG-42")))
            .IsTrue();
        await Assert.That(DestinationBranch.IsHandoff(DestinationBranch.For("GG-42"))).IsFalse()
            .Because("an ordinary landing branch is not a preservation, and a check that said yes "
                   + "to both would mark every push as kept rather than offered.");
    }

    [Test]
    public async Task A_push_says_whether_a_proposal_follows_it()
    {
        // A `gg/` branch with no pull request is not a proposal, and the fact that
        // names it must say so. Otherwise a reader counting branches cannot tell
        // work that was admitted from work that was merely kept.
        var preserved = new DestinationPushed
        {
            Slug = "acme/web",
            Branch = DestinationBranch.ForHandoff("GG-42"),
            Commit = new string('a', 40),
            Preserved = true,
        };

        await Assert.That(DestinationPushed.Validate(preserved)).IsNull();
        await Assert.That(preserved.Preserved).IsTrue();

        // And absent means what it always meant: a push on the ordinary path.
        var ordinary = preserved with { Preserved = null, Branch = DestinationBranch.For("GG-42") };

        await Assert.That(ordinary.Preserved is true).IsFalse();
        await Assert.That(DestinationPushed.Validate(ordinary)).IsNull();
    }
}
