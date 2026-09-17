using Gg.Runner.Sweeps;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner reads a watch's skill at the commit the control plane pinned,
/// once per commit.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.3-04, and two of the owner's decisions of 2026-09-16.</b> The runner
/// reads the skill, with the customer's credential, because the control plane
/// reads nothing from a repository but a narrowings directory; and it caches
/// what it read, because <i>"commits won't change"</i>.
/// </para>
/// <para>
/// <b>At the commit, never at the ref.</b> The pin is what makes the words that
/// run the words somebody reviewed, so a skill read at the branch head after
/// somebody pushed would be the review skipped.
/// </para>
/// <para>
/// <b>Objects, not a working copy.</b> A sweep has no tree: the read fetches the
/// commit, finds the file's blob, and prints it - and the blob's id is the
/// digest the attestation reports, the record of what ran.
/// </para>
/// </remarks>
public class TheRunnerReadsTheSkillAtItsPinTests
{
    internal const string SkillPath = ".goodgrief/skills/triage.md";

    internal sealed class SkillRepository : IDisposable
    {
        internal string Directory { get; }

        internal string BarePath { get; }

        internal string Reviewed { get; }

        internal string Pushed { get; }

        internal SkillRepository()
        {
            Directory = Path.Combine(Path.GetTempPath(), "gg-skill-fixture", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);

            BarePath = Path.Combine(Directory, "skills.git");
            var work = Path.Combine(Directory, "work");

            GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
            GitFixture.Run(Directory, "clone", BarePath, work);

            System.IO.Directory.CreateDirectory(Path.Combine(work, ".goodgrief", "skills"));
            File.WriteAllText(Path.Combine(work, SkillPath), "the reviewed words\n");
            File.WriteAllText(Path.Combine(work, "src.cs"), "class NotASkill {}\n");
            GitFixture.Run(work, "add", ".");
            GitFixture.Run(work, "commit", "-m", "reviewed");
            GitFixture.Run(work, "push", "origin", "main");
            Reviewed = GitFixture.Run(work, "rev-parse", "HEAD").Trim();

            // THE BRANCH MOVES after the pin was made, which is the case the pin
            // exists for.
            File.WriteAllText(Path.Combine(work, SkillPath), "words nobody reviewed\n");
            GitFixture.Run(work, "commit", "-am", "pushed later");
            GitFixture.Run(work, "push", "origin", "main");
            Pushed = GitFixture.Run(work, "rev-parse", "HEAD").Trim();
        }

        internal string BlobAt(string commit, string path) =>
            GitFixture.Run(BarePath, "rev-parse", $"{commit}:{path}").Trim();

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "gg-skill-cache", Guid.NewGuid().ToString("n"));

    private static (SkillReader Reader, string Cache) Reader(SkillRepository repository)
    {
        var cache = Scratch();
        return (new SkillReader(
            [new LocalVcsAdapter(repository.Directory)], cache,
            secretFor: _ => Task.FromResult<string?>(null)), cache);
    }

    private static RepoTarget At(SkillRepository repository, string commit) => new()
    {
        Provider = LocalVcsAdapter.ProviderKey,
        Slug = repository.BarePath,
        PinnedRef = commit,
    };

    [Test]
    public async Task The_skill_is_read_at_the_pinned_commit_and_not_at_the_branch()
    {
        using var repository = new SkillRepository();
        var (reader, _) = Reader(repository);

        var read = await reader.ReadAsync(At(repository, repository.Reviewed), SkillPath);

        await Assert.That(read).IsTypeOf<SkillRead.Read>();
        var skill = ((SkillRead.Read)read).Skill;

        await Assert.That(skill.Content).IsEqualTo("the reviewed words\n")
            .Because("the branch moved after the pin, and the words that run are the ones "
                   + "somebody reviewed.");
        await Assert.That(skill.BlobSha).IsEqualTo(repository.BlobAt(repository.Reviewed, SkillPath))
            .Because("the blob's id is the digest the attestation reports - git's own name for "
                   + "exactly these bytes.");
    }

