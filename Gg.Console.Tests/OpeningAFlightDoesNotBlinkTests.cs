using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A read does not take the screen away and give it back.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole screen flickers on the way into a flight's detail.</b>
/// `ShowFlight` is declared as the shell's, so the session ends, Terminal.Gui
/// is disposed, the alternate screen is left, one HTTP request is made, and a
/// new session is built from the model. That teardown is right for handing the
/// terminal to <c>$EDITOR</c> and it is a blink for fetching a log.
/// </para>
/// <para>
/// <b>The argument for not doing it was already made and won.</b>
/// <c>AutoRefresh</c> is "THE SECOND EXCEPTION TO 'A UI SESSION MAY NOT READ',
/// and the argument for it is that the session still does not": the request
/// runs on a task owned outside every UI lifetime and the tick asks only
/// whether it has finished. What the rule protects is a session that BLOCKS,
/// and nothing here blocks.
/// </para>
/// <para>
/// <b>Which answers the objection the old comment raised.</b> It said opening
/// before the read "would show 'no log fetched' and then never come back to
/// correct itself". With a fold it does come back — and the model already
/// tells the two apart, because <c>FlightLog</c> and <c>Story</c> are nullable:
/// absent is "not fetched", and fetched-and-empty is a value.
/// </para>
/// </remarks>
public class OpeningAFlightDoesNotBlinkTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 9, 4, 0, 0, TimeSpan.Zero);

    private static AppState Looking() => new()
    {
        ActiveTab = TabId.Flights,
        FlightSelected = 0,
        Flights = new FlightList
        {
            Flights =
            [
                new FlightSummary
                {
                    FlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8",
                    FlightNumber = "GG-81",
                    Name = "the one on the screen",
                    Intent = new FlightIntent
                    {
                        Kind = FlightIntentKinds.Text, Text = "count to forty",
                    },
                    CreatedAt = T0,
                    RunnerProtocolVersion = 1,
                    FactVocabularyVersion = "0.25.0",
                    ConstitutionVersion = "1.0.0",
                    EnvelopeVersion = "v6",
                    Attempts = 1,
                    Facts = [],
                },
            ],
        },
    };

    [Test]
    public async Task Opening_a_flight_is_not_the_shell_s()
    {
        // THE FLICKER, AS A DECLARATION. Everything in this set ends the
        // session; for a child that is the point and for a read it is a blink.
        await Assert.That(ShellCommands.Handled.Contains(Command.ShowFlight)).IsFalse()
            .Because("the session ends for everything declared here, and disposing "
                   + "Terminal.Gui to make one HTTP request is a screen taken away and given "
                   + "back.");
    }

    [Test]
    public async Task The_modal_opens_on_what_is_already_known()
    {
        // IMMEDIATELY, on the keypress, from the model. The flight summary is
        // already in hand - the boot fetched the list - so the intent, the
        // number and the name are all there to draw before anything is asked.
        var after = Reducer.Reduce(Looking(), Command.ShowFlight);

        await Assert.That(after.Mode).IsEqualTo(UiMode.FlightDetail);
        await Assert.That(PaneText.Detailed(after)?.FlightNumber).IsEqualTo("GG-81");
    }

    [Test]
    public async Task And_says_the_log_is_still_coming_rather_than_that_there_is_none()
    {
        // THE OBJECTION THE OLD COMMENT RAISED. "No log fetched" as a statement
        // of fact, over a read that is in the air, is the same defect as a live
        // pane that cannot tell an ended watch from a silent agent - and the
        // model already tells them apart, because the log is nullable.
        var after = Reducer.Reduce(Looking(), Command.ShowFlight);

        await Assert.That(after.ReadInFlight).IsTrue()
            .Because("the reducer opened the modal and asked for a read; nothing else knows "
                   + "one is coming.");

        await Assert.That(after.FlightLog).IsNull()
            .Because("nothing has been read yet, and a value here would be a claim.");

        var absence = FlightDetails.LogAbsence(after);

        await Assert.That(absence).Contains("still")
            .Because("\"No story was fetched for this flight\" is a statement of fact and is "
                   + "false while one is being fetched - the same defect as a live pane that "
                   + "cannot tell an ended watch from a silent agent. Said: " + absence);
    }

    [Test]
    public async Task What_the_read_brings_back_is_folded_onto_what_is_on_screen()
    {
        // A PATCH, NOT A MODEL. AutoRefresh's argument, for the same reason: a
        // read answering with a whole AppState is a snapshot taken before the
        // person moved and applied after.
        var reads = new BackgroundReads((_, _) => Task.FromResult<Func<AppState, AppState>>(
            state => state with { FlightLog = new FlightLog { FlightId = "01a08431-a096-72cf-8c8f-55ed2233f2f8", FlightNumber = "GG-81", Entries = [] } }));

        var opened = Reducer.Reduce(Looking(), Command.ShowFlight);

        // TOLD, NOT POLLED - which is what the screen does too. Terminal.Gui's
        // guidance is that a background result reaches the main thread through
        // Invoke, so the read says when it has landed rather than being asked
        // on a timer that is up to its own interval late.
        var landed = new TaskCompletionSource();

        reads.Start(Command.ShowFlight, opened, landed.SetResult);

        await landed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var folded = reads.Advance(opened);

        await Assert.That(folded.FlightLog).IsNotNull()
            .Because("the tick asks only whether it has finished, and folds it when it has.");
        await Assert.That(folded.Mode).IsEqualTo(UiMode.FlightDetail)
            .Because("and folds onto what is on screen, rather than replacing it with a "
                   + "snapshot taken before the modal opened.");
    }
}
