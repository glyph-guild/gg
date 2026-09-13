using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// Turning a help page hands the keyboard to the page turned to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner modal's rule, on the modal beside it.</b> "The modal already
/// has focus, so nothing needs moving" is true of a modal with one place for
/// the keyboard to be and false of one made of pages: the page can turn while
/// the modal keeps focus, and the keyboard has to follow it because each page
/// is a different widget with its own cursor.
/// </para>
/// <para>
/// <b>It did not matter until the pages scrolled.</b> While Environment and
/// Doctor were Labels there was nothing on them to move, so focus staying on
/// the tree behind them was invisible. The moment they became lists, a page
/// could be scrolled only by the person who never left the first one - which
/// is the report this pair closes, one layer under the widget.
/// </para>
/// <para>
/// <b>Measured in a pty before it was written.</b> The Environment page drew
/// correctly, wrapped correctly, and did not move for fourteen presses of the
/// down arrow.
/// </para>
/// </remarks>
public class TheKeyboardFollowsTheHelpPageTests
{
    [Test]
    public async Task Opening_help_lands_on_the_page_that_is_showing()
    {
        await Assert.That(FocusChange.Wanted(
                UiMode.Help,
                TabId.Runners,
                landed: null,
                modalHasFocus: false))
            .IsEqualTo(FocusTarget.HelpPage)
            .Because("a Dialog stops at itself, and the arrows then move nothing at all.");
    }

    [Test]
    public async Task Turning_the_page_moves_it_again()
    {
        await Assert.That(FocusChange.Wanted(
                UiMode.Help,
                TabId.Runners,
                landed: null,
                modalHasFocus: true,
                helpPage: HelpPage.Environment,
                landedHelpPage: HelpPage.Keys))
            .IsEqualTo(FocusTarget.HelpPage)
            .Because("the page a person turned to is the one their arrows should scroll, "
                   + "and the modal holding focus says nothing about which page has it.");
    }

    [Test]
    public async Task And_standing_still_leaves_it_alone()
    {
        await Assert.That(FocusChange.Wanted(
                UiMode.Help,
                TabId.Runners,
                landed: null,
                modalHasFocus: true,
                helpPage: HelpPage.Environment,
                landedHelpPage: HelpPage.Environment))
            .IsEqualTo(FocusTarget.LeaveAlone)
            .Because("Render runs once a second, and re-placing focus then would drag the "
                   + "cursor back to the top of the page while somebody is reading it - "
                   + "the pair is what makes this a change rather than a standing order.");
    }

    [Test]
    public async Task The_fold_key_is_not_advertised_off_the_keys_page()
    {
        // A DEAD KEY, AND IT WAS ON SCREEN. The hint line read "fold or unfold
        // this group" while the Environment page was showing, because the fold
        // is taken from the tree's selection whatever page is in front of it.
        // Pressing it there folds a group nobody can see.
        var state = new AppState { Mode = UiMode.Help, HelpPage = HelpPage.Environment };

        await Assert.That(HelpTree.FoldOver(state, UiMode.Help)).IsNull()
            .Because("the tree is behind another page, so nothing is over a group.");

        await Assert.That(HelpTree.FoldOver(state with { HelpPage = HelpPage.Keys }, UiMode.Help))
            .IsEqualTo(UiMode.Help)
            .Because("and on the page the tree is on, the cursor's group is the answer.");
    }
}
