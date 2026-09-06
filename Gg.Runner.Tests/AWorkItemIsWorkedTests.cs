using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight about a work item is invoked, and the agent is told which one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every ticket flight has been claimed and never worked.</b> The invocation
/// gate requires a non-empty intent uri and a ticket has none — it is a provider
/// and an id. So the runner leased the flight, materialized its tree, and
/// returned: no agent, no refusal, no fact, nothing to read. The gate's own
/// comment says a ticket "no longer" needs a cloned tree, which was fixed for
/// trees while the very next condition excluded tickets anyway.
/// </para>
/// <para>
/// <b>And the prompt would have named nothing.</b> <i>"Work the issue at
/// {IntentUri}"</i> against a null renders <i>"Work the issue at ."</i> — a
/// sentence that reads like an instruction and points at nothing, which is worse
/// than declining, because the agent would try.
/// </para>
/// <para>
/// <b>"in this repository" comes off when there is no repository.</b> A flight
/// may work a ticket without one — that is a ruling, not an oversight — and an
/// instruction naming a tree that is empty is how an agent is sent looking for
/// code that was never checked out.
/// </para>
/// </remarks>
public class AWorkItemIsWorkedTests
{
    private static ExecutorRequest ARequest(
        string? uri = null, string? provider = null, string? id = null,
        string workingDirectory = "/tmp/gg-tree") => new()
    {
        WorkingDirectory = workingDirectory,
        LoopId = "implement",
        IntentUri = uri,
        IntentProvider = provider,
        IntentId = id,
        Moves = [LoopMoves.Read],
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/tmp/gg-transcript.ndjson",
    };

    [Test]
    public async Task An_agent_working_a_work_item_is_told_which_work_item()
    {
        // THE DEFECT'S OTHER HALF. Rendering a null here produced "Work the
        // issue at ." - an instruction pointing at nothing, which an agent will
        // try to follow.
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest(provider: "a-tracker", id: "26"));

        await Assert.That(prompt).Contains("a-tracker");
        await Assert.That(prompt).Contains("26");
        await Assert.That(prompt).DoesNotContain("at .")
            .Because("a sentence that names nothing still reads like an instruction.");
    }

    [Test]
    public async Task An_agent_working_a_link_is_told_the_link_exactly_as_before()
    {
        // THE ANCHOR, and it is every flight in the air. The wording a link
        // flight gets is load-bearing and does not move.
        var prompt = ClaudeCodeExecutor.PromptFor(
            ARequest(uri: "https://forge.invalid/acme/widgets/issues/7"));

        await Assert.That(prompt).Contains("https://forge.invalid/acme/widgets/issues/7");
    }

    [Test]
    public async Task A_flight_that_names_external_work_either_way_is_worth_invoking()
    {
        // THE GATE, as a question the runner can ask without a lease. A ticket
        // and a link are both something to resolve; a flight naming neither has
        // nothing for an agent to look up.
        await Assert.That(ExecutorRequest.NamesWork(uri: null, provider: "a-tracker", id: "26", text: null))
            .IsTrue();
        await Assert.That(ExecutorRequest.NamesWork(uri: "https://x.invalid/1", null, null, null))
            .IsTrue();
        await Assert.That(ExecutorRequest.NamesWork(null, null, null, null)).IsFalse();
    }

    [Test]
    public async Task Half_a_work_item_is_not_a_work_item()
    {
        // A provider with no id names a tracker rather than an item in it, and
        // an id with no provider does not say which tracker it is in. Contract
        // 0.86.0 refuses that pair at intake in exactly these words; the runner
        // must not treat it as workable if one ever arrives.
        await Assert.That(ExecutorRequest.NamesWork(null, "a-tracker", null, null)).IsFalse();
        await Assert.That(ExecutorRequest.NamesWork(null, null, "26", null)).IsFalse();
    }
}
