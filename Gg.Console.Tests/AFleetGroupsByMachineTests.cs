using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The fleet is read as machines: a host's resident sits flush, and everything
/// else running on that host sits under it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported from the running console.</b> Members were nested under
/// <c>vmlinux001:maintain</c>, the runner that attests their pool - a
/// registration that never beats and reads as offline - so two healthy
/// members sat under a dead-looking parent. A person expected them under
/// <c>vmlinux001</c>, the machine.
/// </para>
/// <para>
/// <b>The resident is the anchor.</b> On a host the runner whose label IS the
/// machine name is the one a person thinks of as the machine. The maintainer
/// and the members are processes on it, so they sit under it as peers of one
/// another, not the members under the maintainer.
/// </para>
/// <para>
/// <b>No regression while the control plane catches up.</b> A control plane
/// that has not learned machines sends none, and then members still nest
/// under the runner that warmed them, exactly as they do today.
/// </para>
/// </remarks>
public class AFleetGroupsByMachineTests
{
    private const string Resident = "01a06572-a784-7000-8000-000000000000";
    private const string Maintainer = "01a0632b-e971-7000-8000-000000000000";
    private const string Laptop = "01a082c9-5e33-7000-8000-000000000000";

    private static RunnerSummary Runner(
        string id, string label, string? machine = null, string? host = null) => new()
        {
            RunnerId = id,
            Label = label,
            State = "idle",
            Machine = machine,
            HostRunnerId = host,
        };

    private static AppState With(params RunnerSummary[] fleet) =>
        new() { Runners = new RunnerList { Runners = fleet } };

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    [Test]
    public async Task Everything_on_a_machine_sits_under_its_resident()
    {
        var rows = Rows.Runners(With(
            Runner("m1", "gg-pool-ui-1", machine: "vmlinux001", host: Maintainer),
            Runner(Laptop, "Kevins-MBP", machine: "Kevins-MBP"),
            Runner(Maintainer, "vmlinux001:maintain", machine: "vmlinux001"),
            Runner(Resident, "vmlinux001", machine: "vmlinux001"),
            Runner("m2", "gg-pool-ui-2", machine: "vmlinux001", host: Maintainer)));

        var labels = rows.Select(r => r.Label).ToList();
        var resident = labels.IndexOf("vmlinux001");

        await Assert.That(labels.IndexOf("vmlinux001:maintain")).IsGreaterThan(resident);
        await Assert.That(labels.IndexOf("gg-pool-ui-1")).IsGreaterThan(resident);
        await Assert.That(labels.IndexOf("gg-pool-ui-2")).IsGreaterThan(resident);
        await Assert.That(labels.IndexOf("Kevins-MBP") < resident
                || labels.IndexOf("Kevins-MBP") > labels.IndexOf("gg-pool-ui-2"))
            .IsTrue()
            .Because("another machine never lands in the middle of this one's group. Order: "
                   + string.Join(", ", labels));

        var maintainer = rows.Single(r => r.Label == "vmlinux001:maintain");
        var member = rows.Single(r => r.Label == "gg-pool-ui-1");

        await Assert.That(Indent(Rows.Nested(maintainer)))
            .IsEqualTo(Indent(Rows.Nested(member)))
            .Because("the maintainer and the members are all processes on the machine, so they "
                   + "sit at one depth under it - a member is not a child of the maintainer.");
        await Assert.That(Indent(Rows.Nested(member)))
            .IsGreaterThan(Indent(Rows.Nested(rows.Single(r => r.Label == "vmlinux001"))));
    }

    [Test]
    public async Task A_machine_sits_flush_and_so_does_a_laptop()
    {
        var rows = Rows.Runners(With(
            Runner(Resident, "vmlinux001", machine: "vmlinux001"),
            Runner(Laptop, "Kevins-MBP", machine: "Kevins-MBP")));

        await Assert.That(rows.All(r => Indent(Rows.Nested(r)) == 0)).IsTrue()
            .Because("a resident is the machine, and a laptop running nothing else is a "
                   + "machine with nothing under it.");
    }

    [Test]
    public async Task Without_machines_a_member_still_nests_under_its_host()
    {
        // THE TRANSITION. An older control plane sends no machines, and the
        // fleet must look exactly as it did rather than flatten.
        var rows = Rows.Runners(With(
            Runner(Maintainer, "vmlinux001:maintain"),
            Runner("m1", "gg-pool-ui-1", host: Maintainer)));

        var member = rows.Single(r => r.Label == "gg-pool-ui-1");
        var labels = rows.Select(r => r.Label).ToList();

        await Assert.That(labels.IndexOf("gg-pool-ui-1"))
            .IsEqualTo(labels.IndexOf("vmlinux001:maintain") + 1);
        await Assert.That(Indent(Rows.Nested(member))).IsGreaterThan(0)
            .Because("with nothing better to go on, the runner that warmed a member is still "
                   + "the right place to show it.");
    }

    [Test]
    public async Task A_machine_with_no_resident_falls_back_to_the_host()
    {
        // A host running only a maintainer has no anchor to sit under. Its
        // members still belong somewhere a person can see, and the runner
        // that warmed them is that place.
        var rows = Rows.Runners(With(
            Runner(Maintainer, "vmlinux001:maintain", machine: "vmlinux001"),
            Runner("m1", "gg-pool-ui-1", machine: "vmlinux001", host: Maintainer)));

        var maintainer = rows.Single(r => r.Label == "vmlinux001:maintain");
        var member = rows.Single(r => r.Label == "gg-pool-ui-1");

        await Assert.That(Indent(Rows.Nested(maintainer))).IsEqualTo(0);
        await Assert.That(Indent(Rows.Nested(member))).IsGreaterThan(0);
    }
}
