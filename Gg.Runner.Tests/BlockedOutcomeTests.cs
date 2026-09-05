using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// An agent that asked for a decision and stopped is <c>blocked</c>, not
/// <c>completed</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The result record cannot tell these apart, and step 0 measured that.</b>
/// Four real runs against a ticket that could not be satisfied ended
/// <c>subtype: success</c>, <c>is_error: false</c>, having changed no file and
/// said so. The executor chose between <c>completed</c> and <c>failed</c> from
/// that one bit, so all four were recorded as <i>the loop finished on its own
/// terms</i>.
/// </para>
/// <para>
/// <b>So the evidence is something the agent DID.</b> Rule 1: asking is
/// declared, never measured and never inferred - the runner learns of a
/// question because a tool was called, which is a <c>tool_use</c> block in a
/// stream this class already walks. Rule 2 is why it is not the prose: a
/// classifier reading repository content is injectable, and a file in a
/// customer's tree could make a flight declare itself blocked or keep a
/// genuinely stuck one quiet.
/// </para>
/// <para>
/// <b>ASKED AND STOPPED, versus ASKED AND THEN FINISHED.</b> Rule 7 keeps those
/// two apart, and the mechanical line between them is what this step had to
/// decide. It is <i>a tree-changing call after the question</i>: an agent that
/// asked and then edited went on to do the work, and one that asked and did
/// nothing else stopped. Reading later than that - counting turns, or looking
/// at what the closing message says - would be the inference rule 2 refuses.
/// </para>
/// <para>
/// <b>And it is not <c>failed</c>.</b> The vocabulary already makes this
/// argument about <c>exhausted</c> - <i>"calling it failed would put it in the
/// same bucket as a crash, and those need different people"</i> - and it is the
/// same argument verbatim, one state over.
/// </para>
/// </remarks>
public class BlockedOutcomeTests
{
    private const string Question = "Two teams asked for opposite rounding rules and the "
                                  + "ticket does not say which wins.";

    // BUILT BY CONCATENATION rather than as a raw interpolated literal: the
    // shape ends in three closing braces and the `$$` form cannot express that
    // without counting them, which is a puzzle rather than a fixture.
    private static string Call(string id, string tool, string input) =>
        "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
      + "\"id\":\"" + id + "\",\"name\":\"" + tool + "\",\"input\":" + input + "}]}}";

    private static string Result(string id, bool error = false) =>
        "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
      + "\"tool_use_id\":\"" + id + "\",\"is_error\":" + (error ? "true" : "false")
      + ",\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}]}}";

    private static string Asked(string id = "q1") =>
        Call(id, HelpTool.Qualified, "{\"question\":\"" + Question + "\"}")
        + "\n" + Result(id);

    private static string Edited(string id = "e1") =>
        Call(id, ClaudeCodeExecutor.ToolFor(LoopMoves.Edit), "{\"file_path\":\"src/a.py\"}")
        + "\n" + Result(id);

    /// <summary>
    /// The digest's question, with the launcher's mapping handed in - which is
    /// how the real caller asks it, because the digest path may not reference
    /// the class that invokes a model.
    /// </summary>
    private static bool Blocked(string transcript) =>
        TranscriptDigest.Blocked(transcript, ClaudeCodeExecutor.PutsBytesOnDisk);

    private static string Read(string id = "r1") =>
        Call(id, ClaudeCodeExecutor.ToolFor(LoopMoves.Read), "{\"file_path\":\"ISSUE.md\"}")
        + "\n" + Result(id);

    [Test]
    public async Task An_agent_that_asked_and_stopped_is_blocked()
    {
        await Assert.That(Blocked(Read() + "\n" + Asked())).IsTrue()
            .Because("it looked at the work, could not decide it, said so, and stopped. "
                   + "Recording that as 'the loop finished on its own terms' is the whole "
                   + "defect this slice exists for.");
    }

    [Test]
    public async Task An_agent_that_asked_and_then_did_the_work_is_not_blocked()
    {
        // RULE 7, and asserted as a property rather than left to whichever arm
        // was written first: asking and finishing are two facts, not one state.
        // One clarifying question turning a finished flight into a chore is how
        // a feature gets switched off.
        await Assert.That(Blocked(Asked() + "\n" + Edited())).IsFalse()
            .Because("it asked, carried on, and changed the tree. The question is still "
                   + "recorded - that is the fact - but the loop finished.");
    }

    [Test]
    public async Task Reading_after_the_question_is_still_stopping()
    {
        // THE LINE IS A TREE-CHANGING CALL, not any call at all. An agent that
        // asks and then re-reads the file it was asking about has not gone on
        // to do the work, and a rule keyed on 'any later tool call' would
        // record it as finished.
        await Assert.That(Blocked(Asked() + "\n" + Read("r2"))).IsTrue()
            .Because("looking again is not deciding.");
    }

    [Test]
    public async Task An_agent_that_never_asked_is_not_blocked()
    {
        // The liveness twin. A Blocked that answered true for everything would
        // satisfy the first assertion and would make every flight a chore.
        await Assert.That(Blocked(Read() + "\n" + Edited())).IsFalse();
        await Assert.That(Blocked("")).IsFalse()
            .Because("no transcript is no question, and an empty stream must not read as a "
                   + "flight waiting for somebody.");
    }

    [Test]
    public async Task A_question_whose_call_failed_is_not_a_question()
    {
        // The nomination extractor's own rule, one tool over: a refused call is
        // not an answer. An agent whose tool call errored did not successfully
        // ask anything, and recording a flight as waiting on a question nobody
        // received would leave it waiting for ever.
        var refused = Call("q9", HelpTool.Qualified, $$"""{"question":"{{Question}}"}""")
                    + "\n" + Result("q9", error: true);

        await Assert.That(Blocked(refused)).IsFalse();
    }

    [Test]
    public async Task Blocked_is_not_reachable_from_is_error()
    {
        // S25.1-03. The two are decided by DIFFERENT EVIDENCE and this holds
        // them apart: a crash is `failed` whatever the transcript contains, and
        // a question is `blocked` whatever the result record says. Step 0
        // measured that the result record reports success for both.
        var crashed = ExecutorRun.Failed("implement", "the process died", 1, TimeSpan.Zero, []);
        var stuck = ExecutorRun.Blocked("implement", "asked and stopped", 1, TimeSpan.Zero, []);

        await Assert.That(crashed.Outcome).IsEqualTo(LoopOutcomes.Failed);
        await Assert.That(stuck.Outcome).IsEqualTo(LoopOutcomes.Blocked);
        await Assert.That(stuck.Outcome).IsNotEqualTo(LoopOutcomes.Failed)
            .Because("a crash and an impasse need different people, which is the argument "
                   + "the vocabulary already makes about exhausted.");
    }

    [Test]
    public async Task The_outcome_stands_alone_on_the_digest()
    {
        // S25.1-05. `LoopDigest.StopReason` is validated against
        // LoopOutcomes.All, so a fourth value that never joined that list makes
        // every blocked digest refuse - and a person reading the digest must
        // not have to find another fact to learn whether the work finished.
        await Assert.That(LoopOutcomes.All).Contains(LoopOutcomes.Blocked);

        var digest = new LoopDigest
        {
            LoopId = "implement",
            StopReason = LoopOutcomes.Blocked,
            FilesEdited = [],
            FilesReadNotEdited = [],
            Searches = [],
            Errors = [],
            RefusedMoves = [],
            Attempts = 1,
        };

        await Assert.That(LoopDigest.Validate(digest)).IsNull()
            .Because("the digest carries the outcome itself, so it stands alone.");
    }
}
