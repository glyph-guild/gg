using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The executor port: what a loop is asked to do, and what comes back.
/// </summary>
/// <remarks>
/// <para>
/// <b>One adapter is not an abstraction.</b> That has been learned twice here,
/// on VCS and on identity, and the mitigation is not a second adapter - it is
/// declaring capabilities from the first, which is the pattern the provider
/// adapter established. What this executor can report, what it cannot, and
/// what degrades.
/// </para>
/// <para>
/// The declaration is only honest if it was written from what FAILED rather
/// than from what was convenient, so every gap below was measured against the
/// real binary before it was written down.
/// </para>
/// </remarks>
public class ExecutorPortTests
{
    [Test]
    public async Task The_executor_declares_what_it_can_report()
    {
        var declared = ClaudeCodeExecutor.Capabilities;

        await Assert.That(declared.Rung).IsEqualTo(ExecutorRungs.Frontier);
        await Assert.That(declared.ReportsAttempts).IsTrue();
        await Assert.That(declared.ReportsDuration).IsTrue();
        await Assert.That(declared.ReportsMovesUsed).IsTrue();
        await Assert.That(declared.ReportsTokens).IsTrue()
            .Because("the executor reports token usage, which the slice note assumed it would not. "
                   + "Enforcing a token budget is a separate decision; being able to see one is not.");
    }

    [Test]
    public async Task The_executor_declares_what_it_cannot_do()
    {
        // Written from what failed. --allowedTools does not shorten the tool
        // list the session advertises, so the adapter observes moves and does
        // not bound them - which is why the slice records moves rather than
        // enforcing them, and now for a measured reason rather than a chosen
        // one.
        var declared = ClaudeCodeExecutor.Capabilities;

        await Assert.That(declared.DeclaredMoveEnforcement).IsEqualTo(MoveEnforcement.PerTool);
        await Assert.That(declared.AttributesEditsToTools).IsFalse()
            .Because("what the agent touched is read from the tree, not from what it said it did - "
                   + "which is the property that keeps an injected instruction out of a verdict.");
        await Assert.That(declared.Gaps).IsNotEmpty();
    }

    [Test]
    public async Task Every_declared_gap_says_what_it_costs()
    {
        // A gap named without a consequence is a footnote. These are read by
        // somebody deciding whether this executor can do their job.
        foreach (var gap in ClaudeCodeExecutor.Capabilities.Gaps)
        {
            await Assert.That(gap.Name).IsNotEmpty();
            await Assert.That(gap.Consequence.Length).IsGreaterThan(30)
                .Because($"'{gap.Name}' declares a gap and does not say what it costs.");
        }
    }

    // ---- the budget ----

    [Test]
    public async Task A_loop_that_runs_out_of_budget_is_exhausted_rather_than_failed()
    {
        // A REAL STATE, not an error. Calling it failed would put it in the
        // same bucket as a crash, and those need different people. Who the
        // flight waits for next is the envelope's sentence, appended by the
        // runner - ExhaustionReasonTests pins that - so the factory's reason is
        // the measurement and only the measurement.
        var run = ExecutorRun.Exhausted(
            loopId: "implement", after: TimeSpan.FromMinutes(30), movesUsed: ["read"]);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Exhausted);
        await Assert.That(run.Outcome).IsNotEqualTo(LoopOutcomes.Failed);
        await Assert.That(run.Reason.ToLowerInvariant()).Contains("wall-clock budget")
            .Because("the reason states what was measured; the disposition arrives from the "
                   + "envelope, where it is known.");
    }

    [Test]
    public async Task The_outcome_a_loop_reports_is_a_valid_fact()
    {
        // The contract's own rule, so a run this runner produces cannot be one
        // ingress refuses.
        var run = ExecutorRun.Exhausted("implement", TimeSpan.FromSeconds(1), ["read"]);

        await Assert.That(LoopOutcome.Validate(run.ToFact(ExecutorRungs.Frontier))).IsNull();
    }

    // ---- what a person reads ----

    [Test]
    public async Task An_outcome_reason_is_stripped_of_control_sequences()
    {
        // Stripped at INGRESS rather than at render time. It came from a
        // process this machine started, and stdout is what a customer pastes
        // into a ticket.
        var run = ExecutorRun.Completed(
            loopId: "implement",
            reason: "done[31m red [0m",
            attempts: 2,
            took: TimeSpan.FromSeconds(3),
            movesUsed: ["edit"]);

        await Assert.That(run.Reason).DoesNotContain("");
        await Assert.That(run.Reason).DoesNotContain("");
        await Assert.That(run.Reason).Contains("red")
            .Because("stripping is not deleting: what somebody wrote survives, the escape does not.");
    }

    [Test]
    public async Task Moves_used_are_reported_without_duplicates_and_in_a_stable_order()
    {
        // A fact that varied by arrival order would make two identical flights
        // produce two different digests.
        var run = ExecutorRun.Completed(
            "implement", "done", 3, TimeSpan.FromSeconds(1), ["edit", "read", "edit", "read"]);

        await Assert.That(run.MovesUsed).IsEquivalentTo((string[])["edit", "read"]);
    }
}
