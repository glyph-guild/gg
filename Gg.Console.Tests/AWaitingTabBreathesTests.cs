using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A tab with nothing in it yet says so by breathing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because "not read yet" and "read, and empty" look identical.</b> This
/// console already keeps separate sentences for the two - the rule is written
/// across <c>PaneText</c> and <c>Tabs.HasRead</c>, and it is the same rule that
/// gives the board "not read yet" until BOTH its reads answer. What it has not
/// had is something a person sees from across the desk: a tab still filling
/// looks like a tab that came back empty.
/// </para>
/// <para>
/// <b>Pure, and a pure function of the tick.</b> The art is decided here and
/// drawn nowhere else, so the thing that decides what a waiting tab looks like
/// can be tested without a terminal - <c>Keymap.Resolve</c>'s rule, one pane
/// over.
/// </para>
/// <para>
/// <b>Rectangular on purpose.</b> Centring is the view's job and it can only
/// do it against lines of one width; a frame that changed width as it breathed
/// would wander around the pane.
/// </para>
/// </remarks>
public class AWaitingTabBreathesTests
{
    [Test]
    public async Task It_is_a_gg()
    {
        // THE ONE ASSERTION ABOUT THE SHAPE. Two glyphs, which is what the
        // product is called - a single g would be a different mark.
        var art = LoadingArt.Of(0);

        await Assert.That(art).IsNotEmpty();
        await Assert.That(art.Count).IsGreaterThan(4)
            .Because("it was asked to be large, and four rows is a word rather than a mark.");
    }

    [Test]
    public async Task Every_line_is_the_same_width()
    {
        // SO THE VIEW CAN CENTRE IT. A ragged frame centres differently line by
        // line, which reads as the mark wobbling rather than breathing.
        foreach (var tick in (int[])[0, 1, 2, 3, 4, 5, 6, 7])
        {
            var art = LoadingArt.Of(tick);
            var widths = art.Select(line => line.Length).Distinct().ToList();

            await Assert.That(widths.Count).IsEqualTo(1)
                .Because($"tick {tick} drew lines of {widths.Count} different widths.");
        }
    }

    [Test]
    public async Task It_breathes()
    {
        // THE POINT. Something that does not change is a picture, and a picture
        // of a logo in an empty pane says nothing about whether anything is
        // happening.
        var first = string.Join('\n', LoadingArt.Of(0));
        var later = Enumerable.Range(1, 8).Select(t => string.Join('\n', LoadingArt.Of(t)));

        await Assert.That(later.Any(frame => frame != first)).IsTrue();
    }

    [Test]
    public async Task And_comes_back_around()
    {
        // A BREATH, NOT A PROGRESS BAR. It cannot say how far along a read is -
        // nothing here knows - so it must not look like it is counting up to
        // something.
        var start = string.Join('\n', LoadingArt.Of(0));
        var round = string.Join('\n', LoadingArt.Of(LoadingArt.Breath));

        await Assert.That(round).IsEqualTo(start);
    }

    [Test]
    public async Task A_tick_from_anywhere_is_safe()
    {
        // THE COUNTER LIVES IN AppState AND ONLY EVER GOES UP, so this is
        // handed every int there is eventually - and a mark that threw on one
        // of them would take the console down while it waited for a read.
        foreach (var tick in (int[])[-1, -7, int.MinValue, int.MaxValue, 99999])
        {
            await Assert.That(LoadingArt.Of(tick)).IsNotEmpty()
                .Because($"tick {tick} has to draw something.");
        }
    }

    [Test]
    public async Task A_tab_that_has_read_is_not_waiting()
    {
        // IT IS Tabs.HasRead, ASKED THE OTHER WAY. Two answers to "has this
        // arrived" is how a pane comes to show a spinner over a full table.
        var loaded = new AppState
        {
            ActiveTab = TabId.Flights,
            Flights = new Gg.Contracts.FlightList { Flights = [] },
        };

        await Assert.That(LoadingArt.Waiting(loaded)).IsFalse()
            .Because("an empty list is an answer, and the pane already has a sentence "
                   + "for it. Breathing over that would say the read is still coming.");
    }

    [Test]
    public async Task A_tab_still_waiting_is()
    {
        var unread = new AppState { ActiveTab = TabId.Flights };

        await Assert.That(LoadingArt.Waiting(unread)).IsTrue();
    }
}
