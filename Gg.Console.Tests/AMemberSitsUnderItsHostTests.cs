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

    [Test]
    public async Task The_runners_tab_indents_a_member_too()
    {
        // REPORTED FROM THE RUNNING CONSOLE. The tab is a TABLE - it draws
        // `RunnerRow.Runner` as a cell and never passes through PaneText - so
        // indenting the pane left the surface most people actually look at
        // flat. Ordering alone put the members after their host and said
        // nothing about them belonging to it.
        var rows = Rows.Runners(With(
            Runner(HostId, "vmlinux001"),
            Runner("m1", "gg-pool-ui-1", host: HostId),
            Runner(OtherId, "Kevins-MBP")));

        var member = rows.Single(r => r.Label == "gg-pool-ui-1");
        var host = rows.Single(r => r.Label == "vmlinux001");

        // COMPOSED, NOT STORED. The row keeps what is true - which host warmed
        // this one - and `Rows.Nested` is where the nesting is drawn from it.
        // Both surfaces call it, so neither invents its own amount, and the
        // data a person dumps with GG_STATE_DUMP has no spaces in it.
        await Assert.That(Rows.Nested(member).StartsWith("  ", StringComparison.Ordinal)).IsTrue()
            .Because("the table's cell projection and the pane both draw this, so every "
                   + "surface nests. Drawn: [" + Rows.Nested(member) + "]");
        await Assert.That(Rows.Nested(host).StartsWith(" ", StringComparison.Ordinal)).IsFalse()
            .Because("a machine is flush in the same column, or the indent says nothing.");

        await Assert.That(member.Runner.StartsWith(" ", StringComparison.Ordinal)).IsFalse()
            .Because("THE ROW ITSELF CARRIES NO PRESENTATION. It is written to disk under "
                   + "GG_STATE_DUMP and read back by things that are not a renderer, and a "
                   + "tree fed this cell would indent it twice - which a spike proved by "
                   + "having to strip it again.");
    }

    [Test]
    public async Task The_pane_and_the_tab_indent_by_the_same_amount()
    {
        // ONE DECISION, TWO SURFACES. The first fix put the indent in the pane
        // and the tab kept its own idea; this is what stops them drifting
        // apart again.
        var rows = Rows.Runners(With(
            Runner(HostId, "vmlinux001"),
            Runner("m1", "gg-pool-ui-1", host: HostId)));
        var drawn = PaneText.Runners(With(
            Runner(HostId, "vmlinux001"),
            Runner("m1", "gg-pool-ui-1", host: HostId)));

        static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

        var paneMember = drawn.Split('\n')
            .Single(l => l.Contains("gg-pool-ui-1", StringComparison.Ordinal));
        var paneHost = drawn.Split('\n')
            .Single(l => l.Contains("vmlinux001", StringComparison.Ordinal));

        var cellMember = Rows.Nested(rows.Single(r => r.Label == "gg-pool-ui-1"));
        var cellHost = Rows.Nested(rows.Single(r => r.Label == "vmlinux001"));

        // BY HOW MUCH, not from which column. The pane opens every line with a
        // one-character marker the table puts in a column of its own, so the
        // two can never start at the same place - what has to match is the
        // step from a machine to the member under it.
        await Assert.That(Indent(paneMember) - Indent(paneHost))
            .IsEqualTo(Indent(cellMember) - Indent(cellHost))
            .Because("two surfaces that nest by different amounts read as two different "
                   + "shapes for one fleet.");
    }

    [Test]
    public async Task The_pane_indents_a_member_and_leaves_a_machine_flush()
    {
        // WHAT A PERSON ACTUALLY SEES. The order alone does not say a member
        // belongs to the row above it - two rows in a row is just two rows -
        // so the shape has to be drawn.
        var drawn = PaneText.Runners(With(
            Runner(HostId, "vmlinux001"),
            Runner("m1", "gg-pool-ui-1", host: HostId),
            Runner(OtherId, "Kevins-MBP")));

        var lines = drawn.Split('\n');
        var member = lines.Single(l => l.Contains("gg-pool-ui-1", StringComparison.Ordinal));
        var host = lines.Single(l => l.Contains("vmlinux001", StringComparison.Ordinal));
        var laptop = lines.Single(l => l.Contains("Kevins-MBP", StringComparison.Ordinal));

        // RELATIVE, because every row opens with a one-character marker column
        // and an unowned machine's marker is already a space. What matters is
        // that a member sits further in than the machines, not what column any
        // of them starts at.
        static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

        await Assert.That(Indent(member)).IsGreaterThan(Indent(host))
            .Because("indented, so the fleet reads as machines with their members under them. "
                   + $"Drawn host [{host}] and member [{member}]");
        await Assert.That(Indent(laptop)).IsEqualTo(Indent(host))
            .Because("a laptop hosts nothing and is hosted by nothing, so it stands level "
                   + "with the other machines - if everything were indented the indent would "
                   + "say nothing.");
    }
}
