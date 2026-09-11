using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Tests;

/// <summary>
/// How a machine tells a control plane what its allowance has left.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the beat, and not IN it.</b> The heartbeat is liveness only and says
/// so — a runner able to report something about itself can report it while
/// dead. A reading is its own POST carrying its own <c>MeasuredAt</c>, so the
/// control plane can see how old it is instead of believing it, and the beat
/// is merely the cadence that happens to be running.
/// </para>
/// <para>
/// <b>Measured far less often than the beat.</b> A beat is seconds; reading an
/// allowance walks every transcript on the machine. The cadence lives in the
/// reporter, so the loop asks on every beat and is told nothing most times.
/// </para>
/// <para>
/// <b>A refusal never stands the runner down</b>, which is the heartbeat
/// guard's argument one surface along. The control plane is the authority on
/// liveness and on what a machine has spent; a machine that removed itself
/// from the fleet because a reading was refused would have turned a
/// bookkeeping route into an availability dependency.
/// </para>
/// </remarks>
public class ARunnerSaysWhatItsAllowanceHasLeftTests
{
    private static AllowanceReading AReading(string name = "kdee-max") => new()
    {
        Allowance = name,
        MeasuredAt = DateTimeOffset.UnixEpoch,
        Windows =
        [
            new() { Kind = AllowanceWindows.Session, Tokens = 1200, Since = DateTimeOffset.UnixEpoch },
        ],
    };

    [Test]
    public async Task A_beat_carries_the_reading_the_root_hands_it()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                allowance: _ => Task.FromResult<AllowanceReading?>(AReading()))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Readings.Select(r => r.Allowance)).Contains("kdee-max")
            .Because("a reading nothing posts is a measurement this machine keeps to "
                   + "itself, which is the whole of what this is for.");
    }

    [Test]
    public async Task A_machine_with_no_allowance_configured_posts_nothing()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                allowance: _ => Task.FromResult<AllowanceReading?>(null))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Readings).IsEmpty()
            .Because("an allowance nobody named is one nobody agreed to lend. Reporting "
                   + "a placeholder would put a machine into a fleet's accounting by "
                   + "default.");
    }

    [Test]
    public async Task A_loop_wired_without_a_reporter_still_runs()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace())
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Calls).Contains("heartbeat")
            .Because("every existing caller passes nothing for this, and none of them "
                   + "should have to learn about allowances to keep beating.");
        await Assert.That(protocol.Readings).IsEmpty();
    }

    [Test]
    public async Task A_refused_reading_does_not_stand_the_runner_down()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();
        protocol.AllowanceThrows.Enqueue(new HttpRequestException("the control plane is restarting"));

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                allowance: _ => Task.FromResult<AllowanceReading?>(AReading()))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Calls).Contains("heartbeat")
            .Because("the control plane is the authority on liveness and on what a "
                   + "machine has spent. A runner that removed itself from the fleet "
                   + "over a refused reading would have turned bookkeeping into an "
                   + "availability dependency.");
    }

    [Test]
    public async Task The_reporter_measures_no_more_often_than_its_cadence()
    {
        var looks = 0;
        var clock = new MovableClock(DateTimeOffset.UnixEpoch);

        var reporter = new AllowanceReporter(
            () => { looks++; return Measured(); },
            AllowanceReporter.Cadence);

        await Assert.That(await reporter.ReadAsync(clock.UtcNow)).IsNotNull();

        clock.Advance(AllowanceReporter.Cadence - TimeSpan.FromSeconds(1));
        await Assert.That(await reporter.ReadAsync(clock.UtcNow)).IsNull()
            .Because("a beat is seconds and reading an allowance walks every transcript "
                   + "on the machine. The loop asks every beat and is told nothing most "
                   + "times, which is where the cadence belongs.");

        clock.Advance(TimeSpan.FromSeconds(2));
        await Assert.That(await reporter.ReadAsync(clock.UtcNow)).IsNotNull();

        await Assert.That(looks).IsEqualTo(2)
            .Because("the throttle has to stop the WALK, not just the post. A reporter "
                   + "that measured every beat and discarded the answer would cost the "
                   + "machine exactly what the cadence exists to save.");
    }

    [Test]
    public async Task The_measurement_becomes_the_record_that_crosses()
    {
        var reporter = new AllowanceReporter(Measured, AllowanceReporter.Cadence);

        var reading = await reporter.ReadAsync(DateTimeOffset.UnixEpoch);

        await Assert.That(reading).IsNotNull();
        await Assert.That(AllowanceReading.Validate(reading!)).IsNull()
            .Because("the runner refuses its own malformed record here, where the "
                   + "diagnosis is readable, rather than collecting a 400.");
        await Assert.That(reading!.Allowance).IsEqualTo("kdee-max");
        await Assert.That(reading.Windows.Single(w => w.Kind == AllowanceWindows.Session).Tokens)
            .IsEqualTo(1200L);
        await Assert.That(reading.Windows.Single(w => w.Kind == AllowanceWindows.Session).Limit)
            .IsEqualTo(88000L);
    }

    private static MeasuredAllowance Measured() => new()
    {
        Name = "kdee-max",
        MeasuredAt = DateTimeOffset.UnixEpoch,
        Windows =
        [
            new()
            {
                Kind = AllowanceLedger.Session,
                Tokens = 1200,
                Since = DateTimeOffset.UnixEpoch,
                Limit = 88000,
            },
            new()
            {
                Kind = AllowanceLedger.Week,
                Tokens = 4000,
                Since = DateTimeOffset.UnixEpoch,
                Limit = 2400000,
            },
        ],
    };
}
