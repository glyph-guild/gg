using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Enter on a runner opens that runner, not whichever one is on this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>The modal ignored the cursor.</b> It read
/// <c>Rows.Runners(state).FirstOrDefault(r => r.Mine)</c>, so pressing enter on
/// the thirteenth row of a fifteen-row fleet drew the first row's runner —
/// silently, with the right shape and the wrong subject. Nothing in the text
/// named which runner it was about, so the answer looked correct: a person
/// reading "idle on this machine, waiting for work" had no way to tell it was
/// not the row they had just moved to.
/// </para>
/// <para>
/// <b>It was written when there was one row worth opening.</b> The pane began
/// as "the runner on this machine" with a table added under it, and the table
/// grew arrow keys and a cursor without the modal being told. The fix is that
/// the modal takes its subject from the same place the cursor lives.
/// </para>
/// <para>
/// <b>Two things a runner row carries, and only one of them is the fleet's.</b>
/// Pid, exit code and log belong to a child THIS console started; state, labels
/// and last-heard come from the control plane about anybody's. So a selected
/// runner that is not ours has a status and no log, and the keys that act on a
/// pidfile must not be offered for it — a `shut it down` that silently killed
/// the local runner instead would be the same defect with consequences.
/// </para>
/// </remarks>
public class TheRunnerModalShowsTheSelectedOneTests
{
    private const string Me = "01a062f3-42a5-73a4-8c01-ec248bfe5237";

    private static RunnerSummary Runner(string id, string label, string state, string by = Me) =>
        new()
        {
            RunnerId = id,
            Label = label,
            State = state,
            RegisteredByPrincipalId = by,
            LastHeartbeatAt = DateTimeOffset.UnixEpoch,
        };

    /// <summary>This machine's runner first, then two of somebody's elsewhere.</summary>
    private static AppState Fleet(int selected) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        Machine = "Kevins-MBP",
        PrincipalId = Me,
        LocalRunnerId = "01a078bb",
        RunnerSelected = selected,
        Runners = new RunnerList
        {
            Runners =
            [
                Runner("01a078bb", "Kevins-MBP", RunnerStates.Offline),
                Runner("01a06572", "vmlinux001", RunnerStates.Idle),
                Runner("01a06943", "gg-pool-dev-2", RunnerStates.Busy),
            ],
        },
    };

    [Test]
    public async Task The_modal_is_about_the_row_the_cursor_is_on()
    {
        var text = PaneText.Modal(Fleet(selected: 1));

        await Assert.That(text).Contains("01a06572")
            .Because("that is the row somebody pressed enter on, and a modal that names "
                   + "another runner is wrong in a way its own text cannot reveal.");

        await Assert.That(text).DoesNotContain("01a078bb")
            .Because("the runner on this machine is not the subject just because it is the "
                   + "one this console knows most about.");
    }

    [Test]
    public async Task The_first_row_still_reads_as_this_machines_own()
    {
        // The anchor: the case that worked is the case the cursor starts on, so
        // a fix that keyed on the wrong thing entirely would still pass the test
        // above and fail this one.
        var text = PaneText.Modal(Fleet(selected: 0));

        await Assert.That(text).Contains("01a078bb");
        await Assert.That(text).Contains("this machine");
    }

    [Test]
    public async Task A_runner_this_console_did_not_start_offers_no_log()
    {
        var text = PaneText.Modal(Fleet(selected: 2));

        await Assert.That(text).Contains("01a06943");
        await Assert.That(text).DoesNotContain("It has said nothing yet.")
            .Because("that sentence is about a child this console is holding. For a runner "
                   + "on another host there is no log to have said anything, and promising "
                   + "one that can never arrive reads as a runner that has gone quiet.");
    }

    [Test]
    public async Task The_keys_that_need_a_pidfile_are_not_offered_for_somebody_elses_runner()
    {
        // THE ONE WITH CONSEQUENCES. Stop and restart act through a pidfile this
        // machine wrote. Offered on a selected runner they cannot reach, the
        // best case is a key that does nothing and the worst is one that shuts
        // down the local runner while a person is looking at another row.
        var elsewhere = Keymap.Bindings(KeymapContext.For(Fleet(selected: 1)));

        await Assert.That(elsewhere.Any(b => b.Command == Command.StopRunner)).IsFalse();
        await Assert.That(elsewhere.Any(b => b.Command == Command.RestartRunner)).IsFalse();
        await Assert.That(elsewhere.Any(b => b.Command == Command.CloseModal)).IsTrue()
            .Because("a modal with no way out is worse than one with nothing to do.");

        var ours = Keymap.Bindings(KeymapContext.For(Fleet(selected: 0)));

        await Assert.That(ours.Any(b => b.Command == Command.StopRunner)).IsTrue();
        await Assert.That(ours.Any(b => b.Command == Command.RestartRunner)).IsTrue();
    }

    [Test]
    public async Task A_cursor_past_the_end_names_nothing_rather_than_falling_back()
    {
        // The fleet shrinks under a refresh - a revoked runner leaves - and the
        // cursor is an index. Falling back to the local runner here is exactly
        // the defect this file is about, one race later.
        var text = PaneText.Modal(Fleet(selected: 9));

        await Assert.That(text).DoesNotContain("01a078bb");
    }
}
