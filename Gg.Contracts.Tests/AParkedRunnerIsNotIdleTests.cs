using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A runner somebody withheld reads as withheld, not as one with no work.
/// </summary>
/// <remarks>
/// <para>
/// <b>The argument is already in this repository, one path over.</b>
/// <see cref="LeaseClaimStates.Parked"/> exists because a parked runner filtered
/// out inside the matcher would answer <c>pending</c> — the same answer an idle
/// fleet gives — and its remark calls collapsing those two silences <i>the
/// defect <see cref="LeaseClaimStates.Waiting"/> was added to fix</i>.
/// </para>
/// <para>
/// <b>The fleet read collapses exactly those two silences.</b>
/// <see cref="RunnerStates"/> is offline, busy and idle, so a machine a person
/// deliberately withheld is indistinguishable from one that simply has nothing
/// to do. The control plane holds the answer and does not send it.
/// </para>
/// <para>
/// <b>Derived, never reported.</b> Like every other runner state: a runner that
/// could tell the fleet it was parked could tell it that while claiming, and
/// <see cref="RunnerSummary.State"/> is documented as derived for that reason.
/// </para>
/// </remarks>
public class AParkedRunnerIsNotIdleTests
{
    private static RunnerSummary Runner(string state, DateTimeOffset? parked = null) => new()
    {
        RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
        Label = "vmlinux001",
        State = state,
        ParkedAt = parked,
        ParkedBecause = parked is null ? "" : "draining before the kernel upgrade",
    };

    [Test]
    public async Task Parking_is_carried_beside_the_state_rather_than_inside_it()
    {
        // THE ASYMMETRY, AND IT IS LOAD-BEARING RATHER THAN TIDY. State says
        // what a runner IS DOING; parking says what policy allows. They are
        // different axes, and the case that proves it is below.
        await Assert.That(RunnerStates.All).DoesNotContain("parked");

        var parked = Runner(RunnerStates.Idle, DateTimeOffset.UnixEpoch);

        await Assert.That(parked.ParkedAt).IsNotNull();
        await Assert.That(parked.ParkedBecause).IsEqualTo("draining before the kernel upgrade");
    }

    [Test]
    public async Task A_runner_can_be_parked_and_still_working()
    {
        // THE CASE A FOURTH STATE COULD NOT CARRY, and the reason to park
        // anything: let it finish what it has and take nothing more. Parking
        // withholds CLAIMING - RunnerParking never touches a lease - so a
        // draining runner is busy and parked at once. As a state, one of those
        // two facts has to be thrown away.
        var draining = Runner(RunnerStates.Busy, DateTimeOffset.UnixEpoch);

        await Assert.That(draining.State).IsEqualTo(RunnerStates.Busy);
        await Assert.That(draining.ParkedAt).IsNotNull();
    }

    [Test]
    public async Task A_parked_runner_that_stopped_beating_is_still_offline()
    {
        // Parking is not a way to take a machine away, so it does not outrank
        // the one state that is decided before anything else.
        var gone = Runner(RunnerStates.Offline, DateTimeOffset.UnixEpoch);

        await Assert.That(gone.State).IsEqualTo(RunnerStates.Offline);
        await Assert.That(gone.ParkedAt).IsNotNull();
    }

    [Test]
    public async Task A_runner_nobody_parked_carries_neither()
    {
        // Optional for the reason every member added after 1.0 is: an older
        // control plane sends neither, and a runner nobody withheld has no when
        // and no why.
        var free = Runner(RunnerStates.Idle);

        await Assert.That(free.ParkedAt).IsNull();
        await Assert.That(free.ParkedBecause).IsEmpty();
    }

    [Test]
    public async Task The_word_is_the_one_the_claim_path_already_uses()
    {
        // ONE VOCABULARY FOR ONE CONDITION. The claim path made `parked` a
        // first-class outcome because there the values ARE mutually exclusive -
        // a claim was pending, or waiting, or refused because parked, and never
        // two of those. That is why it is a state there and a fact here.
        await Assert.That(LeaseClaimStates.Parked).IsEqualTo("parked");
    }
}
