using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// What the loop does when a beat carries an offer: notices, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>It never applies one, and that is the design rather than a shortcut.</b>
/// Labels, hold, relays and executor are read once at startup into locals handed
/// to this loop, so a file written mid-run would change nothing until the
/// process restarted anyway — and making them mutable instead would let a flight
/// begin under one configuration and land under another. So the loop reports,
/// the composition root decides, and the next startup applies.
/// </para>
/// <para>
/// <b>Only on an idle beat.</b> The loop beats while holding somebody's flight
/// too. Reporting an offer then would invite a caller to end a process that is
/// halfway through work, and the runner's whole bargain is that a lease it holds
/// is finished or explicitly released.
/// </para>
/// <para>
/// <b>The caller decides whether to stop, and the hazard is a restart loop.</b>
/// A runner that stopped for any offer would meet a directed one it may never
/// take, exit, boot, meet it again, and exit — for ever. The root asks
/// <c>OfferedAtStartup.Decide</c> whether a restart would actually write
/// anything, and stops only then, which makes "worth restarting for" the same
/// question as "would be taken".
/// </para>
/// </remarks>
public class TheLoopNoticesAnOfferAndStopsTests
{
    private static OfferedConfiguration AnOffer(string version = "offer@7") => new()
    {
        Version = version,
        OfferedAt = DateTimeOffset.UnixEpoch,
        Settings = [new OfferedSetting { Key = "stun-servers", Value = "stun:relay.invalid:3478" }],
    };

    [Test]
    public async Task An_idle_beat_carrying_one_reports_it()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol { Offered = AnOffer() };

        var seen = new List<OfferedConfiguration>();

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                offered: seen.Add)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(seen.Select(o => o.Version)).Contains("offer@7")
            .Because("the root cannot see the beat, so an offer that reached the loop and "
                   + "stopped there would leave a fleet configured by hand for ever.");
    }

    [Test]
    public async Task A_beat_carrying_nothing_reports_nothing()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();

        var seen = new List<OfferedConfiguration>();

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                offered: seen.Add)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(seen).IsEmpty()
            .Because("absent means nothing is offered, and a caller told about it on every "
                   + "beat would learn nothing from being told at all.");
    }

    [Test]
    public async Task A_loop_wired_without_a_reporter_still_runs()
    {
        // THE DEFAULT PATH, and every existing caller is on it. A loop that
        // required the delegate would make the beat's new job load-bearing for
        // machines nobody has wired it on.
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol { Offered = AnOffer() };

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace())
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Heartbeats).IsGreaterThan(0)
            .Because("it beat, and nothing threw for want of somewhere to report to.");
    }
}
