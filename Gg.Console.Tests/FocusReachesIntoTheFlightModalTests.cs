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
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            if (mode is UiMode.Normal or UiMode.FlightDetail)
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

    [Test]
    public async Task The_containers_between_the_modal_and_its_widgets_can_be_focused()
    {
        // THE RATCHET FOR THE HALF NO TEST CAN REACH. Two plain Views hold the
        // three regions, and a plain View is created with CanFocus false -
        // which silently makes everything beneath it unreachable.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var body = screen[screen.IndexOf("_flightBody = new View", StringComparison.Ordinal)..];
        var fields = screen[screen.IndexOf("_flightFields = new View", StringComparison.Ordinal)..];

        foreach (var (named, declared) in ((string, string)[])
                 [("_flightBody", body[..body.IndexOf("};", StringComparison.Ordinal)]),
                  ("_flightFields", fields[..fields.IndexOf("};", StringComparison.Ordinal)])])
        {
            await Assert.That(declared).Contains("CanFocus = true")
                .Because($"{named} is between the modal and something a person has to reach, "
                       + "and Terminal.Gui will not focus through a container that cannot be.");
            await Assert.That(declared).Contains("TabBehavior.NoStop")
                .Because($"{named} is a container and not a control - tab landing on it would "
                       + "be a stop on nothing.");
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
