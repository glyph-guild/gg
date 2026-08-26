using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A person may say a flight's question stopped applying, and must say which.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0017's second open question, closed the harder way.</b> The ADR asked
/// whether only the system may withdraw a flight; if so, the exit gets counted
/// callers and nothing else. A person may, so it needs a door — and the door has
/// to carry what the counted-caller answer would have got for free: attribution,
/// and a reason.
/// </para>
/// <para>
/// <b>The reason is the guard.</b> <i>The question ceased to apply</i> is the
/// most reachable sentence in the terminal vocabulary and fits almost anything
/// somebody finds inconvenient. Requiring it to be said is what stops
/// <c>withdrawn</c> becoming a garbage collector, and it is required on the wire
/// rather than by convention so no client can omit it politely.
/// </para>
/// </remarks>
public class FlightWithdrawalSurfaceTests
{
    private static Endpoint Withdrawal() =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Path, "/v1/flights/{ref}/withdrawal", StringComparison.Ordinal));

    [Test]
    public async Task The_door_is_declared_and_is_a_persons()
    {
        var door = Withdrawal();

        await Assert.That(door.Method).IsEqualTo("POST");
        await Assert.That(door.Audience).IsEqualTo(Audience.Developer)
            .Because("a runner reports what it observed and never decides; saying a flight's "
                   + "question no longer applies is a judgement, which is a person's.");
        await Assert.That(door.Request).IsEqualTo(typeof(FlightWithdrawalRequest));
        await Assert.That(door.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task There_is_no_200_and_an_ended_flight_is_a_conflict()
    {
        var door = Withdrawal();

        await Assert.That(door.Statuses).DoesNotContain(200)
            .Because("the answer is that the flight is over, and what a caller does next is "
                   + "read it - the retirement door's arrangement one estate over.");
        await Assert.That(door.Statuses).Contains(202);
        await Assert.That(door.Statuses).Contains(409)
            .Because("a flight that has already ended is REFUSED rather than silently "
                   + "accepted: accepting would let a withdrawal appear to rewrite an "
                   + "ending that already happened.");
        await Assert.That(door.Statuses).Contains(404);
    }

    [Test]
    public async Task The_request_says_why_and_says_nothing_about_who()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightWithdrawalRequest)])
            .IsEquivalentTo((string[])["because"])
            .Because("Article XII derives the actor from the authenticated principal. A "
                   + "caller naming somebody else would be a caller choosing its own "
                   + "attribution, which is the one thing attribution may never be.");

        await Assert.That(typeof(FlightWithdrawalRequest)
            .GetProperty(nameof(FlightWithdrawalRequest.Because))!.PropertyType)
            .IsEqualTo(typeof(string))
            .Because("required and non-nullable: a withdrawal that does not say what stopped "
                   + "applying is a flight quietly disposed of.");
    }

    [Test]
    public async Task Withdrawing_is_declared_where_flights_are_governed()
    {
        // The prefix closure is what makes this a door rather than a route:
        // within /v1/flights, anything the control plane serves and this file
        // does not name fails the control plane's own conformance test.
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/flights");

        await Assert.That(Withdrawal().Path).StartsWith("/v1/flights/")
            .Because("withdrawing is something done to a flight, so it is addressed as one - "
                   + "and {ref} resolves a uuid or a flight number through the one parser.");
    }
}
