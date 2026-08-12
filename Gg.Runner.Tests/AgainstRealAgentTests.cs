using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The adapter against a real agent, on a real tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excluded from CI by name</b>, exactly as the provider tests are. These
/// need the executor binary, a credential and the network, and they refuse
/// loudly rather than passing when unconfigured - so excluding them is the
/// difference between a green build and a build that cannot run at all. What
/// they prove is recorded by hand, and the criteria that depend on them say
/// which.
/// </para>
/// <para>
/// The questions a fake cannot answer: does headless invocation work with no
/// terminal, does the agent actually edit the tree, and does the wall-clock
/// budget stop it. Everything here was written after watching a real run.
/// </para>
/// </remarks>
[Category("RealAgent")]
public class AgainstRealAgentTests
{
    private static string Binary =>
        Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
        ?? throw new InvalidOperationException(
            "GG_EXECUTOR_BINARY is not set. These drive a real agent against a real tree; skipping "
          + "them would leave the whole slice's scoping bet unverified.");

    private static (string Tree, string Transcript) Scratch()
    {
        var root = Path.Combine(Path.GetTempPath(), "gg-agent-" + Guid.NewGuid().ToString("N")[..8]);
        var tree = Path.Combine(root, "tree");

        Directory.CreateDirectory(Path.Combine(tree, "src"));
        File.WriteAllText(Path.Combine(tree, "src", "greet.py"),
            "def greet(name):\n    return \"Hello \" + name\n");

        // OUTSIDE the tree, deliberately. The tree is deleted when a flight
        // ends; a transcript inside it would be a reference to something that
        // has already gone by the time anybody follows it.
        return (tree, Path.Combine(root, "state", "transcript.ndjson"));
    }

    private static ExecutorRequest Request(string tree, string transcript, TimeSpan budget) => new()
    {
        WorkingDirectory = tree,
        LoopId = "implement",
        IntentUri = "https://example.invalid/owner/repo/issues/1",
        Moves = [LoopMoves.Read, LoopMoves.Edit],
        WallClock = budget,
        TranscriptPath = transcript,
    };

    [Test]
    public async Task An_agent_works_the_tree_with_no_terminal_anywhere()
    {
        // THE BET THE SLICE IS SCOPED ON. Redirected pipes, closed stdin, no
        // shell - and the agent reads, edits and reports.
        var (tree, transcript) = Scratch();
        File.WriteAllText(Path.Combine(tree, "ISSUE.md"),
            "# Issue 1\n\nAdd a docstring to the greet function in src/greet.py.\n");

        var run = await new ClaudeCodeExecutor(Binary)
            .ExecuteAsync(Request(tree, transcript, TimeSpan.FromMinutes(5)), CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Completed);
        await Assert.That(run.Attempts).IsGreaterThan(0)
            .Because("attempts are measured from the executor's own result, not guessed.");
        await Assert.That(run.MovesUsed).IsNotEmpty();
        await Assert.That(File.ReadAllText(Path.Combine(tree, "src", "greet.py")))
            .Contains("\"\"\"")
            .Because("the agent EDITS FILES - that is the job - and this is the edit.");
    }

    [Test]
    public async Task The_transcript_is_written_outside_the_tree_and_referenced_by_hash()
    {
        var (tree, transcript) = Scratch();
        File.WriteAllText(Path.Combine(tree, "ISSUE.md"), "# Issue\n\nRead src/greet.py and say what it does.\n");

        var run = await new ClaudeCodeExecutor(Binary)
            .ExecuteAsync(Request(tree, transcript, TimeSpan.FromMinutes(5)), CancellationToken.None);

        await Assert.That(run.Transcript).IsNotNull();
        await Assert.That(File.Exists(transcript)).IsTrue();
        await Assert.That(transcript.StartsWith(tree, StringComparison.Ordinal)).IsFalse()
            .Because("the tree is deleted when the flight ends.");

        var reference = run.Transcript!;
        await Assert.That(reference.Bytes).IsEqualTo(new FileInfo(transcript).Length);
        await Assert.That(reference.Scope).IsEqualTo(ArtifactScopes.RunnerLocal)
            .Because("the locator resolves only here, and saying so is the declared gap.");
        await Assert.That(reference.Sha256.Length).IsEqualTo(64);
    }

    [Test]
    public async Task A_budget_that_runs_out_leaves_the_flight_waiting_for_a_person()
    {
        // A real state rather than an error, proven by actually running out
        // rather than by constructing the result.
        var (tree, transcript) = Scratch();
        File.WriteAllText(Path.Combine(tree, "ISSUE.md"),
            "# Issue\n\nRefactor every file in this repository, carefully, one at a time.\n");

        var run = await new ClaudeCodeExecutor(Binary)
            .ExecuteAsync(Request(tree, transcript, TimeSpan.FromSeconds(3)), CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Exhausted);
        await Assert.That(run.Reason.ToLowerInvariant()).Contains("waiting for a person");
        await Assert.That(run.Transcript).IsNotNull()
            .Because("what it managed to say before the budget ran out is still the record of it.");
    }
}
