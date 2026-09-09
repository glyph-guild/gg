using System.Diagnostics;
using Gg.Contracts;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal can go and watch, rather than only naming the command.
/// </summary>
/// <remarks>
/// <para>
/// <b>The modal has named <c>gg runner watch &lt;id&gt;</c> since slice
/// thirty-four and offered no way to run it.</b> ADR-0013 Decision 2 is
/// reference-and-fetch and that is right about the ssh lines - they are about
/// somebody else's machine and gg is guessing - but `watch` is gg's own verb
/// against gg's own runner, and a person reading a command they then have to
/// leave the console to type is a console that knows the answer and will not
/// say it.
/// </para>
/// <para>
/// <b>BETWEEN SESSIONS, WHICH IS WHY THIS NEEDS NO EXCEPTION.</b> Reaching a
/// runner is three calls to the control plane and a WebRTC socket, and a UI
/// session may make none of them — <c>LiveStreamingTests</c> holds that
/// structurally. So this takes the slot <c>$EDITOR</c> already takes: the
/// session ends, the terminal is free, a child owns it, and the model is the
/// only thing that crosses back. The alternative — a live pane inside the modal
/// — would put a socket in a session and has to argue for its own exception
/// rather than inherit this one.
/// </para>
/// <para>
/// <b>Offered only over a runner that is flying something</b>, because a
/// channel to a runner exists only while a flight does. That is the same rule
/// the suggestion text states, and the key now has to obey it rather than
/// describe it.
/// </para>
/// </remarks>
public class WatchFromTheRunnerModalTests
{
    private const string Me = "01a062f3-42a5-73a4-8c01-ec248bfe5237";
    private const string Vmlinux = "01a06572-a784-72ae-b951-f147553cd48e";

    private static readonly DateTimeOffset Beat =
        new(2026, 9, 9, 0, 38, 16, TimeSpan.Zero);

    private static RunnerSummary Runner(string? flying) => new()
    {
        RunnerId = Vmlinux,
        Label = "vmlinux001",
        State = flying is null ? RunnerStates.Idle : RunnerStates.Busy,
        CurrentFlightNumber = flying,
        LastHeartbeatAt = Beat,
        RegisteredByPrincipalId = Me,
        RegisteredBy = "Kevin Deenanauth",
        Labels = [],
    };

    private static AppState State(string? flying) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        Machine = "Kevins-MBP",
        PrincipalId = Me,
        RunnerSelected = 0,
        Runners = new RunnerList { Runners = [Runner(flying)] },
    };

    private static SelfInvocation TheBinary() =>
        new("/usr/local/bin/gg", ["runner", "tools"]);

    [Test]
    public async Task The_modal_offers_a_way_to_watch_a_runner_that_is_flying()
    {
        var keys = Keymap.Bindings(KeymapContext.For(State("GG-71")));

        await Assert.That(keys.Any(k => k.Command == Command.WatchRunner)).IsTrue()
            .Because("the modal has named the command since slice thirty-four and offered no "
                   + "way to run it, which is a console that knows the answer and will not "
                   + "say it. Offered: "
                   + string.Join(", ", keys.Select(k => k.Command.ToString())));
    }

    [Test]
    public async Task A_runner_flying_nothing_is_not_offered_it()
    {
        // THE SAME RULE THE SENTENCE STATES. A channel to a runner exists only
        // while a flight does, so a key here would be one that always fails -
        // and the modal's own text is where a person learns the capability
        // exists on an idle machine.
        var keys = Keymap.Bindings(KeymapContext.For(State(null)));

        await Assert.That(keys.Any(k => k.Command == Command.WatchRunner)).IsFalse()
            .Because("a key that cannot work is worse than a sentence that explains when it "
                   + "would. Offered: "
                   + string.Join(", ", keys.Select(k => k.Command.ToString())));
    }

    [Test]
    public async Task The_modal_names_the_key_rather_than_only_the_command()
    {
        // THE PARAGRAPH AND THE KEY ARE ONE BINDING RENDERED TWICE. This text
        // named a command the console could not run for a whole slice; now it
        // can, and a person reading it has to be told so. The letter is read
        // off the keymap, so a rebind moves both.
        var said = RunnerDetails.Suggestion(Rows.Selected(State("GG-71"))!);

        await Assert.That(said).Contains("`w`")
            .Because("a console that knows the answer and makes a person leave to use it is "
                   + "what this key was added to stop. Said: " + said);

        // AND NOT WHEN THERE IS NOTHING TO PRESS IT ON. The idle sentence still
        // names the capability - that is where somebody learns it exists - but
        // offering a key that is not bound right now is the thing the whole
        // method exists not to do.
        var idle = RunnerDetails.Suggestion(Rows.Selected(State(null))!);

        await Assert.That(idle).DoesNotContain("Press")
            .Because("Said: " + idle);
    }

    [Test]
    public async Task It_hands_the_terminal_to_this_gg_and_names_the_whole_runner_id()
    {
        // WHICH gg, and the whole id. A bare `gg` off PATH is whichever one is
        // installed, which on a developer's machine is routinely not the one
        // they are running - SelfInvocation's own argument. And the grid shows
        // eight characters while the verb takes all of it.
        var info = ConsoleWatchRunner.StartInfoFor(TheBinary(), Vmlinux);

        await Assert.That(info.FileName).IsEqualTo("/usr/local/bin/gg");
        await Assert.That(info.ArgumentList).IsEquivalentTo(
            new[] { "runner", "watch", Vmlinux });
        await Assert.That(info.UseShellExecute).IsFalse()
            .Because("the child owns this terminal; a shell between them owns it instead.");
        await Assert.That(info.RedirectStandardOutput).IsFalse()
            .Because("what the agent is saying has to reach the screen, not a pipe nobody "
                   + "reads.");
    }

    [Test]
    public async Task Watching_runs_the_child_and_says_what_became_of_it()
    {
        ProcessStartInfo? started = null;

        var after = ConsoleWatchRunner.Watch(
            State("GG-71"), TheBinary(), info => { started = info; return 0; });

        await Assert.That(started).IsNotNull();
        await Assert.That(started!.ArgumentList).Contains(Vmlinux);
        await Assert.That(after.LastRunner).Contains("GG-71")
            .Because("the model is the only thing that crosses back from a session that "
                   + "released the terminal, so the receipt has to name what was watched. "
                   + "Said: " + after.LastRunner);
    }

    [Test]
    public async Task A_runner_flying_nothing_is_not_watched_even_if_asked()
    {
        // THE KEY AND THE ACT AGREE. The hint line is derived from one place and
        // dispatch from another; a command that arrived anyway - a rebind, a
        // stale modal - must not start a child that cannot work.
        var ran = false;

        var after = ConsoleWatchRunner.Watch(
            State(null), TheBinary(), _ => { ran = true; return 0; });

        await Assert.That(ran).IsFalse();
        await Assert.That(after.LastRunner).Contains("nothing")
            .Because("it has to say why rather than fail silently. Said: " + after.LastRunner);
    }

    [Test]
    public async Task A_console_that_cannot_name_its_own_binary_says_so()
    {
        // SelfInvocation returns null when it cannot work out how to re-run
        // this process, and handing the flight to whichever gg is on PATH is
        // the failure ConsoleHandFlight already refuses for the same reason.
        var ran = false;

        var after = ConsoleWatchRunner.Watch(
            State("GG-71"), null, _ => { ran = true; return 0; });

        await Assert.That(ran).IsFalse();
        await Assert.That(after.LastRunner).Contains("re-run itself")
            .Because("Said: " + after.LastRunner);
    }
}
