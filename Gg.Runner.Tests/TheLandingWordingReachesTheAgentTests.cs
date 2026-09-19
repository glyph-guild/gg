using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Local;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// What a destination asks a proposal to be called reaches the agent that can
/// say it, and only that agent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found 2026-09-19, and nothing carried it.</b> A destination's
/// <c>title:</c> and <c>description:</c> are prose for the agent. The control
/// plane puts them on <c>LeaseLoop.Landing</c> for every flight, and nothing in
/// this repository read that member. It was not in the executor's request and
/// not in the prompt, so the words an operator wrote were valid, applied, and
/// read by nobody. The proposal was named from the first sentence of the
/// agent's summary instead: GG-187's pull request was.
/// </para>
/// <para>
/// <b>StandingInstructionsReachTheLoopTests one member over.</b> The producer
/// was tested and the consumer existed, and nothing carried the wording from
/// one to the other.
/// </para>
/// <para>
/// <b>Said only where it can be acted on.</b> The contract's own remark: the
/// wording only means anything to a loop that may call the landing tool. That
/// is a loop granted <c>propose-landing</c>, and also a loop that declares
/// <c>anything</c>, which passes no allow-list at all, so every tool the
/// session is served is callable. Telling any other agent how to name a
/// proposal is advice about something it cannot do.
/// </para>
/// </remarks>
public class TheLandingWordingReachesTheAgentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 19, 1, 0, 0, TimeSpan.Zero);

    private const string TitleWording = "An imperative under seventy characters, naming the change.";

    private const string DescriptionWording = "What changed, why, and how it was verified.";

    private static readonly LeaseLanding Asked = new()
    {
        Title = TitleWording,
        Description = DescriptionWording,
    };

    // ---- the runner hands it over ----

    private sealed class CapturingExecutor : IExecutorPort
    {
        internal List<ExecutorRequest> Requests { get; } = [];

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<ExecutorRun?>(ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(1),
                movesUsed: [LoopMoves.Read]));
        }
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture, LeaseLanding? landing) => new()
    {
        LeaseId = "lease-landing-wording",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(187),
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
            Moves = [LoopMoves.Read, LoopMoves.Edit, LoopMoves.ProposeLanding],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
            Landing = landing,
        },
    };

    private static async Task<ExecutorRequest> FlyAsync(LeaseLanding? landing)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, landing)));
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

        // The session's move-bound probe invokes the executor too; the request
        // under test is the work's.
        return executor.Requests.Single(r => r.LoopId != "gg-move-bound-probe");
    }

    [Test]
    public async Task A_lease_asking_for_wording_hands_it_to_the_executor()
    {
        var request = await FlyAsync(Asked);

        await Assert.That(request.LandingWording).IsEqualTo(Asked)
            .Because("the control plane sent it for this flight; a runner that dropped it "
                   + "leaves the operator's words applied and read by nobody.");
    }

    [Test]
    public async Task A_lease_asking_nothing_carries_nothing()
    {
        var request = await FlyAsync(landing: null);

        await Assert.That(request.LandingWording).IsNull();
    }

    // ---- the prompt says it where the agent can act on it ----

    private static string Prompt(IReadOnlyList<string> moves, LeaseLanding? landing = null) =>
        ClaudeCodeExecutor.PromptFor(new ExecutorRequest
        {
            WorkingDirectory = "/work/flight",
            LoopId = "implement",
            Moves = moves,
            IntentUri = "https://example.test/items/18490",
            WallClock = TimeSpan.FromMinutes(20),
            TranscriptPath = "/work/flight/transcript.ndjson",
            LandingWording = landing ?? Asked,
        });

    [Test]
    public async Task An_agent_granted_the_move_is_told_both_and_which_tool_says_them()
    {
        var prompt = Prompt([LoopMoves.Read, LoopMoves.Edit, LoopMoves.ProposeLanding]);

        await Assert.That(prompt).Contains(TitleWording);
        await Assert.That(prompt).Contains(DescriptionWording);
        await Assert.That(prompt).Contains(LandingProposalTool.Qualified)
            .Because("wording with no way to act on it is advice; the tool is the way.");
    }

    [Test]
    public async Task An_unbounded_agent_is_told_too()
    {
        // THE DEV TENANT'S IMPLEMENT LOOP. `anything` passes no allow-list, so
        // the landing tool is as callable as every other tool the session is
        // served - and the wording reached nobody on GG-187 for exactly this loop.
        var prompt = Prompt([LoopMoves.Anything]);

        await Assert.That(prompt).Contains(TitleWording);
        await Assert.That(prompt).Contains(LandingProposalTool.Qualified);
    }

    [Test]
    public async Task An_agent_that_cannot_propose_a_landing_is_not_told()
    {
        var prompt = Prompt([LoopMoves.Read, LoopMoves.Edit]);

        await Assert.That(prompt).DoesNotContain(TitleWording)
            .Because("how to name a proposal is advice about something this agent cannot "
                   + "do, and a prompt that reads as a policy the agent cannot meet is one "
                   + "it will try to meet some other way.");
        await Assert.That(prompt).DoesNotContain(LandingProposalTool.Qualified);
    }

    [Test]
    public async Task Only_what_was_asked_is_said()
    {
        var prompt = Prompt(
            [LoopMoves.ProposeLanding], new LeaseLanding { Title = TitleWording });

        await Assert.That(prompt).Contains(TitleWording);
        await Assert.That(prompt).DoesNotContain("Description:")
            .Because("a destination that asked for no description gets none asked for - "
                   + "a body the agent was told to write anyway is padding under its name.");
    }

    [Test]
    public async Task Nothing_asked_changes_nothing()
    {
        var asked = ClaudeCodeExecutor.PromptFor(new ExecutorRequest
        {
            WorkingDirectory = "/work/flight",
            LoopId = "implement",
            Moves = [LoopMoves.ProposeLanding],
            IntentUri = "https://example.test/items/18490",
            WallClock = TimeSpan.FromMinutes(20),
            TranscriptPath = "/work/flight/transcript.ndjson",
        });

        await Assert.That(asked).DoesNotContain(LandingProposalTool.Qualified)
            .Because("an envelope that asks nothing renders the prompt it always did.");
    }
}
