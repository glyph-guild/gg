using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// <b>S62.3-02</b> - a file over 64 KiB ends the flight before the agent runs, with a sentence
/// naming its size and the bound; nothing is truncated.
/// </summary>
/// <remarks>
/// <b>Refused rather than cut</b>, on the envelope's precedent: an agent reading half a policy
/// cannot tell that it read half. A plan cut at its middle is a plan with legs nobody can see.
/// </remarks>
public class AnOversizedIntentIsRefusedWholeTests
{
    [Test]
    public async Task A_file_over_the_bound_is_refused_naming_both_numbers()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var big = repository.PushAnother(new string('x', IntentFiles.MaxIntentBytes + 1));

        var outcome = await IntentFiles.ReadAsync(
            IntentFileFixture.Reader(repository),
            IntentFileFixture.Lease(repository, big),
            IntentFileFixture.NoSecrets);

        var refused = await Assert.That(outcome).IsTypeOf<IntentFileOutcome.Refused>();
        await Assert.That(refused!.Diagnosis).Contains((IntentFiles.MaxIntentBytes + 1).ToString("N0"));
        await Assert.That(refused.Diagnosis).Contains(IntentFiles.MaxIntentBytes.ToString("N0"));
    }

    [Test]
    public async Task A_file_at_the_bound_is_read()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var exact = repository.PushAnother(new string('x', IntentFiles.MaxIntentBytes));

        await Assert.That(await IntentFiles.ReadAsync(
                IntentFileFixture.Reader(repository),
                IntentFileFixture.Lease(repository, exact),
                IntentFileFixture.NoSecrets))
            .IsTypeOf<IntentFileOutcome.Read>();
    }

    [Test]
    public async Task The_loop_refuses_before_the_agent_rather_than_after()
    {
        // HELD BY SHAPE, because the loop's refusal path is what every pre-agent check uses:
        // a refusal on the invocation ends the flight with its sentence, and nothing below it
        // runs. What this holds is that the intent read is one of those checks.
        var loop = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "Gg.Runner", "RunnerLoop.cs"));

        await Assert.That(loop).Contains("IntentFileOutcome.Refused");
        await Assert.That(loop.IndexOf("IntentFiles.ReadAsync(", StringComparison.Ordinal))
            .IsGreaterThan(-1);
    }

    internal static string RepoRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return at!.FullName;
    }
}
