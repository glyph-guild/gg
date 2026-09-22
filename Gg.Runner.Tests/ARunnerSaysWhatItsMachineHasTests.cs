using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Tests;

/// <summary>
/// How a machine tells a control plane what it has and how much of it is in
/// use.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the beat, and not IN it</b> — the allowance reading's argument, which
/// applies here with a sharper edge. The heartbeat is liveness only because a
/// runner able to report something about itself can report it while dead, and a
/// dead machine still claiming it has cpu to spare is exactly that hazard with
/// a scheduler's decision downstream of it.
/// </para>
/// <para>
/// <b>Every thirty seconds, which is the interval the figure averages over.</b>
/// A cpu number is only meaningful across an interval, and thirty seconds is
/// what the console's own refresh shows — so a pane never draws a figure older
/// than its own tick.
/// </para>
/// <para>
/// <b>A refusal never stands the runner down</b>, as a refused allowance does
/// not: the control plane is the authority on liveness, and a machine that
/// removed itself from the fleet because a reading was refused would have
/// turned a bookkeeping route into an availability dependency.
/// </para>
/// </remarks>
public class ARunnerSaysWhatItsMachineHasTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A meter that answers whatever the test hands it, in order.</summary>
    private static MachineReporter Reporting(params MeasuredMachine[] readings)
    {
        var next = 0;

        return new MachineReporter(
            _ => readings[Math.Min(next++, readings.Length - 1)],
            MachineReporter.Cadence);
    }

    private static MeasuredMachine Measured(
        DateTimeOffset at,
        int? limit = 4000,
        int? used = 250,
        long? memoryLimit = 16_000_000_000,
        long? memoryUsed = 2_000_000_000,
        int? overSeconds = 30) => new()
    {
        MeasuredAt = at,
        Over = overSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
        CpuMilliLimit = limit,
        CpuMilliUsed = used,
        MemoryLimitBytes = memoryLimit,
        MemoryUsedBytes = memoryUsed,
    };

    [Test]
    public async Task Every_figure_crosses_unchanged()
    {
        var reading = Reporting(Measured(T0)).Read(T0);

        await Assert.That(reading).IsNotNull();
        await Assert.That(reading!.MeasuredAt).IsEqualTo(T0);
        await Assert.That(reading.CpuMilliLimit).IsEqualTo(4000);
        await Assert.That(reading.CpuMilliUsed).IsEqualTo(250);
        await Assert.That(reading.MemoryLimitBytes).IsEqualTo(16_000_000_000);
        await Assert.That(reading.MemoryUsedBytes).IsEqualTo(2_000_000_000);
        await Assert.That(reading.OverSeconds).IsEqualTo(30)
            .Because("the wire carries seconds because a duration is not a JSON type, and "
                   + "the interval is what makes the cpu figure mean anything.");
        await Assert.That(MachineReading.Validate(reading)).IsNull();
    }

    [Test]
    public async Task Nothing_is_measured_between_the_thirty_seconds()
    {
        var meter = Reporting(Measured(T0), Measured(T0.AddSeconds(5)));

        await Assert.That(meter.Read(T0)).IsNotNull();

        await Assert.That(meter.Read(T0.AddSeconds(5))).IsNull()
            .Because("the loop asks on every beat and a beat is seconds, so the cadence "
                   + "lives here - and the measurement itself is not taken, because a meter "
                   + "that read each time and threw the answer away would cost exactly what "
                   + "this exists to save.");
        await Assert.That(meter.Read(T0 + MachineReporter.Cadence)).IsNotNull();
    }

    [Test]
    public async Task A_machine_that_can_measure_nothing_reports_nothing()
    {
        var blind = Reporting(new MeasuredMachine { MeasuredAt = T0 });

        await Assert.That(blind.Read(T0)).IsNull()
            .Because("a reading with every figure absent says only that somebody looked, "
                   + "and a request per thirty seconds to say nothing is a request nobody "
                   + "asked for.");
    }

    [Test]
    public async Task A_first_reading_with_no_interval_still_carries_the_limits()
    {
        var reading = Reporting(Measured(T0, used: null, overSeconds: null)).Read(T0);

        await Assert.That(reading).IsNotNull();
        await Assert.That(reading!.CpuMilliUsed).IsNull();
        await Assert.That(reading.OverSeconds).IsNull();
        await Assert.That(reading.CpuMilliLimit).IsEqualTo(4000)
            .Because("what a machine IS does not wait for a second look at what it is "
                   + "doing, and the limits are the half of this that was asked for.");
    }

    // ---- the loop ----

    [Test]
    public async Task A_beat_carries_what_the_root_handed_it()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();

        await new RunnerLoop(protocol, new MovableClock(T0),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                machine: _ => new MachineReading
                {
                    MeasuredAt = T0, CpuMilliLimit = 4000, CpuMilliUsed = 250,
                })
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Machines.Select(m => m.CpuMilliLimit)).Contains(4000)
            .Because("a reading nothing posts is a measurement this machine keeps to "
                   + "itself.");
    }

    [Test]
    public async Task A_runner_that_measures_nothing_posts_nothing()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();

        await new RunnerLoop(protocol, new MovableClock(T0),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                machine: _ => null)
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Machines).IsEmpty();
        await Assert.That(protocol.Calls).DoesNotContain("machine")
            .Because("between cadences the reporter answers nothing, and the loop must ask "
                   + "rather than send.");
    }

    [Test]
    public async Task A_refused_reading_does_not_stand_the_runner_down()
    {
        using var stopping = new CancellationTokenSource();
        var protocol = new FakeProtocol();
        protocol.MachineThrows.Enqueue(new HttpRequestException("no route here"));

        var beats = 0;

        var exit = await new RunnerLoop(protocol, new MovableClock(T0),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                machine: _ =>
                {
                    beats++;
                    return new MachineReading { MeasuredAt = T0, CpuMilliLimit = 4000 };
                })
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(beats).IsGreaterThan(0);
        await Assert.That(exit).IsEqualTo(0)
            .Because("a control plane that will not take a reading is still the authority on "
                   + "liveness - a runner leaving the fleet over a refused measurement turns "
                   + "bookkeeping into an availability dependency, which is the heartbeat "
                   + "guard's own argument one surface along.");
    }

    // ---- what it refuses to say ----

    [Test]
    public async Task A_negative_figure_is_refused_by_name()
    {
        var wrong = new MachineReading { MeasuredAt = T0, CpuMilliUsed = -1 };

        await Assert.That(MachineReading.Validate(wrong)).IsNotNull();
        await Assert.That(MachineReading.Validate(wrong)!).Contains("cpuMilliUsed")
            .Because("a refusal that does not name the member leaves a reader guessing which "
                   + "of five numbers was wrong.");
    }

    [Test]
    public async Task Using_more_than_the_limit_is_a_real_state_and_is_allowed()
    {
        var throttled = new MachineReading
        {
            MeasuredAt = T0, OverSeconds = 30, CpuMilliLimit = 1000, CpuMilliUsed = 1400,
        };

        await Assert.That(MachineReading.Validate(throttled)).IsNull()
            .Because("a quota is enforced over a period, so an average taken across period "
                   + "boundaries can land above it - and a figure clamped to its ceiling is "
                   + "a figure that cannot show a machine being throttled, which is the one "
                   + "thing somebody reading this column wants to see.");
    }
}
