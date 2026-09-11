using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// What a loop spent, from the executor's own result record.
/// </summary>
/// <remarks>
/// <para>
/// <b>This was claimed in prose for a whole slice and was never true.</b> The
/// control plane's <c>LoopBudgetTests</c> says <i>"the executor reports
/// attempts and tokens — measured, not guessed"</i>, and asserts only attempts.
/// Seven declared capabilities were deleted at slice twenty for the honest
/// reason that nothing consumed them, and <c>ReportsTokens</c> was one of them.
/// The counts were in the stream the whole time; three fields were read off the
/// result record and <c>usage</c> was stepped over.
/// </para>
/// <para>
/// <b>Absent, never zero.</b> An attended session hands a person the terminal
/// and has no stream to count, and it reaches this same fact — so the members
/// are nullable and a run that could not measure reports nothing. Zero is
/// <c>AttendedGaps.Turns</c>'s helpful lie in a second place: it reads as
/// <i>this cost nothing</i>, which is the one wrong answer a person acting on a
/// budget would not question.
/// </para>
/// <para>
/// <b>Measured, not asserted by the agent</b> — the rule already on this fact.
/// These come from the executor's own accounting, not from anything the model
/// wrote.
/// </para>
/// </remarks>
public class TheExecutorReportsWhatItSpentTests
{
    [Test]
    public async Task The_result_records_usage_is_what_the_run_spent()
    {
        var spent = TranscriptTokens.Spent(Transcript(
            "{\"type\":\"assistant\",\"message\":{\"content\":\"working\"}}",
            "{\"type\":\"result\",\"is_error\":false,\"num_turns\":7,\"result\":\"done\","
            + "\"usage\":{\"input_tokens\":11,\"output_tokens\":22,"
            + "\"cache_read_input_tokens\":333,\"cache_creation_input_tokens\":4444}}"));

        await Assert.That(spent).IsNotNull();
        await Assert.That(spent!.Input).IsEqualTo(11L);
        await Assert.That(spent.Output).IsEqualTo(22L);
        await Assert.That(spent.CacheRead).IsEqualTo(333L);
        await Assert.That(spent.CacheWrite).IsEqualTo(4444L);
    }

    [Test]
    public async Task A_result_record_with_no_usage_spent_nothing_it_can_report()
    {
        var spent = TranscriptTokens.Spent(Transcript(
            "{\"type\":\"result\",\"is_error\":false,\"num_turns\":1,\"result\":\"done\"}"));

        await Assert.That(spent).IsNull()
            .Because("four zeroes read as a loop that cost nothing, and a person "
                   + "deciding against a budget would not question that.");
    }

    [Test]
    public async Task A_transcript_with_no_result_record_reports_nothing()
    {
        await Assert.That(TranscriptTokens.Spent(Transcript(
            "{\"type\":\"assistant\",\"message\":{\"content\":\"working\"}}",
            "not json at all"))).IsNull();
    }

    [Test]
    public async Task The_fact_carries_what_the_run_spent()
    {
        var fact = Ran(new SpentTokens
        {
            Input = 11, Output = 22, CacheRead = 333, CacheWrite = 4444,
        }).ToFact("claude-code");

        await Assert.That(fact.InputTokens).IsEqualTo(11L);
        await Assert.That(fact.OutputTokens).IsEqualTo(22L);
        await Assert.That(fact.CacheReadTokens).IsEqualTo(333L);
        await Assert.That(fact.CacheWriteTokens).IsEqualTo(4444L);
    }

    [Test]
    public async Task A_run_that_could_not_count_says_so_by_carrying_nothing()
    {
        var fact = Ran(spent: null).ToFact("attended");

        await Assert.That(fact.InputTokens).IsNull();
        await Assert.That(fact.OutputTokens).IsNull();
        await Assert.That(fact.CacheReadTokens).IsNull();
        await Assert.That(fact.CacheWriteTokens).IsNull()
            .Because("an attended session has no stream to count and reaches this same "
                   + "fact. LoopAttended is what says the session was attended, so a "
                   + "reader can tell 'nobody could measure' from 'nobody did'.");
    }

    private static ExecutorRun Ran(SpentTokens? spent) => new()
    {
        LoopId = "a-loop",
        Outcome = LoopOutcomes.Completed,
        Reason = "done",
        Attempts = 7,
        DurationMs = 1000,
        MovesUsed = ["read"],
        Spent = spent,
    };

    private static string Transcript(params string[] lines) => string.Join('\n', lines);
}
