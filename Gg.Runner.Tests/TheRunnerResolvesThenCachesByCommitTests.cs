using Gg.Runner.Sweeps;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner resolves the watch's ref itself, caches by what it resolved, and
/// reports it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 16 amended, decided 2026-09-17 by the owner</b> — <i>"the runners
/// should be doing the work not the control plane"</i>. A sweep is handed the
/// watch's ref rather than a commit, so the machine that fetches is the one
/// that decides which commit that is.
/// </para>
/// <para>
/// <b>AND THE CACHE IS WHY THE ORDER MATTERS.</b> The reader caches on the
/// owner's own reasoning — <i>"we should cache though, commits won't change"</i>
/// — and that premise is false for a ref. Keyed by the ref, a watch whose skill
/// was updated would run the first words this machine ever read, forever, with
/// nothing on any surface saying so. So the ref is resolved FIRST and the cache
/// is keyed by the commit, which keeps both of the owner's instructions: one
/// remote lookup per sweep, and a cache that is still sound because commits
/// still do not change.
/// </para>
/// <para>
/// <b>Staleness is the assertion that earns this file.</b> Reading at a moving
/// ref twice, across a push, has to answer the new words the second time —
/// everything else here could pass with the cache keyed on anything at all.
/// </para>
/// </remarks>
public class TheRunnerResolvesThenCachesByCommitTests
{
    private const string SkillPath = TheRunnerReadsTheSkillAtItsPinTests.SkillPath;

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "gg-skill-cache", Guid.NewGuid().ToString("n"));

    private static SkillReader Reader(
        TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository, string cache) =>
        new([new LocalVcsAdapter(repository.Directory)], cache,
            secretFor: _ => Task.FromResult<string?>(null));

    private static RepoTarget At(
        TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository, string reference) => new()
    {
        Provider = LocalVcsAdapter.ProviderKey,
        Slug = repository.BarePath,
        PinnedRef = reference,
    };

    [Test]
    public async Task A_ref_is_resolved_and_the_commit_is_reported()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var read = await Reader(repository, Scratch())
            .ReadAsync(At(repository, "refs/heads/main"), SkillPath);

        await Assert.That(read).IsTypeOf<SkillRead.Read>();

        var got = (SkillRead.Read)read;

        await Assert.That(got.Commit).IsEqualTo(repository.Pushed)
            .Because("the control plane no longer resolves this, so the commit the attestation "
                   + "reports can only come from the machine that did.");
        await Assert.That(got.Skill.Content).IsEqualTo("words nobody reviewed\n")
            .Because("the branch head is what a ref means, and the amendment accepted that the "
                   + "words that run are whatever it points at now.");
    }

    [Test]
    public async Task A_commit_still_reads_at_that_commit_and_reports_itself()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var read = await Reader(repository, Scratch())
            .ReadAsync(At(repository, repository.Reviewed), SkillPath);

        var got = (SkillRead.Read)read;

        await Assert.That(got.Commit).IsEqualTo(repository.Reviewed);
        await Assert.That(got.Skill.Content).IsEqualTo("the reviewed words\n")
            .Because("a caller holding a commit may still hand one over, and resolving it "
                   + "answers itself.");
    }

    [Test]
    public async Task A_moving_ref_does_not_serve_the_words_it_first_read()
    {
        // THE ASSERTION THIS FILE EXISTS FOR. Keyed by the ref, the second read
        // returns the first read's words and nothing says so - which is worse
        // than the pin this amendment gave up, because it is silent.
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var cache = Scratch();
        var reader = Reader(repository, cache);
        var main = At(repository, "refs/heads/main");

        var first = (SkillRead.Read)await reader.ReadAsync(main, SkillPath);

        await Assert.That(first.Commit).IsEqualTo(repository.Pushed)
            .Because("ASK WHY IT PASSES: if the first read were already stale the assertion "
                   + "below would hold for the wrong reason.");

        var moved = repository.PushAnother("the newest words\n");

        var second = (SkillRead.Read)await reader.ReadAsync(main, SkillPath);

        await Assert.That(second.Commit).IsEqualTo(moved);
        await Assert.That(second.Skill.Content).IsEqualTo("the newest words\n")
            .Because("a watch whose skill was updated running the first words this machine "
                   + "ever read is the staleness the whole resolve-then-cache order exists to "
                   + "prevent.");
    }

    [Test]
    public async Task One_commit_is_read_once_however_it_was_asked_for()
    {
        // THE CACHE STILL EARNS ITS KEEP, which is the other half. Asking by ref
        // and then by the commit it resolves to is one read, because the key is
        // what was resolved rather than what was asked.
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var cache = Scratch();
        var reader = Reader(repository, cache);

        _ = await reader.ReadAsync(At(repository, "refs/heads/main"), SkillPath);

        var entries = Directory.EnumerateFiles(cache, "*.skill").Count();

        _ = await reader.ReadAsync(At(repository, repository.Pushed), SkillPath);

        await Assert.That(Directory.EnumerateFiles(cache, "*.skill").Count()).IsEqualTo(entries)
            .Because("the ref and the commit it points at are one version of one file, and two "
                   + "cache entries for it would mean the key is the question rather than the "
                   + "answer.");
    }

    [Test]
    public async Task A_ref_that_resolves_to_nothing_is_unreadable_and_says_which_ref()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();

        var read = await Reader(repository, Scratch())
            .ReadAsync(At(repository, "refs/heads/nope"), SkillPath);

        await Assert.That(read).IsTypeOf<SkillRead.Unreadable>();
        await Assert.That(((SkillRead.Unreadable)read).Diagnosis).Contains("nope")
            .Because("the sweep attests this sentence, and a person reading it has to know "
                   + "which ref did not resolve.");
    }
}
