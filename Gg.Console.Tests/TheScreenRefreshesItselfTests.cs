using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The screen brings itself up to date every half minute, without going away
/// while it does it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refreshing tore the terminal down.</b> `g' ends the UI session, the
/// application is disposed, the alternate screen is left and re-entered, and a
/// new session is built from the model - correct, and invisible at human speed
/// exactly once. On a timer it is the console vanishing every thirty seconds.
/// </para>
/// <para>
/// <b>So this is the second exception to "a UI session may not read", and it is
/// narrower than it looks.</b> The session does not read: it folds a result
/// that has already arrived. The request runs on a task owned outside every UI
/// lifetime - <c>LiveTails</c>' shape - and the tick asks only whether it has
/// finished. A session that WAITED for one would freeze the keyboard, which is
/// the thing the rule is protecting, and this never waits.
/// </para>
/// <para>
/// <b>What comes back is a patch, not a model.</b> A read that returned a whole
/// <c>AppState</c> would be a snapshot taken before the person moved the cursor
/// and applied after - so the background half produces a function, and the
/// function is applied to whatever is on screen when it lands. Nothing has to
/// list which fields are the read plane, which is the list
/// <c>ConsoleStart</c> exists to avoid.
/// </para>
/// <para>
/// <b>And only the tab in front of somebody.</b> Refreshing the envelope while
/// a person watches the fleet is a request nobody asked for, once every thirty
/// seconds, for as long as the console is open.
/// </para>
/// </remarks>
public class TheScreenRefreshesItselfTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private sealed class Ticking : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = T0;

        internal void Wait(TimeSpan span) => UtcNow += span;
    }

    private sealed class Reads
    {
        internal List<TabId> Asked { get; } = [];

        internal TaskCompletionSource<Func<AppState, AppState>> Answer { get; private set; } =
            new();

        internal Task<Func<AppState, AppState>> Of(TabId tab)
        {
            Asked.Add(tab);
            Answer = new TaskCompletionSource<Func<AppState, AppState>>();

            return Answer.Task;
        }
    }

    private static (AutoRefresh Refresh, Reads Reads, Ticking Clock) Screen()
    {
        var reads = new Reads();
        var clock = new Ticking();

        return (new AutoRefresh(reads.Of, clock, TimeSpan.FromSeconds(30)), reads, clock);
    }

    [Test]
    public async Task Nothing_is_read_until_the_half_minute_is_up()
    {
        var (refresh, reads, clock) = Screen();

        var state = refresh.Advance(new AppState { ActiveTab = TabId.Runners });

        await Assert.That(reads.Asked).IsEmpty();
        await Assert.That(state.Refresh.NextIn).IsEqualTo(30)
            .Because("and the hint line says how long, which is the whole of what a person "
                   + "needs to know about a thing that happens on its own.");

        clock.Wait(TimeSpan.FromSeconds(29));
        state = refresh.Advance(state);

        await Assert.That(reads.Asked).IsEmpty();
        await Assert.That(state.Refresh.NextIn).IsEqualTo(1);

        clock.Wait(TimeSpan.FromSeconds(1));
        state = refresh.Advance(state);

        await Assert.That(reads.Asked).IsEquivalentTo(new[] { TabId.Runners })
            .Because("the tab in front of somebody, and no other.");
        await Assert.That(state.Refresh.Busy).IsTrue();
    }

    [Test]
    public async Task The_session_never_waits_for_it()
    {
        var (refresh, reads, clock) = Screen();

        clock.Wait(TimeSpan.FromSeconds(30));
        var state = refresh.Advance(new AppState { ActiveTab = TabId.Runners });

        // THE READ HAS NOT ANSWERED. Every tick from here has to return, or the
        // keyboard is frozen for as long as the control plane takes - which is
        // the thing the rule against reading in a session is protecting.
        for (var tick = 0; tick < 4; tick++)
        {
            clock.Wait(TimeSpan.FromSeconds(1));
            state = refresh.Advance(state);

            await Assert.That(state.Refresh.Busy).IsTrue();
        }

        await Assert.That(reads.Asked).Count().IsEqualTo(1)
            .Because("and it does not start a second while the first is in the air.");
    }

    [Test]
    public async Task What_lands_is_applied_to_what_is_on_screen_now()
    {
        var (refresh, reads, clock) = Screen();

        clock.Wait(TimeSpan.FromSeconds(30));
        var state = refresh.Advance(new AppState { ActiveTab = TabId.Runners });

        // THE PERSON MOVED WHILE IT WAS IN THE AIR. A read that answered with a
        // whole model would put this cursor back where it was when the request
        // left.
        state = state with { RunnerSelected = 4 };

        reads.Answer.SetResult(s => s with
        {
            Runners = new RunnerList { Runners = [] },
        });

        state = refresh.Advance(state);

        await Assert.That(state.Runners).IsNotNull()
            .Because("what came back is folded in.");
        await Assert.That(state.RunnerSelected).IsEqualTo(4)
            .Because("and where they were looking survives it.");
        await Assert.That(state.Refresh.Busy).IsFalse();
        await Assert.That(state.Refresh.NextIn).IsEqualTo(30)
            .Because("the clock starts again when the answer lands, not when the request "
                   + "left - otherwise a slow control plane means a permanent countdown of "
                   + "zero.");
    }

    [Test]
    public async Task The_key_asks_for_one_now_and_does_not_end_the_session()
    {
        await Assert.That(ShellCommands.Handled).DoesNotContain(Command.Refresh)
            .Because("ending the session is what makes the console disappear, and a refresh "
                   + "that reads one tab has nothing to hand the terminal to.");

        var (refresh, reads, _) = Screen();

        var asked = Reducer.Reduce(
            new AppState { ActiveTab = TabId.Repositories }, Command.Refresh);

        await Assert.That(asked.Refresh.Wanted).IsTrue()
            .Because("the reducer is pure, so pressing the key can only say that one is "
                   + "wanted; the tick is what does it.");

        var state = refresh.Advance(asked);

        await Assert.That(reads.Asked).IsEquivalentTo(new[] { TabId.Repositories });
        await Assert.That(state.Refresh.Wanted).IsFalse()
            .Because("asked and answered, or the next tick asks again for ever.");
    }

    [Test]
    public async Task The_hint_line_counts_down_and_then_says_it_is_working()
    {
        var waiting = Keymap.Hints(new KeymapContext(UiMode.Normal) { Refresh = "27s" });

        await Assert.That(waiting).Contains("g refresh 27s")
            .Because("next to the key it belongs to, which is where somebody reading the line "
                   + $"is already looking. Line: {waiting}");

        var working = Keymap.Hints(new KeymapContext(UiMode.Normal) { Refresh = "⟳" });

        await Assert.That(working).Contains("g refresh ⟳");

        var plain = Keymap.Hints(new KeymapContext(UiMode.Normal));

        await Assert.That(plain).Contains("g refresh ·")
            .Because("and with nothing to say it is the word on its own, so the help page - "
                   + "which is a union over contexts - does not carry a stopped clock.");

        // THE FIRST FRAME, which is drawn before the first tick. Observed: the
        // console opened saying `g refresh 0s', because a countdown nobody has
        // counted yet is zero seconds and reads as a clock that has stopped.
        await Assert.That(AutoRefresh.Says(new RefreshState())).IsEmpty()
            .Because("nothing counted is nothing to say, not zero.");
        await Assert.That(AutoRefresh.Says(new RefreshState { NextIn = 7 })).IsEqualTo("7s");
        await Assert.That(AutoRefresh.Says(new RefreshState { Busy = true })).IsEqualTo("⟳")
            .Because("and busy outranks the number, which is zero while a read is in the "
                   + "air.");
    }

    [Test]
    public async Task A_tab_that_reads_nothing_asks_for_nothing()
    {
        var (refresh, reads, clock) = Screen();

        clock.Wait(TimeSpan.FromSeconds(30));

        // The live pane is a local file and the browser is a child process this
        // console already owns. Neither is a thing to go and ask about.
        var state = refresh.Advance(new AppState { ActiveTab = TabId.Live });

        await Assert.That(reads.Asked).IsEmpty();
        await Assert.That(state.Refresh.NextIn).IsEqualTo(30)
            .Because("and the clock still resets, or the countdown sits at zero on that tab.");
    }
}
