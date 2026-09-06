using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight whose intent is the words somebody typed reaches an agent.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SAME SILENCE, A THIRD TIME, AND THIS IS THE INTENT KIND IT STILL
/// COVERED.</b> First <c>Trees.Count == 0</c> meant a ticket, a text intent and
/// an issue link were all leased, claimed and never worked. That was fixed.
/// Then <c>NamesWork</c> arrived reading only a uri, and every work-item flight
/// went the same way - fixed by giving a ticket a provider and an id to offer.
/// Text was named in both remarks as one of the cases and given nothing to
/// offer either time, so it is still leased, cloned, renewed twice, and
/// released as "landed" with no agent invoked and nothing said.
/// </para>
/// <para>
/// <b>Measured on the live stack rather than argued.</b> Of the forty flights
/// this tenant has flown, every free-text one - GG-3, GG-11, GG-36, GG-40 -
/// records no <c>loop.outcome</c> and <c>attempts none</c>. Three of them were
/// smoke tests whose own text says they verify the agent loop end to end. They
/// verified that a tree can be cloned.
/// </para>
/// <para>
/// <b>Carrying the words is not carrying an issue's body.</b>
/// <c>LeaseGranted.IntentUri</c>'s remark holds the line that an issue's text is
/// customer content and does not cross - the control plane keeps the reference
/// and the runner resolves it with the customer's own credential. Typed words
/// are the opposite case: the operator wrote them at their own terminal, the
/// control plane already holds them and already prints them in
/// <c>gg flights</c>, and handing them back to that operator's own runner
/// exposes nothing that has not already crossed. The rule is about a body
/// nobody here is entitled to read, and this is not one.
/// </para>
/// </remarks>
public class TextIntentReachesTheAgentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Typed =
        "Append one line to GG-SMOKE.md at the repository root. Change nothing else.";

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

    private static LeaseGranted ALeaseFor(GitFixture fixture, string? text) => new()
    {
        LeaseId = "lease-text",
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
        // NO URI, NO TICKET. The shape `gg fly "<text>"` produces, which is the
        // one that has never reached an agent.
        IntentText = text,
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
        },
    };

    private static async Task<CapturingExecutor> FlyAsync(string? text)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, text)));
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

        return executor;
    }

    private static IReadOnlyList<ExecutorRequest> Work(CapturingExecutor executor) =>
        [.. executor.Requests.Where(r => r.LoopId != "gg-move-bound-probe")];

    [Test]
    public async Task A_flight_whose_intent_is_typed_words_invokes_the_agent()
    {
        var executor = await FlyAsync(Typed);

        await Assert.That(Work(executor)).Count().IsEqualTo(1)
            .Because("a person typed work and pressed enter; a flight that leases, clones "
                   + "and reports landed without invoking anything is the silence this is "
                   + "the third fix for.");
    }

    [Test]
    public async Task The_agent_is_told_the_words_rather_than_a_pointer_to_them()
    {
        var executor = await FlyAsync(Typed);

        await Assert.That(Work(executor)[0].IntentText).IsEqualTo(Typed);
        await Assert.That(ClaudeCodeExecutor.PromptFor(Work(executor)[0])).Contains(Typed)
            .Because("there is nothing to resolve: the words ARE the work, and a subject "
                   + "line naming a uri that does not exist would send an agent looking "
                   + "for a tracker item nobody filed.");
    }

    [Test]
    public async Task A_flight_naming_no_work_at_all_still_invokes_nothing()
    {
        // THE BOUND ON THIS CHANGE. An empty intent is not work, and a runner
        // that invoked on one would hand an agent a prompt with no subject.
        var executor = await FlyAsync(text: null);

        await Assert.That(Work(executor)).IsEmpty();
    }

    [Test]
    public async Task Typed_words_name_work_the_way_a_uri_and_a_ticket_do()
    {
        await Assert.That(ExecutorRequest.NamesWork(null, null, null, Typed)).IsTrue();
        await Assert.That(ExecutorRequest.NamesWork(null, null, null, null)).IsFalse();
        await Assert.That(ExecutorRequest.NamesWork(null, null, null, "   ")).IsFalse()
            .Because("whitespace is not a description of work, and a prompt built from it "
                   + "would say Work    . and mean nothing.");
    }
}
