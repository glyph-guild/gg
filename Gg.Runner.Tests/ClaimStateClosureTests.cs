using System.Net;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// Every state the closed claim vocabulary declares is one this binary can
/// read.
/// </summary>
/// <remarks>
/// <para>
/// <b>FOUND BY PARKING A RUNNER IN A REAL DEPLOYMENT, and it took the runner
/// down.</b> Contract 0.94.0 added <c>parked</c> to <c>LeaseClaimStates</c> and
/// taught the control plane to answer it. Nothing taught the RUNNER what to do
/// with it, so the fall-through meant for an out-of-date binary — <i>"which this
/// runner does not know. Its contract version is older than the control
/// plane's"</i> — fired on a binary built from the same commit as the control
/// plane. Parking a runner did not withhold it. It killed it, with an unhandled
/// exception, and the machine stopped taking work until somebody restarted it.
/// </para>
/// <para>
/// <b>The halt is right and its trigger was wrong.</b> A state this binary
/// genuinely does not know must still throw — the closure is what makes a fifth
/// value a version move rather than a guess. What must never happen is the halt
/// firing for a value the binary's OWN contract declares, which is a build that
/// has already shipped the vocabulary and not the behaviour.
/// </para>
/// <para>
/// <b>So this asserts the whole vocabulary, not the one value that bit.</b>
/// Enumerating <c>LeaseClaimStates.All</c> is what makes the next addition fail
/// here — at a compile-and-run, in the repository that declares it — instead of
/// on somebody's pool host at the moment they park a machine.
/// </para>
/// </remarks>
public class ClaimStateClosureTests
{
    [Test]
    public async Task Every_declared_claim_state_is_one_this_binary_can_read()
    {
        // THE RATCHET. A value added to the vocabulary and not to the reader is
        // a runner that halts on it, and the halt reads as "you are out of
        // date" when the truth is that nobody wrote the branch.
        foreach (var state in LeaseClaimStates.All)
        {
            await using var stub = await RunnerConformanceTests.ExerciseAsync(
                async client =>
                {
                    var result = await client.ReadClaimAsync("request-1");
                    await Assert.That(result).IsNotNull()
                        .Because($"'{state}' is declared by this binary's own contract, so "
                               + "reading it must not throw the out-of-date halt.");
                },
                s => s.ClaimReport = new LeaseClaimStatus
                {
                    State = state,
                    Lease = state == LeaseClaimStates.Granted ? StubRunnerSurface.TheLease : null,
                });
        }
    }

    [Test]
    public async Task Parked_is_read_as_itself_rather_than_as_nothing_to_do()
    {
        // PARKED AND IDLE MUST NOT BE ONE SILENCE - the argument `waiting` was
        // added on, and the reason parking is decided before the pick rather
        // than inside the matcher's WHERE. A runner that read this as Nothing
        // would print "nothing ready" on a machine a person has deliberately
        // withheld, which is the collapse the state exists to prevent.
        await using var stub = await RunnerConformanceTests.ExerciseAsync(
            async client =>
            {
                var result = await client.ReadClaimAsync("request-7");
                await Assert.That(result).IsTypeOf<ClaimResult.Parked>();
            },
            s => s.ClaimReport = new LeaseClaimStatus { State = LeaseClaimStates.Parked });
    }

    [Test]
    public async Task A_parked_runner_says_parked_rather_than_nothing_ready()
    {
        // WHAT A PERSON WATCHING THE MACHINE SEES. The console printed
        // "nothing ready" for an idle fleet; a machine somebody deliberately
        // withheld printing the same line is the collapse `parked` exists to
        // prevent, and it is the line an operator would read for a fortnight
        // while wondering why nothing runs here.
        await Assert.That(typeof(IRunnerObserver).GetMethod("Parked")).IsNotNull()
            .Because("the observer is where the loop tells a person what happened, and a "
                   + "parked runner reported through Idle() is indistinguishable from a quiet "
                   + "one - which is exactly the report this state was added to separate.");
    }

    [Test]
    public async Task A_state_nobody_declared_still_halts_loudly()
    {
        // THE CLOSURE, UNTOUCHED. Fixing the false halt must not remove the
        // real one: a value this binary's contract does not declare is a
        // version move, and guessing at it would make the closure decorative.
        await using var stub = await RunnerConformanceTests.ExerciseAsync(
            async client =>
            {
                await Assert.That(async () => await client.ReadClaimAsync("request-8"))
                    .Throws<InvalidOperationException>();
            },
            s => s.ClaimReport = new LeaseClaimStatus { State = "teleported" });
    }
}
