using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// What the help modal is made of, decided in the model.
/// </summary>
/// <remarks>
/// <para>
/// <b>The widgets are Terminal.Gui's and the answers are not.</b> A
/// <c>Tabs</c> holds the pages and a <c>TreeView</c> holds the keys, but which
/// page is showing and which folds are open live in <see cref="AppState"/> —
/// the pattern <c>ConsoleScreen</c> already uses for the main tab bar, where
/// the widget follows <c>ActiveTab</c> behind a sync flag.
/// </para>
/// <para>
/// <b>Which answers an objection this file used to carry.</b> The text tab bar
/// was defended on the grounds that "a Terminal.Gui TabView would put which
/// page is showing inside a widget, where no test can assert it" — true of a
/// widget that OWNS the answer, and not of one that renders it. These tests
/// are what makes the difference real rather than asserted.
/// </para>
/// </remarks>
public class TheHelpPageIsTabbedAndFoldedTests
{
    [Test]
    public async Task Every_page_the_tabs_offer_is_a_page_the_model_knows()
    {
        foreach (var page in HelpPages.All)
        {
            await Assert.That(HelpPages.Title(page)).IsNotEmpty()
                .Because("a tab with no name is a tab nobody can choose, and the widget "
                       + "takes its labels from here rather than inventing them.");
        }
    }

    [Test]
    public async Task Tab_cycles_the_pages_and_comes_back_round()
    {
        var seen = new List<HelpPage>();
        var page = HelpPages.All[0];

        for (var i = 0; i < HelpPages.All.Count; i++)
        {
            seen.Add(page);
            page = HelpPages.Next(page);
        }

        await Assert.That(seen).IsEquivalentTo(HelpPages.All.ToList())
            .Because("every page is reachable by the one key the bar advertises.");

        await Assert.That(page).IsEqualTo(HelpPages.All[0])
            .Because("a cycle that stopped at the end would strand somebody on the last "
                   + "page with no way back but closing the modal.");
    }

    [Test]
    public async Task The_keys_page_is_the_one_that_opens()
    {
        await Assert.That(HelpPages.All[0]).IsEqualTo(HelpPage.Keys)
            .Because("help is opened to find a key far more often than to read a setting, "
                   + "and the first page is the one a person did not have to ask for.");
    }

    [Test]
    public async Task A_fold_can_be_opened_and_closed_and_the_model_remembers()
    {
        var state = new AppState();

        await Assert.That(HelpTree.IsOpen(state, UiMode.Help)).IsFalse();

        var opened = HelpTree.Toggle(state, UiMode.Help);

        await Assert.That(HelpTree.IsOpen(opened, UiMode.Help)).IsTrue()
            .Because("the console rebuilds its views from the model after handing the "
                   + "terminal to an editor, so a fold the TreeView alone remembered would "
                   + "spring shut on the way back.");

        await Assert.That(HelpTree.IsOpen(HelpTree.Toggle(opened, UiMode.Help), UiMode.Help))
            .IsFalse();
    }

    [Test]
    public async Task The_always_available_keys_are_open_before_anybody_asks()
    {
        await Assert.That(HelpTree.IsOpen(new AppState(), UiMode.Normal)).IsTrue()
            .Because("they answer `what can I do', and a tree that folded them would make "
                   + "the common case the hidden one.");
    }
}
