using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Members follow the machine that runs them, indented, instead of standing
/// level with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three machines under fifteen members is a list nobody can use.</b> A
/// member is a runner and belongs in the fleet - it beats, it takes a
/// credential by send, and a maintenance ask can name it - but it is not a
/// peer of the machines a person operates. Sorted flat by the fleet's own
/// order, the machines scatter among the things they created.
/// </para>
/// <para>
/// <b>An orphan is still a row.</b> A member whose host is not in the listing
/// has to stay visible, because the case a person is most likely to be in is
/// the one where something is already wrong.
/// </para>
/// </remarks>
public class AMemberSitsUnderItsHostTests
{
    private const string HostId = "01a0a856-eac3-7671-9f0e-000000000000";
    private const string OtherId = "01a0a81c-2d70-7393-a355-fa3d134ba06c";

    private static RunnerSummary Runner(string id, string label, string? host = null) => new()
    {
        RunnerId = id,
        Label = label,
        State = "idle",
        HostRunnerId = host,
    };

    private static AppState With(params RunnerSummary[] fleet) =>
        new() { Runners = new RunnerList { Runners = fleet } };

    [Test]
    public async Task A_member_follows_its_host_rather_than_the_fleets_order()
    {
        // THE FLEET'S ORDER PUTS THEM ANYWHERE. Here the member arrives first,
        // which is exactly what a listing ordered by registration does once a
        // pool has been rebuilt a few times.
        var rows = Rows.Runners(With(
            Runner("m1", "gg-pool-ui-1", host: HostId),
            Runner(HostId, "vmlinux001"),
            Runner(OtherId, "Kevins-MBP")));

        var labels = rows.Select(r => r.Label).ToList();
        var host = labels.IndexOf("vmlinux001");
        var member = labels.IndexOf("gg-pool-ui-1");

        await Assert.That(member).IsEqualTo(host + 1)
            .Because("a member reads as a thing belonging to the machine above it, and a row "
                   + "between them breaks that. Order was: " + string.Join(", ", labels));
    }

    [Test]
    public async Task A_member_is_marked_so_the_pane_can_indent_it()
    {
        var rows = Rows.Runners(With(
            Runner(HostId, "vmlinux001"),
            Runner("m1", "gg-pool-ui-1", host: HostId)));

        var member = rows.Single(r => r.Label == "gg-pool-ui-1");
        var host = rows.Single(r => r.Label == "vmlinux001");

        await Assert.That(member.HostRunnerId).IsEqualTo(HostId)
            .Because("the row carries it so the render can indent without re-deriving which "
                   + "rows are members, which is the split every other column here keeps.");
        await Assert.That(host.HostRunnerId).IsEqualTo("")
            .Because("a machine is under nothing, and empty rather than null is what every "
                   + "other absent string on this record is.");
    }

    [Test]
    public async Task A_member_whose_host_is_not_listed_is_still_a_row()
    {
        // THE CASE SOMEBODY IS MOST LIKELY TO BE IN. A host that has been
        // revoked, or a listing read mid-change, must not silently swallow the
        // members that named it - losing the row would hide the machine that
        // is actually asking for help.
        var rows = Rows.Runners(With(
            Runner(OtherId, "Kevins-MBP"),
            Runner("m1", "gg-pool-ui-1", host: "a-host-that-is-not-here")));

        await Assert.That(rows.Select(r => r.Label)).Contains("gg-pool-ui-1")
            .Because("an orphan is still a machine somebody may have to act on.");
    }
}
