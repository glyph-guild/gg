using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// Turning the runner modal's view moves the keyboard with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: it turns to environments and snaps back to the log
/// about a second later.</b> The model was measured and was right — a state
/// dump after pressing <c>v</c> and waiting says <c>RunnerView:
/// Environments</c> — so what moved was the widget.
/// </para>
/// <para>
/// <b>Terminal.Gui's <c>Tabs</c> follows FOCUS.</b> Assigning <c>Value</c>
/// focuses the pane, and anything that focuses a child of another pane assigns
/// <c>Value</c> back. Focus was placed on the log's list when the modal opened
/// and nothing moved it afterwards, because
/// <see cref="FocusChange.Wanted"/>'s modal arm answers <c>LeaveAlone</c> once
/// the modal has focus at all. So every render set the bar to the showing view
/// and the still-focused log dragged it back.
/// </para>
/// <para>
/// <b>The fix is the pair <c>landedReading</c> already is for the airspace
/// tab</b> — that parameter's own remark says it: <i>"the pair is what makes
/// this a CHANGE rather than a standing instruction"</i>. Focus is re-placed
/// when the view TURNED, and left alone when it did not.
/// </para>
/// </remarks>
public class TheRunnerViewStaysWhereItWasPutTests
{
    [Test]
    public async Task Turning_the_view_moves_the_keyboard_into_it()
    {
        // THE MODAL ALREADY HAS FOCUS, which is the case that was answering
        // LeaveAlone and is exactly when a person presses `v'.
        var wanted = FocusChange.Wanted(
            UiMode.Runner, TabId.Runners, landed: null, modalHasFocus: true,
            runnerView: RunnerView.Environments, landedRunnerView: RunnerView.Log);

        await Assert.That(wanted).IsEqualTo(FocusTarget.RunnerView)
            .Because("the view turned, so the keyboard has to follow it - and it is the "
                   + "keyboard that decides which tab the bar shows.");
    }

    [Test]
    public async Task A_view_that_did_not_turn_is_left_alone()
    {
        // THE OTHER HALF, AND IT IS WHY THE PAIR EXISTS. Re-placing focus on
        // every render would drag it back out of whatever a person had just
        // clicked into, once a second - the defect landedReading was added to
        // avoid one modal over.
        var wanted = FocusChange.Wanted(
            UiMode.Runner, TabId.Runners, landed: null, modalHasFocus: true,
            runnerView: RunnerView.Environments, landedRunnerView: RunnerView.Environments);

        await Assert.That(wanted).IsEqualTo(FocusTarget.LeaveAlone);
    }

    [Test]
    public async Task A_modal_that_has_not_been_landed_on_yet_is_landed_on()
    {
        var wanted = FocusChange.Wanted(
            UiMode.Runner, TabId.Runners, landed: null, modalHasFocus: false,
            runnerView: RunnerView.Log, landedRunnerView: RunnerView.Log);

        await Assert.That(wanted).IsEqualTo(FocusTarget.RunnerView)
            .Because("opening the modal has to put the keyboard somewhere, and the pair "
                   + "matching must not be read as `already landed'.");
    }

    [Test]
    public async Task The_landing_focuses_the_view_that_is_showing()
    {
        // ASSERTED AGAINST THE SOURCE, because ConsoleScreen cannot be
        // constructed without a terminal - EveryTabSaysWhereFocusLandsTests'
        // technique, and the reason it exists: the defect this class is about
        // was invisible to every test in this suite and obvious in one glance
        // at a screen.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var at = screen.IndexOf("case FocusTarget.RunnerView:", StringComparison.Ordinal);

        await Assert.That(at).IsGreaterThan(-1)
            .Because("the runner modal's landing has to have an arm at all.");

        var arm = screen[at..screen.IndexOf("return;", at, StringComparison.Ordinal)];

        await Assert.That(arm).Contains("State.RunnerView", StringComparison.Ordinal)
            .Because("focusing the log unconditionally is the defect: the bar follows the "
                   + "focused pane, so a landing that always names the log makes the other "
                   + "two views unreachable. Arm:\n" + arm);
    }

    [Test]
    public async Task The_view_it_landed_on_is_remembered()
    {
        // WITHOUT THE FIELD THE PAIR CANNOT BE COMPARED, and the comparison is
        // the whole fix. _landedReading is the same field one modal over.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_landedRunnerView", StringComparison.Ordinal);
    }
}
