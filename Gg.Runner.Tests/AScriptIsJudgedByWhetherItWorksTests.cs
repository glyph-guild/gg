using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Environments;

namespace Gg.Runner.Tests;

/// <summary>
/// A stack script's performance is measured and recorded, so whether it works is
/// a question the record answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six step 6, as the owner replaced it on 2026-09-30:</b> <i>we
/// do not need to worry about a team that knows its own stack — the thing to
/// measure is whether the script works.</i> The criterion this replaces gated
/// where a script came from, which would have refused a document from a team who
/// knew their stack perfectly well, and would have answered the wrong question
/// anyway: a script written after ten rehearsals can be wrong, and one written
/// from memory can be right. Only performing it settles that.
/// </para>
/// <para>
/// <b>And the measurement found two defects in what had just shipped.</b> The
/// runner performed a script and <i>ignored its exit code entirely</i>, so a
/// script that failed was indistinguishable from one that worked. And the
/// ten-minute patience raised <c>OperationCanceledException</c>, which the catch
/// below it does not name — so a hung script threw out of the flight reading as
/// a cancellation, and left the process running on the host.
/// </para>
/// <para>
/// <b>Three outcomes, named rather than inferred.</b> A duration would let a
/// reader guess timeout from unstartable, and guessing from a number is the
/// inference this codebase keeps catching. <c>exit</c> is present for exactly one
/// of them, because a script that never exited has no code and a zero there would
/// read as success.
/// </para>
/// <para>
/// <b>It still does not fail the flight</b>, and the fact is why that is now
/// defensible rather than merely stated: the work failing against a stack that is
/// not there is no longer the only account of what happened.
/// </para>
/// </remarks>
public class AScriptIsJudgedByWhetherItWorksTests
{
    /// <summary>A directory with one executable script in it, deleted by the caller.</summary>
    private static (string Tree, string Script) AScript(string body)
    {
        var tree = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "gg-works-" + Guid.NewGuid().ToString("n"))).FullName;

        var script = Path.Combine(tree, "stack.sh");
        File.WriteAllText(script, "#!/bin/sh\n" + body + "\n");
        // GUARDED FOR THE ANALYZER, as WorkingTreeRoot and HandoffRoot are. A
        // stack script is performed on a Linux pool host; this repository's CI is
        // linux-x64 and the development machine is macOS.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite
                                       | UnixFileMode.UserExecute);
        }

        return (tree, script);
    }

    [Test]
    public async Task A_script_that_works_says_so()
    {
        var (tree, script) = AScript("exit 0");

        try
        {
            var performance = await StackScript.PerformAsync(
                script, StackScript.Attach, tree, StackScript.Patience);

            await Assert.That(performance.Outcome).IsEqualTo(StackOutcomes.Exited);
            await Assert.That(performance.Exit).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_script_that_fails_says_how_it_exited()
    {
        // THE DEFECT THIS REPLACED A GATE WITH. The runner waited for the process
        // and read nothing off it, so `exit 3` and `exit 0` were the same event -
        // and the flight went on to fail against a stack that was not there with
        // nothing in the record naming the cause.
        var (tree, script) = AScript("exit 3");

        try
        {
            var performance = await StackScript.PerformAsync(
                script, StackScript.Attach, tree, StackScript.Patience);

            await Assert.That(performance.Outcome).IsEqualTo(StackOutcomes.Exited);
            await Assert.That(performance.Exit).IsEqualTo(3)
                .Because("a script that works and a script that does not must not produce the "
                       + "same record, which is the whole of what this criterion asks.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_script_that_cannot_start_is_recorded_rather_than_swallowed()
    {
        // THE DOCUMENT NAMED A FILE THE TREE HAS, AND IT IS NOT EXECUTABLE - a
        // mode lost in a checkout, which is ordinary. The caller used to catch
        // this and narrate nothing, so the only thing left was a flight failing
        // for reasons nobody could connect to a script.
        var (tree, script) = AScript("exit 0");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        try
        {
            var performance = await StackScript.PerformAsync(
                script, StackScript.Attach, tree, StackScript.Patience);

            await Assert.That(performance.Outcome).IsEqualTo(StackOutcomes.Unstartable);
            await Assert.That(performance.Exit).IsNull()
                .Because("a process that never ran has no exit code, and a zero here would "
                       + "read as a bring-up that worked.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task A_script_that_hangs_is_abandoned_and_does_not_outlive_the_flight()
    {
        // THE SECOND DEFECT. The patience cancelled WaitForExitAsync, which raises
        // OperationCanceledException - a type the catch below it does not name -
        // so a hung bring-up threw out of the flight reading as a cancellation.
        // And nothing killed the process: it stayed on the host holding the ports
        // the next flight needs, which is exactly what rule 2 exists to prevent.
        //
        // PATIENCE IS THE CALLER'S, so this can ask for a short one. The runner
        // passes StackScript.Patience and that constant is still the policy.
        var (tree, script) = AScript("sleep 600");

        try
        {
            var performance = await StackScript.PerformAsync(
                script, StackScript.Attach, tree, TimeSpan.FromMilliseconds(250));

            await Assert.That(performance.Outcome).IsEqualTo(StackOutcomes.Timeout);
            await Assert.That(performance.Exit).IsNull();
            await Assert.That(performance.Survived).IsFalse()
                .Because("a bring-up abandoned while its process keeps running is an instance "
                       + "the next flight cannot use, and the reclaim is a backstop rather "
                       + "than a licence to leave one.");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Test]
    public async Task The_performance_carries_no_path_of_its_own()
    {
        // ONE SOURCE FOR THE PATH ON THE WIRE, and it is the lease's. What this
        // method is handed is resolved against the checkout, so it is absolute
        // and names where /srv/env puts a tree on the pool host - a host layout
        // the control plane has no business holding. Carrying none forces the
        // caller to use the relative path the kind declared.
        await Assert.That(typeof(StackScript.Performance).GetProperties()
                .Select(property => property.Name).Order(StringComparer.Ordinal).ToList())
            .IsEquivalentTo(new[] { "Exit", "Outcome", "Survived", "Took" }
                .Order(StringComparer.Ordinal).ToList());
    }

    [Test]
    public async Task The_measurement_crosses()
    {
        await Assert.That(FactKinds.All).Contains(FactKinds.StackPerformed);

        await Assert.That(ProtocolSurface.JsonMembers[typeof(StackPerformed)])
            .IsEquivalentTo(new[] { "script", "verb", "outcome", "exit", "seconds" });
    }

    [Test]
    public async Task It_measures_the_episode_rather_than_describing_a_thing()
    {
        // environment.reclaimed WAS SHIPPED Subject BY COPYING ITS NEIGHBOUR and
        // good-grief's guard caught it a version later. This is the same shape as
        // that one - what this flight's runner did to prepare its place - so it
        // is classified beside it rather than beside environment.identity.
        await Assert.That(FactCategories.Of(FactKinds.StackPerformed))
            .IsEqualTo(FactCategories.Flight);
    }

    [Test]
    public async Task An_exit_code_is_present_for_exactly_one_outcome()
    {
        var performed = new StackPerformed
        {
            Script = "scripts/stack.ps1",
            Verb = StackScript.Attach,
            Outcome = StackOutcomes.Exited,
            Exit = 0,
            Seconds = 4,
        };

        await Assert.That(StackPerformed.Validate(performed)).IsNull();

        await Assert.That(StackPerformed.Validate(performed with { Exit = null })).IsNotNull()
            .Because("a performance that exited and did not say how is the measurement this "
                   + "criterion asks for, missing.");

        await Assert.That(StackPerformed.Validate(
                performed with { Outcome = StackOutcomes.Timeout, Exit = 0 })).IsNotNull()
            .Because("a zero beside an outcome that never exited reads as a bring-up that "
                   + "worked, which is the opposite of what happened.");

        await Assert.That(StackPerformed.Validate(
                performed with { Outcome = StackOutcomes.Timeout, Exit = null })).IsNull();
    }

    [Test]
    public async Task A_measurement_nobody_can_act_on_is_refused()
    {
        var performed = new StackPerformed
        {
            Script = "scripts/stack.ps1",
            Verb = StackScript.Attach,
            Outcome = StackOutcomes.Exited,
            Exit = 0,
            Seconds = 4,
        };

        await Assert.That(StackPerformed.Validate(performed with { Script = "  " })).IsNotNull()
            .Because("a kind may name several scripts over its life and a measurement of an "
                   + "unnamed one cannot be compared with anything.");

        await Assert.That(StackPerformed.Validate(performed with { Verb = "restart" })).IsNotNull()
            .Because("the verbs are the two halves of one procedure, and a third would be a "
                   + "reader guessing what a runner did.");

        await Assert.That(StackPerformed.Validate(performed with { Outcome = "fine" })).IsNotNull();

        await Assert.That(StackPerformed.Validate(performed with { Seconds = -1 })).IsNotNull();
    }
}
