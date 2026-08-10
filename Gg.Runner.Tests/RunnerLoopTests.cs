using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner's protocol behaviour, with no real time passing.
/// </summary>
/// <remarks>
/// Everything here is a decision the loop makes about time, so time is
/// injected and the test moves it. What genuinely cannot be tested this way -
/// a lease outliving the process that held it - is proven by killing a real
/// process in the control plane's integration tests, not by mocking harder.
/// </remarks>
public class RunnerLoopTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Time passes THROUGH the wait, which is what happens in reality and what
    /// makes these tests terminate. The loop asks to wait n seconds; the clock
    /// moves n seconds; nothing sleeps.
    /// </summary>
    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer, TimeSpan? holdFor = null) =>
        new(protocol, clock,
            (span, token) =>
            {
                token.ThrowIfCancellationRequested();
                clock.Advance(span);
                return Task.CompletedTask;
            },
            observer)
        {
            HoldFor = holdFor ?? TimeSpan.FromSeconds(3),
        };

    /// <summary>
    /// Cancels once the loop has reported enough. A CONDITION, signalled by
    /// the loop itself - never a duration, and never a poll.
    /// </summary>
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
    public async Task Claim_then_hold_then_release_is_the_whole_life_of_a_flight()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddMinutes(10))));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer).RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(observer.Events).Contains("claimed:lease-1");
        await Assert.That(observer.Events).Contains("released:completed");
    }

    [Test]
    public async Task An_idle_claim_goes_straight_round_again_rather_than_busy_looping()
    {
        // The control plane already held the request open for the long-poll
        // window. Sleeping again on top of that would double an idle runner's
        // latency; spinning without the long poll would hammer the server.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(observer.Events.Count(e => e == "idle")).IsGreaterThanOrEqualTo(3);
        await Assert.That(protocol.Calls).Contains($"claim:{RunnerLoop.ClaimWaitSeconds}")
            .Because("the wait is the long poll, and the client must ask for it.");
    }

    [Test]
    public async Task Heartbeat_and_renew_are_separate_calls()
    {
        // Collapsing them is the obvious simplification and it breaks
        // takeover: a wedged runner that still heartbeats would keep renewing
        // a flight it is no longer working on.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddSeconds(8), renewWithin: 5)));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, holdFor: TimeSpan.FromSeconds(6)).RunAsync("r", [], stopping.Token);

        await Assert.That(protocol.Calls).Contains("heartbeat");
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("renew:", StringComparison.Ordinal))).IsTrue();
        await Assert.That(protocol.Calls.Count(c => c == "heartbeat"))
            .IsNotEqualTo(0);
    }

    [Test]
    public async Task Renewal_is_decided_against_the_control_planes_expiry_not_our_own_elapsed_time()
    {
        // A lease with plenty of time left is not renewed, however long the
        // process has been running. A runner that renewed on its own schedule
        // would keep a lease the control plane had already given away.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddHours(1), renewWithin: 5)));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, holdFor: TimeSpan.FromSeconds(3)).RunAsync("r", [], stopping.Token);

        await Assert.That(protocol.Calls.Any(c => c.StartsWith("renew:", StringComparison.Ordinal))).IsFalse();
        await Assert.That(observer.Events).Contains("released:completed");
    }

    [Test]
    public async Task A_fenced_renewal_stops_the_runner_and_it_does_NOT_release()
    {
        // The heart of it. Releasing after being fenced would terminate the
        // flight of whichever runner now holds it - silent data loss caused by
        // a client behaving perfectly.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddSeconds(5), renewWithin: 5)));
        protocol.Renewals.Enqueue(new RenewResult.Fenced());
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, holdFor: TimeSpan.FromSeconds(30)).RunAsync("r", [], stopping.Token);

        await Assert.That(observer.Events).Contains("fenced:lease-1");
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("release:", StringComparison.Ordinal))).IsFalse()
            .Because("releasing a lease we no longer hold ends somebody else's flight.");
    }

    [Test]
    public async Task A_lease_that_vanished_is_treated_exactly_like_a_fenced_one()
    {
        // Gone and fenced differ in cause and not in what the runner should do,
        // and a runner that retried on Gone would spin against a lease that is
        // never coming back.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddSeconds(5), renewWithin: 5)));
        protocol.Renewals.Enqueue(new RenewResult.Gone());
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer, holdFor: TimeSpan.FromSeconds(30)).RunAsync("r", [], stopping.Token);

        await Assert.That(observer.Events).Contains("fenced:lease-1");
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("release:", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task A_release_refused_by_the_fence_is_reported_rather_than_swallowed()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol
        {
            Release = new ReleaseResult.Fenced(),
        };
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddMinutes(10))));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer).RunAsync("r", [], stopping.Token);

        await Assert.That(observer.Events).Contains("fenced:lease-1");
        await Assert.That(observer.Events.Any(e => e.StartsWith("released:", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task The_runner_presents_the_generation_it_was_granted()
    {
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddMinutes(10), generation: 7)));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        await Build(protocol, clock, observer).RunAsync("r", [], stopping.Token);

        await Assert.That(protocol.Calls).Contains("release:7:completed")
            .Because("the fence is only a fence if the client presents what it was given.");
    }

    [Test]
    public async Task Cancellation_does_not_release_the_lease()
    {
        // Shutting down must NOT look like completing. The lease is left to
        // expire on the control plane's clock, which is the same path a killed
        // process takes - and the path the whole step is about.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(Leases.At(T0.AddHours(1))));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 1);
        await Build(protocol, clock, observer, holdFor: TimeSpan.FromHours(1)).RunAsync("r", [], stopping.Token);

        await Assert.That(observer.Events).Contains("claimed:lease-1");
        await Assert.That(protocol.Calls.Any(c => c.StartsWith("release:", StringComparison.Ordinal))).IsFalse();
    }
}
