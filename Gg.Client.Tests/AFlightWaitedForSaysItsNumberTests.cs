using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg fly --wait</c> stays until the flight has a number, and says it.
/// </summary>
/// <remarks>
/// <b>The door cannot say it.</b> A launch answers 202 before the Flight context
/// has minted the number, so `gg fly` prints an id and "the number is assigned
/// as it starts" - and a person who wants the number runs `gg flights` until it
/// is there, which is the loop this runs for them: the verb's own patience,
/// bounded, and never claiming a number it did not see.
/// </remarks>
public class AFlightWaitedForSaysItsNumberTests
{
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static SubmitAndObserve Instant()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        return new SubmitAndObserve(
            (span, _) => { now = now.Add(span); return Task.CompletedTask; },
            () => now);
    }

    private static async Task<FlightLaunched> FlyAsync(StubControlPlane stub, bool wait)
    {
        using var http = new HttpClient { BaseAddress = new Uri(stub.BaseAddress) };
        var commands = new FlightCommands(new ControlPlaneClient(http), new HeldSessionStore(SignedIn));

        var result = await commands.FlyAsync(
            "stop the pty test flaking", null, wait: wait,
            bound: new ObservationBound
            {
                Wait = TimeSpan.FromSeconds(30),
                FirstDelay = TimeSpan.FromMilliseconds(250),
                MaxDelay = TimeSpan.FromSeconds(2),
            },
            loop: Instant());

        return ((VerbResult.Launched)result).Value;
    }

    [Test]
    public async Task Waiting_answers_with_the_number_once_the_flight_is_listed()
    {
        await using var stub = new StubControlPlane { UnlistedReads = 2 };

        var launched = await FlyAsync(stub, wait: true);

        await Assert.That(launched.FlightNumber).IsEqualTo("GG-42")
            .Because("the number the door could not give, read once the flight is listed.");
    }

    [Test]
    public async Task Not_waiting_answers_at_the_door_as_it_always_did()
    {
        await using var stub = new StubControlPlane();

        var launched = await FlyAsync(stub, wait: false);

        await Assert.That(launched.FlightNumber).IsNull()
            .Because("without --wait the verb answers with the 202, which names no number.");
    }

    [Test]
    public async Task A_flight_never_listed_answers_without_a_number()
    {
        // NEVER A NUMBER IT DID NOT SEE. The patience runs out, and the answer is
        // the door's - which the renderer already says honestly.
        await using var stub = new StubControlPlane { FlightNotFound = true };

        var launched = await FlyAsync(stub, wait: true);

        await Assert.That(launched.FlightNumber).IsNull();
        await Assert.That(launched.FlightId).IsNotNull();
    }
}
