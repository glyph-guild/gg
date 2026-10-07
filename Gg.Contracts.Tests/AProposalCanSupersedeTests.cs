using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A proposal can name the plan it replaces (slice sixty-eight, S68.3-01).
/// </summary>
/// <remarks>
/// <b>Found on ITN-61 and ITN-62.</b> A draft proposed, edited and proposed again minted a second
/// plan holding the first plan's legs again. The door is exactly-once per draft content, so it
/// could not know the second was the first, changed. `Supersedes` says so, and the door then
/// withdraws the first in the same request.
/// </remarks>
public class AProposalCanSupersedeTests
{
    private static readonly ItineraryDraft Draft = new()
    {
        Planner = "plan",
        Intent = FlightIntent.Of("a toolchain refresh"),
        Legs = [new FlightNomination { Subject = "the upgrade", WorkKind = "implement", Reason = "named" }],
    };

    [Test]
    public async Task It_is_a_member_on_the_wire()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ItineraryProposal)]).Contains("supersedes");

        var json = JsonSerializer.Serialize(
            new ItineraryProposal { Draft = Draft, Supersedes = "ITN-61" }, JsonSerializerOptions.Web);
        await Assert.That(json).Contains("\"supersedes\":\"ITN-61\"");
    }

    [Test]
    public async Task It_names_a_plan_or_is_refused()
    {
        await Assert.That(ItineraryProposal.Validate(new ItineraryProposal { Draft = Draft, Supersedes = "ITN-61" }))
            .IsNull();
        await Assert.That(ItineraryProposal.Validate(new ItineraryProposal { Draft = Draft, Supersedes = "GG-61" }))
            .IsNotNull()
            .Because("a flight number is not a plan, and the door would otherwise look for one it cannot find.");
    }

    [Test]
    public async Task The_route_answers_a_supersede_it_cannot_honour_with_409()
    {
        var propose = ProtocolSurface.Endpoints.Single(e => e.Method == "POST" && e.Path == "/v1/itineraries");

        await Assert.That(propose.Statuses).Contains(409);
    }
}
