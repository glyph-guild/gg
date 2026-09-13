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
    public async Task The_bottom_line_carries_it_too()
    {
        // WHEREVER A PERSON IS LOOKING. The live pane is off by default, so a
        // console that only moved there would be still for everybody who never
        // opened it.
        var state = new AppState { Refresh = new RefreshState { NextIn = 3 } };
        var line = PaneText.BottomLine(state);

        await Assert.That(line).Contains(PaneText.Alive(3))
            .Because("the bottom line is on every screen, which is where a person looks to "
                   + "see whether anything is happening at all.");

        await Assert.That(line).StartsWith(Keymap.Hints(KeymapContext.For(state)))
            .Because("the hint line is exactly the keys that are live, which is a rule of "
                   + "its own - so the mark goes BESIDE it rather than into it.");
    }
}
