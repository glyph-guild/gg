using Gg.Contracts;
using Gg.Runner;
using Gg.Contracts.Description;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A repository the control plane could not name a credential for stops the
/// flight before a byte of it is fetched.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this closes predates the lease request and is silent.</b>
/// <c>Workspace.PrepareAsync</c> called <c>TryGetValue</c> on the resolved
/// secrets and never consulted the result: a repository with no credential got
/// <c>null</c>, <c>GitInvocation</c> configured no helper, and the fetch went out
/// ANONYMOUSLY. A public repository succeeded. A private one failed later, at
/// git's own words - <c>git exited 128</c> - with nothing in the flight's record
/// pointing at the missing credential.
/// </para>
/// <para>
/// <b>The push path has always done this correctly</b>, refusing up front with a
/// precise sentence when it has no credential for the destination. This is the
/// same check on the way in, made possible by the control plane finally SAYING
/// which repositories it could not resolve rather than sending a shorter list
/// and leaving the runner to infer it.
/// </para>
/// <para>
/// <b>Consequence, deliberately taken:</b> a public repository with no
/// registered credential is now refused rather than cloned anonymously. Absence
/// cannot keep meaning both "needs none" and "nobody registered one" - that
/// ambiguity IS the bug - so a repository that needs no credential is something
/// somebody says, not something inferred from silence.
/// </para>
/// </remarks>
public class UnresolvedRepoTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Records whether anything was ever asked to be put on disk.</summary>
    private sealed class RecordingWorkspace : IWorkspace
    {
        internal int Prepares { get; private set; }

        public Task<WorkspaceResult> PrepareAsync(
            string flightId,
            IReadOnlyList<LeaseRepoRef> repos,
            IReadOnlyDictionary<string, string> secretsByLocator,
            CancellationToken cancellationToken = default)
        {
            Prepares++;
            return Task.FromResult(new WorkspaceResult([]) { Reused = false });
        }

        public void Release(string flightId) { }

        public HeldTree? Hold(string flightId) => null;

        public int SweepOrphans() => 0;
    }

    private static LeaseGranted ALeaseNaming(params string[] unresolved) => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
        Repos = [new LeaseRepoRef { Provider = "local", Slug = "acme/widgets", PinnedRef = "main" }],
        Credentials = [],
        UnresolvedRepos = unresolved,
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
    };

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer, IWorkspace workspace) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer, new NoCredentialResolver(), workspace)
        {
            HoldFor = TimeSpan.FromSeconds(1),
        };

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

    [Test]
    public async Task An_unresolved_repository_is_refused_before_anything_is_fetched()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNaming("acme/widgets")));
        var observer = new RecordingObserver();
        var workspace = new RecordingWorkspace();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer, workspace).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(workspace.Prepares).IsEqualTo(0)
            .Because("refusing AFTER the clone would have already put a customer's code on our "
                   + "disk to no purpose, and refusing after a FAILED clone is the git exited 128 "
                   + "this replaces.");

        var reported = observer.Events.Single(e => e.StartsWith("workspace:", StringComparison.Ordinal));
        await Assert.That(reported).Contains("acme/widgets")
            .Because("the repository by name. A count says something is wrong; a name says which "
                   + "credential to register, which is the only action available to whoever reads "
                   + "this.");

        await Assert.That(observer.Events).Contains("released:failed")
            .Because("the lease goes straight back rather than being held to expiry - a runner "
                   + "sitting on work it cannot do is the stalled flight wearing a timer.");
    }

    [Test]
    public async Task A_lease_that_resolved_everything_says_nothing_and_proceeds()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseNaming()));
        var observer = new RecordingObserver();
        var workspace = new RecordingWorkspace();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, workspace).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(workspace.Prepares).IsGreaterThanOrEqualTo(1)
            .Because("the ordinary flight is untouched by this check, which is the half of a guard "
                   + "that is easy to leave unasserted and expensive to get wrong.");
        await Assert.That(observer.Events.Any(e => e.StartsWith("workspace:", StringComparison.Ordinal)))
            .IsFalse();
    }
}
