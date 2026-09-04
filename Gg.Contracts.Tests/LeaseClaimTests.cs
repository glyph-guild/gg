using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A claim is a request with a status, and the wire says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because two different facts were the same answer.</b> Claiming was a long
/// poll answering <c>204</c> for "nothing came", and an idle fleet and a fleet
/// blocked on something a person needs to fix were indistinguishable. That was
/// tolerable while the control plane could decide a claim from state it already
/// held; it stopped being tolerable when identity moved behind an announcement,
/// because "the credential is not here yet" became a real state and had nowhere
/// to be reported.
/// </para>
/// <para>
/// <b>This is <c>LandingDecision.Settled</c> again, at the other end of the
/// flight.</b> There the fix was a field saying whether the question had been
/// answered at all, so absence could keep meaning one thing. Here it is a state
/// on a request, for the same reason and against the same failure: a runner that
/// read "no lease" as "nothing to do" would sit idle while the thing it needs
/// sits one announcement away.
/// </para>
/// </remarks>
public class LeaseClaimTests
{
    private static Endpoint Endpoint(string method, string path) =>
        ProtocolSurface.Endpoints.Single(e => e.Method == method && e.Path == path);

    [Test]
    public async Task Claiming_is_accepted_rather_than_answered()
    {
        var claim = Endpoint("POST", "/v1/leases:claim");

        await Assert.That(claim.Statuses).Contains(202);
        await Assert.That(claim.Response).IsEqualTo(typeof(LeaseClaimAccepted));

        // 200 AND 204 BOTH GO, and the pair is the point. The old surface used
        // one for "here is your lease" and the other for "nothing came", which
        // is exactly the conflation this replaces: whether a flight can be
        // handed over depends on state that arrives asynchronously, so at the
        // moment the request is taken neither answer is available.
        await Assert.That(claim.Statuses).DoesNotContain(200)
            .Because("a lease answered inline would be the endpoint waiting for an announcement "
                   + "it does not control - the blocking read this whole change removes.");
        await Assert.That(claim.Statuses).DoesNotContain(204)
            .Because("'nothing came' is now one of four states a request can be in, and a status "
                   + "code carrying it would put that fact back in a place with no room for the "
                   + "other three.");
    }

    [Test]
    public async Task The_request_can_be_asked_about()
    {
        var status = Endpoint("GET", "/v1/leases/claims/{id}");

        await Assert.That(status.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(status.Response).IsEqualTo(typeof(LeaseClaimStatus));
        await Assert.That(status.Statuses).Contains(404)
            .Because("a request id that names nothing must be distinguishable from one that names "
                   + "something with nothing to say yet.");
        await Assert.That(status.RequiredHeaders).IsNotEmpty()
            .Because("a runner's request, read with a runner's credential.");
    }

    [Test]
    public async Task Every_state_a_request_can_reach_is_named()
    {
        await Assert.That(LeaseClaimStates.All).IsEquivalentTo(
            new[] { "pending", "waiting", "granted", "expired", "parked" });

        // THE FIFTH STATE ARRIVED, and the note below predicted exactly what it
        // would cost: a contract version, and every prior reader halting on it.
        // `parked` is a person withholding a runner from claiming - which must
        // not read as `pending`, because an idle fleet and a withheld one would
        // then look identical, and that is the collapse `waiting` exists to fix.
        await Assert.That(LeaseClaimStates.All).Contains(LeaseClaimStates.Parked);

        // WAITING IS THE ONE THAT DID NOT EXIST. The other three are shapes the
        // old long poll had, spelled differently; this is the state that had no
        // answer, and the reason the endpoint changed at all.
        await Assert.That(LeaseClaimStates.All).Contains(LeaseClaimStates.Waiting);

        // Closed, so a fifth state costs a contract version and every prior
        // reader halts on it rather than guessing. Discovered by shape, so this
        // asserts the shape rather than a registration somebody could forget.
        await Assert.That(ClosedVocabularies.Discovered()).Contains(typeof(LeaseClaimStates));
    }

    [Test]
    public async Task What_to_ask_and_when_are_both_the_control_planes_to_say()
    {
        var members = ProtocolSurface.JsonMembers[typeof(LeaseClaimAccepted)];

        await Assert.That(members).IsEquivalentTo(new[] { "requestId", "pollAfterSeconds" });

        // THE CADENCE IS SERVER-SUPPLIED AND LOAD-BEARING. The claim used to be
        // a long poll, so the control plane holding the request open WAS the
        // rate limiter - the runner has no backoff of its own. Removing the hold
        // without sending an interval turns every idle runner into a busy loop
        // against this endpoint.
        await Assert.That(typeof(LeaseClaimAccepted).GetProperty("PollAfterSeconds")!.PropertyType)
            .IsEqualTo(typeof(int));
    }

    [Test]
    public async Task A_lease_is_absent_unless_the_request_was_granted()
    {
        var status = new LeaseClaimStatus { State = LeaseClaimStates.Pending };

        await Assert.That(status.Lease).IsNull();
        await Assert.That(status.WaitingOn).IsEmpty()
            .Because("empty rather than null, so a reader that iterates it does not have to ask "
                   + "first - the arrangement every list on this surface already uses.");

        // The state carries the question and the lease carries the answer, which
        // is the only thing that makes an absent lease readable: without it,
        // 'not yet' and 'never' are one silence. Named for what it is here so
        // the next person to add a field reads the reason before the shape.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseClaimStatus)])
            .IsEquivalentTo(new[] { "state", "waitingOn", "lease" });
    }

    [Test]
    public async Task A_repository_the_control_plane_could_not_resolve_is_named_on_the_lease()
    {
        // BECAUSE AN EMPTY CREDENTIAL LIST WAS TWO FACTS. A repository may have
        // no credential registered at all, or its reference may not have reached
        // this control plane's read model yet, and absence alone cannot tell them
        // apart. A runner that treated both as 'this one needs none' fetched
        // ANONYMOUSLY - fine for a public repository, and for a private one a
        // `git exited 128` with nothing pointing at the cause.
        var granted = new LeaseGranted
        {
            LeaseId = "l", Generation = 1, FlightId = "f", FlightNumber = FlightRef.Format(1),
            Repos = [], Credentials = [],
            ClassificationCeiling = Classifications.Internal,
            ClassificationRules = ClassificationRules.Default,
            ExpiresAt = DateTimeOffset.UnixEpoch, RenewWithinSeconds = 5,
        };

        await Assert.That(granted.UnresolvedRepos).IsEmpty()
            .Because("the ordinary flight resolves everything, and it must not have to say so.");

        await Assert.That(ProtocolSurface.JsonMembers[typeof(LeaseGranted)])
            .Contains("unresolvedRepos");
    }
}
