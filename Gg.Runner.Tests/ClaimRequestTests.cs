using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// Claiming is a request the runner asks about, and waiting is something it says
/// out loud.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rate limiter changed hands and that is the risk.</b> The claim used to
/// be a long poll: the control plane held the request open for
/// <c>ClaimWaitSeconds</c> and going straight round again was a poll rather than
/// a spin. With the request accepted immediately there is nothing holding it
/// open, and this runner has no backoff of its own - so the interval the control
/// plane sends is the whole of it, and a loop that ignored it would hammer an
/// endpoint that answers instantly.
/// </para>
/// <para>
/// Every wait here goes through the injected delegate, which the test advances
/// its clock through. Nothing sleeps.
/// </para>
/// </remarks>
public class ClaimRequestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The loop, plus every span it was asked to wait.</summary>
    private static (RunnerLoop Loop, List<TimeSpan> Waits) Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer)
    {
        var waits = new List<TimeSpan>();
        var loop = new RunnerLoop(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                waits.Add(span);
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer, new NoCredentialResolver(), new NoWorkspace())
        {
            HoldFor = TimeSpan.FromSeconds(1),
        };

        return (loop, waits);
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

    [Test]
    public async Task An_accepted_request_is_asked_about_rather_than_waited_on()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Nothing());
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddMinutes(10))));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        var (loop, _) = Build(protocol, clock, observer);
        await loop.RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(observer.Events).Contains("claimed:lease-1");

        // THE REQUEST IS A THING WITH A NAME, and asking about it is a different
        // call from making it. A status read that could also grant would make
        // the answer depend on who asked first, which is the property the split
        // exists to remove.
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("claim-status:", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(protocol.Calls.Count(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("one request, asked about twice - not two requests. A runner that made a "
                   + "fresh request per poll would leave a trail of abandoned ones behind it.");
    }

    [Test]
    public async Task The_runner_waits_the_interval_the_control_plane_chose()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol { PollAfterSeconds = 7 };
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        var (loop, waits) = Build(protocol, clock, observer);
        await loop.RunAsync("runner-1", [], stopping.Token);

        await Assert.That(waits).Contains(TimeSpan.FromSeconds(7))
            .Because("the server holding the request open WAS the rate limiter. It no longer "
                   + "holds anything, so an interval it did not choose is a busy loop.");
        await Assert.That(waits.Any(w => w == TimeSpan.Zero)).IsFalse()
            .Because("a zero wait between polls is the busy loop wearing the new shape.");
    }

    [Test]
    public async Task Waiting_is_reported_and_is_not_the_same_as_idle()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Waiting(["acme/widgets"]));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 1);
        var (loop, _) = Build(protocol, clock, observer);
        await loop.RunAsync("runner-1", [], stopping.Token);

        // THE WHOLE POINT OF THE CHANGE, at the runner's end. An idle fleet and
        // a fleet blocked on a credential nobody has registered were the same
        // 204, so the only thing a person watching a runner could see was
        // silence - identical to the silence of a system with nothing to do.
        await Assert.That(observer.Events).Contains("waiting:acme/widgets");
        await Assert.That(observer.Events).DoesNotContain("idle")
            .Because("reporting both would put the conflation back one layer up.");
    }

    [Test]
    public async Task A_control_plane_that_still_answers_inline_is_understood()
    {
        // BOTH REPOSITORIES SHIP INDEPENDENTLY, and this is what lets them land
        // in either order - the same tolerance the decisions endpoint was given
        // when it stopped answering inline. When no control plane answers a
        // claim with a lease, this branch is dead and deleting it is a change
        // with a reason of its own.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol
        {
            Acceptance = new ClaimAcceptance.Inline(
                new ClaimResult.Granted(Leases.At(T0.AddMinutes(10)))),
        };
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        var (loop, _) = Build(protocol, clock, observer);
        await loop.RunAsync("runner-1", [], stopping.Token);

        await Assert.That(observer.Events).Contains("claimed:lease-1");
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("claim-status:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("there is nothing to ask about: the answer arrived with the request.");
    }

    [Test]
    public async Task A_request_that_expired_is_not_retried_under_its_old_name()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Expired());
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddMinutes(10))));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        var (loop, _) = Build(protocol, clock, observer);
        await loop.RunAsync("runner-1", [], stopping.Token);

        // A terminal state, so the runner asks for a NEW request rather than
        // polling a dead one forever. Two claims, and the second one granted.
        await Assert.That(protocol.Calls.Count(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsGreaterThanOrEqualTo(2);
        await Assert.That(observer.Events).Contains("claimed:lease-1");
    }
}
