using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight whose intent names no repository is still work, and the agent still
/// gets to do it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three intent kinds legitimately name no repository</b> and the control
/// plane already holds all three: a <c>text</c> intent, a <c>ticket</c> intent,
/// and a uri intent pointing at an issue or work item -
/// <c>FlightRepos.From</c> reads <c>tree</c> and <c>pull</c> pointers and
/// nothing else, deliberately, so an issue link resolves to no repository and is
/// meant to.
/// </para>
/// <para>
/// <b>The runner then dropped all three on the floor.</b> <c>InvokeAsync</c>
/// gated on <c>workspace.Trees.Count == 0</c> and returned null, so the flight
/// was created, leased, claimed, and never worked: no executor run, no fact, no
/// diagnosis. <c>attempts: none</c> and nothing anywhere saying why. That
/// silence is what a real work-item flight produced.
/// </para>
/// <para>
/// <b>The recorded plan for it was a refusal, and this is not that.</b>
/// <c>SilentlyUnworkableFlightTests</c> in the control plane says the fix
/// <i>"belongs where the lease is granted"</i> and would be <i>"a lease-time
/// refusal for a flight with nothing to work on"</i>. That reading assumed a
/// flight without a repository has nothing to work on. It has a ticket, and the
/// agent resolves what the uri points at from inside the customer's environment
/// with the customer's own credential - which is the executor's whole design
/// note about why it is handed a URI and never a body somebody fetched.
/// </para>
/// <para>
/// <b>So the agent works in the flight's own directory instead of in a clone.</b>
/// Nothing is materialized, nothing is pushed, and no proposal is opened -
/// there is no repository for any of that to be about. What the flight produces
/// is the loop's own record.
/// </para>
/// </remarks>
public class RepositorylessFlightTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A work item, which is a thing to work and not a place to clone.</summary>
    private const string TicketUri = "https://forge.example.invalid/org/project/_workitems/edit/18372";

    private sealed class CapturingExecutor : IExecutorPort
    {
        internal List<ExecutorRequest> Requests { get; } = [];

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(ExecutorRun.Completed(
                request.LoopId, "done", attempts: 1, took: TimeSpan.FromSeconds(1),
                movesUsed: [LoopMoves.Read]));
        }
    }

    /// <summary>The lease as the control plane grants it for a ticket: no repos.</summary>
    private static LeaseGranted ATicketLease() => new()
    {
        LeaseId = "lease-ticket",
        Generation = 1,
        FlightId = "flight-ticket",
        FlightNumber = FlightRef.Format(20),

        // THE WHOLE POINT. Not an oversight in the fixture - this is what a
        // ticket flight's lease carries, because there is no repository in a
        // work item url for the ingress to have found.
        Repos = [],

        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentUri = TicketUri,
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
            ResumesFrom = null,
        },
    };

    private static async Task<(CapturingExecutor Executor, RecordingObserver Observer)> FlyAsync()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ATicketLease()));
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

        return (executor, observer);
    }

    /// <summary>The work's request, which is not the session probe's.</summary>
    private static List<ExecutorRequest> WorkOf(CapturingExecutor executor) =>
        [.. executor.Requests.Where(r => r.LoopId != "gg-move-bound-probe")];

    [Test]
    public async Task A_ticket_flight_reaches_the_agent_even_though_it_has_no_repository()
    {
        var (executor, _) = await FlyAsync();

        await Assert.That(WorkOf(executor)).Count().IsEqualTo(1)
            .Because("a work item is work. Returning null because no clone was made is how a "
                   + "flight got created, leased, claimed and never worked, with no diagnosis "
                   + "anywhere saying so.");
    }

    [Test]
    public async Task The_agent_is_given_the_ticket_it_is_meant_to_work()
    {
        // The uri is the whole brief - the agent resolves it from inside the
        // customer's environment. Handing it a working directory and no ticket
        // would be a session with nothing to do.
        var (executor, _) = await FlyAsync();

        await Assert.That(WorkOf(executor).Single().IntentUri).IsEqualTo(TicketUri);
    }

    [Test]
    public async Task The_agent_is_given_a_directory_that_exists_to_work_in()
    {
        // Not a clone, and not null. An executor is handed a working directory
        // and starts a process in it; a path that is not there is a launch
        // failure rather than a flight that did nothing.
        var (executor, _) = await FlyAsync();

        var working = WorkOf(executor).Single().WorkingDirectory;

        await Assert.That(working).IsNotNull().And.IsNotEmpty();
        await Assert.That(Directory.Exists(working!)).IsTrue()
            .Because("the agent has to have somewhere to be, even when there was nothing to "
                   + "clone into it.");
    }

    [Test]
    public async Task Nothing_is_materialized_and_nothing_is_proposed()
    {
        // The twin, and the half that keeps this honest: making a repo-less
        // flight run must not make it invent a repository. No tree is reported
        // and no branch is pushed, because there is nothing for either to be
        // about.
        var (_, observer) = await FlyAsync();

        await Assert.That(observer.Events.Any(
                e => e.StartsWith("materialized:", StringComparison.Ordinal)))
            .IsFalse();
        await Assert.That(observer.Events.Any(
                e => e.StartsWith("pushed:", StringComparison.Ordinal)
                  || e.StartsWith("landed:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a flight with no repository has nothing to push and nowhere to propose it.");
    }
}
