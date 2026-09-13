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

    // "A RUNNER FLYING NOTHING IS NOT OFFERED IT" WAS HERE, and the rule it
    // stated is gone: a runner answers while it is BEATING now, so waiting for
    // work is exactly when the key is worth having. What replaced it -
    // including the offline case, which is the part that really cannot work -
    // is WatchingAnIdleRunnerFromTheModalTests.

    [Test]
    public async Task The_modal_names_the_key_rather_than_only_the_command()
    {
        // THE PARAGRAPH AND THE KEY ARE ONE BINDING RENDERED TWICE. This text
        // named a command the console could not run for a whole slice; now it
        // can, and a person reading it has to be told so. The letter is read
        // off the keymap, so a rebind moves both.
        var said = RunnerDetails.LogAbsence(State("GG-71"));

        await Assert.That(said).Contains("`w`")
            .Because("a console that knows the answer and makes a person leave to use it is "
                   + "what this key was added to stop. Said: " + said);

        // AND WHEN IT IS FLYING NOTHING, WHICH IS THE NEW PART. The pane used
        // to say "No log is available when idle." and stop, because the key was
        // not bound and offering one that is not live teaches somebody to
        // distrust the pane. It is bound now, and an idle machine is the one a
        // person most wants to attach to - so the sentence says so.
        var idle = RunnerDetails.LogAbsence(State(null));

        await Assert.That(idle).Contains("`w`")
            .Because("attaching before there is anything to watch is how somebody sees work "
                   + "arrive, and a pane that does not mention it is a capability nobody "
                   + "finds. Said: " + idle);
    }

    /// <summary>A start that records which runner it was asked about.</summary>
    private static ConsoleWatchRunner.Start Starting(List<string> asked) =>
        runnerId =>
        {
            asked.Add(runnerId);
            return true;
        };

    [Test]
    public async Task Watching_connects_and_leaves_the_pane_on_that_flight()
    {
        // THE WHOLE POINT OF THE CHANGE. Handing the terminal over for the
        // duration was a watch a person had to leave the console to have; this
        // connects with the terminal free and comes back with the pane live.
        var asked = new List<string>();

        var after = ConsoleWatchRunner.Watch(State("GG-71"), Starting(asked));

        await Assert.That(asked).IsEquivalentTo(new[] { Vmlinux })
            .Because("the connect and the pane are both the machine's now, so one identifier "
                   + "crosses where two used to.");

        await Assert.That(after.LiveVisible).IsTrue()
            .Because("a watch that connected and showed nothing is a watch a person cannot "
                   + "tell from one that failed.");

        await Assert.That(after.WatchedRunnerId).IsEqualTo(Vmlinux)
            .Because("the pane needs to be told WHICH flight, and the queue cursor cannot "
                   + "say: the queue holds flights that need somebody, and one being watched "
                   + "is usually just flying.");
    }

    [Test]
    public async Task Nothing_here_waits_for_a_channel()
    {
        // THE COMPLAINT, AS A PROPERTY OF THIS FILE. Watching used to tear the
        // console down, print the connect on the bare terminal, and WAIT there -
        // up to a heartbeat interval when it worked and up to the
        // introduction's whole life when it did not. Raising that deadline so
        // it stopped racing the watch's own diagnosis made the wait worse: a
        // person pressing a key got a blank terminal for a minute.
        //
        // ASSERTED AS ABSENCE, because a wait is easy to reintroduce and hard
        // to see: it looks like one more line asking a reasonable question.
        // Where the console must not be held down is checked for real in
        // OneWatchAtATimeTests, which times Start against a follow that never
        // finishes.
        var source = Sources.Read("Gg.Console", "ConsoleWatchRunner.cs");

        foreach (var waiting in new[] { "Wait", "Opened(", "Result", "GetAwaiter" })
        {
            await Assert.That(source.Contains(waiting, StringComparison.Ordinal)).IsFalse()
                .Because($"a `{waiting}` here holds the console down over exactly the thing a "
                       + "person wants to watch happening.");
        }
    }

    // "A RUNNER FLYING NOTHING IS NOT WATCHED EVEN IF ASKED" WAS HERE, and
    // it inverted with the key above it. Dispatch still re-checks the rule the
    // key obeys - that is the point of checking it twice - but the rule is now
    // "beating", and the test for it is with the others in
    // WatchingAnIdleRunnerFromTheModalTests.

    // "A FLEET THAT DOES NOT SAY WHICH FLIGHT IS NOT WATCHED" WAS HERE, and
    // its rule is gone rather than moved. The pane needed a flight id to key a
    // buffer under, so a fleet answer without one was a refusal; the buffer is
    // keyed by the machine now, and what it draws is whatever that machine is
    // on - so there is nothing left to refuse over.

    [Test]
    public async Task A_flight_that_is_only_flying_is_watchable()
    {
        // THE WHOLE DEFECT, AS A ROW. GG-77 was started, watched, and nothing
        // appeared: the pane was bound to the queue cursor and the queue is a
        // queue of PROBLEMS - awaiting a decision, a lease expired twice, a
        // runner gone. A flight that is simply flying is in none of those, so
        // watching worked for exactly the flights nobody wants to watch.
        var after = ConsoleWatchRunner.Watch(State("GG-77"), _ => true);

        await Assert.That(after.Queue).IsEmpty()
            .Because("this is the case that was broken and it has to stay the case.");
        await Assert.That(after.WatchedRunnerId).IsEqualTo(Vmlinux);
        await Assert.That(after.LiveVisible).IsTrue();
    }
}
