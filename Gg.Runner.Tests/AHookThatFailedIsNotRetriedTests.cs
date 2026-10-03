using Gg.Contracts;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A point that did not work ends the flight, and the refusal names which point.
/// </summary>
/// <remarks>
/// <para>
/// <b>S58.2-02, and it is new behaviour rather than a rename.</b> Before slice
/// fifty-eight the loop PERFORMED a bring-up, recorded what happened as a
/// <c>stack.performed</c> fact, and then carried on regardless — the only reader
/// of the result collected it for shipping. So a stack that failed to come up
/// handed an agent an environment that was not there, and the agent spent its
/// budget finding out.
/// </para>
/// <para>
/// <b>Which point, because five of them can fail and they fail differently.</b>
/// A <c>prepare</c> that could not build is a tree or a registry problem; an
/// <c>attach</c> that exited non-zero is the stack itself; a <c>detach</c> that
/// failed left something running for the next flight to trip over. "A hook
/// failed" sends a reader to read all five.
/// </para>
/// <para>
/// <b>Not retried, deliberately.</b> <see cref="StackScript"/> already retries
/// the START of a process three times, because a fork can lose a race with the
/// filesystem — that is a different failure from a script that ran and said no.
/// Running a bring-up again because it failed is how one half-built stack
/// becomes two.
/// </para>
/// <para>
/// <b>A pure decision, so the loop has nothing to get wrong.</b> The judgement
/// is a function of the point and the performance; the loop releases the lease.
/// Putting the judgement in the loop would make it untestable without driving a
/// claim, which is how the previous version came to ignore an exit code at all.
/// </para>
/// </remarks>
public class AHookThatFailedIsNotRetriedTests
{
    private static StackScript.Performance Performance(
        string outcome, int? exit) => new(outcome, exit, TimeSpan.FromSeconds(1), Survived: false);

    [Test]
    public async Task A_point_that_exited_zero_is_not_a_refusal()
    {
        foreach (var point in EnvironmentPoints.All)
        {
            await Assert.That(StackScript.Refusal(point, Performance(StackOutcomes.Exited, 0)))
                .IsNull()
                .Because($"'{point}' ran and said it worked, which is the whole of what a zero "
                       + "exit means here.");
        }
    }

    [Test]
    public async Task A_non_zero_exit_is_a_refusal_naming_the_point_and_the_code()
    {
        foreach (var point in EnvironmentPoints.All)
        {
            var refusal = StackScript.Refusal(point, Performance(StackOutcomes.Exited, 3));

            await Assert.That(refusal).IsNotNull()
                .Because("before this the loop recorded the exit code as a fact and carried "
                       + "on, so a stack that failed handed an agent an environment that was "
                       + "not there.");

            await Assert.That(refusal!).Contains(point)
                .Because("five points can fail and they fail differently - a prepare that "
                       + "could not build is not an attach that would not come up - so 'a "
                       + "hook failed' sends a reader to read all five.");

            await Assert.That(refusal!).Contains("3")
                .Because("the code is what the author of the script will look up first.");
        }
    }

    [Test]
    public async Task A_timeout_is_a_refusal_and_says_the_patience_rather_than_a_code()
    {
        // NO EXIT CODE EXISTS, which the Performance record already says: `Exit`
        // is "the exit code, for the one outcome that has one". A refusal
        // claiming code 0 here would read as success.
        var refusal = StackScript.Refusal(
            EnvironmentPoints.Attach, Performance(StackOutcomes.Timeout, exit: null));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains(EnvironmentPoints.Attach);
        await Assert.That(refusal!).DoesNotContain("exit 0")
            .Because("a script that never exited has no code, and reporting one would make a "
                   + "hang read as a success.");
    }

    [Test]
    public async Task A_point_that_would_not_start_is_a_refusal()
    {
        // THE FILE IS THERE AND THE HOST WOULD NOT RUN IT - the third outcome,
        // and the one a missing shebang or a lost executable bit produces. It is
        // not a stack that said no; it is a hook that never spoke.
        var refusal = StackScript.Refusal(
            EnvironmentPoints.Prepare, Performance(StackOutcomes.Unstartable, exit: null));

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains(EnvironmentPoints.Prepare);
    }
}
