using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// Withholding a runner from claiming, by a person, without it looking dead.
/// </summary>
/// <remarks>
/// <para>
/// <b>A runner never reports its own status.</b> Parking is a person's
/// declaration, recorded control-plane-side; there is still no endpoint by which
/// a runner says how it is, and adding one here would be the first.
/// </para>
/// <para>
/// <b>Parked is its own claim state, not a filtered-out flight.</b> A parked
/// runner whose claims were simply narrowed away would answer <c>pending</c> —
/// the same answer an idle fleet gets — and collapsing those two silences is
/// exactly the defect <c>Waiting</c> was added to fix: <i>"an idle fleet and a
/// fleet blocked on something looked identical."</i>
/// </para>
/// <para>
/// <b>Growing a closed vocabulary is a version move, and an old runner halting
/// on it is the design working.</b> The vocabulary is closed precisely so a
/// fifth value is visible; guessing would make the closure decorative. Which
/// makes deploy order load-bearing: binaries first, or refuse old revisions at
/// the door — never discover it by parking a runner in production.
/// </para>
/// <para>
/// <b><c>RunnerStates</c> does NOT grow, and the asymmetry is the point.</b>
/// State is derived from three facts, and the precedent is to carry a fourth
/// fact BESIDE the state rather than multiply states. Busy and parked reads
/// <i>draining</i>; idle and parked reads <i>parked</i>; and offline is still
/// decided first, because a parked machine that died is dead.
/// </para>
/// </remarks>
public class ARunnerCanBeParkedTests
{
    private static Gg.Contracts.Description.Endpoint Endpoint(string method) =>
        ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/runners/{id}/parking" && e.Method == method);

    [Test]
    public async Task Parked_is_a_claim_state_of_its_own()
    {
        await Assert.That(LeaseClaimStates.All).Contains(LeaseClaimStates.Parked);
        await Assert.That(LeaseClaimStates.Parked).IsNotEqualTo(LeaseClaimStates.Pending)
            .Because("a parked runner answering `pending` is an idle fleet and a withheld one "
                   + "reading identically, which is the collapse `waiting` exists to prevent.");
    }

    [Test]
    public async Task Both_verbs_are_a_persons_act()
    {
        // A RUNNER MUST NOT PARK ITSELF. Parking withholds work from a machine;
        // a machine able to declare it would be reporting its own status, which
        // this protocol has never had and does not gain here.
        foreach (var method in (string[])["POST", "DELETE"])
        {
            await Assert.That(Endpoint(method).Audience).IsEqualTo(Audience.Developer);
            await Assert.That(Endpoint(method).RequiredHeaders)
                .DoesNotContain(ProtocolSurface.RunnerHeader);
        }
    }

    [Test]
    public async Task Parking_carries_a_reason_a_person_can_read()
    {
        // A runner that takes nothing for a fortnight with no reason attached is
        // the failure mode; the reason is what step 7's withheld-flight sentence
        // will quote back.
        var members = Endpoint("POST").Request!.GetProperties().Select(p => p.Name);

        await Assert.That(members).Contains("Reason");
    }

    [Test]
    public async Task RunnerStates_does_not_grow()
    {
        // THE ASYMMETRY IS THE POINT. Parking is a fact carried beside the
        // state, not a fifth state - the precedent RunnerSnapshot already sets
        // by carrying the last beat and the advertised labels so a person can
        // see WHY a runner reads offline.
        await Assert.That(RunnerStates.All).DoesNotContain("parked");
    }
}
