namespace Gg.Console.Tests;

/// <summary>
/// Every view this console has is on the tab bar from the moment it starts,
/// and each one says which key jumps to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>TABS THAT APPEAR WHEN YOU OPEN THEM ARE NOT TABS.</b> The first version
/// put a tab on the bar once its view had been opened, so a person looking for
/// the repositories saw no repositories tab — the bar could only ever tell them
/// about views they had already found. A tab bar's whole job is to say what
/// there is.
/// </para>
/// <para>
/// <b>Which makes the bar the place the keys live.</b> Six of the eight tabs
/// have a key that jumps straight to them, and a hint line repeating what the
/// bar already shows spends the most valuable line on the screen saying it
/// twice. The keys stay bound; they are advertised on the tab and not below it.
/// </para>
/// <para>
/// <b>The bar is a component now, not the window title.</b> What the title
/// could carry was a string, and a string cannot be selected, cannot scroll,
/// and cannot be clicked - so the model still owns which tab is showing, and
/// what the view does with that is <c>Terminal.Gui.Views.Tabs</c>.
/// </para>
/// </remarks>
public class EveryTabIsOnTheBarTests
{
    [Test]
    public async Task Every_tab_is_on_the_bar_before_anybody_opens_one()
    {
        var bare = new AppState();

        await Assert.That(Tabs.All).IsEquivalentTo(Enum.GetValues<TabId>())
            .Because("the bar says what there is. A tab that appears once you have found "
                   + "the view is a tab that told you nothing.");

        foreach (var tab in Tabs.All)
        {
            await Assert.That(Tabs.Title(bare, tab)).IsNotEmpty()
                .Because($"{tab} is on the bar of a console that has just started, so it "
                       + "needs something to say on it.");
        }
    }

    [Test]
    public async Task A_tab_that_has_a_key_says_so_on_the_tab()
    {
        // AND THE KEY IS THE ONE THAT WORKS, read out of the keymap rather than
        // typed here. A label promising a key that does nothing is worse than
        // no label: it teaches a person the console is broken.
        var bare = new AppState();

        foreach (var tab in Tabs.All)
        {
            if (Tabs.KeyFor(tab) is not { } key)
            {
                continue;
            }

            await Assert.That(Keymap.Resolve(key, new KeymapContext(UiMode.Normal)))
                .IsEqualTo(Tabs.CommandFor(tab))
                .Because($"the bar offers {key.Name} for {tab}, so pressing it has to go there.");

            await Assert.That(Tabs.Title(bare, tab)).Contains(key.Name, StringComparison.Ordinal)
                .Because($"{tab}'s key is on {tab}'s tab, which is why it is not on the hint "
                       + "line. Title: " + Tabs.Title(bare, tab));
        }
    }

    [Test]
    public async Task The_two_that_cannot_be_closed_need_no_key()
    {
        // The queue and the flights are always there and one tab press apart,
        // so a letter spent on either is a letter taken from something a person
        // cannot otherwise reach.
        await Assert.That(Tabs.KeyFor(TabId.Queue)).IsNull();
        await Assert.That(Tabs.KeyFor(TabId.Flights)).IsNull();

        foreach (var tab in Tabs.All.Where(t => t is not (TabId.Queue or TabId.Flights)))
        {
            await Assert.That(Tabs.KeyFor(tab)).IsNotNull()
                .Because($"{tab} is not where a console opens, so something has to reach it.");
        }
    }

    [Test]
    public async Task The_hint_line_does_not_say_again_what_the_bar_already_says()
    {
        var hints = Keymap.Hints(new KeymapContext(UiMode.Normal));

        foreach (var tab in Tabs.All)
        {
            if (Tabs.KeyFor(tab) is not { } key)
            {
                continue;
            }

            await Assert.That(hints).DoesNotContain($"{key.Name} {Tabs.Name(tab).ToLowerInvariant()}",
                    StringComparison.OrdinalIgnoreCase)
                .Because($"{key.Name} is on {tab}'s own tab. Line: " + hints);
        }

        // THE ANCHOR, and it is the whole reason the line survives at all: the
        // keys that are NOT a tab have nowhere else to be advertised.
        foreach (var kept in (string[])["q quit", "g refresh", "? help", "n new flight"])
        {
            await Assert.That(hints).Contains(kept, StringComparison.Ordinal)
                .Because("this key is on no tab, so the line is where a person finds it.");
        }
    }

    [Test]
    public async Task Selecting_a_tab_and_pressing_its_key_are_the_same_act()
    {
        // What the view needs to know when somebody clicks a tab. Without it
        // the bar would be a second way to change the model, and two ways to
        // change one thing is how they come to disagree.
        foreach (var tab in Tabs.All)
        {
            var command = Tabs.CommandFor(tab);

            if (tab is TabId.Queue or TabId.Flights)
            {
                await Assert.That(command).IsNull()
                    .Because($"{tab} is always there; nothing has to be asked for.");
                continue;
            }

            await Assert.That(command).IsNotNull();
            await Assert.That(Keymap.Bindings(new KeymapContext(UiMode.Normal))
                    .Any(b => b.Command == command))
                .IsTrue()
                .Because($"{tab}'s command is one the keymap issues, so clicking the tab and "
                       + "pressing the key take the same path through the shell.");
        }
    }

    [Test]
    public async Task Exactly_one_tab_is_ever_on_the_screen()
    {
        // Kept from the version this replaces, because it is the invariant the
        // view is built from and it survives every tab being on the bar: being
        // ON the bar and HAVING the screen are different things.
        for (var seed = 0; seed < 40; seed++)
        {
            var state = StateGenerator.Next(new Random(seed));
            var showing = Tabs.All.Where(tab => Tabs.Showing(state, tab)).ToList();

            await Assert.That(showing.Count).IsEqualTo(1)
                .Because($"seed {seed} draws {showing.Count} views at once: "
                       + string.Join(", ", showing));
        }
    }
}
