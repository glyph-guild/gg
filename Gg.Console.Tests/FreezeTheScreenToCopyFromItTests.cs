using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Freezing stops the whole screen and hands the mouse back, so a person can
/// select and copy anything on it with their own terminal.
/// </summary>
/// <remarks>
/// <para>
/// <b>The promise was already written down and never kept.</b>
/// <c>ConsoleScreen.Render</c> says <i>"Frozen means the pixels stop moving, so
/// the terminal's own selection can survive being made"</i> - and two things
/// were wrong with that. It guarded ONE pane's text, so the countdown ticked
/// and every table redrew a second later; and gg holds the mouse with
/// <c>?1003h</c>, any-motion reporting, which is what disables a terminal's own
/// click-and-drag selection in the first place. The selection could not be made,
/// so there was nothing for the freeze to preserve.
/// </para>
/// <para>
/// <b>So the freeze does both halves, and it is not the live tab's any more.</b>
/// Anything on the screen is worth copying - a flight id, a credential locator,
/// an error somebody needs to paste into a ticket - and the live pane was never
/// the only place text appears.
/// </para>
/// <para>
/// <b>Why the terminal does the selecting rather than gg.</b> The rendered
/// screen IS readable - <c>IDriver.Contents</c> is a grid of cells - so gg
/// could carry an anchor, draw its own highlight and copy the run out. That
/// builds a second, worse version of something every terminal already has: word
/// and line selection, the clipboard a person already uses, and whatever their
/// terminal does over ssh. What gg is uniquely able to do here is get out of the
/// way, because it is gg that took the mouse.
/// </para>
/// <para>
/// <b><c>ctrl+f</c>, so it spends no letter at all.</b> The plain letters in
/// this keymap are gone, and the last free one is worth more than a mode: `f`
/// alone is fly-this on the browse tab, and a key that covers the whole console
/// cannot be one that means something else on a tab. Ctrl is a whole keyboard
/// nobody here has spent - only <c>ctrl+c</c> and the airspace field's three
/// are taken - and `f` is the letter in the word.
/// </para>
/// <para>
/// <b>And not the ones the terminal eats.</b> <c>ctrl+s</c> and <c>ctrl+q</c>
/// are flow control and <c>ctrl+z</c> suspends the process; a freeze key that
/// stopped the console by stopping the program would be the joke version of
/// this feature.
/// </para>
/// </remarks>
public class FreezeTheScreenToCopyFromItTests
{
    private static AppState On(TabId tab) => new() { ActiveTab = tab };

    private static StreamLine Said(string text) => new()
    {
        Kind = StreamLineKind.Text,
        Text = text,
        At = DateTimeOffset.UnixEpoch,
    };

    [Test]
    public async Task It_freezes_from_any_tab_rather_than_only_the_live_one()
    {
        foreach (var tab in Tabs.All)
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Control('f'), KeymapContext.For(On(tab))))
                .IsEqualTo(Command.ToggleFreeze)
                .Because($"there is text worth copying on {tab}, and the live pane was never "
                       + "the only place text appears.");
        }
    }

    [Test]
    public async Task It_spends_no_letter()
    {
        // THE WHOLE REASON IT IS A CTRL KEY. There is one plain letter left in
        // this keymap and a view nobody can otherwise reach will want it.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(On(TabId.Queue))))
            .IsNull();

        foreach (var eaten in "sqz")
        {
            await Assert.That(Keymap.Resolve(KeyStroke.Control(eaten), KeymapContext.For(On(TabId.Queue))))
                .IsNull()
                .Because($"ctrl+{eaten} is flow control or a suspend, and a terminal may never "
                       + "hand it over - a key that works on one machine and not the next is "
                       + "worse than one nobody bound.");
        }
    }

    [Test]
    public async Task The_live_tabs_old_key_no_longer_means_freeze()
    {
        // ONE WORD, ONE KEY. `f` froze on the live tab and nowhere else; two
        // keys for one act is a second thing to learn, and the tab-scoped one
        // was the half that could not be found from anywhere else.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), KeymapContext.For(On(TabId.Live))))
            .IsNotEqualTo(Command.ToggleFreeze);
    }

    [Test]
    public async Task Freezing_and_thawing_are_the_same_key()
    {
        var frozen = Reducer.Reduce(On(TabId.Queue), Command.ToggleFreeze);

        await Assert.That(frozen.Frozen).IsTrue();

        var thawed = Reducer.Reduce(frozen, Command.ToggleFreeze);

        await Assert.That(thawed.Frozen).IsFalse()
            .Because("a mode with no way out is the thing every modal in this console has "
                   + "exactly one of.");
    }

    [Test]
    public async Task What_arrived_while_it_was_frozen_is_not_lost()
    {
        // THE HALF THAT ALREADY WORKED, kept: the live tail banks its lines
        // while the pixels are still and flushes them on the way out.
        var frozen = Reducer.Reduce(
            On(TabId.Live) with { Live = [Said("one")] }, Command.ToggleFreeze) with
        {
            Held = [Said("two"), Said("three")],
        };

        var thawed = Reducer.Reduce(frozen, Command.ToggleFreeze);

        await Assert.That(thawed.Live.Select(l => l.Text).ToList())
            .IsEquivalentTo((string[])["one", "two", "three"]);
        await Assert.That(thawed.Held).IsEmpty();
    }

    [Test]
    public async Task A_frozen_console_says_so_on_the_screen()
    {
        // A CONSOLE THAT STOPPED REDRAWING AND SAID NOTHING IS A HUNG ONE.
        // Nothing repaints while this is on, so the sentence has to go up on
        // the last paint before everything stops.
        var said = PaneText.Activity(Reducer.Reduce(On(TabId.Queue), Command.ToggleFreeze));

        await Assert.That(said).Contains("frozen", StringComparison.OrdinalIgnoreCase);
        await Assert.That(said).Contains("ctrl+f", StringComparison.OrdinalIgnoreCase)
            .Because("and it names the way out, because the screen it is written on is the "
                   + "one that has stopped answering.");
    }

    [Test]
    public async Task And_an_unfrozen_one_says_nothing_about_it()
    {
        await Assert.That(PaneText.Activity(On(TabId.Queue)))
            .DoesNotContain("frozen", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task The_way_out_is_on_the_hint_line_while_it_is_on()
    {
        var frozen = KeymapContext.For(Reducer.Reduce(On(TabId.Queue), Command.ToggleFreeze));

        await Assert.That(Keymap.Hints(frozen)).Contains("ctrl+f ")
            .Because("the hint line is the last thing drawn before the screen stops, so it is "
                   + "the one place the way out can still be advertised.");
    }
}
