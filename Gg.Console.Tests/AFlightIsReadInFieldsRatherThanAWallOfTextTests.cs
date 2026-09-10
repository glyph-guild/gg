using Gg.Contracts.Description;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The flight modal is widgets over the model, not one string of preformatted
/// text.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything a person opened this to read was in one <c>Label</c>.</b> The
/// flight's identity, its scalars, the reason it cannot start and its whole
/// history were composed into a single string with two-space indents doing the
/// work of columns — so nothing could be selected, nothing could be copied, a
/// history longer than the box scrolled the whole document, and the intent, the
/// one part that can run to paragraphs, was not shown at all.
/// </para>
/// <para>
/// <b>What replaces it is four producers, and they stay pure.</b> The title, the
/// intent, the fields and the log rows are functions of the model that a test
/// can read without a terminal, exactly as <c>PaneText</c> and <c>Rows</c>
/// already are. The view binds them to a frame title, a <c>Markdown</c> view,
/// read-only <c>TextField</c>s and a <c>TableView</c>; which widget is a
/// judgement about the content and stays in the view, and the last test here is
/// what stops the two drifting.
/// </para>
/// </remarks>
public class AFlightIsReadInFieldsRatherThanAWallOfTextTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An intent longer than any box, because that is the case that decided this.
    /// </summary>
    /// <remarks>
    /// <c>gg fly</c> takes whatever a person wrote, and what a person writes to
    /// open a flight is a paragraph often enough that a renderer which assumed a
    /// line was wrong about the common case.
    /// </remarks>
    private const string LongIntent =
        "The login form drops focus after a failed attempt.\n"
      + "\n"
      + "Steps:\n"
      + "\n"
      + "1. Sign in with a password that is wrong.\n"
      + "2. The banner appears and the cursor is gone.\n"
      + "\n"
      + "It should land back in the password box.";

    private static StoryEntry[] Happenings() =>
    [
        new StoryEntry
        {
            At = At,
            Kind = StoryKinds.LeaseGranted,
            Stage = FlightStages.Of(StoryKinds.LeaseGranted),
            Params = ["a-runner"],
            Attempt = 2,
            Actor = new Actor { Kind = ActorKinds.Runner, Name = "a-runner" },
        },
        new StoryEntry
        {
            At = At.AddMinutes(20),
            Kind = StoryKinds.ObligationHalted,
            Stage = FlightStages.Of(StoryKinds.ObligationHalted),
            Params = [],
            Said = "the move bound could not be measured\nand nothing said what it should be",
        },
    ];

    private static FlightStory Story()
    {
        var entries = Happenings();

        return new FlightStory
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            WorkKind = "audit",
            Stage = FlightStoryStages.Reached(entries),
            State = FlightStates.Open,
            Waiting = Reason.For(ReasonKinds.NoRunnerAdvertises, ["linux-x64"]),
            HeldBy = new Actor { Kind = ActorKinds.Person, Name = "kevin" },
            HeldUntil = At.AddMinutes(30),
            Outstanding = [entries[1]],
            Entries = entries,
        };
    }

    private static FlightSummary Listed(FlightIntent intent) => new()
    {
        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        FlightNumber = FlightRef.Format(42),
        Name = "the login form loses focus",
        Intent = intent,
        CreatedAt = At,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 2,
        State = FlightStates.Open,
        Facts = [],
    };

    private static FlightIntent Typed() => new()
    {
        Kind = FlightIntentKinds.Text,
        Text = LongIntent,
    };

    private static AppState Opened(FlightIntent? intent = null, bool fetched = true) => new()
    {
        Mode = UiMode.FlightDetail,
        Story = fetched ? Story() : null,
        Flights = new FlightList { Flights = [Listed(intent ?? Typed())] },
    };

    // ---- the title ----

    [Test]
    public async Task The_title_names_the_flight_it_is_about()
    {
        // "This flight" is true of every flight, which is what makes it worth
        // nothing across the top of one. The number is what a person types, so
        // it is what a person recognises.
        var title = PaneText.ModalTitle(Opened());

        await Assert.That(title).Contains("GG-42")
            .Because("the flight's own number is the thing a person came here about.");
        await Assert.That(title).Contains("the login form loses focus")
            .Because("and the number alone does not say which piece of work it is.");
    }

    [Test]
    public async Task And_says_what_it_can_when_there_is_no_flight_to_name()
    {
        var nothing = new AppState { Mode = UiMode.FlightDetail };

        await Assert.That(PaneText.ModalTitle(nothing))
            .IsEqualTo(PaneText.ModalTitle(UiMode.FlightDetail))
            .Because("a modal with no flight under the cursor still needs a heading, and the "
                   + "one written for the mode is it.");
    }

    // ---- the intent ----

    [Test]
    public async Task The_intent_is_rendered_whole()
    {
        var intent = FlightDetails.Intent(Opened());

        await Assert.That(intent).Contains("The login form drops focus")
            .Because("the modal showed no intent at all, which is the one field that says "
                   + "what the flight is FOR.");
        await Assert.That(intent).Contains("It should land back in the password box.")
            .Because("a paragraph cut off at the first line is a paragraph nobody can act on.");
        await Assert.That(intent).Contains("1. Sign in with a password that is wrong.")
            .Because("what a person wrote is markdown as often as not, and it is handed over "
                   + "as written rather than flattened.");
    }

    [Test]
    public async Task A_uri_intent_and_a_ticket_intent_are_shown_too()
    {
        var uri = FlightDetails.Intent(Opened(new FlightIntent
        {
            Kind = FlightIntentKinds.Uri,
            Uri = "https://example.invalid/issues/4471",
        }));

        await Assert.That(uri).Contains("https://example.invalid/issues/4471");

        var ticket = FlightDetails.Intent(Opened(new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "a-tracker",
            Id = "4471",
        }));

        await Assert.That(ticket).Contains("a-tracker");
        await Assert.That(ticket).Contains("4471");
    }

    [Test]
    public async Task An_intent_that_never_loaded_says_so()
    {
        var nothing = new AppState { Mode = UiMode.FlightDetail };

        await Assert.That(FlightDetails.Intent(nothing)).IsNotEmpty()
            .Because("an empty markdown pane reads as a flight opened for no reason, which is "
                   + "a thing that cannot happen - FlightIntent.Validate refuses it.");
    }

    // ---- the fields ----

    [Test]
    public async Task Every_scalar_is_a_field_with_a_name_beside_it()
    {
        var fields = FlightDetails.Fields(Opened());
        var labels = fields.Select(f => f.Label).ToList();

        foreach (var expected in (string[])
                 ["id", "stage", "state", "held by", "opened", "envelope", "attempts", "facts"])
        {
            await Assert.That(labels).Contains(expected)
                .Because($"'{expected}' was a two-space indent and a column of padding, which "
                       + "is a label a person can neither select nor copy.");
        }

        await Assert.That(fields.Any(f => f.Value.Contains("019fe815", StringComparison.Ordinal)))
            .IsTrue().Because("the flight id is the value most often copied out of this modal.");
    }

    [Test]
    public async Task The_reason_it_cannot_start_is_a_field_of_its_own()
    {
        var story = Story();
        var fields = FlightDetails.Fields(Opened());
        var sentence = Reason.Sentence(story.Waiting!.Kind, story.Waiting.Params);

        await Assert.That(fields.Any(f => f.Value.Contains(sentence, StringComparison.Ordinal)))
            .IsTrue().Because("why a flight will not fly is the field a person opened this for.");
        await Assert.That(fields.Any(f => f.Value.Contains("kevin", StringComparison.Ordinal)))
            .IsTrue().Because("and who is holding it is the other one.");
    }

    [Test]
    public async Task A_flight_with_no_story_still_has_fields()
    {
        var fields = FlightDetails.Fields(Opened(fetched: false));

        await Assert.That(fields.Select(f => f.Label)).Contains("id")
            .Because("the summary carries these whether or not a story was ever fetched, and a "
                   + "modal that showed nothing would read as a flight that is nothing.");
    }

    [Test]
    public async Task The_reason_and_the_thing_owed_are_not_labelled_twice()
    {
        var labels = FlightDetails.Fields(Opened()).Select(f => f.Label).ToList();

        await Assert.That(labels).DoesNotContain("waiting")
            .Because("the contract's sentence opens with the word - 'waiting: no runner "
                   + "advertises linux-x64' - so a label reading 'waiting' says it twice.");
        await Assert.That(labels).Contains("why");
        await Assert.That(labels).Contains("awaiting")
            .Because("the value is the thing that waits, not the person it waits on: "
                   + "'waiting on stopped on a rule' is a sentence that lost its subject.");
    }

    [Test]
    public async Task Every_label_fits_the_gutter_the_view_leaves_for_it()
    {
        // The view puts the values at a fixed column so they do not move
        // between two flights. A label wider than the gutter is a label drawn
        // over the value beside it.
        foreach (var label in FlightDetails.Fields(Opened()).Select(f => f.Label))
        {
            await Assert.That(label.Length).IsLessThanOrEqualTo(10)
                .Because($"'{label}' has to fit beside the value, and the gutter is fixed.");
        }
    }

    // ---- how tall the intent's box is ----

    [Test]
    public async Task Even_a_one_line_intent_gets_a_box_worth_reading_in()
    {
        // THIS TEST ASSERTED THE OPPOSITE AND THE OPPOSITE WAS WRONG. It said a
        // one-line intent gets three rows, on the argument that a box sized for
        // a page around `fix the login bug' is empty rows taken off the log.
        // True as far as it went, and it optimised the wrong thing: the intent
        // is why the flight exists, the log is what it did, and shrinking the
        // first to nothing whenever it happens to be short makes the modal jump
        // between layouts and leaves the common case unreadable the moment
        // somebody pastes two paragraphs.
        //
        // So the intent gets a FLOOR as well as a cap - two fifths of the body,
        // which roughly halves the log - and content-sizes between them.
        var typed = Opened(new FlightIntent
        {
            Kind = FlightIntentKinds.Text,
            Text = "fix the login bug",
        });

        await Assert.That(FlightDetails.IntentLines(typed)).IsEqualTo(1);
        await Assert.That(FlightDetails.IntentRows(1, room: 30)).IsEqualTo(12)
            .Because("two fifths of the body, so a short intent still has somewhere to be "
                   + "read and the pane does not resize as the text changes.");
    }

    [Test]
    public async Task And_a_long_one_does_not_take_the_log_with_it()
    {
        await Assert.That(FlightDetails.IntentLines(Opened())).IsGreaterThan(5)
            .Because("the fixture's intent is a paragraph with steps in it.");

        // TWO THIRDS, NOT UNDER A HALF. 45% was chosen to protect the log and
        // protected it too well: an intent is the thing a person opens this
        // modal to read, and thirteen rows of thirty for a long one meant
        // scrolling the part that says what the flight is FOR while the part
        // saying what it did sat idle underneath. The cap stays a share rather
        // than a row count, because what it protects is a proportion.
        await Assert.That(FlightDetails.IntentRows(200, room: 30)).IsEqualTo(20)
            .Because("a long intent gets two thirds of the body, and the log keeps a third.");
        await Assert.That(FlightDetails.IntentRows(8, room: 30)).IsEqualTo(12)
            .Because("an eight-line intent wants ten and the floor gives it twelve, because "
                   + "below two fifths the log is taking room nothing is reading.");
        await Assert.That(FlightDetails.IntentRows(14, room: 30)).IsEqualTo(16)
            .Because("and between the floor and the cap it still content-sizes, which is the "
                   + "part worth keeping from the version this replaced.");
    }

    [Test]
    public async Task Before_anything_is_laid_out_it_asks_for_what_it_wants()
    {
        // A render happens before the layout does, so the first pass is asked
        // with no room to cap against. Capping to nothing would draw a box
        // three rows tall and then never revisit it.
        await Assert.That(FlightDetails.IntentRows(9, room: 0)).IsEqualTo(11);
        await Assert.That(FlightDetails.IntentRows(1, room: 0)).IsEqualTo(3);
    }

    // ---- the log ----

    [Test]
    public async Task The_log_is_rows_rather_than_lines()
    {
        var rows = Rows.Log(Opened());
        var story = Story();

        await Assert.That(rows.Count).IsEqualTo(story.Entries.Count);

        await Assert.That(rows[0].Event)
            .IsEqualTo(FlightStory.Sentence(story.Entries[0].Kind, story.Entries[0].Params))
            .Because("a sentence, not a kind - the cell holds what happened, in the contract's "
                   + "own words.");
        await Assert.That(rows[0].Attempt).IsEqualTo("2")
            .Because("the attempt is a column, so a reader can see which pass an entry is from "
                   + "without counting lease grants.");
        await Assert.That(rows[0].Time).Contains("2026-09-06");
    }

    [Test]
    public async Task What_somebody_wrote_travels_with_the_entry()
    {
        var rows = Rows.Log(Opened());

        await Assert.That(rows[1].Detail).Contains("the move bound could not be measured")
            .Because("the agent's account of why it stopped is the only thing that says what "
                   + "to do about it.");
        await Assert.That(rows[1].Detail).Contains("and nothing said what it should be")
            .Because("and the second line of it is not less true than the first.");
        await Assert.That(rows[1].Detail).DoesNotContain("\n")
            .Because("a table cell is one line; the newlines are flattened here rather than "
                   + "handed to a widget that would draw them as a broken row.");
    }

    [Test]
    public async Task An_entry_from_no_attempt_leaves_the_column_empty()
    {
        // Null and never zero: an entry from a record that never carried an
        // attempt is absent rather than first, and a cell reading `0' would be
        // this console inventing one.
        await Assert.That(Rows.Log(Opened())[1].Attempt).IsEmpty();
    }

    [Test]
    public async Task The_log_is_called_the_log()
    {
        // WHAT THE FRAME OVER THE TABLE SAYS. "what happened, in order:" was a
        // sentence doing a heading's job, and it read as an instruction.
        await Assert.That(FlightDetails.LogTitle).IsEqualTo("Log");
        await Assert.That(PaneText.Modal(Opened()))
            .DoesNotContain("what happened, in order");
    }

    // ---- what is absent, said as absent ----

    [Test]
    public async Task A_story_nobody_fetched_is_not_a_flight_nothing_happened_to()
    {
        var unfetched = FlightDetails.LogAbsence(Opened(fetched: false));

        await Assert.That(unfetched).IsNotEmpty();
        await Assert.That(unfetched).Contains("fetched")
            .Because("a person shown 'nothing happened' when the truth is 'nobody asked' "
                   + "stops looking.");
        await Assert.That(Rows.Log(Opened(fetched: false))).IsEmpty();
    }

    [Test]
    public async Task And_a_story_with_no_entries_says_that_instead()
    {
        var empty = Opened() with { Story = Story() with { Entries = [], Outstanding = [] } };

        await Assert.That(FlightDetails.LogAbsence(empty)).Contains("recorded");
        await Assert.That(FlightDetails.LogAbsence(Opened())).IsEmpty()
            .Because("a table with rows in it needs no sentence explaining why it has none.");
    }

    // ---- the view binds these and composes nothing of its own ----

    [Test]
    public async Task The_view_binds_the_producers_rather_than_formatting_its_own()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        foreach (var producer in (string[])
                 ["PaneText.ModalTitle(State)", "FlightDetails.Intent", "FlightDetails.Fields",
                  "FlightDetails.LogAbsence", "Rows.Log(State)"])
        {
            await Assert.That(screen).Contains(producer)
                .Because($"{producer} is where the model is turned into what a person reads; a "
                       + "view that formatted its own would be a second layout nothing can "
                       + "test.");
        }
    }

    [Test]
    public async Task And_uses_the_widgets_the_content_asks_for()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("new Markdown")
            .Because("an intent runs to paragraphs and a person wrote it as markdown.");
        await Assert.That(screen).Contains("ReadOnly = true")
            .Because("a field a person can put a cursor in is a field a person can copy out "
                   + "of, which a Label is not.");
    }

    [Test]
    public async Task The_fields_are_rebuilt_only_when_they_change()
    {
        // WHY IT MATTERS THAT THEY ARE NOT REBUILT EVERY TIME. Terminal.Gui's
        // RemoveAll hands the caller the lifetime of what it removed - "the
        // caller must call Dispose on any Views that were added" - and Render
        // runs on the live tail's timer four times a second, so a modal left
        // open over a running flight would drop two undisposed views per field
        // per tick.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("RemoveAll")
            .Because("a flight waiting on three people has three rows more than one waiting "
                   + "on nobody, so the column is built rather than assigned into.");
        await Assert.That(screen).Contains("Dispose()")
            .Because("RemoveAll transfers the lifetime to the caller; a view removed and not "
                   + "disposed is a view that stays.");
        await Assert.That(screen).Contains("_fieldsShowing")
            .Because("and the cheapest disposal is the rebuild that does not happen: the "
                   + "fields are a value, so whether they changed is a comparison.");
    }

    [Test]
    public async Task Fields_are_values_so_two_renders_of_one_flight_are_equal()
    {
        // WHAT MAKES THAT COMPARISON WORK. FlightField is a record, so the
        // guard is structural equality over the list rather than a hash
        // somebody has to remember to update when a field is added.
        var once = FlightDetails.Fields(Opened());
        var again = FlightDetails.Fields(Opened());

        await Assert.That(once.SequenceEqual(again)).IsTrue();

        var moved = FlightDetails.Fields(Opened() with
        {
            Story = Story() with { Outstanding = [] },
        });

        await Assert.That(once.SequenceEqual(moved)).IsFalse()
            .Because("a flight that stopped waiting on somebody has one field fewer, and the "
                   + "column has to lose the row.");
    }

    /// <summary>
    /// The linear rendering shows everything the widgets do.
    /// </summary>
    /// <remarks>
    /// <b><c>PaneText.Modal</c> is no longer what the screen draws for this
    /// mode, and that is exactly why this is here.</b> <c>StoryFieldParityTests</c>
    /// holds the modal against the CLI over one string; if that string were
    /// composed independently of the widgets it would be a guard over text
    /// nobody sees. It is composed FROM the same four producers, and this says
    /// so.
    /// </remarks>
    [Test]
    public async Task The_text_rendering_is_the_widgets_read_in_order()
    {
        var text = PaneText.Modal(Opened());

        await Assert.That(text).Contains(PaneText.ModalTitle(Opened()));
        await Assert.That(text).Contains("The login form drops focus");

        foreach (var field in FlightDetails.Fields(Opened()))
        {
            await Assert.That(text).Contains(field.Value)
                .Because($"the field '{field.Label}' is on the screen, so it is in the "
                       + "rendering the parity guard reads.");
        }

        foreach (var row in Rows.Log(Opened()))
        {
            await Assert.That(text).Contains(row.Event);
            await Assert.That(text).Contains(row.Detail);
        }
    }
}
