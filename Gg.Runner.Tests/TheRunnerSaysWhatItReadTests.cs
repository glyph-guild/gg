using Gg.Contracts;
using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// <b>S62.3-03</b> - the runner reports <c>intent.read</c> with the commit it resolved the ref
/// to, and a read that fails reports why instead.
/// </summary>
/// <remarks>
/// <b>The commit is the whole point.</b> The control plane reads no repository bytes, so it
/// cannot know which commit a branch named when this runner read it - and a plan's legs read
/// that commit rather than the branch. A fact naming the ref would pin nothing.
/// </remarks>
public class TheRunnerSaysWhatItReadTests
{
    [Test]
    public async Task A_branch_is_reported_as_the_commit_it_named()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var outcome = await IntentFiles.ReadAsync(
            IntentFileFixture.Reader(repository),
            IntentFileFixture.Lease(repository, "main"),
            IntentFileFixture.NoSecrets);

        var fact = (await Assert.That(outcome).IsTypeOf<IntentFileOutcome.Read>())!.Fact;

        await Assert.That(fact.RequestedRef).IsEqualTo("main");
        await Assert.That(fact.Commit).IsEqualTo(repository.Pushed)
            .Because("'main' named the pushed commit when this ran, and the fact records which.");
        await Assert.That(fact.FileSha).IsEqualTo(
            repository.BlobAt(repository.Pushed, TheRunnerReadsTheSkillAtItsPinTests.SkillPath));
        await Assert.That(IntentRead.Validate(fact)).IsNull();
    }

    [Test]
    public async Task A_missing_file_is_refused_naming_it_and_reports_no_read()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var outcome = await IntentFiles.ReadAsync(
            IntentFileFixture.Reader(repository),
            IntentFileFixture.Lease(repository, "main", path: "docs/no-such-plan.md"),
            IntentFileFixture.NoSecrets);

        var refused = await Assert.That(outcome).IsTypeOf<IntentFileOutcome.Refused>();
        await Assert.That(refused!.Diagnosis).Contains("docs/no-such-plan.md");
        await Assert.That(refused.Diagnosis).Contains("this flight's intent");
    }

    [Test]
    public async Task A_flight_that_is_not_about_a_file_reads_nothing()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        await Assert.That(await IntentFiles.ReadAsync(
                IntentFileFixture.Reader(repository),
                IntentFileFixture.Lease(repository, "main") with
                {
                    IntentRepository = null, IntentRepositoryProvider = null, IntentPath = null,
                },
                IntentFileFixture.NoSecrets))
            .IsTypeOf<IntentFileOutcome.None>();
    }

    [Test]
    public async Task The_loop_ships_what_it_read()
    {
        var loop = await File.ReadAllTextAsync(Path.Combine(
            AnOversizedIntentIsRefusedWholeTests.RepoRoot(), "Gg.Runner", "RunnerLoop.cs"));

        await Assert.That(loop).Contains("FactPayload.IntentRead(")
            .Because("a read nobody reports is a commit nobody can pin a leg to.");
    }
}
