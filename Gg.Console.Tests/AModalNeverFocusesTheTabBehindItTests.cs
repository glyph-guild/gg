using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// No arm of the focus switch may fall through to the tab landing beneath it.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: a crash switching tabs on the compose modal's new
/// repositories tab</b> —
/// <c>System.InvalidOperationException: FocusChanging was not cancelled and
/// the HasFocus value did not change.</c>
/// </para>
/// <para>
/// <b><c>ConsoleScreen.Focus()</c> is a switch whose arms place the keyboard,
/// followed by the landing for the TAB BAR.</b> An arm that ends in
/// <c>break</c> rather than <c>return</c> therefore does both: it focuses the
/// modal's widget and then focuses a pane behind the modal. The landing's own
/// comment says what that costs, and it is the exact sentence the owner saw —
/// <i>"focusing a widget inside a SIBLING tab's pane makes Terminal.Gui's Tabs
/// notice that another tab now has focus, assign Value to it and raise
/// ValueChanged; the screen reads that as a person picking a tab, reduces and
/// renders from inside that, and the focus transition that started it comes
/// back to find HasFocus moved."</i>
/// </para>
/// <para>
/// <b>Two arms did it.</b> <c>WorkKindChoices</c> had ended in <c>break</c>
/// since it was written — harmless while it almost never ran, because the
/// modal had one place for the keyboard and the guard above short-circuited
/// it. Giving that modal a second tab made it run on every turn. The other was
/// newer and worse: a <c>LeaveAlone</c> arm that broke out when the focused
/// view had been hidden, which is exactly the moment the repositories tab's
/// table replaces its own absence sentence.
/// </para>
/// <para>
/// <b>Asserted structurally, because the behaviour needs a terminal.</b> This
/// was not reproducible in a pty harness — it wants a real focus transition
/// mid-render — so what is pinned is the invariant rather than the symptom:
/// every arm returns, and the landing below is reachable only by falling off
/// the end of the switch.
/// </para>
/// </remarks>
public class AModalNeverFocusesTheTabBehindItTests
{
    [Test]
    public async Task Every_arm_of_the_focus_switch_returns()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var at = screen.IndexOf("private void Focus()", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1)
            .Because("the method this is about has to exist to be scanned.");

        // THE SWITCH ONLY, which ends where the tab landing begins. That
        // landing is the thing arms must not reach, so it is the boundary.
        var landing = screen.IndexOf("View landing = State.ActiveTab switch", at, StringComparison.Ordinal);

        await Assert.That(landing).IsGreaterThan(at)
            .Because("the tab landing is what this guard is protecting against reaching.");

        var body = screen[at..landing];

        var arms = body.Split("case FocusTarget.").Skip(1).ToList();

        await Assert.That(arms).IsNotEmpty()
            .Because("a scan that found no arms would pass while asserting nothing.");

        var leaking = arms
            .Where(arm =>
            {
                // WHAT THE ARM ENDS WITH, which is the last of the two words
                // to appear in it. An arm may contain a `break` inside a
                // nested construct and still return; what matters is which
                // one it leaves by.
                var returns = arm.LastIndexOf("return;", StringComparison.Ordinal);
                var breaks = arm.LastIndexOf("break;", StringComparison.Ordinal);

                return breaks > returns;
            })
            .Select(arm => arm[..arm.IndexOf(':', StringComparison.Ordinal)])
            .ToList();

        await Assert.That(leaking).IsEmpty()
            .Because("an arm that breaks falls through to the TAB BAR's landing and focuses "
                   + "a pane behind the modal, which throws `FocusChanging was not cancelled "
                   + "and the HasFocus value did not change'. Found: "
                   + string.Join(", ", leaking));
    }
}
