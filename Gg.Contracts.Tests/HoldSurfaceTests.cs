using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A takeover is a HOLD now, and the declaration is where that becomes true.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was there recorded a takeover after the fact.</b>
/// <c>POST /v1/flights/{id}/takeover</c> took a <c>TakeoverRecord</c> carrying
/// <c>heldForMs</c> - how long somebody held it - so it was written when they were
/// already done. That is a record, not a hold: two people on two machines both see
/// a takeable flight, both take it, and both find out afterwards.
/// </para>
/// <para>
/// <b>So the claim comes first and the return comes last.</b> Three routes where
/// there was one, and the middle one exists because a hold has to expire on a
/// clock: a person who closes their laptop stops renewing, and nothing has to
/// judge whether they are still at their desk.
/// </para>
/// </remarks>
public class HoldSurfaceTests
{
    private static Endpoint? Route(string path) =>
        ProtocolSurface.Endpoints.SingleOrDefault(e => e.Path == path && e.Method == "POST");

    [Test]
    public async Task Claiming_a_flight_is_a_route_of_its_own_that_can_be_refused()
    {
        var claim = Route("/v1/flights/{ref}/takeover:claim");

        await Assert.That(claim).IsNotNull();
        await Assert.That(claim!.Response).IsEqualTo(typeof(TakeoverClaimed));

        await Assert.That(claim.Statuses).Contains(409)
            .Because("a second claimant is REFUSED, and refused is an outcome of correct client "
                   + "behaviour rather than a client bug - two people looking at the same stopped "
                   + "flight is the ordinary case this exists for.");

        await Assert.That(claim.Audience).IsEqualTo(Audience.Developer)
            .Because("a runner able to claim a takeover could hold a flight against the person it "
                   + "is meant to be waiting for.");
        await Assert.That(claim.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
    }

    [Test]
    public async Task A_refusal_names_who_holds_it_and_since_when()
    {
        // THE REFUSAL IS THE PRODUCT HERE. "Somebody else has this" sends a person
        // nowhere; "Ada has held it since 09:12 and until 09:42" tells them whether
        // to wait, ask, or come back - and it is the only thing standing between
        // this and two people editing divergent copies of one flight.
        var members = ProtocolSurface.JsonMembers[typeof(TakeoverHeld)];

        await Assert.That(members).Contains("by");
        await Assert.That(members).Contains("since");
        await Assert.That(members).Contains("heldUntil");
    }

    [Test]
    public async Task A_hold_is_renewed_behind_a_generation_fence()
    {
        var renew = Route("/v1/flights/{ref}/takeover:renew");

        await Assert.That(renew).IsNotNull();
        await Assert.That(renew!.Request).IsEqualTo(typeof(TakeoverRenewalRequest));
        await Assert.That(renew.Response).IsEqualTo(typeof(TakeoverRenewed));

        await Assert.That(renew.Statuses).Contains(409)
            .Because("the same fence POST /v1/leases/{id}/renew uses, for the same reason: a "
                   + "holder whose hold lapsed and was taken by somebody else must be told it is "
                   + "no longer theirs rather than handed it back.");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(TakeoverClaimed)])
            .Contains("renewWithinSeconds")
            .Because("a cadence the client invents is a cadence that is wrong on one of the two "
                   + "machines. DeviceAuthorizationStarted.PollIntervalSeconds is the precedent.");
    }

    [Test]
    public async Task A_decision_comes_back_on_a_route_rather_than_in_a_file()
    {
        var ret = Route("/v1/flights/{ref}/takeover:return");

        await Assert.That(ret).IsNotNull();
        await Assert.That(ret!.Request).IsEqualTo(typeof(TakeoverReturnRequest));

        await Assert.That(ret.Response).IsNull()
            .Because("the write is a command: the record is appended and what a person needs is on "
                   + "the flight log a moment later. A second shape to keep in step buys nothing.");
        await Assert.That(ret.Statuses).Contains(202);
        await Assert.That(ret.Statuses).Contains(409)
            .Because("returning against a hold somebody else now holds is refused, not applied.");
    }

    [Test]
    public async Task The_wire_return_carries_a_generation_and_the_file_return_does_not()
    {
        // TWO TYPES ON PURPOSE, and the split is about who writes each one.
        //
        // TakeoverReturn is a FILE a person writes, so it names the flight - a
        // leftover file from a previous takeover parses perfectly and describes a
        // different flight, and applying it would put one flight's decision onto
        // another. It cannot carry a generation, because nobody types one.
        //
        // TakeoverReturnRequest is what gg POSTs, and the flight is in the path.
        // What it needs instead is the generation, so a decision cannot be applied
        // to a hold that has since moved to somebody else.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(TakeoverReturnRequest)])
            .Contains("generation");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(TakeoverReturn)])
            .DoesNotContain("generation")
            .Because("a person writing a decision by hand cannot know a generation, and a file "
                   + "that required one would be a file nobody can write.");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(TakeoverReturn)]).Contains("flightId")
            .Because("this is the field that makes reading a leftover file safe.");
    }

    [Test]
    public async Task The_route_that_recorded_a_takeover_afterwards_is_gone()
    {
        // DELETED RATHER THAN LEFT BESIDE THE NEW ONE. It is reachable only through
        // a client method nothing in the product calls, because nothing in the
        // product ever took a flight over - so it is unreleased in effect, and
        // Article VI says argue that in the change that makes it rather than leave
        // dead surface for somebody to find and use.
        //
        // Leaving it would be worse than a tidiness problem: a route that records a
        // takeover without holding anything is exactly the read-then-act shape the
        // claim replaces, sitting next to the claim, declared and served.
        await Assert.That(ProtocolSurface.Endpoints.Any(e => e.Path == "/v1/flights/{id}/takeover"))
            .IsFalse();

        await Assert.That(Vocabulary.Types.Any(t => t.Name == "TakeoverRecord")).IsFalse()
            .Because("a wire type no route carries is a shape somebody will build against.");
    }

    [Test]
    public async Task Every_new_hold_type_is_pinned_and_in_the_vocabulary()
    {
        foreach (var type in (Type[])
            [typeof(TakeoverClaimed), typeof(TakeoverHeld), typeof(TakeoverRenewalRequest),
             typeof(TakeoverRenewed), typeof(TakeoverReturnRequest)])
        {
            await Assert.That(
                    type.GetCustomAttributes(typeof(PinnedIdAttribute), inherit: false).Length)
                .IsEqualTo(1)
                .Because($"{type.Name} crosses, and everything that crosses is pinned.");

            await Assert.That(Vocabulary.Types).Contains(type);
        }
    }
}
