using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Exposures;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A slot's credential is resolved on the machine, by the locator the tenant's
/// document named — not from the lease's registered credentials.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes a preview possible from a pool member at all.</b> A
/// member's credential store starts empty and there is no automatic path that
/// fills it: the container create body is three environment variables asserted
/// byte for byte, the member spec has no field a secret could sit in, nonce
/// redemption returns no secret, the control plane holds references rather than
/// values, and the heartbeat's only credential verb is <i>forget</i>. The one
/// door demands a developer session that a machine cannot hold, and anything
/// placed by hand dies with the container.
/// </para>
/// <para>
/// <b>So the secret is not delivered — it is READ, by whatever this machine can
/// read.</b> <c>MachineCredentialStore</c> already routes by scheme: a
/// <c>keyvault://</c> reference goes to the vault this machine's managed
/// identity may read, and everything else goes to its own files. A member
/// inherits its host's identity, so a vault reference needs no delivery, no
/// fourth environment variable, and nothing on the inspectable surface of the
/// container. A resident runner keeps using <c>local:</c> exactly as it did.
/// </para>
/// <para>
/// <b>Why it is by LOCATOR and not by reference.</b> An exposure's credential is
/// named by a tenant document, not registered through
/// <c>gg credential add</c> — so it never appears in <c>lease.Credentials</c>,
/// and the map built from those references can never contain it. That is the
/// defect this pins: the grant arrives, the address is granted, and the lookup
/// misses every time.
/// </para>
/// </remarks>
public class ASlotsCredentialResolvesOnTheMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The locator an exposure document named, in a vault.</summary>
    private const string Locator = "keyvault://ggdev.vault.azure.net/jdapp-01";

    private const string Token = "not-a-real-tunnel-token";

    private sealed class QuietExecutor : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExecutorRun?>(ExecutorRun.Exhausted(
                request.LoopId, request.WallClock, [LoopMoves.Read]));
    }

    /// <summary>A connector that records what it was dialled with and starts nothing.</summary>
    private sealed class RecordingConnector : IExposureConnector
    {
        internal string? Token { get; private set; }

        public Task<string?> RunAsync(string token, CancellationToken cancellationToken)
        {
            Token = token;
            return Task.FromResult<string?>(null);
        }
    }

    private static LeaseGranted ALease(GitFixture fixture) => new()
    {
        LeaseId = "lease-preview",
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

        // EMPTY, AND THAT IS THE POINT. An exposure's credential is named by a
        // tenant document rather than registered, so it is never one of these -
        // and a runner reading only these can never find it.
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        Preview = new LeasePreview
        {
            Exposure = "jdapp",
            Slot = 1,
            Hostname = "jdapp-01.goodgrief.dev",
            Credential = Locator,
        },
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 600,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private sealed record Flown(
        List<string> Asked, RecordingConnector Connector, FakeProtocol Protocol);

    private static async Task<Flown> FlyAsync()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture)));
        var observer = new RecordingObserver();

        var asked = new List<string>();
        var connector = new RecordingConnector();

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
                executor: new QuietExecutor(),
                secretFor: locator =>
                {
                    asked.Add(locator);
                    return string.Equals(locator, Locator, StringComparison.Ordinal)
                        ? Token
                        : null;
                },
                connector: connector)
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return new Flown(asked, connector, protocol);
    }

    [Test]
    public async Task It_asks_this_machine_for_the_locator_the_document_named()
    {
        var flown = await FlyAsync();

        await Assert.That(flown.Asked).Contains(Locator)
            .Because("the locator is a tenant document's, not a registration's, so the only "
                   + "way a runner can have the secret is to ask its own machine for that "
                   + "exact name - which is also what lets a vault answer.");
    }

    [Test]
    public async Task It_dials_the_connector_with_what_the_machine_answered()
    {
        var flown = await FlyAsync();

        await Assert.That(flown.Connector.Token).IsEqualTo(Token)
            .Because("a credential and nothing else. Resolving it and then not using it is "
                   + "the same silence as never resolving it: no address, no fact, and a "
                   + "gate that says the preview is gone.");
    }

    [Test]
    public async Task The_granted_address_is_shipped_as_a_fact()
    {
        var flown = await FlyAsync();

        var preview = flown.Protocol.ShippedFacts
            .SelectMany(b => b.Items)
            .SingleOrDefault(f => f.Kind == FactKinds.PreviewUrl);

        await Assert.That(preview).IsNotNull()
            .Because("the whole point of resolving it is that a person is handed somewhere to "
                   + "look, and the fact is the only thing that carries the address off this "
                   + "machine.");
        await Assert.That(preview!.Preview!.Url).IsEqualTo("https://jdapp-01.goodgrief.dev")
            .Because("built from the grant and never read back out of a connector's output, "
                   + "which is how GG-268 came to serve at a name nobody had registered.");
    }
}
