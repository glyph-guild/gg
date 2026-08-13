using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The loop's order, end to end, with real git underneath.
/// </summary>
/// <remarks>
/// The runner rule spells it out: <i>lease → resolve credentials → materialize
/// → extract facts → compute DIGEST → apply FILTER → emit.</i> The middle of
/// that is now real, and this is where the whole sequence is observed rather
/// than each stage in isolation.
/// </remarks>
public class MaterializeLoopTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static LeaseGranted ALeaseFor(GitFixture fixture, string pinnedRef, params CredentialReference[] credentials)
        => new()
        {
            LeaseId = "lease-1",
            Generation = 1,
            FlightId = "flight-1",
            FlightNumber = FlightRef.Format(1042),
            Repos =
            [
                new LeaseRepoRef
                {
                    Provider = LocalVcsAdapter.ProviderKey,
                    Slug = fixture.BarePath,
                    PinnedRef = pinnedRef,
                },
            ],
            Credentials = credentials,
            ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
            ExpiresAt = T0.AddMinutes(10),
            RenewWithinSeconds = 5,
        };

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer,
        ICredentialResolver resolver, IWorkspace workspace) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer, resolver, workspace)
        {
            HoldFor = TimeSpan.FromSeconds(3),
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
    public async Task A_flight_materializes_its_repository_and_ships_facts_about_it()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, "refs/heads/main")));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        var shipped = protocol.ShippedFacts.SelectMany(b => b.Items).ToList();

        await Assert.That(shipped.Select(f => f.Kind)).Contains(FactKinds.EnvironmentIdentity);
        await Assert.That(shipped.Select(f => f.Kind)).Contains(FactKinds.SourceProvenance);

        var provenance = shipped.Single(f => f.Kind == FactKinds.SourceProvenance).Source!;
        await Assert.That(provenance.HeadCommit).IsEqualTo(fixture.BranchCommit);
    }

    [Test]
    public async Task A_pull_request_flight_ships_provenance_naming_the_fork()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(
            ALeaseFor(fixture, $"refs/pull/{GitFixture.PullNumber}/head")));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        var provenance = protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Single(f => f.Kind == FactKinds.SourceProvenance).Source!;

        await Assert.That(provenance.HeadIsFork).IsTrue();
        await Assert.That(provenance.ForkSlug).IsEqualTo(GitFixture.ForkSlug);
        await Assert.That(provenance.HeadCommit).IsEqualTo(fixture.ForkHeadCommit);
    }

    [Test]
    public async Task No_file_content_reaches_anything_the_runner_sends()
    {
        // The negative claim, at the point where there is finally something
        // real to leak. The fixture's files carry distinctive strings and the
        // clone genuinely puts them on disk; what leaves is paths, counts and
        // hashes.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(
            ALeaseFor(fixture, $"refs/pull/{GitFixture.PullNumber}/head")));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        var sent = string.Join("\n", protocol.Serialized);

        await Assert.That(sent).DoesNotContain(GitFixture.ForkMarker);
        await Assert.That(sent).DoesNotContain(GitFixture.BranchMarker);

        // The twin, through the real path: the markers really were on disk, put
        // there by the actual clone. Without this the absence above is also
        // what a materialize that fetched nothing would produce.
        await Assert.That(protocol.Serialized).IsNotEmpty();
        await Assert.That(sent).Contains(fixture.ForkHeadCommit)
            .Because("the runner did examine that commit, and said so - so its silence about the "
                   + "contents is silence about the contents.");
    }

    /// <summary>
    /// The working root is empty afterwards, whatever happened to the flight.
    /// </summary>
    /// <remarks>
    /// <b>Amended, and the property it protects is unchanged.</b> Slice one said
    /// the tree is gone once the flight is released, and the reason was that the
    /// startup sweep can be stateless: a runner that is starting holds no lease,
    /// so every tree under THIS root belongs to a process that is gone.
    /// </remarks>
    [Test]
    public async Task The_working_root_is_empty_once_the_flight_is_released()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var handoff = new ScratchHandoffRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture, "refs/heads/main")));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new NoCredentialResolver(),
                new Workspace(new LocalVcsAdapter(fixture.Directory), trees.Root, handoff.Root))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(Directory.EnumerateDirectories(trees.Root.Path)).IsEmpty()
            .Because("the sweep's rule is 'all of them', and a tree left here would make that rule "
                   + "wrong about which trees are live.");

        // This flight did not land, so its work exists nowhere else and it is
        // kept - in a root of its own, which the sweep never looks at.
        await Assert.That(handoff.Root.Held().Count).IsEqualTo(1)
            .Because("a customer's source code is on our disk for as long as the flight needs it, "
                   + "and a flight nobody can take over needs it until somebody has.");
    }

    [Test]
    public async Task A_repository_the_adapter_cannot_serve_ends_the_flight_with_a_diagnosis()
    {
        // A declared capability gap, arriving as a refusal rather than as a
        // clone that fails somewhere in the network stack.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(
            ALeaseFor(fixture, $"refs/pull/{GitFixture.PullNumber}/head") with
            {
                Repos =
                [
                    new LeaseRepoRef
                    {
                        Provider = "nopr",
                        Slug = fixture.BarePath,
                        PinnedRef = $"refs/pull/{GitFixture.PullNumber}/head",
                    },
                ],
            }));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new NoCredentialResolver(),
                trees.Workspace(new NoPullRequestsAdapter()))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Calls.Any(c => c.Contains("failed", StringComparison.Ordinal))).IsTrue()
            .Because("the flight cannot proceed and must say so rather than stall.");
        await Assert.That(observer.Events.Any(e => e.StartsWith("workspace:", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task A_lease_with_no_repositories_materializes_nothing_and_still_ships_the_environment()
    {
        // Nothing has repos until a flight names one, and a runner that refused
        // a flight for a repository nobody asked for would be Article XI
        // pointed at itself. The environment fact is about the machine, so it
        // is produced either way.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddMinutes(10))));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        var shipped = protocol.ShippedFacts.SelectMany(b => b.Items).ToList();

        await Assert.That(shipped.Select(f => f.Kind)).Contains(FactKinds.EnvironmentIdentity);
        await Assert.That(shipped.Any(f => f.Kind == FactKinds.SourceProvenance)).IsFalse();
    }
}
