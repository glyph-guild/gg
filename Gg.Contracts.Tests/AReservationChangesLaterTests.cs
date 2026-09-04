using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Reserving and releasing a runner after it was registered.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registration is the only moment step 3 covered, and machines outlive
/// decisions.</b> A laptop registered before anybody thought about reservations
/// is every laptop in the fleet; requiring it to be re-registered would mean
/// destroying a thirty-day credential to change one boolean.
/// </para>
/// <para>
/// <b>Developer audience, session header.</b> A runner may not reserve, release
/// or read its own — the value governs it and must not be its to choose, which
/// is the pool-member rule applied one noun over.
/// </para>
/// <para>
/// <b>404, never 403, for a runner that is not yours.</b> A person learning
/// which runner ids exist by the shape of the refusal is a person enumerating
/// the tenant's fleet — the reason the heartbeat route already answers this way.
/// </para>
/// <para>
/// <b>DELETE releases and is idempotent.</b> Releasing a runner nobody reserved
/// is not an error: it is the state the caller asked for, and a 409 there would
/// make "make sure this is free" a two-step dance with a race in the middle.
/// </para>
/// </remarks>
public class AReservationChangesLaterTests
{
    private static Gg.Contracts.Description.Endpoint Endpoint(string method) =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/reservation" && e.Method == method);

    [Test]
    public async Task Both_verbs_are_declared_on_one_path()
    {
        await Assert.That(Endpoint("POST").Path).IsEqualTo("/v1/runners/{id}/reservation");
        await Assert.That(Endpoint("DELETE").Path).IsEqualTo("/v1/runners/{id}/reservation");
    }

    [Test]
    public async Task Reserving_is_a_persons_act_and_needs_a_session()
    {
        // A RUNNER MUST NOT REACH THIS. The value decides what work this runner
        // is offered, so a runner able to set it could widen its own queue -
        // which is the one thing "reserved" exists to stop.
        foreach (var method in (string[])["POST", "DELETE"])
        {
            await Assert.That(Endpoint(method).Audience).IsEqualTo(Audience.Developer);
            await Assert.That(Endpoint(method).RequiredHeaders)
                .Contains(ProtocolSurface.SessionHeader);
            await Assert.That(Endpoint(method).RequiredHeaders)
                .DoesNotContain(ProtocolSurface.RunnerHeader);
        }
    }

    [Test]
    public async Task Someone_elses_runner_is_a_404_rather_than_a_403()
    {
        // The heartbeat route's own rule, for its own reason: a caller learning
        // which ids exist from the shape of a refusal is a caller enumerating
        // the fleet.
        foreach (var method in (string[])["POST", "DELETE"])
        {
            await Assert.That(Endpoint(method).Statuses).Contains(404);
        }
    }

    [Test]
    public async Task Reserving_carries_no_principal()
    {
        // v0 reserves to the CALLER on every path. A body naming a principal
        // would be one person reserving another's runner - a different act with
        // a different approver - reachable by accident.
        var request = Endpoint("POST").Request;

        await Assert.That(request).IsNotNull();
        await Assert.That(request!.GetProperties().Select(p => p.Name))
            .DoesNotContain("PrincipalId");
    }

    [Test]
    public async Task Releasing_takes_no_body_at_all()
    {
        // There is nothing to say. A body here would invite a "release it to
        // somebody else", which is the act v0 does not have.
        await Assert.That(Endpoint("DELETE").Request).IsNull();
    }
}
