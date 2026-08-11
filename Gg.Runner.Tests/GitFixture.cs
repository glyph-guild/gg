using System.Diagnostics;
using System.Text;

namespace Gg.Runner.Tests;

/// <summary>
/// A real bare repository on disk, with a branch and a pull-request head.
/// </summary>
/// <remarks>
/// <para>
/// Real git, because every interesting thing about materialize is something
/// git decides: what <c>refs/pull/&lt;n&gt;/head</c> resolves to, what a shallow
/// fetch of a pinned ref actually puts on disk, and what is left behind when
/// nobody cleans up. A double would be asserting against a re-implementation of
/// git's answers.
/// </para>
/// <para>
/// <b>The pull-request head is a real commit that is not on any branch</b>,
/// which is exactly the shape GitHub publishes for a fork's PR: the base
/// repository can serve the head without holding a branch for it, and without
/// anybody holding a credential for the fork.
/// </para>
/// <para>
/// Hermetic: global and system git config are pointed at nothing, so a
/// developer's own <c>init.defaultBranch</c>, signing key or hooks cannot change
/// what these tests see.
/// </para>
/// </remarks>
internal sealed class GitFixture : IDisposable
{
    /// <summary>What the fork's head commit contains, so an absence scan has something to find.</summary>
    internal const string ForkMarker = "MARKER-FORK-HEAD-CONTENT";

    /// <summary>What the branch head contains.</summary>
    internal const string BranchMarker = "MARKER-BRANCH-HEAD-CONTENT";

    /// <summary>The pull request number the fixture publishes a head for.</summary>
    internal const int PullNumber = 7;

    /// <summary>Who the fork belongs to, as the local adapter's stand-in for PR metadata.</summary>
    internal const string ForkSlug = "someone-else/widgets";

    internal string Directory { get; }

    /// <summary>The bare repository, as a path an adapter can turn into a file:// url.</summary>
    internal string BarePath { get; }

    internal string BranchCommit { get; }

    internal string ForkHeadCommit { get; }

    internal GitFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "gg-git-fixture", Guid.NewGuid().ToString("n"));
        System.IO.Directory.CreateDirectory(Directory);

        BarePath = Path.Combine(Directory, "base.git");
        var work = Path.Combine(Directory, "work");

        Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
        Run(Directory, "clone", BarePath, work);

        File.WriteAllText(Path.Combine(work, "README.md"), BranchMarker + "\n");
        File.WriteAllText(Path.Combine(work, "package-lock.json"), """{"lockfileVersion":3}""" + "\n");
        Run(work, "add", ".");
        Run(work, "commit", "-m", "base");
        Run(work, "push", "origin", "main");
        BranchCommit = Run(work, "rev-parse", "HEAD").Trim();

        // The fork's head: a commit that exists in the base repository and is
        // on no branch of it, published under refs/pull/<n>/head. Fetching it
        // needs nothing from the fork.
        File.WriteAllText(Path.Combine(work, "CHANGED.md"), ForkMarker + "\n");
        Run(work, "add", ".");
        Run(work, "commit", "-m", "from a fork");
        ForkHeadCommit = Run(work, "rev-parse", "HEAD").Trim();
        Run(work, "push", "origin", $"HEAD:refs/pull/{PullNumber}/head");

        // The local adapter's stand-in for what a provider's PR metadata says.
        // GitHub answers this from its API; a bare repository has nowhere else
        // to put it, and inventing a ref for it would be worse.
        Run(BarePath, "config", $"gg.pull.{PullNumber}.origin", ForkSlug);
    }

    /// <summary>Runs git hermetically and returns stdout, throwing on failure.</summary>
    internal static string Run(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // A developer's own configuration must not reach these tests, and a
        // commit here must not need a signing key somebody happens to have.
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        start.Environment["GIT_AUTHOR_NAME"] = "gg tests";
        start.Environment["GIT_AUTHOR_EMAIL"] = "tests@good-grief.invalid";
        start.Environment["GIT_COMMITTER_NAME"] = "gg tests";
        start.Environment["GIT_COMMITTER_EMAIL"] = "tests@good-grief.invalid";

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git did not start.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? stdout
            : throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} exited {process.ExitCode}: {stderr}{stdout}");
    }

    /// <summary>Every byte of every file in a tree, for the content assertions.</summary>
    internal static string AllTextUnder(string directory)
    {
        var text = new StringBuilder();
        foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            text.AppendLine(File.ReadAllText(file));
        }
        return text.ToString();
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
