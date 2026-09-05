using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// An exhausted reason names the disposition the envelope declared.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect:</b> <c>ExecutorRun.Exhausted</c> hard-codes <i>"This flight
/// is waiting for a person."</i> - written when <c>handoff-to-human</c> was the
/// only value, and never revisited when <c>handoff-to-agent</c> arrived. So a
/// flight the control plane is about to requeue for another agent ships a fact
/// telling every reader a person is needed, and a console queue shows somebody
/// a wait that is not theirs.
/// </para>
/// <para>
/// <b>The shape of the fix this pins:</b> the executor measured the stop and
/// only the stop - who the flight waits for is the envelope's knowledge, so the
/// sentence is appended where the envelope is known, from
/// <c>loop.OnExhaustion</c>, and the factory makes no claim about who waits.
/// </para>
/// </remarks>
public class ExhaustionReasonTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class ExhaustingExecutor : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExecutorRun?>(ExecutorRun.Exhausted(
                request.LoopId, request.WallClock, [LoopMoves.Read]));
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture, string onExhaustion) => new()
    {
        LeaseId = "lease-exhausted",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
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
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = onExhaustion,
        },
    };

    private static async Task<string> ShippedReasonAsync(string onExhaustion)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, onExhaustion)));
        var observer = new RecordingObserver();

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
                executor: new ExhaustingExecutor())
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Single(f => f.Kind == FactKinds.LoopOutcome)
            .Loop!.Reason;
    }

    [Test]
    public async Task A_handoff_to_agent_exhaustion_stops_claiming_a_person_is_waiting()
    {
        var reason = await ShippedReasonAsync(ExhaustionPolicies.HandoffToAgent);

        await Assert.That(reason.ToLowerInvariant()).Contains("another agent")
            .Because("the envelope declared where the flight goes next, and the fact should "
                   + "say so in those words.");
        await Assert.That(reason.ToLowerInvariant()).DoesNotContain("waiting for a person")
            .Because("a console queue reading this would show somebody a wait that is not "
                   + "theirs - the control plane is about to requeue it for an agent.");
    }

    [Test]
    public async Task A_handoff_to_human_exhaustion_still_waits_for_a_person_in_those_words()
    {
        var reason = await ShippedReasonAsync(ExhaustionPolicies.HandoffToHuman);

        await Assert.That(reason.ToLowerInvariant()).Contains("waiting for a person")
            .Because("the fix cannot pass by deleting the sentence - under handoff-to-human "
                   + "it is the state, and the queue shows it to somebody in those words.");
    }

    [Test]
    public async Task The_factory_alone_makes_no_claim_about_who_waits()
    {
        var run = ExecutorRun.Exhausted(
            loopId: "implement", after: TimeSpan.FromMinutes(30), movesUsed: [LoopMoves.Read]);

        await Assert.That(run.Reason.ToLowerInvariant()).DoesNotContain("person");
        await Assert.That(run.Reason.ToLowerInvariant()).DoesNotContain("agent")
            .Because("the executor measured the stop and only the stop; who the flight waits "
                   + "for is the envelope's knowledge, and the factory has never seen the "
                   + "envelope.");
    }
}
