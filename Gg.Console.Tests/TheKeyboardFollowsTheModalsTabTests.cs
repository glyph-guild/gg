using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// The two modals that grew tabs move the keyboard with the tab.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE, as a crash: "sometimes when switching between tabs the
/// app crashes" —</b>
/// <c>System.InvalidOperationException: FocusChanging was not cancelled and the
/// HasFocus value did not change.</c> That sentence is already written down in
/// this console, in <c>ConsoleScreen</c>'s tab landing: <i>"focusing a widget
/// inside a SIBLING tab's pane makes Terminal.Gui's Tabs notice that another
/// tab now has focus, assign Value to it and raise ValueChanged; the screen
/// reads that as a person picking a tab, reduces and renders from inside that,
/// and the focus transition that started it comes back to find HasFocus
/// moved."</i>
/// </para>
/// <para>
/// <b>What changed under it is where the two landing widgets live.</b> The
/// flight log used to be the third region of the DETAILS tab —
/// <c>_flightDetailsTab.Add(_flightIntentPane, _flightFields, _flightLogPane)</c>
/// — so naming it unconditionally named something in the tab that was showing.
/// It is its own tab now, and the work item's history moved the same way in the
/// same evening. <see cref="FocusChange"/> was not touched by either commit, so
/// both modals open on their first tab and immediately place the keyboard in
/// their last.
/// </para>
/// <para>
/// <b>This is the third and fourth time, and the answer is the one already
/// here.</b> The runner modal's <c>RunnerView</c> target carries it: <i>"Was
/// RunnerLog, and the rename is the defect. The modal has three views now, and
/// a landing that always named the log made the other two unreachable."</i>
/// Help and the filter modal each needed it after that. A modal made of tabs
/// names the tab, and carries the pair that says the tab TURNED — because
/// re-placing focus every render drags it out of whatever a person just
/// clicked into, once a second.
/// </para>
/// </remarks>
public class TheKeyboardFollowsTheModalsTabTests
{
    [Test]
    public async Task Turning_the_flight_modals_tab_moves_the_keyboard_into_it()
    {
        // THE MODAL ALREADY HAS FOCUS, which is exactly when a person presses
        // `v' - and which the arm below this one answered LeaveAlone for.
        var wanted = FocusChange.Wanted(
            UiMode.FlightDetail, TabId.Flights, landed: null, modalHasFocus: true,
            flightTab: FlightTab.Log, landedFlightTab: FlightTab.Details);

        await Assert.That(wanted).IsEqualTo(FocusTarget.FlightTab)
            .Because("the tab turned, so the keyboard has to follow it - and it is the "
                   + "keyboard that decides which tab the bar shows.");
    }

    [Test]
    public async Task A_flight_tab_that_did_not_turn_is_left_alone()
    {
        var wanted = FocusChange.Wanted(
            UiMode.FlightDetail, TabId.Flights, landed: null, modalHasFocus: true,
            flightTab: FlightTab.Log, landedFlightTab: FlightTab.Log);

        await Assert.That(wanted).IsEqualTo(FocusTarget.LeaveAlone)
            .Because("re-placing focus on every render would take the log's cursor back off "
                   + "whatever a person had just scrolled to, once a second.");
    }

    [Test]
    public async Task Opening_a_flight_lands_in_the_tab_it_opens_on()
    {
        // AND THE PAIR MATCHING MUST NOT READ AS `already landed', which is
        // what makes this its own case rather than a consequence of the two
        // above: a modal that has just opened has focus nowhere yet.
        var wanted = FocusChange.Wanted(
            UiMode.FlightDetail, TabId.Flights, landed: null, modalHasFocus: false,
            flightTab: FlightTab.Details, landedFlightTab: FlightTab.Details);

        await Assert.That(wanted).IsEqualTo(FocusTarget.FlightTab);
    }

    [Test]
    public async Task Turning_the_work_item_modals_tab_moves_the_keyboard_into_it()
    {
        // THE SAME DEFECT ONE MODAL OVER, and it arrived the same evening: the
        // history became a tab and the landing still named the history.
        var wanted = FocusChange.Wanted(
            UiMode.WorkItemDetail, TabId.Browse, landed: null, modalHasFocus: true,
            workItemTab: WorkItemTab.History, landedWorkItemTab: WorkItemTab.Details);

        await Assert.That(wanted).IsEqualTo(FocusTarget.WorkItemTab);
    }

    [Test]
    public async Task A_work_item_tab_that_did_not_turn_is_left_alone()
    {
        var wanted = FocusChange.Wanted(
            UiMode.WorkItemDetail, TabId.Browse, landed: null, modalHasFocus: true,
            workItemTab: WorkItemTab.History, landedWorkItemTab: WorkItemTab.History);

        await Assert.That(wanted).IsEqualTo(FocusTarget.LeaveAlone);
    }

    [Test]
    public async Task Opening_a_work_item_lands_in_the_tab_it_opens_on()
    {
        var wanted = FocusChange.Wanted(
            UiMode.WorkItemDetail, TabId.Browse, landed: null, modalHasFocus: false,
            workItemTab: WorkItemTab.Details, landedWorkItemTab: WorkItemTab.Details);

        await Assert.That(wanted).IsEqualTo(FocusTarget.WorkItemTab);
    }

    [Test]
    public async Task Each_landing_focuses_the_tab_that_is_showing()
    {
        // ASSERTED AGAINST THE SOURCE, because ConsoleScreen cannot be
        // constructed without a terminal - TheRunnerViewStaysWhereItWasPut's
        // technique, and the reason it has one: the decision is pure and
        // reachable, the wiring is neither, and the wiring is where this went
        // wrong both times.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        foreach (var (target, asked) in ((string Target, string Asked)[])
                 [("FocusTarget.FlightTab", "State.FlightTab"),
                  ("FocusTarget.WorkItemTab", "State.WorkItemTab")])
        {
            var at = screen.IndexOf($"case {target}:", StringComparison.Ordinal);

            await Assert.That(at).IsGreaterThan(-1)
                .Because($"{target} has to have an arm at all, or the decision is made and "
                       + "thrown away.");

            var arm = screen[at..screen.IndexOf("return;", at, StringComparison.Ordinal)];

            await Assert.That(arm).Contains(asked, StringComparison.Ordinal)
                .Because("naming one tab's widget unconditionally is the defect: the bar "
                       + "follows the focused pane, so a landing in a sibling tab drags the "
                       + "bar back, reduces and renders from inside the focus transition, "
                       + "and throws `FocusChanging was not cancelled'. Arm:\n" + arm);
        }
    }

    [Test]
    public async Task The_tab_each_landed_on_is_remembered()
    {
        // WITHOUT THE FIELDS THE PAIR CANNOT BE COMPARED, and the comparison
        // is what makes this a CHANGE rather than a standing instruction.
        // _landedRunnerView is the same field one modal over.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_landedFlightTab", StringComparison.Ordinal);
        await Assert.That(screen).Contains("_landedWorkItemTab", StringComparison.Ordinal);
    }
}
