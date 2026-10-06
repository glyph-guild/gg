using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// <b>S62.3-01</b> - a file intent is read before the loop starts, and the agent's prompt carries
/// its text under the repository, path and commit it came from.
/// </summary>
/// <remarks>
/// <b>Under where it came from</b>, because a plan names files and tests by path, and an agent
/// that knows which commit it was given can read the neighbouring files at the same commit and
/// cite exactly the words it worked from.
/// </remarks>
public class TheAgentReadsTheFileItWasGivenTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static ExecutorRequest Request() => new()
    {
        Trees = [],
        LoopId = "implement",
        IntentFile = new RequestedIntentFile("JDX/JDNext", "docs/plans/18291.md", Commit,
            "# The plan\n\nSwap the Assign icon; leave Export alone.\n"),
        Moves = [],
        WallClock = TimeSpan.FromMinutes(10),
        TranscriptPath = Path.Combine(Path.GetTempPath(), "t.ndjson"),
    };

    [Test]
    public async Task The_prompt_carries_the_file_under_where_it_came_from()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(Request());

        await Assert.That(prompt).Contains("docs/plans/18291.md");
        await Assert.That(prompt).Contains("JDX/JDNext");
        await Assert.That(prompt).Contains(Commit);
        await Assert.That(prompt).Contains("Swap the Assign icon; leave Export alone.")
            .Because("the file's words are the work - the agent is given them, not sent to find them.");
    }

    [Test]
    public async Task A_file_names_work()
    {
        await Assert.That(ExecutorRequest.NamesWork(null, null, null, null, hasFile: true)).IsTrue()
            .Because("a flight whose only intent is a file would otherwise be leased, read and "
                   + "never worked - the silence NamesWork exists to end.");
    }

    [Test]
    public async Task The_file_is_read_at_its_ref_before_anything_runs()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var outcome = await IntentFiles.ReadAsync(
            IntentFileFixture.Reader(repository),
            IntentFileFixture.Lease(repository, repository.Reviewed),
            IntentFileFixture.NoSecrets);

        var read = await Assert.That(outcome).IsTypeOf<IntentFileOutcome.Read>();
        await Assert.That(read!.File.Content).IsEqualTo("the reviewed words\n");
        await Assert.That(read.File.Commit).IsEqualTo(repository.Reviewed);
    }
}
