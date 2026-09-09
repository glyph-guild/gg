using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The one route an offer may be fetched from, and who it answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule this looks like it breaks, and does not.</b>
/// <c>AnOfferRidesThePollThatExistsTests</c> refuses a route for configuration
/// because <i>"a route of its own is a second way to reach a machine"</i>. That
/// is about INBOUND reach: nothing connects to a laptop, so an offer rides a
/// response the machine already asked for. A person typing a verb adds no
/// inbound reach at all — the machine still starts every conversation.
/// </para>
/// <para>
/// <b>And it is needed, because the directed tier requires a person and no
/// person was reachable.</b> Three of the five offerable keys may only be taken
/// by somebody who sees what is being repointed. That somebody is at a console;
/// the offer arrives on a RUNNER's heartbeat; and <c>Gg.Runner</c> cannot
/// reference <c>Gg.Client</c>, so the code that takes an offer was not reachable
/// from the machine that received one. The tier was built and could not be
/// exercised.
/// </para>
/// <para>
/// <b>Developer, and never Runner.</b> That is the whole structural guarantee
/// rather than a note about who happens to call it: a runner cannot fetch an
/// offer, because the route does not answer to a runner credential. What a
/// runner may have is what rides its heartbeat, which is the posture
/// <c>HeartbeatAccepted</c>'s own remark defends.
/// </para>
/// </remarks>
public class AWaitingOfferIsFetchedByAPersonTests
{
    private static Endpoint Offered() => ProtocolSurface.Endpoints.Single(
        e => string.Equals(e.Method, "GET", StringComparison.Ordinal)
          && string.Equals(e.Path, "/v1/configuration/offered", StringComparison.Ordinal));

    [Test]
    public async Task A_person_can_ask_what_is_offered()
    {
        var offered = Offered();

        await Assert.That(offered.Response).IsEqualTo(typeof(OfferedConfiguration));
        await Assert.That(offered.Request).IsNull()
            .Because("asking what is offered says nothing about this machine. A body would "
                   + "be a machine describing itself to be answered specifically, which is "
                   + "how a tenant-wide offer becomes a per-machine instruction.");
    }

    [Test]
    public async Task It_answers_a_persons_session_and_nothing_else()
    {
        await Assert.That(Offered().Audience).IsEqualTo(Audience.Developer)
            .Because("a runner that could fetch this would be reaching for configuration "
                   + "rather than reading what rode its heartbeat, and the directed keys "
                   + "are offerable only because a person sees them.");

        await Assert.That(Offered().RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task Nothing_offered_is_an_answer_rather_than_a_missing_thing()
    {
        var statuses = Offered().Statuses;

        await Assert.That(statuses).Contains(204)
            .Because("a control plane offering nothing is the ordinary state, and a person "
                   + "asking has to be able to tell it from a failure.");

        await Assert.That(statuses).DoesNotContain(404)
            .Because("there is one offer per tenant and it is always askable. 404 would "
                   + "make 'nothing is offered' and 'this route is wrong' the same answer.");

        await Assert.That(statuses).Contains(ProtocolSurface.ProtocolTooOld)
            .Because("every governed door may refuse a gg below the floor.");
    }

    [Test]
    public async Task No_route_lets_a_runner_near_configuration()
    {
        // THE RATCHET, and it is about the audience rather than the path. A
        // second route here is not the hazard; a route a runner may call is.
        var reachable = ProtocolSurface.Endpoints
            .Where(e => e.Audience == Audience.Runner
                     && e.Path.Contains("configuration", StringComparison.OrdinalIgnoreCase))
            .Select(e => $"{e.Method} {e.Path}")
            .ToList();

        await Assert.That(reachable).IsEmpty()
            .Because("what a runner gets is what rides its heartbeat: "
                   + string.Join(", ", reachable));
    }

    [Test]
    public async Task The_prefix_is_governed_so_a_second_door_cannot_be_quiet()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/configuration")
            .Because("an offer changes where a fleet fetches code from and sends proposals "
                   + "to. A route under this prefix that the declaration did not name would "
                   + "be an unaudited way to repoint machines - the argument /v1/credentials "
                   + "and /v1/invitations came in on.");
    }
}
