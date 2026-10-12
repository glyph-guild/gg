using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner whose renewal names a different model starts the child over on
/// its own session, on that model, within the same lease (slice seventy-six).
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's ask:</b> <i>"during a flight i would like the ability to
/// override the model on the fly."</i> The lease is composed again on every
/// renewal, so the renewal is where a runner holding a child learns a move;
/// the child cannot change model itself, so it is killed and resumed.
/// </para>
/// <para>
/// <b>Two halves, each tested where it lives.</b> The runner's loop hands the
/// renewal's model to the request it built, which a capturing executor can
/// see; the executor's relaunch is driven against a scripted agent that
/// records its arguments, because the kill-and-resume is a process fact no
/// fake can stand in for.
/// </para>
/// </remarks>
public class TheRunnerMovesARunningFlightTests
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Returns once the request's move signal fires, so the renewal has to reach it.</summary>
    private sealed class WaitingExecutor : IExecutorPort
    {
        internal List<ExecutorRequest> Requests { get; } = [];
        internal string? SawMoveTo { get; private set; }

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public async Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.LoopId == "gg-move-bound-probe")
            {
                return ExecutorRun.Completed(request.LoopId, "probed", 1, TimeSpan.Zero, []);
            }

            if (request.Moved is { } moved)
            {
                await moved.NextAsync(cancellationToken);
                SawMoveTo = moved.Latest;
                // What the real executor says when it starts the agent over.
                moved.Relaunched(SawMoveTo!);
            }

            return ExecutorRun.Completed(
                request.LoopId, "done", attempts: 2, took: TimeSpan.FromSeconds(1),
                movesUsed: [LoopMoves.Read]) with
            {
                Launches =
                [
                    new LoopModel { LoopId = request.LoopId, Model = request.Model },
                    new LoopModel { LoopId = request.LoopId, Model = SawMoveTo, Relaunched = true },
                ],
            };
        }

        public Task<AttendedSession?> AttendedAsync(
            ExecutorRequest request, TimeSpan held, CancellationToken cancellationToken = default) =>
            Task.FromResult<AttendedSession?>(null);
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture) => new()
    {
        LeaseId = "lease-moved",
        Generation = 1,
        FlightId = "flight-moved",
        FlightNumber = FlightRef.Format(76),
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
            LoopId = "investigate",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            Model = "claude-sonnet-5-5",
        },
    };

    [Test]
    public async Task The_renewals_model_reaches_the_executor_and_every_launch_is_shipped()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        // THE FIRST RENEWAL CARRIES THE MOVE, the second only the new expiry:
        // the executor above waits for exactly one.
        protocol.Renewals.Enqueue(new RenewResult.Renewed(T0.AddMinutes(20), "claude-opus-5-5"));
        var observer = new RecordingObserver();
        var executor = new WaitingExecutor();

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

        var request = executor.Requests.Single(r => r.LoopId == "investigate");
        await Assert.That(request.Moved).IsNotNull()
            .Because("a flight's launch is handed the signal its renewals will write to.");
        await Assert.That(executor.SawMoveTo).IsEqualTo("claude-opus-5-5")
            .Because("the renewal said the lease now runs on it, and nothing else reaches a "
                   + "runner holding a child.");

        var launches = protocol.ShippedFacts
            .SelectMany(f => f.Items)
            .Where(f => f.Kind == FactKinds.LoopModel)
            .Select(f => f.Model!)
            .ToList();
        await Assert.That(launches.Select(l => l.Model ?? "<default>")).IsEquivalentTo(["claude-sonnet-5-5", "claude-opus-5-5"])
            .Because("one fact per launch, so two bracket the move. Shipped kinds: "
                   + string.Join(", ", protocol.ShippedFacts.SelectMany(f => f.Items).Select(f => f.Kind)));
        await Assert.That(launches.Select(l => l.Relaunched)).IsEquivalentTo([false, true]);
        await Assert.That(observer.Events).Contains($"relaunched:{FlightRef.Format(76)}:claude-opus-5-5")
            .Because("the runner's log says a move happened when it happens, not only in the facts "
                   + "shipped at the end - the walk on GG-1097 found it silent. Events: "
                   + string.Join(", ", observer.Events));
    }

    [Test]
    public async Task A_scripted_agent_is_killed_and_resumed_on_the_new_model()
    {
        // A FAKE AGENT THAT RECORDS ITS ARGUMENTS. On a first launch it announces
        // itself and waits to be killed; on a resume it finishes. The sleep is
        // the child's, standing in for a model working - the test itself waits
        // on nothing but the run.
        var root = Path.Combine(Path.GetTempPath(), "gg-moves-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "tree"));
        var log = Path.Combine(root, "launches.log");
        var script = Path.Combine(root, "agent.sh");
        File.WriteAllText(script,
            "#!/bin/sh\n"
          + $"printf '%s' \"$*\" | tr '\\n' ' ' >> '{log}'; echo >> '{log}'\n"
          + "case \"$*\" in\n"
          + "  *--resume*)\n"
          + "    echo '{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"carried on\",\"num_turns\":2}'\n"
          + "    exit 0 ;;\n"
          + "  *)\n"
          + "    echo '{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"ignored\"}'\n"
          + "    sleep 60 ;;\n"
          + "esac\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var relaunched = new List<string>();
        var moved = new ModelMoves(relaunched.Add);
        var request = new ExecutorRequest
        {
            WorkingDirectory = Path.Combine(root, "tree"),
            LoopId = "investigate",
            Moves = [LoopMoves.Read],
            IntentUri = "https://example.invalid/work/1",
            WallClock = TimeSpan.FromSeconds(30),
            TranscriptPath = Path.Combine(root, "transcript.ndjson"),
            Model = "claude-sonnet-5-5",
            Moved = moved,
        };

        // MOVED BEFORE THE CHILD SPEAKS, which the executor must handle as a
        // move rather than miss: a pending move is read when the child is
        // started, not only when one arrives later.
        moved.Move("claude-opus-5-5");

        var run = await new ClaudeCodeExecutor(script).ExecuteAsync(request, CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Completed)
            .Because($"the resumed child finished. Reason: {run.Reason}");
        await Assert.That(run.Launches.Select(l => (l.Model ?? "<default>", l.Relaunched)))
            .IsEquivalentTo([("claude-sonnet-5-5", false), ("claude-opus-5-5", true)]);
        await Assert.That(relaunched).IsEquivalentTo(["claude-opus-5-5"])
            .Because("the executor says when it starts the agent over, which is what puts the "
                   + "relaunch in the runner's log.");

        // THE LOG IS THE CHILDREN'S WORD, and the first child may be killed
        // before its shell reaches `echo` - a move already pending is acted on
        // the instant the child starts. So the RUN's launches are the count,
        // and the log proves the resume: its last line is a child started over
        // on the new model by the id the runner chose, and a first line, when
        // the shell got that far, carries that same id as --session-id.
        var launches = File.ReadAllLines(log).Where(line => line.Length > 0).ToList();
        var resumed = launches.SingleOrDefault(line => line.Contains("--resume", StringComparison.Ordinal));
        await Assert.That(resumed).IsNotNull()
            .Because("the second child continues the first's conversation. Launches:\n"
                   + string.Join("\n", launches));
        await Assert.That(resumed!).Contains("--model claude-opus-5-5");
        var session = resumed.Split(' ').SkipWhile(a => a != "--resume").Skip(1).First();
        await Assert.That(Guid.TryParse(session, out _)).IsTrue()
            .Because("the id is the runner's, chosen before the first child spoke.");
        await Assert.That(resumed.Contains("--session-id", StringComparison.Ordinal)).IsFalse();
        if (launches.SingleOrDefault(line => line.Contains("--session-id", StringComparison.Ordinal))
            is { } first)
        {
            await Assert.That(first).Contains("--session-id " + session)
                .And.Contains("--model claude-sonnet-5-5")
                .Because("the first child, when its shell got as far as writing, was given the "
                       + "same id and the model the loop named.");
        }
    }
}
