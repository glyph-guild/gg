using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// <b>S63.3-01</b> - <c>ItineraryMenu</c> and <c>GET /v1/itineraries/menu</c> are declared in the
/// contract, under the governed itineraries prefix.
/// </summary>
/// <remarks>
/// <para>
/// <b>A read of its own</b>, because the check refuses a draft with no legs and the menu is
/// needed before the first leg exists. Stretching the check to answer both would make an empty
/// draft mean two things.
/// </para>
/// <para>
/// <b>The planning tool server's three enums</b> (slice sixty-three rule 6), answered by the
/// control plane from admission's own bounds so the menu cannot offer what the preview refuses.
/// </para>
/// </remarks>
public class TheMenuIsAReadOfItsOwnTests
{
    [Test]
    public async Task The_route_is_a_developer_read_of_the_menu()
    {
        var menu = ProtocolSurface.Endpoints.SingleOrDefault(e =>
            e.Method == "GET" && e.Path == "/v1/itineraries/menu");

        await Assert.That(menu).IsNotNull();
        await Assert.That(menu!.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(menu.Request).IsNull()
            .Because("a read with nothing to send but which planner, which is a query string.");
        await Assert.That(menu.Response).IsEqualTo(typeof(ItineraryMenu));
        await Assert.That(menu.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task The_menu_names_its_destination_and_the_three_lists()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ItineraryMenu)]).IsEquivalentTo(
            (string[])["planner", "destinationId", "workKinds", "repositories", "environments", "refused"]);
        await Assert.That(Vocabulary.Types).Contains(typeof(ItineraryMenu));
    }

    [Test]
    public async Task A_menu_with_a_refusal_offers_nothing()
    {
        var refused = new ItineraryMenu
        {
            Planner = "plan",
            WorkKinds = ["implement"],
            Repositories = [],
            Environments = [],
            Refused = "'plan' composes no destination that opens flights.",
        };

        await Assert.That(ItineraryMenu.Validate(refused)).IsNotNull()
            .Because("a menu that both refuses and offers would let a tool server offer kinds the "
                   + "control plane just said nothing opens.");
    }
}
