using System.Net;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A heartbeat is the one call this runner makes whether or not there is work,
/// and a failed one used to kill the process.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observed on a laptop, idle, printing "nothing ready".</b> The control
/// plane's database began timing out, the next heartbeat got a 500, and the
/// process died with an unhandled
/// <c>HttpRequestException</c> out of <c>RunnerLoop.BeatIfDueAsync</c>. The
/// machine left the fleet permanently and the only record was a stack trace at
/// the end of a log nobody was watching.
/// </para>
/// <para>
/// <b>The asymmetry is the finding.</b> <c>RunAsync</c> calls two things in
/// sequence and only the second is guarded: the claim below catches exactly
/// this failure, and the comment beside it names <i>a database blip</i> as a
/// case it exists for. <c>TransientFailure</c> was written, tested and
/// documented for this, and one of the callers that needed it did not reach it.
/// </para>
/// <para>
/// <b>Not standing down, and that is the design decision here.</b> A runner
/// that cannot beat reads as <c>offline</c> to the control plane, which derives
/// staleness itself and is the authority on liveness - so the fleet's view is
/// already correct, and capability-gated work is only offered to a live runner.
/// Ownership is governed by the lease's generation FENCE and not by the beat,
/// so a swallowed heartbeat cannot put two runners on one flight. Standing down
/// would remove a machine from the fleet for a transient fault, which is the
/// defect this file exists to close, with better manners.
/// </para>
/// <para>
/// <b>What it must not do is go quiet or spin.</b> The crash at least left a
/// stack trace; and <c>_nextBeatDue</c> only ever advanced on success, so a
/// naive catch would try again as fast as the loop turns.
/// </para>
/// </remarks>
public class TransientHeartbeatFailureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 0, 4, 0, TimeSpan.Zero);

    private static HttpRequestException Answering(HttpStatusCode status) =>
        new($"Response status code does not indicate success: {(int)status} ({status}).",
            inner: null, statusCode: status);

    /// <summary>A refused connection: no status at all, which is its own case.</summary>
    private static HttpRequestException Unreachable() =>
        new("No connection could be made because the target machine actively refused it.");

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer,
        TimeSpan? holdFor = null) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer,
            new NoCredentialResolver(),
            new NoWorkspace())
        {
            HoldFor = holdFor ?? TimeSpan.FromSeconds(3),
        };

    /// <summary>Stops once the loop has reported enough, never on a duration.</summary>
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

    private static int Beats(FakeProtocol protocol) =>
        protocol.Calls.Count(c => c == "heartbeat");

    [Test]
    public async Task A_server_error_on_the_heartbeat_does_not_end_the_runner()
    {
        // THE DEFECT, exactly as reported. One 500 and the process was gone -
        // and a runner that dies stops asking for work, which looks exactly
        // like a runner with nothing to do.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.HeartbeatThrows.Enqueue(Answering(HttpStatusCode.InternalServerError));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        var exit = await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(exit).IsEqualTo(0)
            .Because("the run completing at all is the assertion: this used to leave RunAsync "
                   + "as an unhandled exception.");
        await Assert.That(Beats(protocol)).IsGreaterThan(1)
            .Because("saying it is alive again is the whole job. A machine removed from the "
                   + "fleet by somebody else's database blip is the defect.");
    }

    [Test]
    public async Task A_refused_connection_is_survived_the_same_way()
    {
        // No status code at all - a restart mid-request, a DNS blip, a cold
        // start. The shape that carries no status must not fall through to the
        // fatal arm.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.HeartbeatThrows.Enqueue(Unreachable());
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(Beats(protocol)).IsGreaterThan(1);
    }

    [Test]
    public async Task The_runner_says_the_heartbeat_was_refused_rather_than_going_quiet()
    {
        // Surviving quietly is the worse bug. A person who started a runner and
        // closed the window has nothing else to read, and RunnerObserver
        // already has somewhere to say it.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.HeartbeatThrows.Enqueue(Answering(HttpStatusCode.InternalServerError));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(observer.Events.Any(
                e => e.StartsWith("control-plane-refused:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a runner that turns a loud crash into a quiet outage has made the "
                   + "problem harder to find, not smaller.");
    }

    [Test]
    public async Task An_unauthorized_heartbeat_still_stops_the_runner()
    {
        // THE TWIN THAT KEEPS THE FIX HONEST. 401 is not the control plane
        // having a moment - it is this machine's credential, and no amount of
        // waiting fixes it. Retrying forever is a misconfigured machine
        // hammering a control plane where nobody can see it.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.HeartbeatThrows.Enqueue(Answering(HttpStatusCode.Unauthorized));
        var observer = new RecordingObserver();

        using var stopping = new CancellationTokenSource();
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token));

        await Assert.That(thrown!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task A_heartbeat_that_keeps_failing_does_not_beat_as_fast_as_the_loop_turns()
    {
        // _nextBeatDue is advanced only on SUCCESS, so a catch that returned
        // without touching it would try again on the very next turn of the
        // loop - a spin, at whatever rate the claim happens to run at. The beat
        // owns its own timer, and the diagnosis it reports names the wait, so
        // the escalation is readable from the outside.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.HeartbeatAlwaysThrows = Answering(HttpStatusCode.InternalServerError);
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 6);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        var waits = observer.Events
            .Where(e => e.StartsWith("control-plane-refused:", StringComparison.Ordinal))
            .ToList();

        await Assert.That(waits.Count).IsGreaterThan(1)
            .Because("it has to keep trying; this test is about the RATE, not about giving up.");
        await Assert.That(waits[0]).Contains("asking again in 2s");
        await Assert.That(waits.Any(w => w.Contains("asking again in 4s", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a wait that never grows is a spin with a sleep in it.");
    }

    [Test]
    public async Task The_heartbeats_trouble_is_not_the_claims()
    {
        // ONE BACKOFF FIELD, TWO CALLERS, WHICH WOULD HAVE THEM FIGHTING OVER
        // IT. What matters to a person is the consequence: a runner whose
        // heartbeat cannot land must still take the work it is offered. The
        // control plane will judge it offline and stop offering - that is the
        // control plane's decision to make, and this runner's job is to keep
        // doing what it is asked while it can.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.HeartbeatAlwaysThrows = Answering(HttpStatusCode.InternalServerError);
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddSeconds(30), renewWithin: 5)));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 4);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(observer.Events.Any(
                e => e.StartsWith("claimed:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a heartbeat the control plane cannot answer says nothing about whether "
                   + "this machine can do the work it is handed.");
    }

    [Test]
    public async Task A_runner_holding_a_flight_survives_a_failed_heartbeat_too()
    {
        // THE OTHER PLACE A HEARTBEAT CAN KILL THIS PROCESS, and the issue put
        // it in the wrong spot: the beat inside the claim's long poll is
        // already covered, incidentally, because AskForWorkAsync runs inside
        // the claim's own catch. HoldAsync is not - it calls HeartbeatAsync
        // DIRECTLY, on every turn of the hold, and that is the path where a
        // runner is holding a lease and doing somebody's work.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddSeconds(60), renewWithin: 1)));
        protocol.HeartbeatThrows.Enqueue(Answering(HttpStatusCode.InternalServerError));
        protocol.HeartbeatThrows.Enqueue(Answering(HttpStatusCode.InternalServerError));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 4);
        var exit = await Build(protocol, clock, observer, holdFor: TimeSpan.FromSeconds(6))
            .RunAsync("runner-1", [], stopping.Token);

        await Assert.That(exit).IsEqualTo(0)
            .Because("a 500 on a beat must not end a process that is holding somebody's "
                   + "flight - the loudest version of this defect, not the quietest.");
        await Assert.That(observer.Events.Any(
                e => e.StartsWith("released:", StringComparison.Ordinal)
                  || e.StartsWith("renewed:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("and it must go on holding it: the lease is governed by the generation "
                   + "fence, which a heartbeat has no part in.");
    }
}
