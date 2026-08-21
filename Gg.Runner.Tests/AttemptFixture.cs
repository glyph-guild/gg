using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight on its second attempt: a base, a commit attempt one pushed, and a commit
/// attempt two added on top.
/// </summary>
/// <remarks>
/// <para>
/// <b>The materializer decides the basis, not this fixture.</b> What is handed in is what
/// the control plane knows - the branch and where the last attempt left off - and what
/// comes out is whatever the runner made of it. A fixture that set the base and the label
/// itself would agree with the code by construction, which is how a constant digest once
/// collapsed a property in both directions.
/// </para>
/// <para>
/// The changes are real changes: different files with different contents, so "attempt two
/// reported only its own work" is a statement the diff can actually falsify.
/// </para>
/// </remarks>
internal sealed class AttemptFixture : IDisposable
{
    private const string Branch = "gg/flight-1";

    private readonly string _root;
    private readonly string _bare;
    private readonly string _work;
    private readonly string _trees;

    /// <summary>The commit attempt one pushed, which is what destination.pushed recorded.</summary>
    internal string FirstAttemptCommit { get; private set; } = "";

    /// <summary>The commit the flight was pinned to before anybody attempted anything.</summary>
    internal string BaseCommit { get; }

    internal AttemptFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "gg-attempt-fixture", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_root);

        _bare = Path.Combine(_root, "widgets.git");
        _work = Path.Combine(_root, "work");
        _trees = Path.Combine(_root, "trees");

        GitFixture.Run(_root, "init", "--bare", "--initial-branch=main", _bare);
        GitFixture.Run(_root, "clone", _bare, _work);

        Write("README.md", "widgets\n");
        GitFixture.Run(_work, "add", ".");
        GitFixture.Run(_work, "commit", "-m", "base");
        GitFixture.Run(_work, "push", "origin", "main");
        BaseCommit = GitFixture.Run(_work, "rev-parse", "HEAD").Trim();

        GitFixture.Run(_work, "checkout", "-b", Branch);
    }

    /// <summary>
    /// The manifest a flight with no prior attempt produces.
    /// </summary>
    internal ChangeManifest? FirstAttemptManifest((string Path, string Content) change)
    {
        Commit("first attempt", change);
        GitFixture.Run(_work, "push", "origin", Branch);

        return Extract(continuesFrom: null);
    }

    /// <summary>
    /// The manifest attempt two produces, having been told where attempt one left off.
    /// </summary>
    internal ChangeManifest? SecondAttemptManifest(
        (string Path, string Content) firstAttempt,
        (string Path, string Content) secondAttempt)
    {
        Commit("first attempt", firstAttempt);
        GitFixture.Run(_work, "push", "origin", Branch);
        FirstAttemptCommit = GitFixture.Run(_work, "rev-parse", "HEAD").Trim();

        // ATTEMPT TWO IS AN EDIT TO THE MATERIALIZED TREE, not a commit that
        // pre-exists it. This fixture used to commit both attempts to the branch
        // and pin the branch, so the clone happened to contain the prior work -
        // which is exactly the arrangement that hid the checkout defect: in
        // production the pinned ref is the base, and the only way attempt one's
        // work reaches attempt two's tree is the continuation being CHECKED OUT.
        // Staging it this way is what makes these tests run the same path a real
        // second attempt runs.
        var tree = Materialize(continuesFrom: FirstAttemptCommit);

        var edited = Path.Combine(
            tree.Path, secondAttempt.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(edited)!);
        File.WriteAllText(edited, secondAttempt.Content);

        return ChangeExtractor.Extract(tree, ClassificationRules.Default);
    }

    private ChangeManifest? Extract(string? continuesFrom) =>
        ChangeExtractor.Extract(Materialize(continuesFrom), ClassificationRules.Default);

    private Materialized Materialize(string? continuesFrom)
    {
        var adapter = new LocalVcsAdapter(_root);
        var materializer = new Materializer(adapter, new WorkingTreeRoot(_trees));

        return materializer.MaterializeAsync(
            "flight-1",
            new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = _bare,
                PinnedRef = $"refs/heads/{Branch}",
                ContinuesFrom = continuesFrom,
            },
            secret: null).GetAwaiter().GetResult();
    }

    private void Commit(string message, (string Path, string Content) change)
    {
        Write(change.Path, change.Content);
        GitFixture.Run(_work, "add", "--all");
        GitFixture.Run(_work, "commit", "-m", message);
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_work, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
