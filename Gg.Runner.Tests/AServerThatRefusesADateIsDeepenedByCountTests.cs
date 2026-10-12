using System.Diagnostics;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A server that refuses <c>--shallow-since</c> is deepened by count instead,
/// in measured steps, until the shallow boundary is older than the window
/// (slice seventy-four, after GG-1085).
/// </summary>
/// <remarks>
/// <para>
/// <b>What the walk found, one release on.</b> 0.88.32 fetched a history
/// loop's tree with <c>--shallow-since</c>, and GG-1085 failed in one second:
/// <i>fatal: Server does not support --shallow-since</i>. one hosted forge has no
/// <c>deepen-since</c> and no <c>deepen-relative</c> either (<c>--deepen</c>
/// hangs up), and a fresh <c>--depth 1500</c> brought 4,976 commits and
/// 196 MB, because depth counts every parent path on a merge-heavy branch.
/// The one thing it does support is re-fetching an existing shallow clone
/// with a larger <c>--depth</c>, which deepens it: measured on JDNext, 64
/// reached April 2026, 256 reached October 2025, 1024 the root.
/// </para>
/// <para>
/// <b>So the date is asked for first and the count is the fallback</b>, in
/// steps that quadruple, stopping when the oldest shallow-boundary commit is
/// older than the window or the repository is no longer shallow. On a server
/// that honours the date nothing changes. On one that does not, the window is
/// a floor rather than a ceiling: the tree holds at least the year, and
/// sometimes more.
/// </para>
/// </remarks>
public class AServerThatRefusesADateIsDeepenedByCountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 2, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task A_counted_depth_is_a_depth_flag()
    {
        var plan = GitInvocation.Fetch(
            "https://example.invalid/acme/widgets.git", "refs/heads/develop", secret: null,
            depth: FetchDepth.Deepened(256));

        await Assert.That(plan.Arguments).Contains("--depth");
        await Assert.That(plan.Arguments).Contains("256")
            .Because("the count, as git takes it. Arguments: " + string.Join(" ", plan.Arguments));
        await Assert.That(plan.Arguments.Any(a => a.StartsWith("--shallow-since", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task The_refusal_is_recognised_by_gits_own_words()
    {
        await Assert.That(ShallowDeepening.RefusesShallowSince(
                "git exited 128: fatal: Server does not support --shallow-since\n"
              + "fatal: the remote end hung up unexpectedly\n"))
            .IsTrue();
        await Assert.That(ShallowDeepening.RefusesShallowSince("git exited 128: fatal: could not read Username"))
            .IsFalse()
            .Because("a credential failure is not a reason to fetch more.");
    }

    [Test]
    public async Task The_steps_are_the_measured_ones()
    {
        await Assert.That(FetchDepth.DeepeningSteps).IsEquivalentTo([64, 256, 1024, 4096])
            .Because("measured on JDNext: 64 reached six months, 256 a year, 1024 the root. "
                   + "Quadrupling reaches a year in two round trips and the root in three.");
    }

    [Test]
    public async Task Deepening_stops_at_the_first_step_whose_boundary_is_older_than_the_window()
    {
        // ONE COMMIT A DAY FOR A HUNDRED DAYS, newest at HEAD. A window of
        // seventy days is beyond the first step (64 commits, so 63 days back)
        // and inside the second (256, which is the whole history and no longer
        // shallow).
        var directory = Path.Combine(Path.GetTempPath(), "gg-deepen-fixture", Guid.NewGuid().ToString("n"));
        var bare = Path.Combine(directory, "base.git");
        var work = Path.Combine(directory, "work");
        var tree = Path.Combine(directory, "tree");
        Directory.CreateDirectory(bare);
        Directory.CreateDirectory(work);

        try
        {
            GitFixture.Run(bare, "init", "--quiet", "--bare", "--initial-branch=main");
            GitFixture.Run(work, "init", "--quiet", "--initial-branch=main");
            for (var daysAgo = 99; daysAgo >= 0; daysAgo--)
            {
                CommitAt(work, Now.AddDays(-daysAgo), $"day -{daysAgo}");
            }

            GitFixture.Run(work, "push", "--quiet", bare, "main:main");

            Directory.CreateDirectory(tree);
            GitFixture.Run(tree, "init", "--quiet");
            var url = new Uri(bare).AbsoluteUri;

            var reached = await ShallowDeepening.DeepenUntilAsync(
                url, "refs/heads/main", tree, secret: null, since: Now.AddDays(-70));

            await Assert.That(reached).IsEqualTo(256)
                .Because("64 commits reach 63 days, short of 70; 256 reaches everything.");

            var log = GitFixture.Run(tree, "log", "--format=%s", "FETCH_HEAD");
            await Assert.That(log).Contains("day -70")
                .Because("the window is a floor: the commit at the edge of it is in the tree.");
            await Assert.That(GitFixture.Run(tree, "rev-list", "--count", "FETCH_HEAD").Trim())
                .IsEqualTo("100")
                .Because("the second step took the whole history, which is more than the "
                       + "window and the honest cost of a count.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task Deepening_stops_at_the_first_step_when_it_already_reaches()
    {
        // TEN COMMITS, A WINDOW OF FIVE DAYS: the first step takes everything
        // and the repository stops being shallow, so one round trip is all.
        var directory = Path.Combine(Path.GetTempPath(), "gg-deepen-fixture", Guid.NewGuid().ToString("n"));
        var bare = Path.Combine(directory, "base.git");
        var work = Path.Combine(directory, "work");
        var tree = Path.Combine(directory, "tree");
        Directory.CreateDirectory(bare);
        Directory.CreateDirectory(work);

        try
        {
            GitFixture.Run(bare, "init", "--quiet", "--bare", "--initial-branch=main");
            GitFixture.Run(work, "init", "--quiet", "--initial-branch=main");
            for (var daysAgo = 9; daysAgo >= 0; daysAgo--)
            {
                CommitAt(work, Now.AddDays(-daysAgo), $"day -{daysAgo}");
            }

            GitFixture.Run(work, "push", "--quiet", bare, "main:main");
            Directory.CreateDirectory(tree);
            GitFixture.Run(tree, "init", "--quiet");

            var reached = await ShallowDeepening.DeepenUntilAsync(
                new Uri(bare).AbsoluteUri, "refs/heads/main", tree, secret: null, since: Now.AddDays(-5));

            await Assert.That(reached).IsEqualTo(64);
            await Assert.That(GitFixture.Run(tree, "rev-list", "--count", "FETCH_HEAD").Trim()).IsEqualTo("10");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void CommitAt(string work, DateTimeOffset when, string message)
    {
        File.WriteAllText(Path.Combine(work, "README.md"), message + Environment.NewLine);
        GitFixture.Run(work, "add", "README.md");

        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["commit", "--quiet", "-m", message])
        {
            start.ArgumentList.Add(argument);
        }

        var stamp = when.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        start.Environment["GIT_AUTHOR_NAME"] = "gg tests";
        start.Environment["GIT_AUTHOR_EMAIL"] = "tests@good-grief.invalid";
        start.Environment["GIT_COMMITTER_NAME"] = "gg tests";
        start.Environment["GIT_COMMITTER_EMAIL"] = "tests@good-grief.invalid";
        start.Environment["GIT_AUTHOR_DATE"] = stamp;
        start.Environment["GIT_COMMITTER_DATE"] = stamp;

        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git commit exited {process.ExitCode}: {stderr}");
        }
    }
}
