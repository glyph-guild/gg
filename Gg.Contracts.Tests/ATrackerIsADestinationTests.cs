using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A tracker is somewhere work goes, and a flight can be about one.
/// </summary>
/// <remarks>
/// <para>
/// <b>S35.4-01, and the half of it the slice did not write down.</b> The
/// criterion names a destination kind. A triage flight also needs a SUBJECT
/// kind, because <c>Envelope.Accepts</c> has to range over something and a
/// flight that reads a backlog is about neither a repository nor an envelope.
/// That is exactly the argument <see cref="SubjectKinds.Envelope"/> was added
/// under - a work kind that ships with no honest declaration available to it,
/// where <c>[]</c> says it is about nothing and <c>[repository]</c> says it is
/// about a tree.
/// </para>
/// <para>
/// <b>And it has no tree, which is a classification rather than a refusal.</b>
/// The schema computes the path bound from whether the subject has one:
/// <c>[repository]</c> gets a glob and everything else gets <c>none</c>. A
/// tracker kind that forgot to say would be a flight whose file scope nothing
/// could compute.
/// </para>
/// </remarks>
public class ATrackerIsADestinationTests
{
    [Test]
    public async Task A_tracker_is_somewhere_work_can_be_admitted_to()
    {
        await Assert.That(DestinationKinds.All).Contains(DestinationKinds.WorkItemTracker)
            .Because("a destination nothing declares is one no envelope can name, and the "
                   + "admission this slice is built on has nowhere to be asked for.");
    }

    [Test]
    public async Task A_flight_can_be_about_a_tracker_and_a_tracker_has_no_tree()
    {
        await Assert.That(SubjectKinds.All).Contains(SubjectKinds.Tracker);
        await Assert.That(SubjectKinds.IsKnown(SubjectKinds.Tracker)).IsTrue();

        await Assert.That(SubjectKinds.HasTree(SubjectKinds.Tracker)).IsFalse()
            .Because("a backlog is not a working tree, and a subject that claimed one would "
                   + "make the schema ask for a path glob over something with no paths.");
    }

    [Test]
    public async Task An_envelope_about_a_tracker_validates()
    {
        // THE POINT OF THE SUBJECT KIND, and the thing a constant alone would
        // not prove: a work kind declaring it has to survive the envelope's own
        // validation, which is where the tree classification is read.
        var triage = new Envelope
        {
            // `none`, AND THE VALIDATOR TAUGHT THIS TEST THAT. It was written
            // with a glob, because a work kind with a scope is what every other
            // one looks like - and a tracker has no paths for a glob to select,
            // so the bound has to say so rather than select nothing quietly.
            // Which is HasTree being load-bearing rather than decorative.
            Context = new ContextBinding
            {
                Scope = EnvelopeScopes.None,
                Constitution = "1.0.0",
            },
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
            Destinations =
            [
                new Destination
                {
                    Id = "backlog",
                    Kind = DestinationKinds.WorkItemTracker,
                    Requires = ["loop-ran"],
                    // DECLARED SINCE THE MENU EXISTS, and this test is how it
                    // was noticed: a tracker destination that permits no
                    // operation is one whose admission can never act, so the
                    // envelope this row is about stopped validating the day
                    // may-perform arrived. Which is the rule working on the
                    // first document that had to obey it.
                    MayPerform = [WorkItemOperations.Field, WorkItemOperations.Score],
                },
            ],
        };

        await Assert.That(Envelope.Validate(triage)).IsNull()
            .Because("a triage work kind is the whole reason both constants exist, and one "
                   + "that cannot be written down is a constant with no use.");
    }
}
