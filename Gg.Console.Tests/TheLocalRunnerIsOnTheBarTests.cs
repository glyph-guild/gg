using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A Runners tab, and the first row is this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fleet was already in the model and had nowhere to be read.</b> The
/// boot fetches the runner list - the queue uses it to say a flight is stranded
/// on an offline runner - and nothing rendered it. "Is my runner up, and is it
/// doing anything" is the question a person asks before they wonder why their
/// flight has not moved, and the console could not answer it.
/// </para>
/// <para>
/// <b>This machine's row is first, and it is the only row here that is about
/// a decision somebody can make.</b> Another tenant's runner being busy is
/// information; this one being absent means <c>gg runner up</c> was never run
/// or has died, which is a thing to go and do.
/// </para>
/// <para>
/// <b>The id crosses and the token does not.</b> <c>StoredRunner</c> holds a
/// runner token, <c>AppState</c> is serialized to disk under
/// <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle, and a secret in a
/// document is a secret in a bug report. Only the id is needed to say which row
/// is this machine's.
/// </para>
/// </remarks>
public class TheLocalRunnerIsOnTheBarTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Mine = "01a06572-a784-72ae-b951-f147553cd48e";

    private static RunnerSummary ARunner(
        string id, string state, string? flight = null, string label = "somebody's laptop") => new()
    {
        RunnerId = id,
        Label = label,
        State = state,
        CurrentFlightNumber = flight,
        CurrentFlightId = flight is null ? null : "f-" + flight,
        LastHeartbeatAt = T0,
    };

    private static AppState Fleet(string? local, params RunnerSummary[] runners) => new()
    {
        ActiveTab = TabId.Runners,
        LocalRunnerId = local,
        Runners = new RunnerList { Runners = runners },
    };

    [Test]
    public async Task Runners_is_a_tab_with_a_key_of_its_own()
    {
        await Assert.That(Tabs.All).Contains(TabId.Runners);

        var key = Tabs.KeyFor(TabId.Runners);

        await Assert.That(key).IsNotNull()
            .Because("a view you have to learn to click is one somebody does not reach.");
        await Assert.That(Keymap.Resolve(key!.Value, new KeymapContext(UiMode.Normal)))
            .IsEqualTo(Tabs.CommandFor(TabId.Runners))
            .Because("the bar and the keymap say the same thing about the same key, which is "
                   + "what Tabs.KeyFor exists for.");
    }

    [Test]
    public async Task This_machine_is_the_first_row()
    {
        var rows = Rows.Runners(Fleet(
            Mine,
            ARunner("other-1", RunnerStates.Idle),
            ARunner(Mine, RunnerStates.Idle),
            ARunner("other-2", RunnerStates.Busy, "GG-40")));

        await Assert.That(rows).Count().IsEqualTo(3);
        await Assert.That(rows[0].Mine).IsTrue()
            .Because("this row is the only one on the tab that is about something a person "
                   + "sitting here can do.");
        await Assert.That(rows[0].Runner).Contains(Mine[..8], StringComparison.Ordinal);
        await Assert.That(rows.Skip(1).Any(r => r.Mine)).IsFalse();
    }

    [Test]
    public async Task It_says_whether_it_is_running_and_what_it_is_working_on()
    {
        var working = Rows.Runners(Fleet(Mine, ARunner(Mine, RunnerStates.Busy, "GG-42")))[0];

        await Assert.That(working.State).IsEqualTo(RunnerStates.Busy);
        await Assert.That(working.Work).IsEqualTo("GG-42")
            .Because("what it is working on is the second half of the question.");

        var waiting = Rows.Runners(Fleet(Mine, ARunner(Mine, RunnerStates.Idle)))[0];

        await Assert.That(waiting.State).IsEqualTo(RunnerStates.Idle);
        await Assert.That(waiting.Work).IsEmpty()
            .Because("idle and holding a flight are different, and a dash for both would say "
                   + "neither.");
    }

    [Test]
    public async Task A_runner_registered_here_that_the_fleet_has_never_seen_is_still_a_row()
    {
        // THE CASE THE FLEET'S LIST CANNOT SHOW, and it is the one a person is
        // most likely to be in: `gg runner up` registered this machine and the
        // process is not running, so it has never heartbeated and the control
        // plane has nothing to list.
        var rows = Rows.Runners(Fleet(Mine, ARunner("other-1", RunnerStates.Idle)));

        await Assert.That(rows[0].Mine).IsTrue();
        await Assert.That(rows[0].State).IsEqualTo(RunnerStates.Offline)
            .Because("registered and never heard from is what offline means, and inventing a "
                   + "fourth word for it would be a second vocabulary.");
    }

    [Test]
    public async Task A_machine_with_no_runner_registered_says_what_to_do()
    {
        var pane = PaneText.ForTab(
            Fleet(null, ARunner("other-1", RunnerStates.Idle)), TabId.Runners);

        await Assert.That(pane).Contains("gg runner up")
            .Because("nothing registered here is not an error, it is a thing to go and do - "
                   + "and a pane that only said `no local runner' would leave a person "
                   + "guessing which command makes one.");
    }

    [Test]
    public async Task An_empty_fleet_reads_as_empty_rather_than_as_unread()
    {
        var pane = PaneText.ForTab(
            new AppState { ActiveTab = TabId.Runners }, TabId.Runners);

        await Assert.That(pane).IsNotEmpty()
            .Because("the boot fetches the runner list, so this tab is never waiting on a "
                   + "read - it either has runners or genuinely has none.");
    }
}
