using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The one paragraph without which none of this is used.
/// </summary>
/// <remarks>
/// <para>
/// <b>Step 0 measured what the prompt says today, by consequence.</b> Four real
/// runs against a ticket recording two teams asking for opposite things, with
/// nothing in the tree to choose between them: all four PICKED ONE, justified
/// it, and wrote a patch. Not one asked. The prompt tells an agent to work the
/// subject and make the changes it asks for, and there is not a word about what
/// to do when it cannot - so the agent does the only thing it has been told to
/// do.
/// </para>
/// <para>
/// <b>"Asking is not failing" is in there deliberately.</b> The agent is
/// otherwise being told to complete a task by a system it cannot see, and the
/// failure mode this whole slice exists to fix is an agent that produced
/// something rather than say it was stuck.
/// </para>
/// <para>
/// <b>Asserted on the string, because wording only a process launch can observe
/// is wording nothing pins.</b> That is the same argument <c>PromptFor</c> was
/// made public under, and this is the line whose absence makes the feature
/// unused rather than broken - which is the failure no other test can see.
/// </para>
/// </remarks>
public class PromptTests
{
    private static string Prompt(IReadOnlyList<string>? moves = null) =>
        ClaudeCodeExecutor.PromptFor(new ExecutorRequest
        {
            WorkingDirectory = "/work/flight",
            LoopId = "implement",
            Moves = moves ?? [LoopMoves.Read, LoopMoves.Edit],
            IntentUri = "https://example.test/items/812",
            WallClock = TimeSpan.FromMinutes(30),
            TranscriptPath = "/work/flight/transcript.ndjson",
        });

    [Test]
    public async Task It_says_asking_is_not_failing_in_those_words()
    {
        await Assert.That(Prompt()).Contains("Asking is not failing")
            .Because("the agent is being told to complete a task by a system it cannot see. "
                   + "Without this sentence the honest thing looks like the failing thing, "
                   + "and step 0 measured what it does instead: it decides.");
    }

    [Test]
    public async Task It_names_the_tool_it_is_telling_the_agent_about()
    {
        await Assert.That(Prompt()).Contains(HelpTool.Qualified)
            .Because("a paragraph describing a channel without naming it leaves the agent "
                   + "to guess which of the tools it can see is the one meant.");
    }

    [Test]
    public async Task It_says_to_stop_rather_than_do_something_else()
    {
        var prompt = Prompt();

        await Assert.That(prompt.Contains("guess", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("the failure this replaces is an agent that guessed, and naming it is "
                   + "what makes the instruction about that rather than about tidiness.");
        await Assert.That(prompt.Contains("different piece of work", StringComparison.Ordinal))
            .IsTrue()
            .Because("substituting other work is the second-best-looking thing a stuck agent "
                   + "can do, and it produces a flight that changed something nobody asked "
                   + "for - which reads as progress.");
    }

    [Test]
    public async Task Every_flight_is_told_whatever_its_moves_declare()
    {
        // Rule 5 in the prompt as well as in the grant. A read-only loop can be
        // as stuck as a writing one, and telling only some agents about the
        // channel would make the tier depend on the envelope - which is the
        // thing this tool is deliberately outside.
        await Assert.That(Prompt([LoopMoves.Read])).Contains("Asking is not failing");
        await Assert.That(Prompt([LoopMoves.Read, LoopMoves.Write]))
            .Contains("Asking is not failing");
    }

    [Test]
    public async Task The_work_it_was_given_still_comes_first()
    {
        // The liveness twin, and it is about ORDER rather than presence: a
        // prompt that led with what to do when it cannot would be a prompt
        // about failing. The instruction is the last paragraph of the work.
        var prompt = Prompt();

        await Assert.That(prompt.IndexOf("Asking is not failing", StringComparison.Ordinal))
            .IsGreaterThan(prompt.IndexOf("Work ", StringComparison.Ordinal))
            .Because("an agent reads the task first. This is what to do when the task cannot "
                   + "be done, and putting it first would change what the prompt is about.");
    }
}

/// <summary>
/// Where standing instructions land in the prompt.
/// </summary>
/// <remarks>
/// <para>
/// <b>S30.2-02 to -05. Order is the decision, so it is asserted on the composed
/// string.</b> Instructions come after the work and before any record of a
/// prior attempt: an agent should know what it is doing and under what standing
/// policy before it reads what somebody else tried and how that went.
/// </para>
/// <para>
/// <b>And the compatibility claim is asserted, not assumed.</b> An envelope
/// declaring none renders a prompt byte for byte identical to the one it
/// rendered before this field existed — which is the only thing that makes this
/// safe to ship ahead of the walk that measures whether it helps.
/// </para>
/// </remarks>
public class InstructionsInThePromptTests
{
    private static ExecutorRequest Request(string? instructions = null, string? resumesFrom = null) =>
        new()
        {
            WorkingDirectory = "/tmp/gg-tree",
            LoopId = "implement",
            IntentProvider = "a-tracker",
            IntentId = "26",
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClock = TimeSpan.FromMinutes(30),
            TranscriptPath = "/tmp/gg-transcript.ndjson",
            Instructions = instructions,
            ResumesFrom = resumesFrom,
        };

    [Test]
    public async Task An_envelope_with_none_renders_the_prompt_it_always_did()
    {
        // THE COMPATIBILITY CLAIM, byte for byte. Every flight running today
        // has no instructions, and none of them may see a changed prompt
        // because of a field they do not use.
        await Assert.That(ClaudeCodeExecutor.PromptFor(Request()))
            .IsEqualTo(ClaudeCodeExecutor.PromptFor(Request(instructions: null)));

        await Assert.That(ClaudeCodeExecutor.PromptFor(Request()))
            .DoesNotContain("standing instructions");
    }

    [Test]
    public async Task They_appear_after_the_work_and_before_a_prior_attempt()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(Request(
            instructions: "\n\nThe operator's standing instructions: reproduce it in a test.",
            resumesFrom: "the last attempt got as far as the parser"));

        var work = prompt.IndexOf("work item 26", StringComparison.Ordinal);
        var standing = prompt.IndexOf("standing instructions", StringComparison.Ordinal);
        var prior = prompt.IndexOf("the last attempt got as far as the parser", StringComparison.Ordinal);

        await Assert.That(work).IsGreaterThanOrEqualTo(0);
        await Assert.That(standing).IsGreaterThan(work);
        await Assert.That(prior).IsGreaterThan(standing)
            .Because("an agent should know what it is doing and under what policy before it "
                   + "reads what somebody else tried.");
    }

    [Test]
    public async Task The_rendered_block_is_inserted_verbatim()
    {
        // NO SECOND RENDERING. The contract rendered it; this inserts it. A
        // runner that reformatted would be the second wording the whole shape
        // of LeaseLoop.Instructions exists to prevent.
        const string Rendered = "\n\nThe operator's standing instructions: prefer small commits.";

        await Assert.That(ClaudeCodeExecutor.PromptFor(Request(instructions: Rendered)))
            .Contains(Rendered);
    }

    [Test]
    public async Task The_prompt_still_says_what_to_do_when_it_cannot()
    {
        // THE PARAGRAPH WITHOUT WHICH NONE OF THIS IS USED, and a new section
        // landing in the middle of the prompt is exactly how one gets lost.
        await Assert.That(ClaudeCodeExecutor.PromptFor(Request(instructions: "\n\nsomething")))
            .Contains("Asking is not failing");
    }
}
