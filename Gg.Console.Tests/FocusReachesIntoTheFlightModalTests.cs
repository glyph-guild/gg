using Gg.Console.Views;

namespace Gg.Console.Tests;

/// <summary>
/// A modal made of widgets is a modal whose widgets can be reached.
/// </summary>
/// <remarks>
/// <para>
/// <b>Terminal.Gui will not focus a view whose SuperView cannot be focused</b> -
/// "SuperView must also have CanFocus set to true", and a plain <c>View</c> is
/// created with it false. The flight modal put its three regions inside two
/// such containers, so focus arrived at the modal frame and stopped there: the
/// intent could not be scrolled, the log's cursor could not be driven, and the
/// read-only fields could not be put a cursor in - which was the entire reason
/// they are fields and not labels.
/// </para>
/// <para>
/// <b>Found by running it.</b> Every test passed, the modal rendered correctly
/// in a capture, and the defect was only visible to somebody pressing a key.
/// The half that can be held here is the decision - where focus should land -
/// and the half that cannot is a source guard, because a <c>ConsoleScreen</c>
/// cannot be constructed without a terminal.
/// </para>
/// <para>
/// <b>It lands on the log.</b> The log is the only part of this modal with a
/// cursor, so it is the only part where a keypress means something a person
/// would predict; the intent is sized to its content and is read without being
/// focused. This is the third time <see cref="FocusChange"/> has been wrong,
/// which is why the answer is a value and not a line in the view.
/// </para>
/// </remarks>
public class FocusReachesIntoTheFlightModalTests
{
    [Test]
    public async Task Opening_a_flight_puts_the_cursor_in_its_log()
    {
        await Assert.That(FocusChange.Wanted(
                UiMode.FlightDetail, TabId.Flights, TabId.Flights, modalHasFocus: false))
            .IsEqualTo(FocusTarget.FlightLog)
            .Because("the log is the only part of this modal with a cursor, so it is the only "
                   + "part where an arrow key does what a person expects.");
    }

    [Test]
    public async Task Every_other_modal_is_still_just_the_modal()
    {
        // THE TWO MADE OF WIDGETS ARE EXEMPT BY NAME, and naming them is the
        // point: a modal that grows parts has to say so here, or it keeps
        // taking focus at its frame and the arrows do nothing inside it. The
        // runner modal joined the flight modal in slice thirty-three.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            if (mode is UiMode.Normal or UiMode.FlightDetail or UiMode.Runner)
            {
                continue;
            }

            await Assert.That(FocusChange.Wanted(
                    mode, TabId.Flights, TabId.Flights, modalHasFocus: false))
                .IsEqualTo(FocusTarget.Modal)
                .Because($"{mode} is a question with two keys and nothing inside it to point "
                       + "at.");
        }
    }

    [Test]
    public async Task And_a_person_who_moved_the_focus_keeps_it()
    {
        // The countdown's lesson, which this must not undo: a render once a
        // second that re-asserted focus would take the intent away from
        // somebody who had just tabbed to it.
        await Assert.That(FocusChange.Wanted(
                UiMode.FlightDetail, TabId.Flights, null, modalHasFocus: true))
            .IsEqualTo(FocusTarget.LeaveAlone);
    }

    /// <summary>
    /// Every container between the modal and a control is focusable, and a stop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two rules, and the second one cost a second attempt.</b> A view is
    /// focusable only if its SuperView is, so the two plain <c>View</c>s needed
    /// <c>CanFocus</c> - that much was in the property's own documentation. It
    /// was not enough: focus advances by asking a view for its DIRECT subviews
    /// whose <c>TabStop</c> MATCHES the behaviour being advanced, so a
    /// container that does not match is never descended into and its children
    /// are not candidates at all.
    /// </para>
    /// <para>
    /// <b>Which made <c>NoStop</c> exactly the wrong answer, and it was the
    /// first one tried</b> - a container is not a control, so not being a tab
    /// stop reads as obviously right. Measured against the library's own
    /// <c>AdvanceFocus</c>: with the containers set to <c>NoStop</c> it would
    /// not leave the control it started on, six calls in a row. <c>FrameView</c>
    /// is created as a <c>TabGroup</c>, which is the same mismatch by default,
    /// so the two region frames are named here too.
    /// </para>
    /// </remarks>
    [Test]
    public async Task The_containers_between_the_modal_and_its_widgets_are_stops()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        foreach (var named in (string[])
                 ["_flightBody = new View", "_flightFields = new View",
                  "_flightIntentPane = new FrameView", "_flightLogPane = new FrameView"])
        {
            var from = screen.IndexOf(named, StringComparison.Ordinal);

            await Assert.That(from).IsGreaterThan(-1).Because($"{named} should exist.");

            var declared = screen[from..];
            declared = declared[..declared.IndexOf("};", StringComparison.Ordinal)];

            await Assert.That(declared).Contains("TabBehavior.TabStop")
                .Because($"{named} is on the path from the modal to a control, and navigation "
                       + "descends only through containers whose TabStop matches. NoStop and "
                       + "TabGroup both leave everything under it unreachable.");
        }

        foreach (var named in (string[]) ["_flightBody = new View", "_flightFields = new View"])
        {
            var from = screen.IndexOf(named, StringComparison.Ordinal);
            var declared = screen[from..];
            declared = declared[..declared.IndexOf("};", StringComparison.Ordinal)];

            await Assert.That(declared).Contains("CanFocus = true")
                .Because($"{named} is a plain View, which is created unfocusable - and "
                       + "Terminal.Gui will not focus through a container that cannot be.");
        }
    }

    [Test]
    public async Task The_view_honours_where_the_focus_was_asked_for()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("FocusTarget.FlightLog")
            .Because("a target the view does not answer is a decision made twice.");
    }
}
