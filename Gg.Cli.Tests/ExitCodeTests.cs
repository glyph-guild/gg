using Gg.Cli;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Cli.Tests;

/// <summary>
/// Three outcomes reach three exit codes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first thing a customer does with <c>gg decide --json</c> is script
/// it.</b> A script reads the exit code, so the distinction between <i>your
/// decision was refused</i> and <i>we do not know yet</i> has to survive the
/// process boundary or it does not exist. One non-zero for both would make a slow
/// control plane indistinguishable from a rejection - and the retry that follows
/// is answered "nothing is waiting on a decision" for work that succeeded.
/// </para>
/// <para>
/// <b>A timeout is never a refusal.</b> That is the assertion this file exists
/// for; everything else here is the frame that makes it mean something.
/// </para>
/// </remarks>
public class ExitCodeTests
{
    private static VerbResult.Decided Observed(string state, string? outcome = null) =>
        new(new DecisionReport
        {
            Observation = new Observation
            {
                State = state,
                Because = "because",
                Outcome = outcome,
                WaitedSeconds = 0,
                BoundSeconds = 30,
                Polls = 1,
            },
        });

    [Test]
    public async Task A_decision_that_was_observed_exits_zero()
    {
        await Assert.That(ExitCodes.For(Observed(ObservationStates.Decided, "satisfied")))
            .IsEqualTo(0);
    }

    [Test]
    public async Task A_rejection_that_was_recorded_still_exits_zero()
    {
        // A REJECTION IS A SUCCESS OF THE VERB. `gg decide GG-42 obl rejected`
        // doing what it was asked is not a failure, and exiting non-zero for it
        // would make every scripted rejection look like a broken command.
        await Assert.That(ExitCodes.For(Observed(ObservationStates.Decided, "violated")))
            .IsEqualTo(0);
    }

    [Test]
    public async Task A_refused_submission_exits_as_a_refusal()
    {
        await Assert.That(ExitCodes.For(Observed(ObservationStates.Refused)))
            .IsEqualTo(ExitCodes.Refused);
    }

    [Test]
    public async Task Not_yet_visible_has_a_code_of_its_own_and_it_is_not_the_refusal_one()
    {
        // THE WHOLE POINT. If these two ever collapse, a timeout becomes a
        // recorded rejection to everything downstream.
        await Assert.That(ExitCodes.For(Observed(ObservationStates.NotYetVisible)))
            .IsEqualTo(ExitCodes.NotYetVisible);
        var distinct = new[]
        {
            ExitCodes.NotYetVisible, ExitCodes.Refused, ExitCodes.Ok, ExitCodes.Unavailable,
        };

        await Assert.That(distinct.Distinct().Count()).IsEqualTo(4)
            .Because("'the control plane could not be reached' and 'it was reached and has not "
                   + "answered yet' are different facts, and only the second one means the work "
                   + "may already have landed.");
    }

    [Test]
    public async Task Every_state_the_vocabulary_names_has_a_code()
    {
        // A state with no mapping would fall through to a default, and a default
        // that returned zero would report success for something nobody read.
        var codes = ObservationStates.All
            .Select(s => ExitCodes.For(Observed(s)))
            .ToList();

        await Assert.That(codes.Distinct().Count()).IsEqualTo(ObservationStates.All.Count)
            .Because("three states, three codes - collapsing any pair loses a distinction "
                   + "somebody's script is reading.");
    }

    [Test]
    public async Task An_unknown_state_fails_closed()
    {
        await Assert.That(ExitCodes.For(Observed("something-this-version-does-not-know")))
            .IsNotEqualTo(ExitCodes.Ok)
            .Because("unreachable today because the vocabulary is closed, and reporting success "
                   + "for a state nobody has read is the failure this codebase keeps finding.");
    }

    [Test]
    public async Task A_verb_that_does_not_observe_is_unaffected()
    {
        await Assert.That(ExitCodes.For(new VerbResult.Gates(new GateList { Gates = [] })))
            .IsEqualTo(0);
    }
}
