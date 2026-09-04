using Gg.Contracts.Description;
using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A work item this runner cannot read is refused with a reason, on the flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal has to reach a FACT, not a log line.</b> A runner that
/// declined quietly is where this whole set of defects started: a work-item
/// flight that leased, cloned, and came back with nothing to read. Somebody
/// looking at the flight has to be able to see why, and the loop outcome is
/// where they look.
/// </para>
/// <para>
/// <b>Before the agent, not by the agent.</b> An agent handed a work item it has
/// no tool for spends the loop's entire wall-clock budget establishing that, and
/// reports it as prose — which is exactly what the flight that started this did,
/// and what took an SSH session to read.
/// </para>
/// </remarks>
public class AnUnreadableWorkItemIsRefusedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Fails the test if it is ever reached: the point is that it is not.</summary>
    private sealed class NeverInvoked : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "the agent was invoked for a work item this runner cannot read");
    }

    private static LeaseGranted ALease(string? provider) => new()
    {
        LeaseId = "lease-unreadable",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(26),
        Repos = [],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentProvider = provider,
        IntentId = "26",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
        },
    };

    private static async Task<string> ShippedReasonAsync(string declaration)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease("a-tracker")));
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
                executor: new NeverInvoked(),
                readers: IntentConfiguration.FromEnvironment(declaration))
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
    public async Task The_flight_says_which_tracker_this_runner_cannot_read()
    {
        // THE DEFECT, ended. Before this the agent was invoked, failed slowly,
        // and said so in prose - and the reason was cut at the first paragraph
        // on the way out, so even that was unreadable.
        var reason = await ShippedReasonAsync(declaration: "");

        await Assert.That(reason).Contains("a-tracker")
            .Because("an operator reading the flight has to be told which declaration is "
                   + "missing, or the refusal is not actionable.");
        await Assert.That(reason).Contains(IntentConfiguration.ReadersVariable)
            .Because("naming the variable is the difference between a diagnosis and a "
                   + "complaint.");
    }

    [Test]
    public async Task The_refusal_names_what_this_runner_can_read_when_it_can_read_something()
    {
        // A runner serving the wrong tracker is a routing mistake, and the
        // sentence that helps says what this one DOES serve - otherwise the
        // operator's next step is to go and look it up.
        var reason = await ShippedReasonAsync(declaration: "another-tracker=other-mcp");

        await Assert.That(reason).Contains("another-tracker");
    }
}
