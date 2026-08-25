using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Registering a name is a widening, and the doors say so: the three
/// registration endpoints may answer 202 with the flight that carries the
/// registration and who the gate awaits.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice eight's placeholder retires.</b> Charting shipped "unrestricted
/// and logged" as a named deferral pointing at ADR-0016 section 6; this is
/// that section arriving. A registration makes a name reachable that was not
/// - reach that did not exist before is a widening by definition, so the
/// direction is a constant of the act, recorded rather than computed.
/// </para>
/// <para>
/// <b>The pending shape is one record, declared once.</b> The done shapes
/// (EnvironmentCharted, TopologyName, RepositoryRegistered) carry required
/// members a pending answer cannot honestly fill - who registered it and
/// when, which have not happened yet. A second declared response type per
/// door keeps both answers honest, and the surface says which endpoints may
/// give it.
/// </para>
/// </remarks>
public class RegistrationPendingTests
{
    private static readonly string[] _doors =
        ["/v1/environments", "/v1/airspace/names", "/v1/airspace/repositories"];

    [Test]
    public async Task The_airspace_registration_destination_kind_is_declared()
    {
        await Assert.That(DestinationKinds.AirspaceRegistration).IsEqualTo("airspace-registration")
            .Because("the registration flight needs a destination whose admission blocks on "
                   + "the widens-designated gate, and a kind nobody declared cannot be one.");
    }

    [Test]
    public async Task Every_registration_door_may_answer_pending_with_a_flight_and_an_approver()
    {
        foreach (var path in _doors)
        {
            var endpoint = ProtocolSurface.Endpoints.Single(
                e => e.Method == "POST" && e.Path == path);

            await Assert.That(endpoint.Statuses).Contains(202)
                .Because($"POST {path} gates a widening behind a person, and a door that "
                       + "cannot say 'not yet, and here is who decides' can only refuse or lie.");
            await Assert.That(endpoint.PendingResponse).IsEqualTo(typeof(RegistrationPending))
                .Because($"POST {path}'s 202 body is part of the protocol, not a convention.");
        }
    }

    [Test]
    public async Task A_pending_answer_names_the_flight_the_approver_and_what_widens()
    {
        var pending = new RegistrationPending
        {
            Flight = "01a03712-92f7-71b8-8db3-f565dcb57740",
            Awaiting = "platform-owner",
            Widens = "airspace.repositories",
        };

        await Assert.That(pending.Flight).IsNotEmpty();
        await Assert.That(pending.Awaiting).IsNotEmpty();
        await Assert.That(pending.Widens).IsNotEmpty()
            .Because("all three are required: a pending answer that cannot say which flight, "
                   + "who decides, or what would widen is a wait with no address.");
    }

    [Test]
    public async Task The_pending_record_is_pinned_and_in_the_vocabulary()
    {
        await Assert.That(Vocabulary.Types).Contains(typeof(RegistrationPending));
        await Assert.That(ProtocolSurface.JsonMembers[typeof(RegistrationPending)])
            .IsEquivalentTo((string[])["flight", "awaiting", "widens"]);
    }

    [Test]
    public async Task A_declared_pending_response_implies_202_everywhere()
    {
        // The coherence guard: an endpoint that declares a pending body but
        // not the status - or the reverse on a registration door - is a
        // surface that cannot be conformed to from either side.
        var incoherent = ProtocolSurface.Endpoints
            .Where(e => e.PendingResponse is not null && !e.Statuses.Contains(202))
            .Select(e => $"{e.Method} {e.Path}")
            .ToList();

        await Assert.That(incoherent).IsEmpty();
    }
}
