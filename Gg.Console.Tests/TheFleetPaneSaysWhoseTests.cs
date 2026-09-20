using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The fleet a person reads says whose each machine is (slice forty-six, step 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three gave a machine an owner and the console did not draw
/// it.</b> `gg runners` has said "the tenant's", "open" and "somebody's,
/// reserved" since that slice's step 2; the pane drew the mark, the name, the
/// state and what it was working on, and `RunnerRow` had no ownership member at
/// all. A person at the console could not see what a person at a terminal could.
/// </para>
/// <para>
/// <b>The pane learns it before any key acts on it</b>, which is the order
/// <c>VerbParityTests</c> wrote down when it called claiming a gap: a key that
/// changes a fact the pane cannot draw acts blind.
/// </para>
/// </remarks>
public class TheFleetPaneSaysWhoseTests
{
    private const string Id = "01a06385-322f-7371-93a2-ce35db5c4fbe";

    private static AppState Fleet(params RunnerSummary[] runners) => new()
    {
        RunnerSelected = 0,
        Runners = new RunnerList { Runners = runners },
    };

    private static RunnerSummary A(
        string label,
        string ownership = "",
        string owner = "",
        bool reserved = false,
        bool resident = false,
        string? profile = null) => new()
    {
        RunnerId = Id,
        Label = label,
        State = RunnerStates.Idle,
        Ownership = ownership,
        Owner = owner,
        Reserved = reserved,
        Resident = resident,
        Profile = profile,
    };

    [Test]
    public async Task A_row_says_the_tenants_open_or_a_persons_with_the_reservation()
    {
        var tenant = Rows.Runners(Fleet(A("vmlinux002", RunnerOwnerships.Tenant)))[0];
        var open = Rows.Runners(Fleet(A("vmlinux002", RunnerOwnerships.Open)))[0];
        var claimed = Rows.Runners(Fleet(A("vmlinux002", RunnerOwnerships.Claimed, "Kevin")))[0];
        var reserved = Rows.Runners(
            Fleet(A("vmlinux002", RunnerOwnerships.Claimed, "Kevin", reserved: true)))[0];

        await Assert.That(Rows.Whose(tenant)).IsEqualTo("the tenant's");
        await Assert.That(Rows.Whose(open)).IsEqualTo("open");
        await Assert.That(Rows.Whose(claimed)).IsEqualTo("Kevin's");
        await Assert.That(Rows.Whose(reserved)).IsEqualTo("Kevin's, reserved")
            .Because("reservation rides the owner: nothing is reserved that is not claimed, so "
                   + "a cell saying reserved beside nobody would describe a state the control "
                   + "plane will not produce.");
    }

    [Test]
    public async Task A_control_plane_that_does_not_say_gets_no_column_rather_than_open()
    {
        var row = Rows.Runners(Fleet(A("vmlinux001")))[0];

        await Assert.That(Rows.Whose(row)).IsEqualTo("")
            .Because("a row reading 'open' from a control plane with no claim door invites a "
                   + "key that cannot succeed - the command line's rule, and the same one here.");
        await Assert.That(PaneText.ForTab(Fleet(A("vmlinux001")), TabId.Runners))
            .DoesNotContain("open", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_pane_draws_whose_it_is_its_profile_and_that_it_is_a_resident()
    {
        var state = Fleet(A(
            "vmlinux002", RunnerOwnerships.Claimed, "Kevin", reserved: true,
            resident: true, profile: "dev-worker"));

        var pane = PaneText.ForTab(state, TabId.Runners);

        await Assert.That(pane).Contains("Kevin's, reserved", StringComparison.Ordinal);
        await Assert.That(pane).Contains("profile dev-worker", StringComparison.Ordinal);
        await Assert.That(pane).Contains("resident", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_table_draws_the_same_row_through_the_same_projection()
    {
        // ONE PROJECTION, TWO READERS. The cells lived inside a Terminal.Gui
        // callback, where no test could reach them, and the pane built its own
        // line - so the two could say different things about one machine and
        // nothing would have failed.
        var row = Rows.Runners(Fleet(A(
            "vmlinux002", RunnerOwnerships.Tenant, resident: true, profile: "dev-worker")))[0];

        var cells = Rows.RunnerCells(row);

        await Assert.That(cells.Length).IsEqualTo(Rows.RunnerColumns.Count)
            .Because("a cell per column, or the table draws one machine's fact under another's "
                   + "heading.");

        var whose = cells[Rows.RunnerColumns.ToList().IndexOf("whose")];
        await Assert.That(whose).IsEqualTo("the tenant's · resident");
        await Assert.That(cells[Rows.RunnerColumns.ToList().IndexOf("profile")])
            .IsEqualTo("dev-worker");
    }

    [Test]
    public async Task What_a_row_carries_is_the_word_rather_than_the_sentence()
    {
        // The record is written to disk under GG_STATE_DUMP and read back by
        // things that are not a renderer, and step 2's keys have to ask whether
        // they apply - which is a comparison against a word, not English.
        var row = Rows.Runners(Fleet(A("vmlinux002", RunnerOwnerships.Claimed, "Kevin")))[0];

        await Assert.That(row.Ownership).IsEqualTo(RunnerOwnerships.Claimed);
        await Assert.That(row.Owner).IsEqualTo("Kevin");
        await Assert.That(row.Mine).IsFalse()
            .Because("Mine has meant 'registered on this machine' since before ownership "
                   + "existed, and this slice does not quietly move it to mean 'claimed by me'.");
    }
}
