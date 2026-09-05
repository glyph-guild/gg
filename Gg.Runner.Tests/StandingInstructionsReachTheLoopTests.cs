using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The instructions a lease carries reach the executor's request, untouched.
/// </summary>
/// <remarks>
/// <para>
/// <b>The middle link, and it was missing.</b> Step one put
/// <c>Envelope.Instructions</c> on the document and composed it; step two
/// rendered it into <c>LeaseLoop.Instructions</c> and taught the prompt to
/// place it. Both halves were tested and both passed, and nothing carried the
/// text from one to the other: <c>RunnerLoop</c> built its
/// <c>ExecutorRequest</c> with <c>ResumesFrom</c> and <c>Feedback</c> beside
/// each other and no <c>Instructions</c>, so the only requests that ever held
/// one were the ones a test constructed by hand.
/// </para>
/// <para>
/// <b>Its twin already said so.</b> <c>ResumptionContextTests</c> exists
/// because the same gap opened for the resumption seed, and its remark is the
/// sentence this class is a second instance of: the runner has to HAND it to
/// the loop it starts, or the delivery proved nothing. A criterion for the
/// producer and a criterion for the consumer do not add up to a criterion for
/// the wire between them.
/// </para>
/// <para>
/// <b>Verbatim, for the reason the seed is verbatim.</b> The contract renders
/// the blocks once, control-plane-side, in layer order with their provenance
/// already attached. A runner that re-wrapped or re-ordered them would be a
/// second rendering of a document <c>EnvelopeText</c> already renders - the
/// duplication <c>LeaseLoop.Instructions</c> exists to prevent.
/// </para>
/// </remarks>
public class StandingInstructionsReachTheLoopTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Two blocks from two layers, as the composer hands them over.</summary>
    private const string Standing =
        "instructions:\n"
      + "  - text: Prefer the smallest change that makes the test pass.\n"
      + "    from: root\n"
      + "  - text: Name the ticket in the branch.\n"
      + "    from: work-kind/fix\n";

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

    private static LeaseGranted ALeaseFor(GitFixture fixture, string? instructions) => new()
    {
        LeaseId = "lease-instructions",
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
            OnExhaustion = ExhaustionPolicies.HandoffToAgent,
            Instructions = instructions,
        },
    };

    private static async Task<(CapturingExecutor Executor, FakeProtocol Protocol)> FlyAsync(
        string? instructions)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, instructions)));
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

        return (executor, protocol);
    }

    private static ExecutorRequest TheWorks(CapturingExecutor executor)
    {
        // The session's move-bound probe invokes the executor too; the request
        // under test is the WORK's.
        var work = executor.Requests
            .Where(r => r.LoopId != "gg-move-bound-probe")
            .ToList();

        return work.Count == 1
            ? work[0]
            : throw new InvalidOperationException(
                $"expected exactly one work request, saw {work.Count}");
    }

    [Test]
    public async Task A_lease_carrying_instructions_hands_them_to_the_executor_verbatim()
    {
        var (executor, _) = await FlyAsync(Standing);

        await Assert.That(TheWorks(executor).Instructions).IsEqualTo(Standing)
            .Because("the blocks are a rendered document; a runner that re-wrapped or "
                   + "re-ordered them would be a second implementation of what the "
                   + "contract renders once, in layer order, with provenance attached.");
    }

    [Test]
    public async Task A_lease_with_no_instructions_carries_none()
    {
        var (executor, _) = await FlyAsync(instructions: null);

        await Assert.That(TheWorks(executor).Instructions).IsNull()
            .Because("an envelope that declares none renders a prompt byte for byte "
                   + "unchanged, and a synthesized empty block would be a policy "
                   + "nobody wrote.");
    }

    [Test]
    public async Task Nothing_the_runner_reports_carries_an_instruction()
    {
        // S30.2-06, AND IT ONLY BECAME ASSERTABLE WITH THE LINE ABOVE. Until the
        // runner carried the text there was no path for it to leak down, so a
        // test written earlier would have passed against a feature that did not
        // run - which is the failure mode this whole pair is about.
        var (_, protocol) = await FlyAsync(Standing);

        // LIVENESS FIRST. An absence over an empty collection is not a finding.
        await Assert.That(protocol.ShippedFacts).IsNotEmpty()
            .Because("with no facts shipped this asserts nothing.");
        await Assert.That(protocol.Serialized).IsNotEmpty();

        // EVERYTHING THE RUNNER SENDS, as bytes, rather than the payload members
        // somebody thought to look at. An instruction is configuration the agent
        // READ; a fact is something the flight MEASURED. Carrying the first back
        // as the second would put an operator's standing policy into an evidence
        // budget and into the digest a reviewer reads as findings.
        foreach (var body in protocol.Serialized)
        {
            await Assert.That(body).DoesNotContain("Prefer the smallest change")
                .Because("standing instructions are configuration the agent was given, "
                       + "not something this flight measured.");
            await Assert.That(body).DoesNotContain("Name the ticket in the branch");
        }
    }
}
