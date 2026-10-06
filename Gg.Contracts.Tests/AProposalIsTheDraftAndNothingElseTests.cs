using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// <b>S65.1-01</b> - <c>POST /v1/itineraries</c> is declared with <c>ItineraryProposal</c> and
/// <c>ItineraryProposed</c>, under the governed prefix; the proposal carries the draft and
/// <c>via</c> and nothing else.
/// </summary>
/// <remarks>
/// <b>The conversation is not kept</b> (ADR-0038 Decision 5), and the strongest form of that is a
/// type with nowhere to put it - the argument <c>RecordedIntent</c>'s member list makes. A member
/// added here is a diff somebody has to justify.
/// </remarks>
public class AProposalIsTheDraftAndNothingElseTests
{
    [Test]
    public async Task The_route_is_a_developer_post_on_the_collection()
    {
        var propose = ProtocolSurface.Endpoints.SingleOrDefault(e =>
            e.Method == "POST" && e.Path == "/v1/itineraries");

        await Assert.That(propose).IsNotNull();
        await Assert.That(propose!.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(propose.Request).IsEqualTo(typeof(ItineraryProposal));
        await Assert.That(propose.Response).IsEqualTo(typeof(ItineraryProposed));
        await Assert.That(propose.Statuses).Contains(202);
        await Assert.That(propose.Statuses).Contains(400);
        await Assert.That(propose.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task A_proposal_is_the_draft_and_the_agent_that_acted()
    {
        var members = typeof(ItineraryProposal).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsEquivalentTo((string[])["Draft", "Via"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ItineraryProposal)])
            .IsEquivalentTo((string[])["draft", "via"]);
    }

    [Test]
    public async Task The_answer_names_the_plan_its_pass_and_its_gates()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(ItineraryProposed)])
            .IsEquivalentTo((string[])["itinerary", "pass", "gates"]);
        await Assert.That(Vocabulary.Types).Contains(typeof(ItineraryProposal));
        await Assert.That(Vocabulary.Types).Contains(typeof(ItineraryProposed));
    }

    [Test]
    public async Task A_proposal_validates_its_draft_and_bounds_its_label()
    {
        var draft = new ItineraryDraft
        {
            Planner = "plan",
            Intent = FlightIntent.Of("three findings"),
            Legs = [new FlightNomination { Subject = "the icon", WorkKind = "implement", Reason = "named" }],
        };

        await Assert.That(ItineraryProposal.Validate(new ItineraryProposal { Draft = draft })).IsNull();
        await Assert.That(ItineraryProposal.Validate(new ItineraryProposal { Draft = draft with { Legs = [] } }))
            .IsNotNull();
        await Assert.That(ItineraryProposal.Validate(new ItineraryProposal
        {
            Draft = draft,
            Via = new string('x', ItineraryProposal.MaxVia + 1),
        })).IsNotNull().Because("a label, not a place a transcript could be smuggled in.");
    }
}
