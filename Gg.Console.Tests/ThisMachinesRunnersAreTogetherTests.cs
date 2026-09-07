using Gg.Contracts;
namespace Gg.Console.Tests;
/// <summary>
/// This machine's runners sort to the top of the fleet, together.
/// </summary>
/// <remarks>
/// <para>
/// <b>One row was hoisted and the rest were left where they fell.</b> The lift
/// keyed on <c>LocalRunnerId</c> — the registration in the file THIS console
/// wrote — so a machine that has registered more than once, which is what
/// running <c>gg runner up</c> twice does, had its other runners scattered
/// through a fleet sorted by nothing in particular. The live fleet is fifteen
/// runners, fourteen of them dead registrations from one host, so a row that is
/// not at the top is a row nobody scrolls to.
/// </para>
/// <para>
/// <b>"Mine" and "here" are two different claims and both are kept.</b> The
/// arrow means the runner this console can stop, start and read the log of.
/// The rest of this machine's registrations cannot be acted on from here — they
/// are other processes, mostly gone — but they are still the rows a person
/// sitting at this keyboard is looking for, so they group with it rather than
/// borrow its mark.
/// </para>
/// <para>
/// <b>What this cannot do is group by PERSON.</b> A runner registers with a
/// session, so the control plane knows who brought each one up; RunnerSummary
/// does not carry it, so the console cannot know. The machine is the strongest
/// claim available here, and it is a narrower one — the same person's runners
/// on another host sort with the fleet.
/// </para>
/// </remarks>
public class ThisMachinesRunnersAreTogetherTests
{
    private const string Machine = "Kevins-MBP";
    private static RunnerSummary Runner(string id, string label, string state = RunnerStates.Offline) =>
        new() { RunnerId = id, Label = label, State = state };
    /// <summary>A fleet shaped like the real one: one host, many registrations.</summary>
    private static AppState Fleet(string? local) => new()
    {
        Machine = Machine,
        LocalRunnerId = local,
        Runners = new RunnerList
        {
            Runners =
            [
                Runner("01a06385", "vmlinux001"),
                Runner("01a078bb", Machine),
                Runner("01a06572", "vmlinux001", RunnerStates.Idle),
                Runner("01a06aa2", Machine),
                Runner("01a0632b", "vmlinux001:maintain"),
            ],
        },
    };
    [Test]
    public async Task Every_runner_from_this_machine_is_above_every_other()
    {
        var rows = Rows.Runners(Fleet(local: "01a078bb"));
        await Assert.That(rows.Take(2).All(r => r.Machine)).IsTrue()
            .Because("both registrations from this machine belong together at the top; one "
                   + "of them being reachable from this console is what orders them WITHIN "
                   + "the group, not whether they are in it.");
        await Assert.That(rows.Skip(2).Any(r => r.Machine)).IsFalse()
            .Because("a group with something else in the middle of it is not a group.");
    }
    [Test]
    public async Task The_one_this_console_owns_leads_them()
    {
        var rows = Rows.Runners(Fleet(local: "01a06aa2"));
        await Assert.That(rows[0].Runner).Contains("01a06aa2")
            .Because("it is the only row on the screen with keys behind it - stop, restart, "
                   + "and the log - so it is the one a person is looking for first.");
        await Assert.That(rows[0].Mine).IsTrue();
        await Assert.That(rows[1].Mine).IsFalse();
        await Assert.That(rows[1].Machine).IsTrue();
    }
    [Test]
    public async Task The_fleet_below_keeps_the_order_it_arrived_in()
    {
        // Nothing here is a sort. The control plane decides what order the rest
        // of the fleet is in, and re-deciding it locally would be this console
        // inventing a ranking that the same list read through `gg runner list`
        // would not have.
        var rest = Rows.Runners(Fleet(local: "01a078bb"))
            .Where(r => !r.Machine)
            .Select(r => r.Runner)
            .ToList();
        await Assert.That(rest[0]).Contains("01a06385");
        await Assert.That(rest[1]).Contains("01a06572");
        await Assert.That(rest[2]).Contains("01a0632b");
    }
    [Test]
    public async Task A_label_that_merely_starts_with_this_machines_name_is_not_this_machine()
    {
        // `vmlinux001:maintain` is a real label on the live fleet - the maintain
        // runner names itself after its host with a suffix. Matching on a prefix
        // would put another machine's housekeeping runner in the group a person
        // reads as "mine".
        var rows = Rows.Runners(new AppState
        {
            Machine = "vmlinux001",
            Runners = new RunnerList
            {
                Runners = [Runner("01a0632b", "vmlinux001:maintain"), Runner("01a06572", "vmlinux001")],
            },
        });
        await Assert.That(rows.Single(r => r.Machine).Runner).Contains("01a06572");
    }
    [Test]
    public async Task A_console_that_does_not_know_its_machine_groups_nothing()
    {
        // The name is passed in, like LocalRunnerId, so a caller that never
        // passed one must not have every row silently claim to be local.
        var rows = Rows.Runners(new AppState
        {
            Runners = new RunnerList
            {
                Runners = [Runner("01a078bb", Machine), Runner("01a06572", "vmlinux001")],
            },
        });
        await Assert.That(rows.Any(r => r.Machine)).IsFalse();
    }
    [Test]
    public async Task The_arrow_still_means_the_one_this_console_can_act_on()
    {
        var rows = Rows.Runners(Fleet(local: "01a078bb"));
        await Assert.That(rows.Count(r => r.Here.Trim().Length > 0 && r.Mine)).IsEqualTo(1)
            .Because("stop, restart and the log all act on the runner this console started. "
                   + "A second row wearing the same mark is a key pointed at a process this "
                   + "console has no pidfile for.");
        await Assert.That(rows[1].Here).IsNotEqualTo(rows[0].Here)
            .Because("the group is only legible if a person can see where 'the one I can "
                   + "touch' ends and 'the others from this machine' begins.");
    }
    [Test]
    public async Task The_composition_root_says_which_machine_this_is()
    {
        var root = ConsoleSource.Text("Gg.Cli", "Program.cs");
        await Assert.That(root).Contains("Machine = Environment.MachineName")
            .Because("Rows cannot read the environment - it is pure, and the console's rule "
                   + "is that one place reads what this machine is and hands it on. Unpassed, "
                   + "this groups nothing and the defect is invisible.");
    }
}
