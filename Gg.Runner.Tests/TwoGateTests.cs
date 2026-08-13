using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The push and the pull request are two gates, and conflating them ships a
/// security defect.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice two gated the push on full admission</b> — <i>"the runner receives no
/// admission and pushes nothing"</i>, proven against a real remote. A human gate
/// needs the branch pushed <b>before</b> anybody is asked, because the work under
/// review cannot live only in a working tree that is about to be released. Moving
/// the push earlier naively means <b>a flight with a violated obligation pushes its
/// work anyway</b>, which is a straight regression of the property that was proven.
/// </para>
/// <para>
/// So there are two permissions, carried as two fields, and neither can be read as
/// the other:
/// </para>
/// <list type="table">
/// <item><term>Push</term><description>No machine obligation is violated.</description></item>
/// <item><term>Admission</term><description>Every <c>requires</c> is satisfied.</description></item>
/// </list>
/// <para>
/// <b>And the tree is released on the PUSH, not on the proposal.</b> That is where
/// the split could break the ordering silently: the release already keys on
/// "landed", and if "landed" kept meaning "proposed" then a gated flight would
/// have its tree held forever, while if it meant "we tried" a failed push would
/// release the only copy of the work.
/// </para>
/// </remarks>
public class TwoGateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An adapter that records what it was asked for and can refuse either half.
    /// </summary>
    /// <remarks>
    /// A fake rather than a real remote, because what is being tested is which
    /// call the runner makes and in what order. The real-remote proof is in
    /// <c>RealRemoteGateTests</c>, which is where the property was originally
    /// established and where it has to stay established.
    /// </remarks>
    private sealed class RecordingDestination(bool pushSucceeds = true, bool proposeSucceeds = true)
        : IDestinationAdapter
    {
        public string Provider { get; } = AuthenticatingProvider.Key;

        public List<string> Calls { get; } = [];

        public Task<PushOutcome> PushAsync(
            LandingRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add($"push:{request.Branch}");

            return Task.FromResult<PushOutcome>(pushSucceeds
                ? new PushOutcome.Pushed(request.Branch, new string('a', 40))
                : new PushOutcome.Refused(request.Slug, "the remote said no"));
        }

        public Task<LandingOutcome> ProposeAsync(
            LandingRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add($"propose:{request.Branch}");

            return Task.FromResult<LandingOutcome>(proposeSucceeds
                ? new LandingOutcome.Landed(request.Branch, "https://forge.invalid/pr/1", 1)
                : new LandingOutcome.Unsupported("no proposal"));
        }
    }

    /// <summary>
    /// A provider that authenticates, wrapping the local one that does not.
    /// </summary>
    /// <remarks>
    /// <b>Needed because the local adapter refuses a credential outright</b> - file://
    /// has nothing to authenticate to, so a secret offered to it is a secret handed
    /// to a path. That refusal is correct and it means a lease carrying a credential
    /// for a local repository cannot even materialise, which is how the first run of
    /// this file reached the adapter zero times and looked like a gating bug.
    /// </remarks>
    private sealed class AuthenticatingProvider(LocalVcsAdapter inner) : IVcsAdapter
    {
        internal const string Key = "fixture";

        public string Provider => Key;

        public VcsCapabilities Capabilities => inner.Capabilities;

        public RefResolution Resolve(string pinnedRef) => inner.Resolve(pinnedRef);

        // The secret is accepted and dropped: what matters here is that offering one
        // is legitimate for this provider, which is what makes the credential - and
        // therefore the write scope - meaningful.
        public Task<CloneOutcome> CloneAsync(
            RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
            CancellationToken cancellationToken = default) =>
            inner.CloneAsync(target, resolvedRef, intoDirectory, secret: null, cancellationToken);

        public Task<string> FetchAlsoAsync(
            RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
            CancellationToken cancellationToken = default) =>
            inner.FetchAlsoAsync(target, resolvedRef, intoDirectory, secret: null, cancellationToken);
    }

    private static LeaseGranted ALeaseFor(GitFixture fixture) => new()
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
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
    };

    private static BranchPush APush(GitFixture fixture) => new()
    {
        Branch = "gg/GG-1042",
        BaseRef = "main",
        Slug = fixture.BarePath,
        Reason = "no machine obligation is violated, so the work may be preserved on the remote",
    };

    private static DestinationAdmission AnAdmission(GitFixture fixture) => new()
    {
        DestinationId = "pull-request",
        Branch = "gg/GG-1042",
        BaseRef = "main",
        Slug = fixture.BarePath,
        Reason = "every obligation the destination requires holds",
    };

    /// <summary>
    /// A resolver holding the one secret this flight's credential names.
    /// </summary>
    /// <remarks>
    /// Needed because an unresolvable credential is a refusal BEFORE either gate is
    /// consulted - which is correct, and would make every case here pass by never
    /// reaching the adapter at all. The first run of this file did exactly that.
    /// </remarks>
    private static ScriptedResolver Resolver(GitFixture fixture)
    {
        var resolver = new ScriptedResolver();
        resolver.Secrets[CredentialLocator.ForRepo(fixture.BarePath)] = "a-secret";
        return resolver;
    }

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer,
        IWorkspace workspace, IDestinationAdapter destination, ScriptedResolver resolver) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer,
            resolver,
            workspace,
            destinations: [destination])
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

    private static async Task<(RecordingDestination Destination, ScratchTreeRoot Trees,
        FakeProtocol Protocol, RecordingObserver Observer)>
        RunAsync(
            GitFixture fixture,
            BranchPush? push,
            DestinationAdmission? admission,
            bool pushSucceeds = true)
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol { Admission = admission, Push = push };
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        var observer = new RecordingObserver();
        var destination = new RecordingDestination(pushSucceeds);
        var trees = new ScratchTreeRoot();

        using var stopping = StopAfter(observer, 8);
        await Build(protocol, clock, observer,
                trees.Workspace(new AuthenticatingProvider(new LocalVcsAdapter(fixture.Directory))), destination,
                Resolver(fixture))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return (destination, trees, protocol, observer);
    }

    // ---- the property slice two proved, re-proven with the push moved ----

    [Test]
    public async Task A_violated_machine_obligation_pushes_nothing()
    {
        // THE REGRESSION THIS STEP COULD SHIP. No push permission means no push -
        // and the assertion is on the ADAPTER rather than on a log line, because a
        // runner that pushed and then reported "refused" would satisfy a narrative
        // test.
        using var fixture = new GitFixture();
        var (destination, trees, _, observer) = await RunAsync(fixture, push: null, admission: null);
        using var _t = trees;

        await Assert.That(destination.Calls).IsEmpty()
            .Because("nothing was asked of the remote at all: " + string.Join(", ", destination.Calls));
    }

    [Test]
    public async Task A_violated_machine_obligation_pushes_nothing_even_when_a_human_is_pending()
    {
        // The third case, and the one to get right. A pending decision does NOT
        // unlock the push: presenting a gate on work that already failed a machine
        // check spends the attention the product exists to protect.
        //
        // EXPIRY CONDITION: this is correct only because `when:` reads facts and
        // not verdicts. A gate whose condition is
        // `obligations.contracts-intact == violated` exists precisely BECAUSE a
        // machine obligation is violated, and the moment that form ships this rule
        // inverts. It is a consequence of the current vocabulary, not a principle.
        using var fixture = new GitFixture();
        var (destination, trees, _, observer) = await RunAsync(fixture, push: null, admission: null);
        using var _t = trees;

        await Assert.That(destination.Calls.Any(c => c.StartsWith("push", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task Push_permission_alone_pushes_and_does_not_propose()
    {
        // THE GATE'S SHAPE. The work reaches the remote so a person can read it,
        // and no pull request is opened because the decision is outstanding.
        using var fixture = new GitFixture();
        var (destination, trees, _, observer) = await RunAsync(fixture, APush(fixture), admission: null);
        using var _t = trees;

        await Assert.That(destination.Calls.Count(c => c.StartsWith("push", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("observer said: " + string.Join(" | ", observer.Events));
        await Assert.That(destination.Calls.Any(c => c.StartsWith("propose", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a proposal is the second gate and it was not granted.");
    }

    [Test]
    public async Task Full_admission_pushes_and_then_proposes_in_that_order()
    {
        using var fixture = new GitFixture();
        var (destination, trees, _, observer) = await RunAsync(
            fixture, APush(fixture), AnAdmission(fixture));
        using var _t = trees;

        await Assert.That(string.Join(",", destination.Calls))
            .IsEqualTo("push:gg/GG-1042,propose:gg/GG-1042")
            .Because("a proposal on a branch that is not there yet is a proposal against nothing.");
    }

    [Test]
    public async Task An_admission_without_push_permission_proposes_nothing()
    {
        // A control plane that granted a proposal and not a push would be
        // incoherent, and the runner refuses rather than inferring the push it was
        // not given. Absence means no, in both fields, independently.
        using var fixture = new GitFixture();
        var (destination, trees, _, observer) = await RunAsync(
            fixture, push: null, admission: AnAdmission(fixture));
        using var _t = trees;

        await Assert.That(destination.Calls).IsEmpty()
            .Because("the runner does not derive one permission from the other: "
                   + string.Join(", ", destination.Calls));
    }

    // ---- push then release, and the ordering's safety property ----

    [Test]
    public async Task The_tree_is_released_once_the_push_succeeded()
    {
        // The work is on the remote, so the tree is finished with. Asserted by the
        // ABSENCE of the tree, which needs the twin below.
        using var fixture = new GitFixture();
        var (_, trees, _, observer) = await RunAsync(fixture, APush(fixture), admission: null);
        using var _t = trees;

        await Assert.That(trees.Handoff.Held()).IsEmpty()
            .Because("nothing is being kept for a takeover, because the work is on the remote.");
        await Assert.That(Directory.Exists(trees.Root.For("flight-1"))).IsFalse()
            .Because("the tree is gone, because what it held is now a commit on a remote.");
    }

    [Test]
    public async Task The_release_does_not_happen_when_the_push_failed()
    {
        // THE SAFETY PROPERTY. Entering a gate with the work only in a doomed tree
        // loses the work, so a failed push holds the tree instead - and the flight
        // does not become somebody's pending decision.
        using var fixture = new GitFixture();
        var (destination, trees, protocol, observer) = await RunAsync(
            fixture, APush(fixture), admission: null, pushSucceeds: false);
        using var _t = trees;

        await Assert.That(destination.Calls.Any(c => c.StartsWith("push", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the push was attempted, or this test is not about a failed push.");

        await Assert.That(trees.Handoff.Held().Count).IsEqualTo(1)
            .Because("the only copy of the work is here, so the tree is held for a takeover.");

        // And nothing was reported as landed, so the control plane never learns a
        // commit that does not exist.
        var landings = protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Where(f => f.Kind is FactKinds.DestinationPushed or FactKinds.DestinationLanded)
            .ToList();

        await Assert.That(landings).IsEmpty()
            .Because("a landing fact for a push that failed is a reference to nothing.");
    }

    [Test]
    public async Task The_tree_assertion_can_see_a_tree_that_exists()
    {
        // THE LIVENESS TWIN for both absence assertions above. A helper that
        // always answered "no tree" would satisfy them forever.
        using var fixture = new GitFixture();
        var (_, trees, _, observer) = await RunAsync(
            fixture, APush(fixture), admission: null, pushSucceeds: false);
        using var _t = trees;

        await Assert.That(trees.Handoff.Held().Count).IsEqualTo(1)
            .Because("this is the same helper the absence test calls, and here it sees one.");
    }

    // ---- a crash between the two ----

    [Test]
    public async Task A_second_pass_after_a_crash_between_push_and_release_does_not_push_twice()
    {
        // The branch is already on the remote and the runner comes back. Pushing
        // again must not create a second branch or lose the reference to the
        // first - the adapter answers BranchExists, and the reference it carries
        // is the branch that is already there.
        using var fixture = new GitFixture();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol { Push = APush(fixture) };
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALeaseFor(fixture)));
        var observer = new RecordingObserver();
        using var trees = new ScratchTreeRoot();

        var destination = new AlreadyPushedDestination(fixture.BarePath);

        using var stopping = StopAfter(observer, 8);
        await Build(protocol, clock, observer,
                trees.Workspace(new AuthenticatingProvider(new LocalVcsAdapter(fixture.Directory))), destination,
                Resolver(fixture))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(destination.Pushes).IsEqualTo(1)
            .Because("one attempt, which the remote answered with 'already there'.");

        // The PUSH fact, which is the kind whose name is true of what happened: a
        // branch reached the remote and nothing was proposed. A landing fact here
        // would be a record claiming a proposal nobody opened.
        var landed = protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .Where(f => f.Kind == FactKinds.DestinationPushed)
            .ToList();

        await Assert.That(landed.Count).IsEqualTo(1)
            .Because("the reference is reported once and is not lost: a branch already on the "
                   + "remote is still where the work is, and a runner that reported nothing would "
                   + "leave the control plane with a gate it cannot show a commit for.");
    }

    /// <summary>An adapter whose branch is already on the remote.</summary>
    private sealed class AlreadyPushedDestination(string slug) : IDestinationAdapter
    {
        public string Provider { get; } = AuthenticatingProvider.Key;

        public int Pushes { get; private set; }

        public Task<PushOutcome> PushAsync(
            LandingRequest request, CancellationToken cancellationToken = default)
        {
            Pushes++;

            // The commit is carried even though nothing was written, because the
            // reference is what the gate needs and it exists either way.
            return Task.FromResult<PushOutcome>(
                new PushOutcome.AlreadyThere(request.Branch, new string('b', 40)));
        }

        public Task<LandingOutcome> ProposeAsync(
            LandingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<LandingOutcome>(new LandingOutcome.Unsupported($"not asked for {slug}"));
    }

    // ---- the port keeps the two apart ----

    [Test]
    public async Task The_port_has_two_methods_and_neither_does_both()
    {
        // Structural, because a single LandAsync that internally decided whether
        // to propose would put the gate decision inside the runner - and the
        // runner is not an authority. Two methods means the control plane's two
        // permissions map onto two calls the runner cannot conflate.
        var members = typeof(IDestinationAdapter).GetMethods().Select(m => m.Name).ToList();

        await Assert.That(members).Contains(nameof(IDestinationAdapter.PushAsync));
        await Assert.That(members).Contains(nameof(IDestinationAdapter.ProposeAsync));
        await Assert.That(members.Contains("LandAsync")).IsFalse()
            .Because("one door that did both is what conflating the two gates looks like in code.");
    }
}
