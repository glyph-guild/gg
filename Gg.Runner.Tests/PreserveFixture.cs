using Gg.Contracts.Description;
using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// One flight, pushed and not proposed, driven through the runner's own path.
/// </summary>
/// <remarks>
/// <b>Its own fixture rather than a widened <c>TwoGateTests</c> helper.</b> That
/// file's helper builds the push it wants; what matters here is the push the
/// control plane granted - a handoff branch or an ordinary one - so the branch has
/// to be the parameter. Reaching into another test's private helper to change what
/// it decides would make both files about the other's fixture.
/// </remarks>
internal static class PreserveFixture
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Runs one flight whose push was granted and whose proposal was not.
    /// </summary>
    /// <param name="push">
    /// What the control plane cleared. Its BRANCH is the whole subject: the runner
    /// reports which kind of push it made from what it was told to push.
    /// </param>
    internal static async Task<(RecordingDestination Destination, ScratchTreeRoot Trees,
        FakeProtocol Protocol, RecordingObserver Observer)>
        RunAsync(GitFixture fixture, BranchPush push)
    {
        var clock = new MovableClock(T0);

        // ADMISSION NULL, in both cases this fixture serves. A preservation is never
        // admitted - the flight violated something - and a gated push is not admitted
        // yet. What separates them is the branch, which is the point.
        var protocol = new FakeProtocol { Admission = null, Push = push };
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture)));

        var observer = new RecordingObserver();
        var destination = new RecordingDestination();
        var trees = new ScratchTreeRoot();

        using var stopping = StopAfter(observer, 8);

        var resolver = new ScriptedResolver();
        resolver.Secrets[CredentialLocator.ForRepo(fixture.BarePath)] = "a-secret";

        var loop = new RunnerLoop(
            protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer,
            resolver,
            trees.Workspace(new AuthenticatingProvider(new LocalVcsAdapter(fixture.Directory))),
            destinations: [destination])
        {
            HoldFor = TimeSpan.FromSeconds(3),
        };

        await loop.RunAsync("runner-1", ["linux"], stopping.Token);

        return (destination, trees, protocol, observer);
    }

    private static CancellationTokenSource StopAfter(RecordingObserver observer, int events)
    {
        var stopping = new CancellationTokenSource();
        var seen = 0;
        observer.OnEvent = _ =>
        {
            if (Interlocked.Increment(ref seen) >= events)
            {
                stopping.Cancel();
            }
        };
        return stopping;
    }

    private static LeaseGranted ALease(GitFixture fixture) => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
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
                Identity = "gg-tests",
                Scopes = [CredentialScopes.Write],
            },
        ],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(1),
        RenewWithinSeconds = 5,
    };
}
