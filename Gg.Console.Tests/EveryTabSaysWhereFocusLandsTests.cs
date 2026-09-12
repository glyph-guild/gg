using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Every tab names the widget focus lands on when a person arrives, and the
/// default is not a widget on another tab.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE ALLOWANCES CRASH, AND IT WAS NEVER THE TAB BAR'S MEMBERSHIP.</b>
/// Arriving on a tab calls <c>SetFocus</c> on the widget a switch in
/// <c>ConsoleScreen</c> names. Allowances had no arm, so it fell to
/// <c>_ =&gt; _queue</c> — and <c>_queue</c> lives inside the QUEUE tab's pane,
/// which is a different child of the same <c>Tabs</c>.
/// </para>
/// <para>
/// <b>Focusing a sibling tab's widget makes the bar change tabs underneath the
/// focus change.</b> Terminal.Gui's <c>Tabs.OnFocusedChanged</c> looks for
/// whichever tab now has focus and assigns <c>Value</c> to it; that raises
/// <c>ValueChanged</c>, the screen treats it as a person picking a tab,
/// reduces and renders re-entrantly — and the focus transition that started it
/// finds <c>HasFocus</c> moved out from under it:
/// <i>"FocusChanging was not cancelled and the HasFocus value did not
/// change."</i>
/// </para>
/// <para>
/// <b>The quieter footprint is the one that was on screen all along.</b>
/// Pressing <c>v</c> put Allowances on the bar and left <c>ActiveTab</c> at
/// <c>Queue</c> — the re-entrant handler switching the model back. That was
/// visible in a state dump before this was understood, and read as an
/// unrelated oddity.
/// </para>
/// <para>
/// <b>A fallback that answers is worse than one that refuses.</b> The switch
/// had a default, so adding a tab could never fail to compile and could never
/// fail a test; it just quietly aimed focus at another tab's list. This is the
/// fifth follower a new tab needs, and the first four are already written
/// down.
/// </para>
/// </remarks>
public class EveryTabSaysWhereFocusLandsTests
{
    [Test]
    public async Task The_landing_switch_names_every_tab()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var landing = screen[screen.IndexOf(
            "View landing = State.ActiveTab switch", StringComparison.Ordinal)..];

        landing = landing[..landing.IndexOf("landing.SetFocus()", StringComparison.Ordinal)];

        foreach (var tab in Tabs.All)
        {
            await Assert.That(landing).Contains($"TabId.{tab}", StringComparison.Ordinal)
                .Because($"{tab} has to say where focus lands, or it inherits an arm "
                       + "written for a different tab - and the widget that arm names is "
                       + "inside a different tab's pane, which makes the bar switch tabs "
                       + "underneath the focus change and throws.");
        }
    }

    [Test]
    public async Task The_default_arm_refuses_rather_than_guessing()
    {
        // NOT `_ => _queue`. A default that answers means a tab added later
        // compiles, runs, and aims focus at another tab's list - which is
        // exactly what happened, and no test could have caught it because
        // every tab had an answer.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var landing = screen[screen.IndexOf(
            "View landing = State.ActiveTab switch", StringComparison.Ordinal)..];

        landing = landing[..landing.IndexOf("landing.SetFocus()", StringComparison.Ordinal)];

        await Assert.That(landing).DoesNotContain("_ =>", StringComparison.Ordinal)
            .Because("an exhaustive switch makes the compiler ask the question a default "
                   + "answers wrongly and silently.");
    }
}
