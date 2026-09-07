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
    [Test]
    public async Task Parked_is_a_state_a_runner_can_be_in()
    {
        await Assert.That(RunnerStates.Parked).IsEqualTo("parked");
    }

    [Test]
    public async Task It_is_the_same_word_the_claim_path_already_uses()
    {
        // ONE VOCABULARY FOR ONE CONDITION. A fleet that said "withheld" while
        // a claim said "parked" would make a person searching for either find
        // half the story.
        await Assert.That(RunnerStates.Parked).IsEqualTo(LeaseClaimStates.Parked);
    }

    [Test]
    public async Task It_is_one_of_the_states_a_fleet_row_may_carry()
    {
        await Assert.That(RunnerStates.All).Contains(RunnerStates.Parked);
    }

    [Test]
    public async Task Why_it_was_parked_travels_with_it()
    {
        // A REASON IS THE POINT OF PARKING. "Withheld" without "why" leaves a
        // person to ask somebody, and the control plane already stores the
        // answer beside parked_at and parked_by.
        var runner = new RunnerSummary
        {
            RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
            Label = "vmlinux001",
            State = RunnerStates.Parked,
            ParkedBecause = "draining before the kernel upgrade",
        };

        await Assert.That(runner.ParkedBecause).IsEqualTo("draining before the kernel upgrade");
    }

    [Test]
    public async Task A_runner_that_is_not_parked_says_nothing_about_why()
    {
        // Optional for the reason every other member here is: an older control
        // plane sends neither the state nor the reason, and a runner nobody
        // parked has no reason to carry.
        var runner = new RunnerSummary
        {
            RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
            Label = "vmlinux001",
            State = RunnerStates.Idle,
        };

        await Assert.That(runner.ParkedBecause).IsEmpty();
    }
}
