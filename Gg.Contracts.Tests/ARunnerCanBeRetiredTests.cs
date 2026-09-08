using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Taking a runner out of the fleet, by a person, without losing what it did.
/// </summary>
/// <remarks>
/// <para>
/// <b>The protocol could register a runner and never retire one.</b>
/// <c>RunnerRegistry.RevokeAsync</c> has existed control-plane-side the whole
/// time — it sets <c>revoked_at</c>, publishes <c>RunnerRevoked</c>, and the
/// fleet read already filters on the projection's <c>Revoked</c> — and nothing
/// has ever called it. Dead code behind a read that was ready for it.
/// </para>
/// <para>
/// <b>What that cost, measured on the development fleet.</b> Fifteen rows, two
/// machines: one laptop and fourteen registrations of the same pool host, twelve
/// of them abandoned by repeated <c>gg runner up</c>. And not merely untidy —
/// <c>RunnerRegistry.TokenLifetime</c> is thirty days, so each abandoned row
/// still holds a bearer credential for the runner protocol surface.
/// </para>
/// <para>
/// <b>Retirement is not deletion, and the difference is the whole design.</b>
/// The row stays and history keeps pointing at it: leases name the runner that
/// held them and the attestation ledger names the runner that attested. A DELETE
/// that removed the row would orphan both to save a line on a screen.
/// </para>
/// <para>
/// <b>One way, and the missing twin is what says so.</b> Parking and reservation
/// each have a POST and a DELETE because a person changes their mind about them.
/// Nothing un-retires a runner: bringing that machine back is registering a
/// runner, which is a different act with a different credential.
/// </para>
/// </remarks>
public class ARunnerCanBeRetiredTests
{
    private static Gg.Contracts.Description.Endpoint Retirement =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/retirement" && e.Method == "POST");

    [Test]
    public async Task Retiring_is_a_persons_act()
    {
        // A RUNNER MUST NOT RETIRE ITSELF, for parking's reason and one more: a
        // runner that could revoke its own credential could take itself out of a
        // fleet somebody is relying on, and the only trace would be the runner's
        // own word for why.
        await Assert.That(Retirement.Audience).IsEqualTo(Audience.Developer);
        await Assert.That(Retirement.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
        await Assert.That(Retirement.RequiredHeaders)
            .DoesNotContain(ProtocolSurface.RunnerHeader);
    }

    [Test]
    public async Task Nothing_un_retires_a_runner()
    {
        // THE ABSENT VERB IS THE ASSERTION. Parking and reservation both carry a
        // DELETE twin; this deliberately does not, and a later reader adding one
        // should have to delete this test and say why.
        await Assert.That(
                ProtocolSurface.Endpoints.Any(e => e.Path == "/v1/runners/{id}/retirement"
                                                && e.Method != "POST"))
            .IsFalse()
            .Because("bringing the machine back is registering a runner - a different act, "
                   + "with a different credential - not undoing this one.");
    }

    [Test]
    public async Task Retiring_one_already_retired_is_the_state_the_caller_asked_for()
    {
        // NO 409, exactly as releasing a reservation nobody holds has none:
        // "make sure this is out of the fleet" has to be one call rather than a
        // read and then a write with a race in the middle.
        await Assert.That(Retirement.Statuses).DoesNotContain(409);
        await Assert.That(Retirement.Statuses).Contains(200);
        await Assert.That(Retirement.Statuses).Contains(404)
            .Because("a runner that is not this tenant's answers the way the heartbeat route "
                   + "does: the shape of a refusal must not tell a caller which ids exist.");
    }

    [Test]
    public async Task It_answers_with_when_rather_than_whether()
    {
        // A BOOLEAN WOULD LOSE THE ONLY INTERESTING PART. "Already retired" and
        // "retired just now" are the same outcome and different facts, and the
        // instant is what tells a person whether somebody else got there first.
        var members = Retirement.Response!.GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).Contains("RetiredAt");
        await Assert.That(members).Contains("RunnerId");
    }

    [Test]
    public async Task The_request_names_nothing_because_the_path_already_does()
    {
        // RunnerReservationRequest's argument, applied again: the act is "retire
        // this one", the path names it, and the only member this could grow is a
        // principal - which would make retiring somebody else's runner reachable
        // through a request the caller composes.
        await Assert.That(Retirement.Request!.GetProperties()).IsEmpty();
    }
}