    [Test]
    public async Task A_second_read_at_the_same_commit_asks_no_forge()
    {
        // COMMITS WON'T CHANGE, so the second read is the cache's. Proved by
        // removing the repository between the two: a read that still answers
        // did not fetch.
        using var repository = new SkillRepository();
        var (reader, _) = Reader(repository);

        _ = await reader.ReadAsync(At(repository, repository.Reviewed), SkillPath);

        System.IO.Directory.Delete(repository.BarePath, recursive: true);

        var again = await reader.ReadAsync(At(repository, repository.Reviewed), SkillPath);

        await Assert.That(again).IsTypeOf<SkillRead.Read>();
        await Assert.That(((SkillRead.Read)again).Skill.Content).IsEqualTo("the reviewed words\n");
    }

    [Test]
    public async Task The_cache_is_keyed_by_path_and_commit_and_not_only_by_repository()
    {
        using var repository = new SkillRepository();
        var (reader, _) = Reader(repository);

        _ = await reader.ReadAsync(At(repository, repository.Reviewed), SkillPath);

        System.IO.Directory.Delete(repository.BarePath, recursive: true);

        await Assert.That(await reader.ReadAsync(At(repository, repository.Pushed), SkillPath))
            .IsTypeOf<SkillRead.Unreadable>()
            .Because("another commit is another skill, and a cache that answered it from the "
                   + "first would run words nobody pinned.");
        await Assert.That(await reader.ReadAsync(At(repository, repository.Reviewed), "src.cs"))
            .IsTypeOf<SkillRead.Unreadable>();
    }

    [Test]
    public async Task A_path_that_is_not_there_at_the_commit_is_unreadable_and_says_which()
    {
        using var repository = new SkillRepository();
        var (reader, _) = Reader(repository);

        var read = await reader.ReadAsync(
            At(repository, repository.Reviewed), ".goodgrief/skills/missing.md");

        await Assert.That(read).IsTypeOf<SkillRead.Unreadable>();
        await Assert.That(((SkillRead.Unreadable)read).Diagnosis).Contains("missing.md");
    }

    [Test]
    [Arguments("../outside.md")]
    [Arguments("/etc/passwd")]
    [Arguments(".goodgrief/../../outside.md")]
    [Arguments("")]
    public async Task A_path_that_leaves_the_repository_is_refused_before_anything_is_fetched(
        string path)
    {
        using var repository = new SkillRepository();
        var (reader, cache) = Reader(repository);

        var read = await reader.ReadAsync(At(repository, repository.Reviewed), path);

        await Assert.That(read).IsTypeOf<SkillRead.Unreadable>();
        await Assert.That(System.IO.Directory.Exists(cache)
            && System.IO.Directory.EnumerateFileSystemEntries(cache).Any()).IsFalse()
            .Because("nothing was fetched and nothing was kept.");
    }

    [Test]
    public async Task A_provider_this_runner_does_not_serve_is_unreadable_and_says_which()
    {
        using var repository = new SkillRepository();
        var (reader, _) = Reader(repository);

        var read = await reader.ReadAsync(
            At(repository, repository.Reviewed) with { Provider = "forge.elsewhere" }, SkillPath);

        await Assert.That(read).IsTypeOf<SkillRead.Unreadable>();
        await Assert.That(((SkillRead.Unreadable)read).Diagnosis).Contains("forge.elsewhere");
    }

    [Test]
    public async Task A_pin_that_is_not_a_commit_is_refused()
    {
        using var repository = new SkillRepository();
        var (reader, _) = Reader(repository);

        var read = await reader.ReadAsync(At(repository, "refs/heads/main"), SkillPath);

        await Assert.That(read).IsTypeOf<SkillRead.Unreadable>()
            .Because("a ref moves, and the cache is keyed by something that must not.");
    }

    [Test]
    public async Task Nothing_but_the_cached_words_is_left_behind()
    {
        using var repository = new SkillRepository();
        var (reader, cache) = Reader(repository);

        _ = await reader.ReadAsync(At(repository, repository.Reviewed), SkillPath);

        var kept = System.IO.Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories)
            .Select(f => File.ReadAllText(f))
            .ToList();

        await Assert.That(kept.Any(k => k.Contains("class NotASkill", StringComparison.Ordinal)))
            .IsFalse()
            .Because("the fetch is objects in a scratch directory that is removed; only the "
                   + "skill's own words are kept, and nothing else from the repository is.");
        await Assert.That(System.IO.Directory
            .EnumerateDirectories(cache, ".git", SearchOption.AllDirectories).Any()).IsFalse();
    }
}
