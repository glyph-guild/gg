using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A bare repository standing in for a customer's remote, and a working tree that
/// pushes to it through the runner's own push path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real git on both ends.</b> The remote is a bare repository reached over a
/// <c>file://</c> url, which git treats as a remote in every way that matters here:
/// fast-forward rules, refspec handling, force semantics and ref advertisement are the
/// same code inside git as they are for https. What is NOT the same is authentication and
/// pull requests, and neither is what this proves.
/// </para>
/// <para>
/// The commits are made with raw git and the pushes go through <see cref="GitPush"/>, so
/// what is under test is the runner's push decision and not the fixture's idea of one.
/// </para>
/// </remarks>
internal sealed class PushFixture : IDisposable
{
    private const string Branch = "gg/flight-1";

    private readonly string _root;
    private readonly string _work;
    private readonly string _bare;
    private readonly string _url;

    internal PushFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "gg-push-fixture", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_root);

        _bare = Path.Combine(_root, "remote.git");
        _work = Path.Combine(_root, "work");
        _url = new Uri(_bare).AbsoluteUri;

        GitFixture.Run(_root, "init", "--bare", "--initial-branch=main", _bare);
        GitFixture.Run(_root, "clone", _bare, _work);

        // A base commit on main, so the branch below has somewhere to come from - the
        // shape a flight starts in rather than an empty repository.
        File.WriteAllText(Path.Combine(_work, "README.md"), "widgets\n");
        GitFixture.Run(_work, "add", ".");
        GitFixture.Run(_work, "commit", "-m", "base");
        GitFixture.Run(_work, "push", "origin", "main");
        GitFixture.Run(_work, "checkout", "-b", Branch);
    }

    /// <summary>Commits a change and pushes it the way the runner does. Returns the commit.</summary>
    internal string CommitAndPush(string message, string path, string content)
    {
        Commit(message, path, content);

        var outcome = GitPush
            .PushAsync(_url, _work, Branch, "acme/widgets", secret: null)
            .GetAwaiter().GetResult();

        return outcome is PushOutcome.Pushed(_, var commit)
            ? commit
            : throw new InvalidOperationException($"the fixture's own push did not land: {outcome}");
    }

    /// <summary>Attempt two commits and pushes, and the answer is the test's subject.</summary>
    internal Task<PushOutcome> AttemptTwoPushes(string path, string content)
    {
        Commit("second attempt", path, content);
        return GitPush.PushAsync(_url, _work, Branch, "acme/widgets", secret: null);
    }

    /// <summary>Pushes again with nothing new, which is what a runner that died mid-report does.</summary>
    internal Task<PushOutcome> PushTheSameCommitAgain() =>
        GitPush.PushAsync(_url, _work, Branch, "acme/widgets", secret: null);

    /// <summary>
    /// A developer pushing to the same branch by hand between attempts.
    /// </summary>
    /// <remarks>
    /// On top of what is already there, because that is the realistic case: somebody
    /// looked at the branch, fixed the thing that got it rejected, and pushed. It is a
    /// legitimate fast-forward for them and a non-fast-forward for us.
    /// </remarks>
    internal string SomebodyElsePushes(string path, string content)
    {
        var theirs = Path.Combine(_root, "theirs");
        GitFixture.Run(_root, "clone", "--branch", Branch, _bare, theirs);

        File.WriteAllText(Path.Combine(theirs, path), content);
        GitFixture.Run(theirs, "add", ".");
        GitFixture.Run(theirs, "commit", "-m", "fixed it myself");
        GitFixture.Run(theirs, "push", "origin", Branch);

        return GitFixture.Run(theirs, "rev-parse", "HEAD").Trim();
    }

    /// <summary>Rewrites the branch, deliberately, with raw git and nothing of ours.</summary>
    internal void ForcePushADivergentHistory()
    {
        var elsewhere = Path.Combine(_root, "elsewhere");
        GitFixture.Run(_root, "clone", "--branch", "main", _bare, elsewhere);

        File.WriteAllText(Path.Combine(elsewhere, "unrelated.md"), "a different history\n");
        GitFixture.Run(elsewhere, "add", ".");
        GitFixture.Run(elsewhere, "commit", "-m", "an unrelated commit");
        GitFixture.Run(elsewhere, "push", "--force", "origin", $"HEAD:{Branch}");
    }

    /// <summary>What the remote's branch points at now.</summary>
    internal string RemoteTip() =>
        GitFixture.Run(_bare, "rev-parse", $"refs/heads/{Branch}").Trim();

    /// <summary>
    /// Whether any ref on the remote leads to this commit.
    /// </summary>
    /// <remarks>
    /// Reachability, not existence. A force-pushed commit survives as a dangling object
    /// until garbage collection, so asking whether the object is present would answer yes
    /// about a commit on its way to being deleted - and "my commit is still there" means
    /// a ref leads to it.
    /// </remarks>
    internal bool ReachableFromBranch(string commit) =>
        GitFixture.Run(_bare, "rev-list", $"refs/heads/{Branch}")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Any(line => string.Equals(line.Trim(), commit, StringComparison.Ordinal));

    private void Commit(string message, string path, string content)
    {
        var full = Path.Combine(_work, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        GitFixture.Run(_work, "add", ".");
        GitFixture.Run(_work, "commit", "-m", message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
