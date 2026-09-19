using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// An agent's scratch - scripts, notes, anything that is not the change - lives
/// in the flight's own directory and goes wherever the flight's directory goes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found reviewing GG-190, 2026-09-19.</b> The agent wrote three scanner
/// scripts to <c>/tmp/s3928*.py</c> on vmlinux001 and they were still there
/// after the flight: the flight's directory was removed on release and
/// <c>/tmp</c> was never part of it. The resident runner runs its agent on the
/// host as <c>gg</c>, <c>PrivateTmp=no</c>, and nothing set <c>TMPDIR</c> - so
/// the machine's temporary directory was the flight's, shared with every flight
/// after it.
/// </para>
/// <para>
/// <b>Two halves, because the agent did both.</b> <c>TMPDIR</c>, <c>TMP</c> and
/// <c>TEMP</c> point at the scratch directory, which catches every tool that
/// asks the environment. And the prompt says where scratch goes, because that
/// agent typed <c>/tmp</c> literally, which no environment variable reaches.
/// </para>
/// <para>
/// <b>Not isolation.</b> The agent can still reach anything its user can; this
/// keeps what it makes in one place that has an owner and an end.
/// </para>
/// </remarks>
public class AFlightKeepsItsScratchTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 19, 5, 30, 0, TimeSpan.Zero);

    /// <summary>Records each request, and whether its scratch existed when the agent started.</summary>
    private sealed class CapturingExecutor : IExecutorPort
    {
        internal List<(ExecutorRequest Request, bool ScratchExisted)> Runs { get; } = [];

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Runs.Add((request, request.ScratchDirectory is { } scratch && Directory.Exists(scratch)));
            return Task.FromResult<ExecutorRun?>(ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(1),
                movesUsed: [LoopMoves.Read]));
        }
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture) => new()
    {
        LeaseId = "lease-scratch",
        Generation = 1,
        FlightId = "flight-scratch",
        FlightNumber = FlightRef.Format(190),
        Repos =
        [
            new LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Anything],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
        },
    };

    private static async Task<(ExecutorRequest Request, bool ScratchExisted, string TreeRoot, bool StillThere)> FlyAsync()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        var observer = new RecordingObserver();
        var executor = new CapturingExecutor();

        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.CompletedTask;
                },
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                executor: executor)
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        var (request, existed) = executor.Runs.Single(r => r.Request.LoopId != "gg-move-bound-probe");

        // MEASURED HERE, before the test's own tree root is disposed - after
        // that, every path under it is gone and the answer would prove nothing.
        return (request, existed, trees.Root.For(ALeaseFor(fixture).FlightId),
            request.ScratchDirectory is { } scratch && Directory.Exists(scratch));
    }

    [Test]
    public async Task The_agent_is_handed_a_scratch_directory_inside_the_flights_own()
    {
        var (request, existed, _, _) = await FlyAsync();

        await Assert.That(request.ScratchDirectory).IsNotNull();
        await Assert.That(Path.GetDirectoryName(request.ScratchDirectory)).IsEqualTo(request.WorkingDirectory)
            .Because("inside the flight's own directory, so it is released, or held for a "
                   + "takeover, with everything else the flight put on disk.");
        await Assert.That(existed).IsTrue()
            .Because("a directory named to the agent that is not there sends it back to /tmp.");
    }

    [Test]
    public async Task Nothing_of_it_outlives_the_flight_where_the_flight_was()
    {
        var (request, _, treeRoot, stillThere) = await FlyAsync();

        await Assert.That(stillThere).IsFalse()
            .Because("GG-190's scripts outlived it because /tmp was never the flight's. What "
                   + "the flight's directory takes with it, this goes with.");
        await Assert.That(request.ScratchDirectory!).StartsWith(treeRoot);
    }

    private static ExecutorRequest ARequest(string? scratch) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "implement",
        Moves = [LoopMoves.Anything],
        IntentUri = "https://example.test/items/18489",
        WallClock = TimeSpan.FromMinutes(20),
        TranscriptPath = "/work/flight/transcript.ndjson",
        ScratchDirectory = scratch,
    };

    [Test]
    public async Task Every_temporary_directory_variable_points_at_it()
    {
        foreach (var info in new[]
                 {
                     ClaudeCodeExecutor.StartInfoFor(ARequest("/work/flight/scratch"), []),
                     AttendedExecutor.StartInfoFor(ARequest("/work/flight/scratch"), []),
                 })
        {
            foreach (var variable in (string[])["TMPDIR", "TMP", "TEMP"])
            {
                await Assert.That(info.Environment[variable]).IsEqualTo("/work/flight/scratch")
                    .Because($"{variable} is what a tool asks for when it wants somewhere temporary.");
            }
        }
    }

    [Test]
    public async Task With_no_scratch_the_environment_is_inherited_as_it_was()
    {
        var info = ClaudeCodeExecutor.StartInfoFor(ARequest(scratch: null), []);

        // ABSENT IS AN INHERITED VALUE TOO. macOS always sets TMPDIR; a Linux
        // CI runner does not, and the indexer throws for a key that is not
        // there - which is how this first failed, on CI only.
        _ = info.Environment.TryGetValue("TMPDIR", out var inherited);

        await Assert.That(inherited)
            .IsEqualTo(Environment.GetEnvironmentVariable("TMPDIR"))
            .Because("a sweep and the move-bound probe have no flight directory, and changing "
                   + "their environment would be a second decision nobody asked for.");
    }

    [Test]
    public async Task The_prompt_says_where_scratch_goes_and_that_it_is_not_tmp()
    {
        var prompt = ClaudeCodeExecutor.PromptFor(ARequest("/work/flight/scratch"));

        await Assert.That(prompt).Contains("/work/flight/scratch");
        await Assert.That(prompt).Contains("/tmp")
            .Because("GG-190's agent typed /tmp by name, which no environment variable reaches; "
                   + "only saying so does.");
    }

    [Test]
    public async Task With_no_scratch_the_prompt_says_nothing_about_it()
    {
        await Assert.That(ClaudeCodeExecutor.PromptFor(ARequest(scratch: null))).DoesNotContain("scratch");
    }
}
