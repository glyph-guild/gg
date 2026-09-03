using System.Net;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A control plane having a bad moment must not permanently remove a machine
/// from the fleet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observed twice on one afternoon, both times from an ordinary deploy.</b>
/// A resident runner exited with an unhandled
/// <c>HttpRequestException: … 500 (Internal Server Error)</c> out of
/// <c>ReadClaimAsync</c>, and the process was gone. Restarting it claimed the
/// same flight and ran it to completion, so the error was transient every time.
/// </para>
/// <para>
/// <b>The failure is invisible from the other end.</b> A runner that dies stops
/// asking for work, and a runner that stops asking for work looks exactly like a
/// runner with nothing to do. Nothing reports it, nothing retries it, and it
/// waits for a person to notice.
/// </para>
/// <para>
/// <b>Surviving is not the same as swallowing, which is why the reporting test
/// is here.</b> A runner that silently retried forever would turn a loud crash
/// into a quiet outage, and the quiet version is worse: the crash at least left
/// a stack trace on somebody's console.
/// </para>
/// <para>
/// <b>And not everything deserves a retry.</b> A 5xx, a timeout and a refused
/// connection are the control plane's problem and will pass. A 401 is this
/// runner's problem and will not: retrying it forever is a machine burning a
/// request loop over a credential no amount of waiting will fix.
/// </para>
/// </remarks>
public class TransientClaimFailureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static HttpRequestException Answering(HttpStatusCode status) =>
        new($"Response status code does not indicate success: {(int)status} ({status}).",
            inner: null, statusCode: status);

    /// <summary>A refused connection: no status at all, which is its own case.</summary>
    private static HttpRequestException Unreachable() =>
        new("No connection could be made because the target machine actively refused it.");

    private static RunnerLoop Build(
        FakeProtocol protocol, MovableClock clock, RecordingObserver observer) =>
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
            HoldFor = TimeSpan.FromSeconds(3),
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

    [Test]
    public async Task A_server_error_on_the_claim_does_not_end_the_runner()
    {
        // THE DEFECT. One 500 and the process was gone.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.ClaimThrows.Enqueue(Answering(HttpStatusCode.InternalServerError));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(protocol.Calls.Count(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsGreaterThan(1)
            .Because("asking again is the whole job. A runner that asked once and died is a "
                   + "machine removed from the fleet by somebody else's deploy.");
    }

    [Test]
    public async Task A_refused_connection_is_survived_the_same_way()
    {
        // No status code at all - a restart mid-request, a DNS blip, a cold
        // start. The shape that carries no status must not fall through to the
        // fatal arm.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.ClaimThrows.Enqueue(Unreachable());
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(protocol.Calls.Count(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsGreaterThan(1);
    }

    [Test]
    public async Task The_runner_says_the_control_plane_refused_rather_than_retrying_in_silence()
    {
        // Surviving quietly is the worse bug. The crash at least left a stack
        // trace; a silent retry loop leaves a runner that looks idle and a
        // person with nothing to read.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.ClaimThrows.Enqueue(Answering(HttpStatusCode.InternalServerError));
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 3);
        await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(observer.Events.Any(
                e => e.StartsWith("control-plane-refused:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a runner that turns a loud crash into a quiet outage has made the problem "
                   + "harder to find, not smaller.");
    }

    [Test]
    public async Task An_unauthorized_runner_stops_instead_of_asking_forever()
    {
        // THE TWIN THAT KEEPS THE FIX HONEST. 401 is not the control plane
        // having a moment - it is this runner's credential, and no amount of
        // waiting fixes it. Retrying it forever is a machine burning a request
        // loop on something only a person can resolve.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.ClaimThrows.Enqueue(Answering(HttpStatusCode.Unauthorized));
        var observer = new RecordingObserver();

        using var stopping = new CancellationTokenSource();
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token));

        await Assert.That(thrown!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized)
            .Because("a runner nobody authorized must stop loudly. Retrying it is how a "
                   + "misconfigured machine hammers a control plane forever.");
    }

    [Test]
    public async Task Cancellation_still_ends_the_loop_without_a_failure()
    {
        // The anchor. Shutting down is not an error and must not start looking
        // like one now that failures are caught.
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        var observer = new RecordingObserver();

        using var stopping = StopAfter(observer, 2);
        var exit = await Build(protocol, clock, observer).RunAsync("runner-1", [], stopping.Token);

        await Assert.That(exit).IsEqualTo(0);
    }
}
