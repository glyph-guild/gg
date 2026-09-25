using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine brings its slot up when it learns of one, and brings it up once.
/// </summary>
/// <remarks>
/// <para>
/// <b>The daemon belongs where the served app is, and it is established when
/// the machine is</b> — the owner's decision, 2026-09-24. A connector spawned
/// mid-flight is what left GG-268's tunnel serving JDX+ to the open internet
/// for twenty-four hours after its flight ended, because the agent had started
/// it with <c>setsid nohup</c> and nothing in gg owned the process.
/// </para>
/// <para>
/// <b>On the beat, because a machine already makes one.</b> The response that
/// carries an offer and a revocation is the response that carries this. Nothing
/// connects inbound to a machine and nothing is going to; a route of its own
/// would be a second way to reach one, which is the channel this design exists
/// to avoid.
/// </para>
/// <para>
/// <b>Once, and that assertion is the load-bearing one.</b> Two connectors on
/// one token are replicas of one tunnel. Measured on 2026-09-24: thirty
/// requests to one hostname all went to the first replica, and killing it moved
/// every request to the second with no configuration change anywhere. So a
/// machine that re-dialled on every beat would be racing itself, and a machine
/// that re-dialled after a grant it already holds would serve another flight's
/// address.
/// </para>
/// </remarks>
public class AMachineBringsUpItsSlotTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private const string Locator = "keyvault://ggdev.vault.example/jdapp-03";

    private static LeasePreview Granted() => new()
    {
        Exposure = "jdapp",
        Slot = 3,
        Hostname = "jdapp-03.goodgrief.dev",
        Credential = Locator,
        Port = 8080,
    };

    private sealed class CountingConnector : IExposureConnector
    {
        internal int Dials { get; private set; }

        internal string? Token { get; private set; }

        internal int? Port { get; private set; }

        public Task<string?> RunAsync(string token, int? port, CancellationToken cancellationToken)
        {
            Dials++;
            Token = token;
            Port = port;
            return Task.FromResult<string?>(null);
        }
    }

    private sealed record Beaten(CountingConnector Connector, List<string> Asked, int Beats);

    /// <summary>Runs the loop until it has beaten <paramref name="beats"/> times.</summary>
    private static async Task<Beaten> BeatingAsync(int beats, LeasePreview? granted)
    {
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol { Preview = granted, HeartbeatSeconds = 1 };
        var observer = new RecordingObserver();
        var connector = new CountingConnector();
        var asked = new List<string>();

        using var stopping = new CancellationTokenSource();

        await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    if (protocol.Heartbeats >= beats)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                observer, new NoCredentialResolver(),
                trees.Workspace(new Vcs.LocalVcsAdapter(Path.GetTempPath())),
                secretFor: locator =>
                {
                    asked.Add(locator);
                    return string.Equals(locator, Locator, StringComparison.Ordinal)
                        ? "not-a-real-tunnel-token"
                        : null;
                },
                connector: connector)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return new Beaten(connector, asked, protocol.Heartbeats);
    }

    [Test]
    public async Task It_dials_the_slot_the_beat_granted_it()
    {
        var beaten = await BeatingAsync(beats: 2, Granted());

        await Assert.That(beaten.Connector.Dials).IsGreaterThan(0)
            .Because("the daemon belongs where the served app is, established when the machine "
                   + "is - so learning of a slot is what brings it up, not a flight landing.");
        await Assert.That(beaten.Connector.Token).IsEqualTo("not-a-real-tunnel-token");
        await Assert.That(beaten.Connector.Port).IsEqualTo(8080);
    }

    [Test]
    public async Task It_brings_the_same_slot_up_only_once()
    {
        var beaten = await BeatingAsync(beats: 5, Granted());

        await Assert.That(beaten.Connector.Dials).IsEqualTo(1)
            .Because("two connectors on one token are replicas of one tunnel. Measured: thirty "
                   + "requests to one hostname all went to the first, and killing it moved "
                   + $"every request to the second. Beat {beaten.Beats} times and dialled "
                   + $"{beaten.Connector.Dials}.");
    }

    [Test]
    public async Task A_machine_granted_no_slot_dials_nothing()
    {
        var beaten = await BeatingAsync(beats: 3, granted: null);

        await Assert.That(beaten.Connector.Dials).IsEqualTo(0)
            .Because("every tenant today has declared no exposure, and a machine told of no "
                   + "slot must behave exactly as it did before this existed.");
        await Assert.That(beaten.Asked).IsEmpty()
            .Because("and it must not go looking for a credential nobody named.");
    }
}
