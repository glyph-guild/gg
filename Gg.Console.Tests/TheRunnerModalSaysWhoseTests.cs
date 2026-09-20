using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal says whose a machine is, before any key changes it
/// (slice forty-six, step 1).
/// </summary>
/// <remarks>
/// <b>This is where step 2's keys will act</b>, and the order is the point: a
/// person about to claim, reserve or release a machine is looking at this
/// modal, and until it said who owns the machine the key would have acted on a
/// fact the person could not see.
/// </remarks>
public class TheRunnerModalSaysWhoseTests
{
    private static AppState Fleet(RunnerSummary runner) => new()
    {
        RunnerSelected = 0,
        Runners = new RunnerList { Runners = [runner] },
    };

    private static RunnerSummary A(
        string ownership = "",
        string owner = "",
        bool reserved = false,
        bool resident = false,
        string? profile = null) => new()
    {
        RunnerId = "01a0bca3-b788-72e5-b40a-be3811653226",
        Label = "vmlinux002",
        State = RunnerStates.Idle,
        Ownership = ownership,
        Owner = owner,
        Reserved = reserved,
        Resident = resident,
        Profile = profile,
    };

    private static string ValueOf(AppState state, string label) =>
        RunnerDetails.Fields(state).FirstOrDefault(f => f.Label == label)?.Value ?? "";

    [Test]
    public async Task The_modal_says_whose_it_is_what_it_enrolled_as_and_whether_it_is_a_resident()
    {
        var state = Fleet(A(
            RunnerOwnerships.Claimed, "Kevin", reserved: true, resident: true,
            profile: "dev-worker"));

        await Assert.That(ValueOf(state, "whose")).IsEqualTo("Kevin's, reserved");
        await Assert.That(ValueOf(state, "profile")).IsEqualTo("dev-worker");
        await Assert.That(ValueOf(state, "resident")).IsNotEmpty();
    }

    [Test]
    public async Task A_machine_nobody_has_said_anything_about_gets_no_fields_about_it()
    {
        var state = Fleet(A());

        await Assert.That(ValueOf(state, "whose")).IsEqualTo("")
            .Because("empty is not open: a field reading open, from a control plane with no "
                   + "claim door, invites a key that cannot succeed.");
        await Assert.That(ValueOf(state, "profile")).IsEqualTo("")
            .Because("a machine brought up by hand enrolled under nothing, and a blank field "
                   + "would read as a profile named nothing.");
        await Assert.That(ValueOf(state, "resident")).IsEqualTo("");
    }

    [Test]
    public async Task Whose_is_said_before_what_it_is_doing()
    {
        // The order a person reads a machine in: which one, whose, then what it
        // is up to. It matters here more than on the pane, because this is the
        // surface the keys act from.
        var state = Fleet(A(RunnerOwnerships.Open) with { CurrentFlightNumber = "GG-208" });

        var labels = RunnerDetails.Fields(state).Select(f => f.Label).ToList();

        await Assert.That(labels.IndexOf("whose")).IsGreaterThan(-1);
        await Assert.That(labels.IndexOf("whose")).IsLessThan(labels.IndexOf("working on"));
    }

    [Test]
    public async Task The_row_carries_ownership_as_data_and_Mine_keeps_its_meaning()
    {
        // A row carrying "the tenant's" could not be asked whether a key
        // applies without matching English - and this record is written to disk
        // under GG_STATE_DUMP and read back by things that are not a renderer.
        var row = Rows.Runners(Fleet(A(RunnerOwnerships.Claimed, "Kevin", reserved: true)))[0];

        await Assert.That(row.Ownership).IsEqualTo(RunnerOwnerships.Claimed);
        await Assert.That(row.Owner).IsEqualTo("Kevin");
        await Assert.That(row.Reserved).IsTrue();
        await Assert.That(row.Mine).IsFalse()
            .Because("Mine means registered on THIS machine, and has since before ownership "
                   + "existed. A slice that quietly moved it to mean 'claimed by me' would "
                   + "change which row the modal opens on.");
    }
}
