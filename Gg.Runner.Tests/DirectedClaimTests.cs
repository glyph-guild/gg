using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner can ask for one named flight, and asking for none is what every
/// runner has always done.
/// </summary>
/// <remarks>
/// <para>
/// <b>One optional member, and the whole of the wire change for flying by
/// hand.</b> A person at a terminal is a runner that wants a specific flight —
/// the one they just opened — rather than whatever the fleet has ready. There
/// is no second endpoint, no second vocabulary and no second set of claim
/// states: <c>pending</c>, <c>waiting</c>, <c>granted</c>, <c>expired</c> and
/// <c>parked</c> all mean here exactly what they mean to the fleet.
/// </para>
/// <para>
/// <b>Optional is the compatibility claim and it is asserted rather than
/// assumed.</b> A runner built before this exists sends three members and must
/// keep being understood, because the fleet is not upgraded in step with the
/// control plane. Asserted through the runner's OWN serializer context rather
/// than a fresh options bag, because that context is what actually writes the
/// request - a shape proved against a different serializer proves nothing about
/// what crosses.
/// </para>
/// <para>
/// <b>What this does NOT settle is whether the flight can be granted.</b> The
/// request says which flight is wanted; every check that decides whether this
/// runner may have it lives on the other side, and step 0 measured that there
/// are six of them rather than the two everybody remembers.
/// </para>
/// </remarks>
public class DirectedClaimTests
{
    private static LeaseClaimRequest Asking(string? flightId = null) => new()
    {
        RunnerId = "runner-1",
        Labels = ["linux"],
        MaxWaitSeconds = 30,
        FlightId = flightId,
    };

    [Test]
    public async Task A_claim_can_name_the_flight_it_wants()
    {
        await Assert.That(Asking("01a072d5-d397-72b3-b1db-54acafcb9c01").FlightId)
            .IsEqualTo("01a072d5-d397-72b3-b1db-54acafcb9c01");
    }

    [Test]
    public async Task A_claim_that_names_none_is_every_claim_before_this_one()
    {
        // NULL IS THE FLEET, and it has to be reachable without saying so: a
        // runner built before this member existed sends three members, and the
        // fleet is not upgraded in step with the control plane.
        await Assert.That(Asking().FlightId).IsNull();
    }

    [Test]
    public async Task The_ordinary_claim_round_trips_unchanged()
    {
        // IT WRITES `"flightId": null`, AND THAT IS THE PACKAGE'S CONVENTION
        // RATHER THAN AN OVERSIGHT. Every optional member in this contract does
        // - `baseRef` and `continuesFrom` on a repo reference, `lease` on a
        // claim status - and no JsonIgnore appears anywhere in it. This test was
        // first written asserting the member is ABSENT, which would have made
        // one member behave unlike its neighbours for a compatibility reason
        // that does not exist: what a reader depends on is that the three
        // members it knows are unchanged and that an unknown one is ignorable.
        //
        // So the claim is the round trip, not the byte count.
        var written = JsonSerializer.Serialize(Asking(), RunnerJsonContext.Default.LeaseClaimRequest);
        var read = JsonSerializer.Deserialize(written, RunnerJsonContext.Default.LeaseClaimRequest)!;

        await Assert.That(read.RunnerId).IsEqualTo("runner-1");
        await Assert.That(read.Labels).IsEquivalentTo(new[] { "linux" });
        await Assert.That(read.MaxWaitSeconds).IsEqualTo(30);
        await Assert.That(read.FlightId).IsNull();
    }

    [Test]
    public async Task An_older_runners_request_still_parses()
    {
        var read = JsonSerializer.Deserialize(
            """{"runnerId":"runner-1","labels":["linux"],"maxWaitSeconds":30}""",
            RunnerJsonContext.Default.LeaseClaimRequest);

        await Assert.That(read).IsNotNull();
        await Assert.That(read!.FlightId).IsNull();
        await Assert.That(read.RunnerId).IsEqualTo("runner-1");
    }

    [Test]
    public async Task The_protocol_surface_declares_it()
    {
        // THE TWO REPOSITORIES CANNOT REFERENCE EACH OTHER, so this declaration
        // is the only thing holding the member's name together across them.
        // good-grief's ProtocolConformanceTests reads exactly this list.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseClaimRequest)])
            .Contains("flightId");
    }

    [Test]
    public async Task The_claim_states_are_unchanged()
    {
        // A DIRECTED CLAIM REUSES EVERY ONE OF THEM, which is the argument for
        // not building a second path: `pending` becomes "not yet", `waiting`
        // still names the repositories whose credential has not arrived - which
        // is precisely the sentence somebody staring at a terminal needs - and
        // `parked` still means a person withheld this machine.
        //
        // Asserted here because "no new vocabulary" is a claim about this file,
        // and a fifth state added quietly for the attended path would be the
        // second lease path arriving under another name.
        await Assert.That(LeaseClaimStates.All).IsEquivalentTo(new[]
        {
            LeaseClaimStates.Pending,
            LeaseClaimStates.Waiting,
            LeaseClaimStates.Granted,
            LeaseClaimStates.Expired,
            LeaseClaimStates.Parked,
        });
    }
}
