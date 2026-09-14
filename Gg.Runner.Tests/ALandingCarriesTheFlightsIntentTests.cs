using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// What the runner hands a destination, measured where it is handed over.
/// </summary>
/// <remarks>
/// <b>The adapter's own tests prove it posts what it is given.</b> This one
/// proves it is given the right thing, which is the half that was wrong: the
/// intent never reached <c>LandingRequest</c> at all, and the title was composed
/// from the admission while the loop's outcome sat unread in the same method's
/// caller.
/// </remarks>
public class ALandingCarriesTheFlightsIntentTests
{
    private sealed class DidTheWork : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExecutorRun?>(ExecutorRun.Completed(
                "implement",
                "Removed the last explicitly-typed local in the unit tests. It came in with a "
              + "later change, after the sweep.",
                7, TimeSpan.FromMinutes(4), [LoopMoves.Edit]));
    }

    private static readonly DateTimeOffset T0 = new(2026, 9, 14, 23, 0, 0, TimeSpan.Zero);

    private static LeaseGranted ALease(GitFixture fixture) => new()
    {
        LeaseId = "lease-intent",
        Generation = 1,
        FlightId = Guid.NewGuid().ToString(),
        FlightNumber = "GG-118",
        Repos =
        [
            new LeaseRepoRef
            {
                Provider = AuthenticatingProvider.Key,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials =
        [
            new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.ForRepo(fixture.BarePath),
                Identity = "gg-fixture",
                Scopes = [CredentialScopes.Write],
            },
        ],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,

        // THE TICKET THE FLIGHT WAS OPENED FROM, which is what `gg fly --ticket
        // ado#18490` puts here and what a reviewer wants a link back to.
        IntentProvider = "ado",
        IntentId = "18490",
        IntentUri = "https://dev.azure.invalid/acme/_workitems/edit/18490",

        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = "fixture-agent",
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 60,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private static async Task<RecordingDestination> FlownAsync()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);

        var resolver = new ScriptedResolver();
        resolver.Secrets[CredentialLocator.ForRepo(fixture.BarePath)] = "a-secret";

        var protocol = new FakeProtocol
        {
            Push = new BranchPush
            {
                Branch = "gg/GG-118",
                BaseRef = "main",
                Slug = fixture.BarePath,
                Reason = "no machine obligation is violated",
            },
            Admission = new DestinationAdmission
            {
                DestinationId = "pull-request",
                Branch = "gg/GG-118",
                BaseRef = "main",
                Slug = fixture.BarePath,
                Reason = "Destination 'pull-request' requires 'in-scope', and it holds.",
            },
        };
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture)));

        var observer = new RecordingObserver();
        var destination = new RecordingDestination();
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
                observer, resolver,
                trees.Workspace(new AuthenticatingProvider(new LocalVcsAdapter(fixture.Directory))),
                executor: new DidTheWork(),
                destinations: [destination])
        {
            HoldFor = TimeSpan.FromSeconds(3),
        }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return destination;
    }

    [Test]
    public async Task The_proposal_is_told_which_work_item_it_answers()
    {
        var destination = await FlownAsync();

        var proposed = destination.Asked.LastOrDefault();

        await Assert.That(proposed).IsNotNull()
            .Because("nothing was proposed at all: " + string.Join(", ", destination.Calls));

        await Assert.That(proposed!.Intent?.Provider).IsEqualTo("ado");
        await Assert.That(proposed.Intent?.Id).IsEqualTo("18490");
    }

    [Test]
    public async Task The_title_is_the_agents_account_and_not_the_admissions()
    {
        var destination = await FlownAsync();

        var proposed = destination.Asked.Last();

        await Assert.That(proposed.Title)
            .IsEqualTo("GG-118: Removed the last explicitly-typed local in the unit tests.");

        await Assert.That(proposed.Title).DoesNotContain("requires")
            .Because("an obligation verdict answers why this was allowed to land. It is written "
                   + "for an audit trail and reads as nonsense on a list of changes.");
    }
}
