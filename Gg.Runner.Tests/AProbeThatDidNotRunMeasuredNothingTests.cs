using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A probe whose agent never ran has measured nothing, and said the bound
/// held.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured inside a live pool member.</b> The probe reported <i>"held in
/// 0.5s"</i>. Against the real binary it takes fifteen to twenty-one seconds —
/// <c>What_the_probe_costs_against_the_real_binary</c> is the test that
/// establishes that number — so half a second is not a fast agent, it is no
/// agent. The member's executor had no credential, exited immediately, and the
/// probe read the empty tree as proof that withholding <c>Edit</c> and
/// <c>Write</c> withholds them on that machine.
/// </para>
/// <para>
/// <b>It is the one wrong answer this probe must never give.</b> Everything it
/// is for is in <c>MoveBoundProbeTests</c>'s own sentence: <i>"without it the
/// probe would pass on a machine where nothing is enforced and on a machine
/// where nothing ran."</i> The first half was built. The second half was
/// asserted only for an executor that THREW — <c>UNKNOWN IS NOT FALSE</c>, with
/// its own arm — and an executor that returns a failed run walks straight past
/// it.
/// </para>
/// <para>
/// <b>The run says so, and the probe discarded it.</b>
/// <c>IExecutorPort.ExecuteAsync</c> answers with an <c>ExecutorRun?</c>
/// carrying an outcome, and this called it for its side effects only. A
/// <c>Failed</c> run is an agent that did not do the work; a null one is an
/// executor that produced no account of anything. Neither is evidence about a
/// bound.
/// </para>
/// <para>
/// <b>Fail closed, deliberately.</b> An unmeasured bound makes the runner exit
/// 69 rather than fly — the startup refusal's existing behaviour — because a
/// governed flight on a machine whose governance was never demonstrated is
/// exactly what this probe exists to prevent. A probe that guessed "held" to
/// avoid grounding a runner would be a lock nobody tested, installed before a
/// door.
/// </para>
/// </remarks>
public class AProbeThatDidNotRunMeasuredNothingTests
{
    /// <summary>An executor that answers from the test, without a network.</summary>
    private sealed class StubExecutor(Func<ExecutorRequest, ExecutorRun?> respond) : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(respond(request));
    }

    private static ExecutorRun Ran(string outcome, string reason = "done") => new()
    {
        LoopId = "gg-move-bound-probe",
        Outcome = outcome,
        Reason = reason,
        Attempts = 1,
        DurationMs = 500,
        MovesUsed = [],
    };

    [Test]
    public async Task An_agent_that_failed_to_run_did_not_prove_a_bound()
    {
        // THE MEMBER, exactly. The executor's binary was there and started and
        // could not authenticate, so it failed in half a second and touched
        // nothing.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => Ran(
                LoopOutcomes.Failed, "Invalid API key · Please run /login")),
            CancellationToken.None);

        await Assert.That(result.Bound).IsFalse()
            .Because("an empty tree is what a refused agent and an absent agent both leave, "
                   + "and reporting the second as the first is a lock nobody tested.");
    }

    [Test]
    public async Task And_it_says_so_rather_than_claiming_a_move_held()
    {
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => Ran(
                LoopOutcomes.Failed, "Invalid API key · Please run /login")),
            CancellationToken.None);

        await Assert.That(result.Diagnosis).Contains("could not be measured")
            .Because("the throwing arm already says this sentence, and a reader should not "
                   + "have to know which of two ways the agent failed to work out which "
                   + "message they got.");
        await Assert.That(result.Diagnosis).Contains("Invalid API key")
            .Because("the agent's own words are the only thing that tells somebody WHY, and "
                   + "on a pool member they are the only thing anybody can read.");
    }

    [Test]
    public async Task Nothing_is_recorded_as_having_held()
    {
        // A held list is a claim about a move that was attempted and refused.
        // Attempting nothing produces no such claim, and a list saying Edit and
        // Write held would be read by a person as evidence.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => Ran(LoopOutcomes.Failed)),
            CancellationToken.None);

        await Assert.That(result.Held).IsEmpty();
        await Assert.That(result.Broke).IsEmpty()
            .Because("nothing broke either. Both lists empty and Bound false is what "
                   + "'not measured' looks like, and it already is for the throwing arm.");
    }

    [Test]
    public async Task An_executor_that_answered_with_no_run_at_all_is_the_same()
    {
        // ExecuteAsync returns ExecutorRun?, and null is a real answer the probe
        // discarded along with every other one.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => null), CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Diagnosis).Contains("could not be measured");
    }

    [Test]
    public async Task An_agent_that_ran_and_was_refused_still_passes()
    {
        // THE ANCHOR, and the reason the rule is the outcome rather than the
        // clock or the moves it reached for. A correctly bounded agent runs,
        // tries, is refused, and leaves the tree alone - and that is a pass. A
        // rule written as "it must have taken fifteen seconds" would ground
        // every runner on a fast machine.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => Ran(LoopOutcomes.Completed, "I cannot edit files")),
            CancellationToken.None);

        await Assert.That(result.Bound).IsTrue();
        await Assert.That(result.Held).Contains(ClaudeCodeExecutor.ToolFor(LoopMoves.Write));
    }

    [Test]
    public async Task An_agent_that_blocked_or_exhausted_itself_still_measured_something()
    {
        // Blocked is an agent that asked for a decision; exhausted is one that
        // ran out of attempts. Both RAN, both were refused the write, and both
        // are ordinary answers rather than absent ones - so neither may be
        // treated as an unmeasured bound, or a slow machine grounds itself.
        foreach (var outcome in new[] { LoopOutcomes.Blocked, LoopOutcomes.Exhausted })
        {
            var result = await MoveBoundProbe.RunAsync(
                new StubExecutor(_ => Ran(outcome)), CancellationToken.None);

            await Assert.That(result.Bound).IsTrue()
                .Because($"'{outcome}' is an agent that worked and did not write.");
        }
    }

    [Test]
    public async Task A_failed_run_that_wrote_anyway_is_still_a_broken_bound()
    {
        // THE PRECEDENCE QUESTION, and it has one right answer. If bytes landed
        // on disk then the bound did not hold, whatever the agent then reported
        // about itself - the file is evidence and the outcome is an account.
        // Reading them the other way round would let an agent write and then
        // exit non-zero to have the write forgiven.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(request =>
            {
                File.WriteAllText(
                    Path.Combine(request.WorkingDirectory, MoveBoundProbe.Canary), "anyway");
                return Ran(LoopOutcomes.Failed, "and then I fell over");
            }),
            CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Broke).Contains(ClaudeCodeExecutor.ToolFor(LoopMoves.Write))
            .Because("a write that happened is not undone by the agent failing afterwards.");
        await Assert.That(result.Diagnosis).Contains(MoveBoundProbe.Canary);
    }

    [Test]
    public async Task The_unmeasured_probe_cleans_up_after_itself_too()
    {
        // The scratch directory is the probe's, on a customer's machine, at
        // every start. The failing paths are the ones that would not have been
        // noticed.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => Ran(LoopOutcomes.Failed)),
            CancellationToken.None);

        await Assert.That(Directory.Exists(result.Workspace)).IsFalse();
    }
}
