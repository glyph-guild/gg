using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Watching is a modal a person opens over the flight, not a tab the console
/// sends them to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The live view was a tab, and the watch closed the modal to reach it.</b>
/// <c>ConsoleWatchRunner</c> said so outright - "the thing it was asked from is
/// now happening behind it and the pane it happens in is another tab" - so
/// asking to watch took away the flight a person was reading to show them a
/// pane about it. Two moves away from what they were looking at, to see more
/// of what they were looking at.
/// </para>
/// <para>
/// <b>Which is also what the rule wanted.</b> The live pane is "a trust
/// artifact meant to decay": off by default, reached when somebody wants it.
/// A permanent seat on the tab bar is the opposite of decaying, and a flag
/// that defaults to false is a weaker way of saying it than a modal nobody
/// opens.
/// </para>
/// <para>
/// <b>It is the first modal that closes into another one.</b> Every other one
/// returns to <c>Normal</c>, because every other one was opened from there.
/// This one has two parents - the flight and the runner - so what it returns
/// to is recorded when it opens rather than assumed.
/// </para>
/// </remarks>
public class AWatchIsAModalOverTheFlightTests
{
    private static readonly DateTimeOffset At =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static FlightSummary AFlight(string id, string number) => new()
    {
        FlightId = id,
        FlightNumber = number,
        Name = "the one being watched",
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "do it" },
        CreatedAt = At,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "none",
        Attempts = 1,
        Facts = [],
    };

    private static StreamLine Said(string text) =>
        new() { Kind = StreamLineKind.Text, Text = text, At = At };

    private static AppState Reading(string id = "flight-1", string number = "GG-1") =>
        new()
        {
            Mode = UiMode.FlightDetail,
            Flights = new FlightList { Flights = [AFlight(id, number)] },
            FlightSelected = 0,
        };

    // ---- the key ----

    [Test]
    public async Task Pressing_w_over_a_flight_asks_to_watch_it()
    {
        // w WAS FREE HERE and means watch one level up already: the runner
        // modal has had it since watching existed. The flight modal is where a
        // person is when they want to see what is happening to this flight, and
        // it answered nothing at all.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('w'), KeymapContext.For(Reading())))
            .IsEqualTo(Command.WatchThisFlight);
    }

    [Test]
    public async Task And_the_watch_is_drawn_as_a_dialog()
    {
        await Assert.That(Modals.IsDrawn(UiMode.Watching)).IsTrue()
            .Because("it is a frame with a title over the flight that opened it, which is "
                   + "what Drawn means.");
    }

    // ---- what it opens ----

    [Test]
    public async Task Watching_names_the_flight_rather_than_following_the_cursor()
    {
        var watching = Reducer.Reduce(Reading("flight-84"), Command.WatchThisFlight);

        await Assert.That(watching.Mode).IsEqualTo(UiMode.Watching);

        // THE FIELD THAT HAD NO WRITER. WatchedFlightId was declared, read by
        // LiveTails and set to null by the runner watch, and nothing in the
        // product ever gave it a value - so the pane could only ever follow the
        // queue cursor. This is the way in it was waiting for.
        await Assert.That(watching.WatchedFlightId).IsEqualTo("flight-84")
            .Because("a person watching a flight from its own modal has named it, and the "
                   + "queue cursor is somewhere else entirely.");

        await Assert.That(watching.LiveVisible).IsTrue()
            .Because("LiveTails reads nothing while this is false, so the modal would open "
                   + "over a buffer nobody is filling.");
    }

    [Test]
    public async Task And_starts_from_an_empty_buffer()
    {
        // WHAT WAS THERE BELONGED TO SOMETHING ELSE. Lines left from a previous
        // watch would sit above this one's, unlabelled and older, reading as
        // this flight's first words.
        var before = Reading() with
        {
            Live = [Said("from the last thing watched")],
        };

        await Assert.That(Reducer.Reduce(before, Command.WatchThisFlight).Live).IsEmpty();
    }

    // ---- the way back ----

    [Test]
    public async Task Escape_goes_back_to_the_flight_it_was_opened_from()
    {
        var watching = Reducer.Reduce(Reading(), Command.WatchThisFlight);

        await Assert.That(watching.ModeBeneath).IsEqualTo(UiMode.FlightDetail)
            .Because("what it returns to is recorded when it opens: this modal has two "
                   + "parents and cannot infer which one it has.");

        var closed = Reducer.Reduce(watching, Command.CloseModal);

        // EVERY OTHER MODAL LANDS ON Normal, and that is right for every other
        // modal: they were opened from there. Landing on Normal here would
        // close the flight somebody was reading as the price of having looked
        // at it.
        await Assert.That(closed.Mode).IsEqualTo(UiMode.FlightDetail);

        await Assert.That(closed.LiveVisible).IsFalse()
            .Because("the tail is read on a timer while this is true, and nobody is "
                   + "watching a modal they have closed.");
    }

    [Test]
    public async Task And_the_flight_modal_still_closes_to_normal()
    {
        // THE ORDINARY WAY OUT IS UNCHANGED. Two escapes from a watch leave the
        // console where one escape from the flight always left it.
        var watching = Reducer.Reduce(Reading(), Command.WatchThisFlight);
        var flight = Reducer.Reduce(watching, Command.CloseModal);

        await Assert.That(Reducer.Reduce(flight, Command.CloseModal).Mode)
            .IsEqualTo(UiMode.Normal);
    }

    // ---- what it offers ----

    [Test]
    public async Task The_words_can_be_taken_and_the_modal_can_be_left()
    {
        var watching = Reducer.Reduce(Reading(), Command.WatchThisFlight);
        var context = KeymapContext.For(watching);

        // COPY IS THE POINT OF THE WHOLE THING as often as watching is: what a
        // runner said is what somebody pastes into a message about why it went
        // wrong. The command, the key and the platform table all existed.
        await Assert.That(Keymap.Resolve(KeyStroke.Char('c'), context))
            .IsEqualTo(Command.CopyModal);

        await Assert.That(Keymap.Resolve(KeyStroke.Esc, context))
            .IsEqualTo(Command.CloseModal);
    }

    [Test]
    public async Task And_none_of_the_keys_underneath_it_answer()
    {
        // THE DEFAULT ARM OF Bindings IS NORMAL'S WHOLE KEYSET, so a mode with
        // no arm of its own is not a modal at all - it is the main screen with
        // a frame drawn over it, answering fly, ground and take-over to
        // somebody who thinks they are reading output.
        var context = KeymapContext.For(Reducer.Reduce(Reading(), Command.WatchThisFlight));

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), context)).IsNull();
        await Assert.That(Keymap.Resolve(KeyStroke.Char('x'), context)).IsNull();
    }

    // ---- what it shows ----

    [Test]
    public async Task The_modal_shows_what_the_runner_said()
    {
        var watching = Reducer.Reduce(Reading(), Command.WatchThisFlight) with
        {
            Live = [Said("cloning the repository")],
        };

        // THE SAME PRODUCER THE PANE USED. PaneText.Modal is also what
        // CopyModal copies, so the words taken are the words shown rather than
        // a second rendering that could differ from it.
        await Assert.That(PaneText.Modal(watching)).Contains("cloning the repository");
    }

    [Test]
    public async Task And_says_which_flight_it_is_watching()
    {
        var watching = Reducer.Reduce(Reading("flight-1", "GG-4211"), Command.WatchThisFlight);

        // BY NUMBER. Nothing a person reads in this product is named by a guid,
        // and a title saying only "watching" over a full screen of output is a
        // title that could be about anything.
        await Assert.That(PaneText.ModalTitle(watching)).Contains("GG-4211");
    }

    [Test]
    public async Task And_takes_the_whole_screen()
    {
        // BIGGER THAN THE MODAL UNDERNEATH IT, which is already 92 by 88. Two
        // near-full boxes one over the other read as one box redrawn, and the
        // thing this is for - a long tail of output - is the one modal with a
        // real use for the last eight per cent.
        await Assert.That(PaneText.ModalIsFullScreen(UiMode.Watching)).IsTrue();

        await Assert.That(PaneText.ModalIsFullScreen(UiMode.FlightDetail)).IsFalse()
            .Because("it is a document, which is 92 by 88, and it is what this opens over.");
    }
}
