using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A person may move a flight in the air to another model root lists
/// (slice seventy-six), through one door.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's ask:</b> <i>"during a flight i would like the ability to
/// override the model on the fly - but it needs to be enabled in the root
/// airspace."</i> The enabling is <c>models:</c> at root; this is the door.
/// </para>
/// <para>
/// <b>Grounding's shape, deliberately.</b> A person acts on a flight by
/// reference, the door answers that it was done and what a caller does next
/// is read the flight, and a flight that has ended is refused rather than
/// allowed to appear to change. What differs is the body: a name against a
/// list, where grounding carries a reason against nothing.
/// </para>
/// </remarks>
public class AFlightIsMovedToAModelSurfaceTests
{
    private static Endpoint Door() =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Path, "/v1/flights/{ref}/model", StringComparison.Ordinal));

    private static Endpoint Grounding() =>
        ProtocolSurface.Endpoints.Single(e =>
            string.Equals(e.Path, "/v1/flights/{ref}/grounding", StringComparison.Ordinal));

    [Test]
    public async Task The_door_is_declared_and_is_a_persons()
    {
        var door = Door();
        await Assert.That(door.Method).IsEqualTo("POST");
        await Assert.That(door.Audience).IsEqualTo(Audience.Developer)
            .Because("a runner able to choose a flight's model would be a runner choosing what "
                   + "it costs, for a person who did not.");
        await Assert.That(door.Request).IsEqualTo(typeof(FlightModelRequest));
        await Assert.That(door.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task It_answers_as_grounding_does()
    {
        await Assert.That(Door().Statuses).IsEquivalentTo(Grounding().Statuses)
            .Because("no 200 - what a caller does next is read the flight; 400 for a name root "
                   + "does not list, or a root that lists nothing; 409 for a flight that has "
                   + "already ended.");
    }

    [Test]
    public async Task The_request_names_a_model_and_nothing_about_who()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightModelRequest)])
            .IsEquivalentTo((string[])["model"]);
        await Assert.That(typeof(FlightModelRequest)
            .GetProperty(nameof(FlightModelRequest.Model))!.PropertyType)
            .IsEqualTo(typeof(string))
            .Because("required and non-nullable: clearing back to the account's default is "
                   + "out of scope, so a request always names one.");
    }

    [Test]
    public async Task It_is_declared_where_flights_are_governed()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/flights");
        await Assert.That(Door().Path).StartsWith("/v1/flights/");
    }
}
