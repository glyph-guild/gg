using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A triage flight declares the repository its skill lives in, as well as the
/// tracker it is about.
/// </summary>
/// <remarks>
/// <para>
/// <b>S36.1-01, and it corrects S35.5-05 rather than extending it.</b> That
/// criterion says "the skill lives in the repository the flight is about" and
/// slice thirty-five's step 0 proved a skill in <c>.claude/skills/…/SKILL.md</c>
/// reaches the agent under <c>--setting-sources project</c>. Both are true and
/// they do not meet: a triage flight declares <c>accepts: [tracker]</c>,
/// <see cref="SubjectKinds.HasTree"/> is false for it, its scope is
/// <c>none</c>, and the runner hands it an empty directory. There is no
/// <c>.claude/</c> to load.
/// </para>
/// <para>
/// <b>The fix needs no new mechanism, which is why this file is assertions and
/// not code.</b> A flight may accept more than one subject kind, and
/// <c>HasTree</c> is asked of the SET - so naming a repository beside the
/// tracker makes the tree real, the scope a glob, and the skill reachable. The
/// tracker is still read over its api, because a subject is what a flight is
/// about and not how it reads it.
/// </para>
/// </remarks>
public class ATriageFlightReadsItsSkillFromARepositoryTests
{
    private static Envelope Triaging(IReadOnlyList<string> accepts, string scope) => new()
    {
        Context = new ContextBinding { Scope = scope, Constitution = "1.0.0" },
        Accepts = accepts,
        Produces = [FactKinds.WorkItemProposal],
        Obligations =
        [
            new Obligation
            {
                Id = "looked",
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
                Discharges = ["looked"],
                Moves = [LoopMoves.Read, LoopMoves.ProposeWorkItem],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "the-backlog",
                Kind = DestinationKinds.WorkItemTracker,
                Requires = ["looked"],
                MayPerform = [WorkItemOperations.Field, WorkItemOperations.Score],
                // OWED SINCE THE FIELD MENU EXISTS: a destination that may set
                // fields and names none can never act.
                MayWrite = ["Custom.RiceScore"],
            },
        ],
    };

    [Test]
    public async Task A_tracker_alone_has_no_tree_for_a_skill_to_be_in()
    {
        // THE GAP, ASSERTED RATHER THAN DESCRIBED. This is the shape slice
        // thirty-five wrote, and it is the shape that cannot carry a skill: no
        // tree means no working directory with a `.claude/` in it, and
        // `--setting-sources project` reads a project that is not there.
        await Assert.That(SubjectKinds.HasTree(SubjectKinds.Tracker)).IsFalse();

        await Assert.That(Envelope.Validate(
            Triaging([SubjectKinds.Tracker], EnvelopeScopes.None))).IsNull()
            .Because("it is a legal envelope - which is the problem. Nothing refuses it, and "
                   + "the flight it opens has nowhere for its instructions to live.");
    }

    [Test]
    public async Task Naming_the_repository_beside_it_makes_the_tree_real()
    {
        // HasTree IS ASKED OF THE SET, so one subject with a tree is enough.
        // That is what makes this a declaration rather than a feature.
        await Assert.That(
            new[] { SubjectKinds.Tracker, SubjectKinds.Repository }.Any(SubjectKinds.HasTree))
            .IsTrue();

        await Assert.That(Envelope.Validate(
            Triaging([SubjectKinds.Tracker, SubjectKinds.Repository], "**"))).IsNull()
            .Because("a flight about a backlog, working in a repository whose `.claude/` holds "
                   + "the rubric it scores by. Both subjects are real and both are declared.");
    }

    [Test]
    public async Task And_the_scope_must_then_be_a_glob_rather_than_none()
    {
        // THE RULE THAT MAKES IT HONEST. `none` means there is no tree to
        // bound; declaring a repository and then saying `none` would be a
        // flight with a checkout nothing bounds, which is the widening the
        // scope exists to stop.
        var refused = Envelope.Validate(
            Triaging([SubjectKinds.Tracker, SubjectKinds.Repository], EnvelopeScopes.None));

        await Assert.That(refused).IsNotNull()
            .Because("a subject with a tree and a scope of none is an unbounded checkout.");
    }
}
