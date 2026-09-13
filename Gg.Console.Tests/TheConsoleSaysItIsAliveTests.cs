using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A pane waiting on a machine says so, and the console shows it is still alive.
/// </summary>
/// <remarks>
/// <para>
/// <b>An empty box cannot say why it is empty.</b> The pane already says which
/// silence it is in - nothing is writing, the tail stopped, nothing said yet -
/// and a watch attached to a machine that is flying nothing is a FOURTH one. The
/// nearest existing sentence claims "the flight is writing a live view", which
/// on an idle machine is a statement about a flight that does not exist.
/// </para>
/// <para>
/// <b>And a console that is waiting has to look different from one that has
/// stopped.</b> Watching an idle runner means sitting in front of a box that is
/// correctly empty, sometimes for a long time - which is exactly what a frozen
/// console looks like. The tail of the pane moves while the console is alive.
/// </para>
/// <para>
/// <b>Derived from the countdown rather than stored.</b> <c>Refresh.NextIn</c>
/// already ticks once a second and is already in the model, so nothing new has
/// to be kept and nothing new can go stale. It also stops when the console does,
/// which is what makes it liveness rather than decoration.
/// </para>
/// </remarks>
public class TheConsoleSaysItIsAliveTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    private static AppState Waiting(int nextIn) => new()
    {
        LiveVisible = true,
        WatchedRunnerId = "vmlinux001",
        Silence = LiveSilence.Waiting,
        Refresh = new RefreshState { NextIn = nextIn },
    };

    [Test]
    public async Task A_watch_with_nothing_flying_says_what_it_is_waiting_for()
    {
        var said = PaneText.Live(Waiting(3));

        await Assert.That(said).Contains("waiting")
            .Because("an empty box cannot say why it is empty, and the nearest sentence "
                   + "claims the flight is writing a live view - which on a machine flying "
                   + "nothing is about a flight that does not exist. Said: " + said);
    }

    [Test]
    public async Task And_it_moves_while_the_console_is_alive()
    {
        var one = PaneText.Live(Waiting(3));
        var two = PaneText.Live(Waiting(2));

        await Assert.That(one).IsNotEqualTo(two)
            .Because("sitting in front of a correctly empty box is exactly what a frozen "
                   + "console looks like, so something has to move. Said: " + one + " / " + two);
    }

    [Test]
    public async Task The_mark_is_the_same_whenever_the_countdown_is()
    {
        // A FUNCTION OF THE MODEL AND NOTHING ELSE. A mark that read a clock of
        // its own would make this pane the one thing in the console that cannot
        // be redrawn from state - which is the rule terminal release rests on.
        await Assert.That(PaneText.Alive(4)).IsEqualTo(PaneText.Alive(4));

        await Assert.That(PaneText.Alive(4)).IsNotEqualTo(PaneText.Alive(5))
            .Because("consecutive seconds have to look different or nothing is moving.");
    }

    [Test]
    public async Task A_pane_with_lines_in_it_carries_the_mark_too()
    {
        // NOT ONLY THE EMPTY CASE. A flight that has said something and then
        // gone quiet for a minute is the same question as an empty box: is
        // anything still running, or did this stop?
        var speaking = Waiting(3) with
        {
            Silence = LiveSilence.Speaking,
            Live = [new StreamLine { Kind = StreamLineKind.Text, Text = "working", At = T0 }],
        };

        await Assert.That(PaneText.Live(speaking)).EndsWith(PaneText.Alive(3))
            .Because("the pane is where somebody watching is looking, so that is where the "
                   + "mark has to be. Said: " + PaneText.Live(speaking));

        await Assert.That(PaneText.Live(speaking)).IsNotEqualTo(PaneText.Live(speaking with
        {
            Refresh = new RefreshState { NextIn = 2 },
        }));
    }

    [Test]
    public async Task A_frozen_pane_does_not_move_at_all()
    {
        // FREEZING IS A PROMISE THAT THE PIXELS STOP, so that a terminal's own
        // selection survives being made. A mark that went on moving would break
        // exactly the thing freezing is for.
        var frozen = Waiting(3) with
        {
            Silence = LiveSilence.Speaking,
            Frozen = true,
            Live = [new StreamLine { Kind = StreamLineKind.Text, Text = "working", At = T0 }],
        };

        await Assert.That(PaneText.Live(frozen))
            .IsEqualTo(PaneText.Live(frozen with { Refresh = new RefreshState { NextIn = 2 } }))
            .Because("held still is held still, and a mark ticking under a frozen pane is a "
                   + "screen that cannot be selected from.");
    }

    [Test]
    public async Task And_the_rest_of_the_console_does_not_carry_it()
    {
        // IT BELONGS TO THE LIVE TAB. A mark along the bottom of the whole
        // application is on every screen whether or not anything is being
        // watched - which makes it furniture rather than an answer, and puts it
        // on the one line whose rule is that it names exactly the keys that are
        // live.
        var hints = Keymap.Hints(KeymapContext.For(Waiting(3)));

        await Assert.That(hints.Contains(PaneText.Alive(3), StringComparison.Ordinal)).IsFalse()
            .Because("the hint line is the keys that work right now and nothing else.");

        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen.Contains("BottomLine", StringComparison.Ordinal)).IsFalse()
            .Because("and the screen composes its hint line from the keymap alone, which is "
                   + "what keeps that rule assertable.");
    }
}
