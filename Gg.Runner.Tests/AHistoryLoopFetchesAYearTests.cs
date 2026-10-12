using System.Diagnostics;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A loop that declares <c>history</c> is fetched a year back; every other
/// loop is fetched to the tip, as before (slice seventy-four, after the walk).
/// </summary>
/// <remarks>
/// <para>
/// <b>What the walk found.</b> GG-1084 was the first flight granted
/// <c>read_history</c>. Its <c>blame</c> calls ran and every <c>log --grep</c>
/// printed nothing: the tree was one commit deep, so there was no history to
/// read. The tool was plumbed correctly and handed a history one commit long.
/// </para>
/// <para>
/// <b>A date, a year, blobs and all</b> - the reasons are on
/// <see cref="FetchDepth"/>. What this class holds to is that the plan says
/// <c>--shallow-since</c> and not <c>--depth</c> when asked for history, that
/// a real fetch against a real repository brings the dated commits and no
/// older ones, and that the runner asks for it exactly when the lease's loop
/// declares the move.
/// </para>
/// </remarks>
public class AHistoryLoopFetchesAYearTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 23, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task The_policy_is_a_year_back_for_history_and_the_tip_otherwise()
    {
        var history = FetchDepth.For([LoopMoves.Read, LoopMoves.History], Now);
        var plain = FetchDepth.For([LoopMoves.Read, LoopMoves.Search], Now);

        await Assert.That(history.Since).IsEqualTo(Now - FetchDepth.HistoryWindow)
            .Because("a loop reading history is fetched a year back from now.");
        await Assert.That(plain.Since).IsNull()
            .Because("every other loop pays nothing: the tip, as before.");
        await Assert.That(FetchDepth.For(null, Now).Since).IsNull();
    }

    [Test]
    public async Task The_fetch_plan_says_shallow_since_and_drops_depth()
    {
        var dated = GitInvocation.Fetch(
            "https://example.invalid/acme/widgets.git", "refs/heads/main", secret: null,
            depth: FetchDepth.Back(new DateTimeOffset(2025, 10, 11, 23, 0, 0, TimeSpan.Zero)));

        await Assert.That(dated.Arguments).Contains("--shallow-since=2025-10-11T23:00:00Z")
            .Because("git takes the date in ISO 8601, and a plan a test can read is the point "
                   + "of a plan. Arguments: " + string.Join(" ", dated.Arguments));
        await Assert.That(dated.Arguments.Contains("--depth")).IsFalse()
            .Because("git refuses --depth beside --shallow-since.");

        var tip = GitInvocation.Fetch("https://example.invalid/acme/widgets.git", "refs/heads/main", secret: null);

        await Assert.That(tip.Arguments.Any(a => a.StartsWith("--shallow-since", StringComparison.Ordinal)))
            .IsFalse();
        await Assert.That(tip.Arguments).Contains("--depth")
            .Because("the tip is still the default, and still one commit.");
    }

    [Test]
    public async Task A_real_fetch_brings_the_years_commits_and_not_the_older_ones()
    {
        // THREE COMMITS, DATED: two years ago, six months ago, today. A year
        // back must bring the last two and leave the first behind; the tip
        // brings one.
        using var trees = new ScratchTreeRoot();
        var directory = Path.Combine(Path.GetTempPath(), "gg-depth-fixture", Guid.NewGuid().ToString("n"));
        var bare = Path.Combine(directory, "base.git");
        var work = Path.Combine(directory, "work");
        Directory.CreateDirectory(bare);
        Directory.CreateDirectory(work);

        try
        {
            GitFixture.Run(bare, "init", "--quiet", "--bare", "--initial-branch=main");
            GitFixture.Run(work, "init", "--quiet", "--initial-branch=main");
            foreach (var (when, message) in new[]
                     {
                         (Now.AddYears(-2), "AB#17000 two years ago"),
                         (Now.AddMonths(-6), "AB#17860 six months ago"),
                         (Now, "AB#18492 today"),
                     })
            {
                CommitAt(work, when, message);
            }

            GitFixture.Run(work, "push", "--quiet", bare, "main:main");

            var target = new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey, Slug = bare, PinnedRef = "refs/heads/main",
            };

            var dated = await new Materializer(new LocalVcsAdapter(directory), trees.Root)
                .MaterializeAsync("flight-dated", target, secret: null, depth: FetchDepth.Back(Now.AddYears(-1)));
            var log = GitFixture.Run(dated.Path, "log", "--format=%s");

            await Assert.That(log).Contains("AB#17860 six months ago")
                .Because("the commit that closed the story is what a history loop is after. Log:\n" + log);
            await Assert.That(log).Contains("AB#18492 today");
            await Assert.That(log.Contains("AB#17000 two years ago", StringComparison.Ordinal)).IsFalse()
                .Because("older than the window stays behind: a year, not the whole repository.");

            var tip = await new Materializer(new LocalVcsAdapter(directory), trees.Root)
                .MaterializeAsync("flight-tip", target, secret: null);
            var one = GitFixture.Run(tip.Path, "rev-list", "--count", "HEAD").Trim();

            await Assert.That(one).IsEqualTo("1").Because("the tip, as before, for every other loop.");
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
        // THE COMMITTER DATE, because --shallow-since reads that one.
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

    private sealed class RecordingWorkspace : IWorkspace
    {
        internal List<FetchDepth> Depths { get; } = [];

        public Task<WorkspaceResult> PrepareAsync(
            string flightId, IReadOnlyList<LeaseRepoRef> repos,
            IReadOnlyDictionary<string, string> secretsByLocator, FetchDepth depth,
            CancellationToken cancellationToken = default)
        {
            Depths.Add(depth);
            return Task.FromResult(new WorkspaceResult([]) { Reused = false, Root = Path.GetTempPath() });
        }

        public Task<WorkspaceResult> PrepareAsync(
            string flightId, IReadOnlyList<LeaseRepoRef> repos,
            IReadOnlyDictionary<string, string> secretsByLocator,
            CancellationToken cancellationToken = default) =>
            PrepareAsync(flightId, repos, secretsByLocator, FetchDepth.Tip, cancellationToken);

        public void Release(string flightId) { }
        public HeldTree? Hold(string flightId) => null;
        public int SweepOrphans() => 0;
    }

    private static LeaseGranted ALeaseWhoseLoopMoves(params string[] moves) => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1084),
        Repos = [new LeaseRepoRef { Provider = "local", Slug = "acme/widgets", PinnedRef = "main" }],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = Now.AddMinutes(10),
        RenewWithinSeconds = 5,
        Loop = new LeaseLoop
        {
            LoopId = "investigate",
            Executor = ExecutorRungs.Frontier,
            Moves = moves,
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private static async Task<FetchDepth> DepthTheRunnerAsksForAsync(params string[] moves)
    {
        var clock = new MovableClock(Now);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseWhoseLoopMoves(moves)));
        var observer = new RecordingObserver();
        var workspace = new RecordingWorkspace();

        var stopping = new CancellationTokenSource();
        observer.OnEvent = _ =>
        {
            if (workspace.Depths.Count > 0)
            {
                stopping.Cancel();
            }
        };

        var loop = new RunnerLoop(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer, new NoCredentialResolver(), workspace)
        {
            HoldFor = TimeSpan.FromSeconds(1),
        };

        try
        {
            await loop.RunAsync("runner-1", [], stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }

        await Assert.That(workspace.Depths).Count().IsEqualTo(1)
            .Because("the workspace was prepared once. Events: " + string.Join(", ", observer.Events));
        return workspace.Depths[0];
    }

    [Test]
    public async Task The_runner_asks_for_a_year_when_the_loop_declares_history()
    {
        var depth = await DepthTheRunnerAsksForAsync(LoopMoves.Read, LoopMoves.History);

        await Assert.That(depth.Since).IsEqualTo(Now - FetchDepth.HistoryWindow)
            .Because("the lease's loop declared history, and the tree it works in has to hold some.");
    }

    [Test]
    public async Task The_runner_asks_for_the_tip_when_it_does_not()
    {
        var depth = await DepthTheRunnerAsksForAsync(LoopMoves.Read, LoopMoves.Edit);

        await Assert.That(depth.Since).IsNull()
            .Because("every other loop is fetched as it always was.");
    }
}
