using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner that came for one flight asks for that one, by name.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE HALF NOBODY WIRED.</b> <c>LeaseClaimRequest</c> has carried an
/// optional <c>FlightId</c> since contract 0.102.0, the control plane grants a
/// directed claim and re-asserts all six checks where the grant happens, and
/// both are proven. Nothing ever asked. <c>RequestClaimAsync</c> built the
/// request without the member and <c>RunAsync</c> had no flight to pass, so the
/// member's own remark — <i>"A person at a terminal is a runner that wants a
/// SPECIFIC flight, the one they just opened"</i> — described a request no
/// runner had ever sent.
/// </para>
/// <para>
/// <b>Without it a hand-flight is a race whose failure is not an error.</b> The
/// person waits at a prompt while the flight they just opened is cloned on
/// somebody else's laptop, and every part reports success.
/// </para>
/// <para>
/// <b>Null stays the ordinary claim, and that is asserted beside it.</b> A fleet
/// runner asks the queue what is available; making the member required, or
/// defaulting it to anything, would turn every runner in the estate into one
/// that wants a particular flight.
/// </para>
/// </remarks>
public class ARunnerAsksForOneFlightTests
{
    [Test]
    public async Task A_runner_that_came_for_one_flight_names_it_in_the_claim()
    {
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Nothing());

        using var stopping = new CancellationTokenSource();
        stopping.CancelAfter(TimeSpan.FromSeconds(5));

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace())
            .RunAsync("runner-1", ["linux"], stopping.Token, flightId: "flight-7");

        await Assert.That(protocol.Serialized.Any(s => s.Contains("\"flightId\":\"flight-7\"",
                StringComparison.Ordinal))).IsTrue()
            .Because("the person opened THAT flight and is waiting at a terminal for it. "
                   + "Asking the queue what is available is a race whose failure is not an "
                   + "error: they wait at a prompt while their flight is cloned somewhere "
                   + "else, and every part reports success.");
    }

    [Test]
    public async Task An_ordinary_runner_still_asks_for_whatever_is_ready()
    {
        // THE OTHER HALF, AND IT IS WHAT KEEPS THE FLEET A FLEET. Every runner
        // in the estate goes through this call, so a default here would make all
        // of them ask for a particular flight and none of them take queued work.
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Nothing());

        using var stopping = new CancellationTokenSource();
        stopping.CancelAfter(TimeSpan.FromSeconds(5));

        await new RunnerLoop(protocol, new MovableClock(DateTimeOffset.UnixEpoch),
                (_, _) => { stopping.Cancel(); return Task.CompletedTask; },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace())
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(protocol.Serialized.Any(s => s.Contains("flightId",
                StringComparison.Ordinal))).IsFalse()
            .Because("null is every claim the fleet makes, and gg omits a null member "
                   + "entirely rather than writing it - so an older control plane sees the "
                   + "request it has always seen.");
    }
}
