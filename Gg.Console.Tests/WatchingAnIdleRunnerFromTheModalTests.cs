using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The key is offered over a runner that is beating, flying or not.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was withheld unless the runner was flying, and the reason was true
/// when it was written.</b> A channel existed only while a flight did, so on an
/// idle machine the key would have been one that always fails - and a key that
/// cannot work is worse than a sentence explaining when it would.
/// </para>
/// <para>
/// <b>The runner answers while it is beating now, so the key follows.</b> What
/// it cannot do is reach a machine that is not beating at all: an introduction
/// is picked up on a heartbeat, so an offline runner never sees one and the
/// console would wait out the introduction's whole life to learn nothing. That
/// is the same rule, asked of the thing that is actually required.
/// </para>
/// <para>
/// <b>And the watch is the MACHINE's rather than one flight's.</b> A person
/// attaching to a runner that is waiting cannot name a flight - there is not one
/// yet, which is the point - so what the pane is told is the runner, and what it
/// draws is whatever that runner is saying now.
/// </para>
/// </remarks>
public class WatchingAnIdleRunnerFromTheModalTests
{
    private const string Me = "01a062f3-42a5-73a4-8c01-ec248bfe5237";
    private const string Vmlinux = "01a06572-a784-72ae-b951-f147553cd48e";

    private static readonly DateTimeOffset Beat =
        new(2026, 9, 9, 0, 38, 16, TimeSpan.Zero);

    private static AppState Over(string state, string? work = null) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        PrincipalId = Me,
        RunnerSelected = 0,
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = Vmlinux,
                    Label = "vmlinux001",
                    State = state,
                    CurrentFlightNumber = work,

                    // DELIBERATELY ABSENT EVEN WHEN IT IS FLYING. The watch is
                    // the machine's now, so the one thing the old path refused
                    // over is a thing nothing asks for.
                    CurrentFlightId = null,
                    LastHeartbeatAt = state == RunnerStates.Offline ? null : Beat,
                    RegisteredByPrincipalId = Me,
                    RegisteredBy = "Kevin Deenanauth",
                    Labels = [],
                },
            ],
        },
    };

    [Test]
    public async Task An_idle_runner_that_is_beating_is_offered_the_key()
    {
        var bindings = Keymap.Bindings(KeymapContext.For(Over(RunnerStates.Idle)));

        await Assert.That(bindings.Any(b => b.Command == Command.WatchRunner)).IsTrue()
            .Because("waiting for work is the state a person most wants to attach in, and "
                   + "it was the one state the key was withheld in.");
    }

    [Test]
    public async Task An_offline_one_is_not()
    {
        var bindings = Keymap.Bindings(KeymapContext.For(Over(RunnerStates.Offline)));

        await Assert.That(bindings.Any(b => b.Command == Command.WatchRunner)).IsFalse()
            .Because("an introduction is picked up on a heartbeat, so a machine that is not "
                   + "beating never sees one - and the console would sit out its whole life "
                   + "to learn nothing.");
    }

    [Test]
    public async Task Watching_names_the_runner_rather_than_a_flight()
    {
        var asked = new List<string>();

        var after = ConsoleWatchRunner.Watch(
            Over(RunnerStates.Idle),
            runnerId => { asked.Add(runnerId); return true; });

        await Assert.That(asked).IsEquivalentTo(new List<string> { Vmlinux });

        await Assert.That(after.WatchedRunnerId).IsEqualTo(Vmlinux)
            .Because("a person attaching to a machine that is waiting cannot name a flight - "
                   + "there is not one yet, which is the point.");

        await Assert.That(after.LiveVisible).IsTrue();
        await Assert.That(after.ActiveTab).IsEqualTo(TabId.Live);
        await Assert.That(after.Mode).IsEqualTo(UiMode.Normal)
            .Because("the modal closes, because what it was asked from is now happening "
                   + "behind it in another tab.");
    }

    [Test]
    public async Task A_runner_that_is_flying_is_watched_the_same_way()
    {
        // ONE PATH, NOT TWO. The old one needed the fleet to say which flight
        // and refused when it did not; there is nothing left for it to refuse
        // over, because the runner answers about whatever it is on.
        var after = ConsoleWatchRunner.Watch(
            Over(RunnerStates.Busy, "GG-84"),
            _ => true);

        await Assert.That(after.WatchedRunnerId).IsEqualTo(Vmlinux);
        await Assert.That(after.LastRunner).Contains("vmlinux001");
    }

    [Test]
    public async Task An_offline_one_is_refused_even_if_asked()
    {
        // THE SAME RULE THE KEY OBEYS, checked again here rather than trusted.
        // The hint line is derived in one place and dispatch happens in another.
        var after = ConsoleWatchRunner.Watch(
            Over(RunnerStates.Offline),
            _ => throw new InvalidOperationException("nothing should be started"));

        await Assert.That(after.LastRunner).Contains("beating")
            .Because("the sentence has to say what is missing, or a person presses the key "
                   + "again and waits out another introduction.");
    }
}
