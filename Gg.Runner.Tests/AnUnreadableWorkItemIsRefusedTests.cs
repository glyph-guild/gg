using Gg.Local;
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

        public Task<ExecutorRun?> ExecuteAsync(
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

    private static async Task<string> ShippedReasonAsync(string declaration) =>
        (await RanAsync(declaration)).ShippedFacts
            .SelectMany(b => b.Items)
            .Single(f => f.Kind == FactKinds.LoopOutcome)
            .Loop!.Reason;

    /// <summary>Drives the whole loop over an unreadable item and hands back the protocol.</summary>
    /// <remarks>
    /// <b>The protocol rather than one fact, because the refusal has two halves
    /// and only one was ever read.</b> What the flight SAYS is the loop outcome;
    /// what the flight BECOMES is the release disposition. A flight whose fact
    /// says <c>failed</c> and whose state says <c>landed</c> is one nobody will
    /// think to re-run.
    /// </remarks>
    private static async Task<FakeProtocol> RanAsync(string declaration)
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

        return protocol;
    }

    [Test]
    public async Task A_loop_that_failed_does_not_report_the_flight_as_completed()
    {
        // GG-98, EXACTLY. A work item this runner could not read, the loop
        // ending `failed` before any agent was invoked, and the flight LANDED -
        // because the disposition was chosen from the landing alone:
        //
        //   Outstanding(landing) => landing is { Push: not null, Admission: null }
        //
        // A loop that fails before producing a push has no push, and no push
        // read as nothing outstanding, so `completed` went out and `landed` came
        // back. The run's own outcome was sitting at that line, unread.
        //
        // GG-93 ESCAPED THIS BY ACCIDENT and that is why it went unnoticed: it
        // named a repository, so it had a tree, so it had a manifest to push -
        // and the same failure reported outstanding.
        var protocol = await RanAsync(declaration: "");

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsNotEqualTo(RunnerDisposition.Completed)
            .Because("`completed` maps to `landed` and the exit claim is first-writer-wins, "
                   + "so a flight that scored nothing is recorded as finished and nothing "
                   + "corrects it afterwards.");
    }

    [Test]
    public async Task It_ends_the_flight_rather_than_leaving_it_for_another_runner()
    {
        // FAILED IS A CONCLUSION, which is what separates it from abandoned.
        // Nothing here is going to come good on a second runner: the item is
        // unreadable by this fleet's declaration, so handing the flight on
        // would fly it into the same refusal and cost another lease to learn it.
        var protocol = await RanAsync(declaration: "");

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsEqualTo(RunnerDisposition.Failed);
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
