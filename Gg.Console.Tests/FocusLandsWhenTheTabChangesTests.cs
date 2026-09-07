using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// Focus is placed when the tab changes, and left alone every other time.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first guard was too broad and broke the thing it was protecting.</b>
/// "Leave the tab alone when it already has the focus" stops a once-a-second
/// render from overruling a person - and it also stops the landing that happens
/// when a person switches tabs, because a pane reports <c>HasFocus</c> the
/// moment it is shown. So arriving on the runners tab focused nothing in
/// particular and Terminal.Gui picked the first focusable child, which is the
/// button rather than the table.
/// </para>
/// <para>
/// <b>The question is not who has the focus, it is whether the tab is new.</b>
/// Both readings agree on the render case and disagree on the one that matters,
/// which is why the first one passed its own tests: nothing there switched
/// tabs.
/// </para>
/// <para>
/// <b>Pure, because the screen cannot be constructed.</b> The last version of
/// this decision lived inside <c>ConsoleScreen</c> and could only be asserted
/// by reading the file for a word - which is how a guard that read correctly
/// shipped a defect a person found in one press.
/// </para>
/// </remarks>
public class FocusLandsWhenTheTabChangesTests
{
    [Test]
    public async Task Arriving_on_a_tab_puts_the_focus_somewhere()
    {
        await Assert.That(FocusChange.Wanted(UiMode.Normal, TabId.Runners, landed: null, modalHasFocus: false))
            .IsEqualTo(FocusTarget.Tab)
            .Because("nothing has been focused yet, so something must be - this is the case "
                   + "the first guard skipped, and it is every first render.");

        await Assert.That(FocusChange.Wanted(UiMode.Normal, TabId.Runners, TabId.Flights, false))
            .IsEqualTo(FocusTarget.Tab)
            .Because("the tab changed, which is what `focus follows the tab' means.");
    }

    [Test]
    public async Task And_every_render_after_that_leaves_it_alone()
    {
        await Assert.That(FocusChange.Wanted(UiMode.Normal, TabId.Runners, TabId.Runners, false))
            .IsEqualTo(FocusTarget.LeaveAlone)
            .Because("a render once a second may not overrule a person who arrowed onto the "
                   + "button - which is the defect this decision exists to prevent, and the "
                   + "half the first version got right.");
    }

    [Test]
    public async Task A_modal_takes_the_focus_once_and_then_keeps_it()
    {
        await Assert.That(FocusChange.Wanted(UiMode.FlightDetail, TabId.Runners, TabId.Runners, false))
            .IsEqualTo(FocusTarget.Modal)
            .Because("a modal owns the keyboard, so it has to hold the focus too.");

        await Assert.That(FocusChange.Wanted(UiMode.FlightDetail, TabId.Runners, TabId.Runners, true))
            .IsEqualTo(FocusTarget.LeaveAlone)
            .Because("and once it has it, re-asserting it every second would move a cursor "
                   + "inside the modal exactly as it moved one behind it.");
    }

    [Test]
    public async Task Closing_a_modal_lands_the_focus_again()
    {
        // THE CASE A `LAST FOCUSED TAB' GETS WRONG ON ITS OWN. While the modal
        // was open the tab did not change, so a decision keyed only on that
        // would leave focus on a modal that is no longer on the screen - a
        // keyboard that appears frozen, which is the symptom this console has
        // twice been reported for.
        //
        // Recording the landing as null while a modal holds it is what makes
        // closing one a change again.
        await Assert.That(FocusChange.Wanted(UiMode.Normal, TabId.Runners, landed: null, modalHasFocus: false))
            .IsEqualTo(FocusTarget.Tab);
    }

    [Test]
    public async Task The_screen_asks_rather_than_deciding()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("FocusChange.Wanted")
            .Because("the last version of this decision was three lines inside a class no "
                   + "test can construct, and it read correctly while being wrong.");
    }
}
