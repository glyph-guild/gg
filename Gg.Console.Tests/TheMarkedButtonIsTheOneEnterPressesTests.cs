using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// The button wearing the default marks is the one enter presses.
/// </summary>
/// <remarks>
/// <para>
/// <b>The marks are a promise, and until now they were pointing at the wrong
/// button.</b> A dialog draws <c>&#x25BA; &#x25C4;</c> around one button to say
/// "this is what enter does". Terminal.Gui puts them on the LAST button added
/// and enter presses the FOCUSED one, so on a modal with two answers those are
/// different buttons - and on the runner's modal the marked one was
/// <c>Shut down</c> while enter restarted.
/// </para>
/// <para>
/// <b>Nobody would have been hurt and that is not the point.</b> Enter does the
/// safe thing either way; what is wrong is that the screen says it does the
/// other one, so the person who reads before pressing is the one misled. The
/// same rollout that put a second button on this modal is what made the two
/// disagree, because until then the only modal with two was
/// <c>Editor</c> / <c>Agent</c>, where both answers are the same size.
/// </para>
/// <para>
/// <b>What the view relies on is asserted here, because the view cannot be.</b>
/// A <c>ConsoleScreen</c> needs a terminal, so the three Terminal.Gui behaviours
/// the fix is built on are pinned against real widgets - a version that changes
/// any of them should fail here rather than in somebody's hands - and the line
/// in the view that uses them is held by reading it.
/// </para>
/// </remarks>
public class TheMarkedButtonIsTheOneEnterPressesTests
{
    private static (Dialog Dialog, Button First, Button Second) TwoAnswers()
    {
        var dialog = new Dialog();
        var first = new Button { Text = "Restart" };
        var second = new Button { Text = "Shut down" };

        dialog.AddButton(first);
        dialog.AddButton(second);

        return (dialog, first, second);
    }

    [Test]
    public async Task Adding_a_button_moves_the_marks_onto_it()
    {
        // WHY THE MARKS WERE ON THE WRONG ONE. Nothing in this console asked
        // for that; it is what AddButton does, and the buttons are added in the
        // order the keymap declares its answers - so the marks land on the LAST
        // answer, which is the one furthest from what a person wants by
        // reflex.
        var (_, first, second) = TwoAnswers();

        await Assert.That(first.IsDefault).IsFalse();
        await Assert.That(second.IsDefault).IsTrue()
            .Because("the last button added is the one Terminal.Gui marks.");
    }

    [Test]
    public async Task Marking_one_button_does_not_unmark_the_others()
    {
        // AND WHY THE FIX HAS TO SET IT ON EVERY BUTTON RATHER THAN ONE. Setting
        // the first leaves the second marked too, so a modal would draw two
        // buttons both claiming to be what enter does.
        var (_, first, second) = TwoAnswers();

        first.IsDefault = true;

        await Assert.That(second.IsDefault).IsTrue()
            .Because("marking a button is not a radio button, so the view has to say false "
                   + "for the ones that are not it.");
    }

    [Test]
    public async Task Enter_presses_the_focused_button_and_not_the_marked_one()
    {
        // THE FACT THAT MAKES THE MARKS A LIE RATHER THAN A HAZARD. Enter goes
        // where focus is, so the console has never done the wrong thing here -
        // it has only said it would.
        var (dialog, first, second) = TwoAnswers();

        var pressed = new List<string>();
        first.Accepting += (_, e) => { pressed.Add("first"); e.Handled = true; };
        second.Accepting += (_, e) => { pressed.Add("second"); e.Handled = true; };

        first.SetFocus();
        dialog.NewKeyDownEvent(Key.Enter);

        await Assert.That(pressed).IsEquivalentTo((string[])["first"])
            .Because($"the second button carries the marks (IsDefault={second.IsDefault}), so "
                   + "if enter followed them this would say 'second' - and the fix would have "
                   + "to move focus rather than the marks.");
    }

    [Test]
    public async Task The_view_marks_the_button_it_puts_focus_on()
    {
        // THE LINE THAT USES ALL THREE, held by reading it because a
        // ConsoleScreen cannot be built without a terminal. Focus starts on the
        // first answer - the least consequential one, by the order the keymap
        // declares them - so that is the button the marks belong on, and every
        // other one has to be told it is not.
        var view = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var from = view.IndexOf("private void RenderModalButtons(", StringComparison.Ordinal);
        await Assert.That(from).IsGreaterThan(-1)
            .Because("this scans one method, and a scan that found nothing would pass "
                   + "silently.");

        var method = view[from..view.IndexOf("\n    }", from, StringComparison.Ordinal)];

        await Assert.That(method).Contains("IsDefault", StringComparison.Ordinal)
            .Because("the buttons are drawn with whatever marks AddButton left on them, which "
                   + "is the last answer rather than the one enter presses.");
    }
}
