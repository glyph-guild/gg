namespace Gg.Console.Tests;


/// <summary>
/// S53.6-02. The Itineraries tab is on the bar, answers a key nothing else
/// answers, and focus lands inside its own pane.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SHAPE THAT HAS CRASHED THIS CONSOLE BEFORE.</b> A tab costs six
/// exhaustive switches that throw on a word they do not know, and the two that
/// were missed last time each killed the console on a frame - one on the first
/// render, one on arrival. Four of them are in <c>Tabs</c>; the fifth is where
/// focus lands and the sixth is the pane's own text.
/// </para>
/// <para>
/// <b>Focus is the one that reads as a framework bug.</b> A landing arm that
/// fell through to another tab's widget produced <i>"FocusChanging was not
/// cancelled and the HasFocus value did not change"</i> - a sentence about
/// Terminal.Gui that says nothing about the missing arm. It refuses now
/// instead.
/// </para>
/// <para>
/// <b>And the key is punctuation because the letters ran out.</b> Normal mode
/// holds every letter but <c>z</c>, and the three newest tabs took <c>,</c>
/// <c>.</c> and <c>;</c> - so this takes the fourth of that family, which sits
/// under the same hand.
/// </para>
/// </remarks>
public class TheItinerariesTabIsOnTheBarTests
{
    private static AppState Bare() => new();

    [Test]
    public async Task It_is_a_tab_and_it_is_offered()
    {
        await Assert.That(Tabs.All).Contains(TabId.Itineraries);

        await Assert.That(Tabs.Offered(Bare())).Contains(TabId.Itineraries)
            .Because("a plan surface nobody can reach is one nobody will read - and unlike "
                   + "allowances there is no reason to hide it: a tenant with no plans sees "
                   + "an empty tab, which is an answer.");
    }

    [Test]
    public async Task It_sits_after_the_three_that_are_pinned()
    {
        // QUEUE, BOARD, FLIGHTS hold the first three places and nothing may
        // displace them - what needs somebody, what was nominated, what has
        // run. A plan is read after those, so this is appended rather than
        // inserted.
        await Assert.That(Tabs.All.ToList().IndexOf(TabId.Itineraries))
            .IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task Its_key_is_its_own_and_is_on_the_tab()
    {
        var key = Tabs.KeyFor(TabId.Itineraries);

        await Assert.That(key).IsNotNull()
            .Because("every tab says how to get to it.");

        await Assert.That(Keymap.Resolve(key!.Value, KeymapContext.For(Bare())))
            .IsEqualTo(Tabs.CommandFor(TabId.Itineraries))
            .Because("selecting the tab and pressing its key are the same act, and a tab "
                   + "advertising a key that resolves to nothing is a dead one.");

        await Assert.That(Tabs.Title(Bare(), TabId.Itineraries)).Contains(key.Value.Name)
            .Because("the key is on the tab, which is why it is kept off the hint line.");
    }

    [Test]
    public async Task Its_key_means_nothing_anywhere_else()
    {
        // A KEY THE SESSION HANDLES IN ONE MODE AND NOT ANOTHER IS ONE A
        // PERSON CANNOT LEARN. The tab keys are Normal's alone.
        var key = Tabs.KeyFor(TabId.Itineraries)!.Value;

        foreach (var mode in Enum.GetValues<UiMode>().Where(m => m != UiMode.Normal))
        {
            await Assert.That(Keymap.Resolve(key, KeymapContext.For(Bare() with { Mode = mode })))
                .IsNull()
                .Because($"'{key.Name}' is a tab key, and in {mode} it would shadow whatever "
                       + "that mode's keys are for.");
        }
    }

    [Test]
    public async Task Every_switch_that_throws_on_an_unknown_tab_knows_it()
    {
        // THE FOUR IN Tabs, CALLED RATHER THAN READ. Each throws
        // ArgumentOutOfRangeException on a word it does not know, so calling
        // them IS the assertion - and Tabs.Title calls two of them on every
        // render, which is why a miss kills the first frame.
        await Assert.That(Tabs.Name(TabId.Itineraries)).IsNotEmpty();
        await Assert.That(Tabs.Title(Bare(), TabId.Itineraries)).IsNotEmpty();
        await Assert.That(Tabs.CommandFor(TabId.Itineraries)).IsNotNull();

        // HasRead answers false before anything is read, which is the state
        // the tab spends its life in until somebody asks for the read.
        await Assert.That(Tabs.HasRead(Bare(), TabId.Itineraries)).IsFalse();

        // And the pane says something rather than nothing while it waits.
        await Assert.That(PaneText.ForTab(Bare(), TabId.Itineraries)).IsNotEmpty();
    }

    [Test]
    public async Task Focus_lands_inside_its_own_pane()
    {
        // THE FIFTH FOLLOWER, and the one whose failure reads as a framework
        // bug rather than a missing arm. Asserted over the SOURCE, because the
        // switch is in a screen this test cannot start.
        var landing = ConsoleScreenSource();

        var from = landing.IndexOf("View landing = State.ActiveTab switch", StringComparison.Ordinal);
        await Assert.That(from).IsGreaterThan(0)
            .Because("this test says nothing if the switch it reads has been renamed.");

        var arm = landing[from..landing.IndexOf("landing.SetFocus()", from, StringComparison.Ordinal)];

        await Assert.That(arm).Contains("TabId.Itineraries")
            .Because("a tab with no landing arm throws on arrival - and before the arm "
                   + "refused, it focused a sibling tab's widget and produced a sentence "
                   + "about Terminal.Gui instead.");
    }

    private static string ConsoleScreenSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(
            dir!.FullName, "Gg.Console", "Views", "ConsoleScreen.cs"));
    }
}
