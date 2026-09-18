using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// While a modal is open the mouse belongs to the terminal, not to gg.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported from the live console, and it ended the process.</b> Clicking
/// <c>repositories</c> in the compose modal threw
/// <c>InvalidOperationException: FocusChanging was not cancelled and the
/// HasFocus value did not change</c> out of Terminal.Gui's own title command:
/// a tab that is not the selected one has a hidden body, and the library
/// throws rather than declining when asked to focus what cannot take it.
/// Reproduced at the exact cell in a pty, and the same click on the console's
/// own tab bar is fine - so it is modals, where a click has no model behind it.
/// </para>
/// <para>
/// <b>Three narrower fixes were tried and none reaches it.</b> Taking the mouse
/// on the tab bar does not fire, because a click lands on the deepest view
/// rather than the bar; the title view inside the border is not reachable from
/// this version's API; and clearing the border's Title setting leaves the
/// command bound. What is left is to stop the events arriving.
/// </para>
/// <para>
/// <b>Which the console already knows how to do, and says why.</b>
/// <c>TerminalMouse</c> exists because a frozen screen must let a terminal draw
/// its own selection; a modal is the same situation for the same reason. The
/// rule was always "a modal owns the keyboard" - this makes it own the mouse,
/// which is what a person means by it.
/// </para>
/// <para>
/// <b>What it costs, stated.</b> A modal's buttons stop being clickable. Every
/// one of them is a key and is labelled with it, and in exchange the text in a
/// modal - which is usually a refusal somebody has to paste somewhere - can be
/// selected with the terminal, which is the same thing freezing was built for.
/// </para>
/// </remarks>
public class AModalOwnsTheMouseTooTests
{
    [Test]
    public async Task The_console_holds_the_mouse_while_nothing_is_open()
    {
        await Assert.That(ConsoleMouse.OursWhile(new AppState())).IsTrue()
            .Because("clicking a row, a tab or a pane is how half the console is used, and "
                   + "none of that goes near the path that throws.");
    }

    [Test]
    public async Task And_hands_it_over_for_every_modal()
    {
        foreach (var mode in Modals.Drawn)
        {
            await Assert.That(ConsoleMouse.OursWhile(new AppState { Mode = mode })).IsFalse()
                .Because($"{mode} draws over what is behind it, and a click that lands on "
                       + "what it covers asks the library to focus a hidden view.");
        }
    }

    [Test]
    public async Task A_field_that_owns_the_keyboard_keeps_the_mouse_here()
    {
        // THE MODES THAT ARE NOT DIALOGS. Nothing is drawn over anything, so a
        // click reaches what a person can see - and the airspace path is one
        // somebody pastes into, which is a mouse act on some terminals.
        foreach (var mode in Modals.NotDrawn.Keys)
        {
            await Assert.That(ConsoleMouse.OursWhile(new AppState { Mode = mode })).IsTrue();
        }
    }

    [Test]
    public async Task And_a_frozen_screen_still_hands_it_over()
    {
        // FREEZING ALREADY DID THIS, and it has to keep doing it whichever mode
        // it froze in: the pixels have stopped, so a selection is the only
        // thing the mouse is for.
        await Assert.That(ConsoleMouse.OursWhile(new AppState { Frozen = true })).IsFalse();

        await Assert.That(ConsoleMouse.OursWhile(
            new AppState { Frozen = true, Mode = UiMode.Help })).IsFalse();
    }
}
