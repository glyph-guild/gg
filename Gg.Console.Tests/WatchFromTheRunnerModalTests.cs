using Gg.Contracts;
using Gg.Console;

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

        // WHERE THE FLIGHT ID COMES FROM. The row shows a NUMBER because that
        // is what a person types; the pane needs an id, and the fleet answer is
        // the only place this console has both.
        CurrentFlightId = flying is null ? null : TheFlight,
        LastHeartbeatAt = Beat,
        RegisteredByPrincipalId = Me,
        RegisteredBy = "Kevin Deenanauth",
        Labels = [],
    };

    private const string TheFlight = "01a08388-4474-7733-b566-d5c2bf369645";

    private static AppState State(string? flying) => new()
    {
        Mode = UiMode.Runner,
        ActiveTab = TabId.Runners,
        Machine = "Kevins-MBP",
        PrincipalId = Me,
        RunnerSelected = 0,
        Runners = new RunnerList { Runners = [Runner(flying)] },

        // NO QUEUE, WHICH IS THE ORDINARY CASE AND THE ONE THAT WAS BROKEN.
        // The queue is a queue of PROBLEMS - awaiting a decision, a lease
        // expired twice, a runner gone - so a flight that is simply flying is
        // never in it. Binding the pane to the queue cursor meant watching
        // worked for exactly the flights nobody wants to watch.
    };

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

    /// <summary>A connect that works, and reports which runner it was asked about.</summary>
    private static ConsoleWatchRunner.Connect Reaching(List<string> asked) =>
        (runnerId, flightId, _) =>
        {
            asked.Add($"{runnerId}/{flightId}");
            return true;
        };

    [Test]
    public async Task Watching_connects_and_leaves_the_pane_on_that_flight()
    {
        // THE WHOLE POINT OF THE CHANGE. Handing the terminal over for the
        // duration was a watch a person had to leave the console to have; this
        // connects with the terminal free and comes back with the pane live.
        var asked = new List<string>();

        var after = ConsoleWatchRunner.Watch(State("GG-71"), Reaching(asked), say: _ => { });

        await Assert.That(asked).IsEquivalentTo(new[] { $"{Vmlinux}/{TheFlight}" })
            .Because("the connect is asked for by runner and the pane is keyed by flight, so "
                   + "both have to cross.");

        await Assert.That(after.LiveVisible).IsTrue()
            .Because("a watch that connected and showed nothing is a watch a person cannot "
                   + "tell from one that failed.");

        await Assert.That(after.WatchedFlightId).IsEqualTo(TheFlight)
            .Because("the pane needs to be told WHICH flight, and the queue cursor cannot "
                   + "say: the queue holds flights that need somebody, and one being watched "
                   + "is usually just flying.");
    }

    [Test]
    public async Task The_steps_of_the_connect_reach_the_freed_terminal()
    {
        // WHERE THEY BELONG. The connect happens between sessions, so the
        // terminal is this console's to write on - and the wait it narrates is
        // up to a full heartbeat interval, which is the difference between a
        // person waiting and a person deciding it has hung.
        var said = new List<string>();

        _ = ConsoleWatchRunner.Watch(
            State("GG-71"),
            (_, _, say) => { say("asking the control plane"); say("connected"); return true; },
            say: said.Add);

        await Assert.That(said).IsNotEmpty()
            .Because("a connect that says nothing is one a person cannot tell from a hang.");
    }

    [Test]
    public async Task A_connect_that_fails_says_so_and_shows_no_pane()
    {
        // AND DOES NOT LEAVE THE PANE OPEN OVER NOTHING, which would be the
        // silence this whole path keeps producing: a box that means "the agent
        // is working" over a conversation that never happened.
        var after = ConsoleWatchRunner.Watch(
            State("GG-71"), (_, _, _) => false, say: _ => { });

        await Assert.That(after.LiveVisible).IsFalse();
        await Assert.That(after.LastRunner).IsNotNull();
    }

    [Test]
    public async Task A_runner_flying_nothing_is_not_watched_even_if_asked()
    {
        // THE KEY AND THE ACT AGREE. The hint line is derived from one place and
        // dispatch from another; a command that arrived anyway - a rebind, a
        // stale modal - must not connect to something that cannot answer.
        var reached = false;

        var after = ConsoleWatchRunner.Watch(
            State(null), (_, _, _) => { reached = true; return true; }, say: _ => { });

        await Assert.That(reached).IsFalse();
        await Assert.That(after.LastRunner).Contains("nothing")
            .Because("it has to say why rather than fail silently. Said: " + after.LastRunner);
    }

    [Test]
    public async Task A_fleet_that_does_not_say_which_flight_is_not_watched()
    {
        // THE ROW SHOWS A NUMBER AND THE PANE NEEDS AN ID. The fleet answer
        // carries both; a row without the id is one this console cannot key a
        // buffer under, which is a refusal rather than a blank box.
        var reached = false;

        var state = State("GG-71");
        var blind = state with
        {
            Runners = new RunnerList
            {
                Runners = [state.Runners!.Runners[0] with { CurrentFlightId = null }],
            },
        };

        var after = ConsoleWatchRunner.Watch(
            blind, (_, _, _) => { reached = true; return true; }, say: _ => { });

        await Assert.That(reached).IsFalse();
        await Assert.That(after.LastRunner).Contains("GG-71")
            .Because("it has to name the flight it could not place. Said: " + after.LastRunner);
    }

    [Test]
    public async Task A_flight_that_is_only_flying_is_watchable()
    {
        // THE WHOLE DEFECT, AS A ROW. GG-77 was started, watched, and nothing
        // appeared: the pane was bound to the queue cursor and the queue is a
        // queue of PROBLEMS - awaiting a decision, a lease expired twice, a
        // runner gone. A flight that is simply flying is in none of those, so
        // watching worked for exactly the flights nobody wants to watch.
        var after = ConsoleWatchRunner.Watch(
            State("GG-77"), (_, _, _) => true, say: _ => { });

        await Assert.That(after.Queue).IsEmpty()
            .Because("this is the case that was broken and it has to stay the case.");
        await Assert.That(after.WatchedFlightId).IsEqualTo(TheFlight);
        await Assert.That(after.LiveVisible).IsTrue();
    }
}
