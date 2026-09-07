using Gg.Console.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// A render does not move the focus a person moved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The button could be reached and not held.</b> Up from the top of the
/// runners table focuses `Start a runner here`, and about a second later the
/// focus snapped back to the table - so pressing the button meant hitting a
/// moving target. Reported after one use.
/// </para>
/// <para>
/// <b>It is the automatic refresh, and it was harmless before that existed.</b>
/// <c>Render</c> ends by calling <c>Focus</c>, which puts focus on the active
/// tab's pane. Until the countdown arrived, a render only ever followed a
/// keypress - and the keypress was the thing that had just decided where focus
/// belonged, so re-asserting it changed nothing. The countdown renders once a
/// second whether anybody pressed anything or not, and re-asserting focus then
/// overrules the person.
/// </para>
/// <para>
/// <b>"Focus follows the tab" means when the tab changes.</b> If focus is
/// already somewhere inside the tab on screen, a person put it there. The guard
/// is what the sentence already meant; the code was asserting something
/// stronger and getting away with it because nothing rendered on its own.
/// </para>
/// </remarks>
public class FocusStaysWhereItWasPutTests
{
    private static (FrameView Pane, Button Button, TableView Table) APane()
    {
        var pane = new FrameView { Width = 80, Height = 20 };
        var button = new Button { X = 0, Y = 1, Text = "Start a runner here" };
        var table = CollectionViews.Table();
        table.Y = 2;
        pane.Add(button, table);

        return (pane, button, table);
    }

    [Test]
    public async Task A_pane_knows_when_anything_in_it_has_the_focus()
    {
        // THE ANCHOR, and the fact the guard stands on. It is Terminal.Gui's
        // rather than ours, so a version that changes it should fail here
        // rather than in somebody's hands.
        var (pane, button, table) = APane();

        table.SetFocus();

        await Assert.That(pane.HasFocus).IsTrue();
        await Assert.That(button.HasFocus).IsFalse();

        button.SetFocus();

        await Assert.That(pane.HasFocus).IsTrue()
            .Because("the pane reports focus for the button as readily as for the table, so "
                   + "`is the focus already in this tab' is one question with one answer.");
        await Assert.That(table.HasFocus).IsFalse();
    }

    [Test]
    public async Task And_re_asserting_it_is_what_moved_the_person()
    {
        // THE DEFECT, DEMONSTRATED. This is what Render did once a second: put
        // focus back on the tab's default view, whatever the person had chosen.
        var (_, button, table) = APane();

        button.SetFocus();
        table.SetFocus();

        await Assert.That(button.HasFocus).IsFalse()
            .Because("which is the bug: a second after arrowing up to the button, the "
                   + "countdown's render took it back.");
    }

    /// <summary>The body of <c>ConsoleScreen.Focus</c>, and nothing else.</summary>
    /// <remarks>
    /// Sliced rather than searched, because <c>HasFocus</c> is a common enough
    /// word that finding it somewhere in a nine-hundred-line file would prove
    /// nothing about the method this is a claim about.
    /// </remarks>
    private static string TheFocusMethod()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");
        var opened = screen.IndexOf("private void Focus()", StringComparison.Ordinal);
        var closed = screen.IndexOf("\n    }", opened, StringComparison.Ordinal);

        return opened >= 0 && closed > opened ? screen[opened..closed] : "";
    }

    [Test]
    public async Task The_screen_leaves_a_tab_that_already_has_the_focus_alone()
    {
        var focusing = TheFocusMethod();

        await Assert.That(focusing).IsNotEmpty()
            .Because("the method this is about has to be found before anything is claimed "
                   + "of it - a slice that found nothing would pass every row below.");

        await Assert.That(focusing).Contains("HasFocus")
            .Because("focus follows the tab, which means WHEN THE TAB CHANGES - if it is "
                   + "already inside the tab on screen, a person put it there, and a render "
                   + $"that happens once a second may not overrule them. Found:\n{focusing}");
    }

    [Test]
    public async Task Whether_to_move_the_focus_is_one_decision_and_not_an_arm_per_tab()
    {
        // THIS ASSERTED THE PER-TAB SWITCH WAS GONE, and then passed on a
        // technicality when it came back as a switch EXPRESSION - `case
        // TabId.Runners:` absent, `TabId.Runners =>` present. The claim was
        // false and the test was green, which is the shape this suite exists to
        // catch, arriving in one of its own rows.
        //
        // WHAT IS ACTUALLY TRUE is a smaller and better claim. WHETHER to move
        // focus is one decision for every tab, and it is pure - FocusChange has
        // been wrong twice and both times only a person pressing a key found
        // out. WHERE focus lands inside a pane is per-tab and legitimately so:
        // a table takes it when it has rows and the label beside it when it
        // does not, which is a real difference between tabs rather than drift.
        var focusing = TheFocusMethod();

        await Assert.That(focusing).Contains("FocusChange.Wanted")
            .Because("one decision, asked once, where a test can reach it.");

        await Assert.That(focusing.Split("=>").Length - 1).IsGreaterThan(1)
            .Because("and the landing stays per-tab, because which view inside a pane should "
                   + "take the focus differs by tab. Naming that drift would be the wrong "
                   + "lesson from the right bug.");
    }
}
