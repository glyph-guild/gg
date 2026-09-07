using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console.Tests;

/// <summary>
/// A table cell is one line, and the entry under the cursor is not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The single line per row is the thing worth keeping, and the truncation is
/// the price it charged.</b> A halt's diagnosis is the only part of the log
/// that says what to do about the halt, and it was reaching the screen as
/// <c>the move bound could not be mea…</c> - readable only by scrolling the
/// table sideways, which is a thing nobody discovers.
/// </para>
/// <para>
/// <b>Terminal.Gui has no variable row heights</b>, so an entry that unwraps
/// does it by becoming several rows: the first carries the entry, the rest
/// carry the remainder of its detail with the other three columns empty. Every
/// row is still one line, which is why the table still reads as a table.
/// </para>
/// <para>
/// <b>The wrap needs a width, and a width is a thing only the terminal knows.</b>
/// So it is a parameter: the view measures and this decides, which keeps the
/// arithmetic checkable and leaves the measuring where it has to be.
/// </para>
/// </remarks>
public class TheLogUnwrapsTheRowUnderTheCursorTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string LongSaid =
        "the move bound could not be measured, and nothing in the envelope said what it should be";

    private static StoryEntry[] Happenings() =>
    [
        new StoryEntry
        {
            At = At,
            Kind = StoryKinds.LeaseGranted,
            Stage = FlightStages.Of(StoryKinds.LeaseGranted),
            Params = ["mac-studio-01"],
            Attempt = 1,
        },
        new StoryEntry
        {
            At = At.AddMinutes(4),
            Kind = StoryKinds.ObligationHalted,
            Stage = FlightStages.Of(StoryKinds.ObligationHalted),
            Params = [],
            Attempt = 1,
            Said = LongSaid,
        },
        new StoryEntry
        {
            At = At.AddMinutes(9),
            Kind = StoryKinds.LeaseGranted,
            Stage = FlightStages.Of(StoryKinds.LeaseGranted),
            Params = ["mac-studio-01"],
            Attempt = 2,
        },
    ];

    private static AppState Opened(int cursor = 0)
    {
        var entries = Happenings();

        var story = new FlightStory
        {
            FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
            FlightNumber = FlightRef.Format(42),
            Stage = FlightStoryStages.Reached(entries),
            State = FlightStates.Open,
            Entries = entries,
        };

        return new AppState
        {
            Mode = UiMode.FlightDetail,
            ActiveTab = TabId.Flights,
            Story = story,
            LogSelected = cursor,
            Flights = new FlightList
            {
                Flights =
                [
                    new FlightSummary
                    {
                        FlightId = story.FlightId,
                        FlightNumber = story.FlightNumber,
                        Name = "the login form loses focus",
                        Intent = new FlightIntent
                        {
                            Kind = FlightIntentKinds.Text,
                            Text = "fix the login bug",
                        },
                        CreatedAt = At,
                        RunnerProtocolVersion = 1,
                        FactVocabularyVersion = "0.25.0",
                        ConstitutionVersion = "1.0.0",
                        EnvelopeVersion = "v6",
                        Attempts = 2,
                        State = FlightStates.Open,
                        Facts = [],
                    },
                ],
            },
        };
    }

    // ---- what the columns are called ----

    [Test]
    public async Task The_columns_are_named_the_way_a_log_names_them()
    {
        await Assert.That(Rows.LogColumns).IsEquivalentTo(
            (IReadOnlyList<string>)["time", "attempt", "event", "detail"]);
    }

    [Test]
    public async Task And_the_row_carries_the_same_words()
    {
        // The record's members and the headings are one naming, so a column
        // renamed in one place is a compile error rather than a heading that
        // stopped describing its cell.
        var row = Rows.Log(Opened())[0];

        await Assert.That(row.Time).Contains("2026-09-06");
        await Assert.That(row.Attempt).IsEqualTo("1");
        await Assert.That(row.Event).IsEqualTo(
            FlightStory.Sentence(StoryKinds.LeaseGranted, ["mac-studio-01"]));
        await Assert.That(row.Detail).IsEmpty();
    }

    // ---- which entry a row belongs to ----

    [Test]
    public async Task Every_row_says_which_entry_it_came_from()
    {
        // WHAT MAKES THE CURSOR SURVIVE AN UNWRAP. A continuation row is not an
        // entry, so a cursor kept as a row number would point at a different
        // thing the moment anything expanded. The model keeps the ENTRY and the
        // view maps both ways through this.
        var rows = Rows.Log(Opened());

        await Assert.That(rows.Select(r => r.Entry)).IsEquivalentTo((IReadOnlyList<int>)[0, 1, 2]);
    }

    // ---- the unwrap ----

    [Test]
    public async Task The_entry_under_the_cursor_becomes_as_many_rows_as_it_needs()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 20);

        // Three entries, and the middle one is now several rows.
        await Assert.That(shown.Count).IsGreaterThan(3);
        await Assert.That(shown.Count(r => r.Entry == 0)).IsEqualTo(1);
        await Assert.That(shown.Count(r => r.Entry == 2)).IsEqualTo(1);
        await Assert.That(shown.Count(r => r.Entry == 1)).IsGreaterThan(1)
            .Because("the entry under the cursor is the one that unwraps.");
    }

    [Test]
    public async Task Nothing_of_what_was_written_is_dropped()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 20);
        var rebuilt = string.Join(" ", shown.Where(r => r.Entry == 1).Select(r => r.Detail));

        await Assert.That(rebuilt).IsEqualTo(LongSaid)
            .Because("unwrapping is a line break, not an edit: every word comes back in "
                   + "order.");
    }

    [Test]
    public async Task And_no_line_is_wider_than_the_column()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 20);

        foreach (var row in shown.Where(r => r.Entry == 1))
        {
            await Assert.That(row.Detail.Length).IsLessThanOrEqualTo(20)
                .Because($"'{row.Detail}' would be truncated by the widget, which is the "
                       + "thing being fixed.");
        }
    }

    [Test]
    public async Task The_continuation_rows_carry_only_the_detail()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 20);
        var carried = shown.Where(r => r.Entry == 1).Skip(1).ToList();

        await Assert.That(carried).IsNotEmpty();

        foreach (var row in carried)
        {
            await Assert.That(row.Time).IsEmpty();
            await Assert.That(row.Attempt).IsEmpty();
            await Assert.That(row.Event).IsEmpty()
                .Because("a timestamp repeated down the left of one entry reads as several "
                       + "things happening at once.");
            await Assert.That(row.Detail).IsNotEmpty();
        }
    }

    [Test]
    public async Task An_entry_with_nothing_written_against_it_stays_one_row()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 0)), selected: 0, width: 20);

        await Assert.That(shown.Count).IsEqualTo(3)
            .Because("the first entry has no detail, so there is nothing to unwrap and the "
                   + "table does not change shape under a cursor that landed on it.");
    }

    [Test]
    public async Task A_detail_that_already_fits_stays_one_row()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 200);

        await Assert.That(shown.Count).IsEqualTo(3);
        await Assert.That(shown[1].Detail).IsEqualTo(LongSaid);
    }

    [Test]
    public async Task Before_anything_is_measured_nothing_is_wrapped()
    {
        // A render happens before the layout does, so the first pass is asked
        // with no width. Wrapping to nothing would be one row per character.
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 0);

        await Assert.That(shown.Count).IsEqualTo(3);
    }

    [Test]
    public async Task A_word_longer_than_the_column_is_broken_rather_than_lost()
    {
        var rows = Rows.Log(Opened(cursor: 1));
        var long_ = rows[1] with { Detail = new string('x', 45) };

        var shown = Rows.Unwrapped([rows[0], long_, rows[2]], selected: 1, width: 20);
        var rebuilt = string.Concat(shown.Where(r => r.Entry == 1).Select(r => r.Detail));

        await Assert.That(rebuilt).IsEqualTo(new string('x', 45))
            .Because("a path or a hash with no spaces in it is exactly the thing somebody "
                   + "opened the log to read.");
    }

    // ---- the width the view measures ----

    [Test]
    public async Task The_detail_column_gets_what_the_other_three_leave()
    {
        var rows = Rows.Log(Opened());

        // time is 20 wide, attempt is its heading at 7, and event is the
        // longest sentence - each with one column of separator after it.
        var width = Rows.DetailWidth(rows, available: 100);
        var events = rows.Max(r => r.Event.Length);

        await Assert.That(width).IsEqualTo(100 - (20 + 1) - (7 + 1) - (events + 1));
    }

    [Test]
    public async Task And_is_never_negative_on_a_narrow_terminal()
    {
        await Assert.That(Rows.DetailWidth(Rows.Log(Opened()), available: 10))
            .IsEqualTo(0)
            .Because("a width below zero would be a wrap that never terminates.");
    }

    [Test]
    public async Task The_width_does_not_move_when_a_row_unwraps()
    {
        // THE COLUMNS MUST NOT JITTER. A continuation row is empty in the three
        // columns that size themselves, so expanding one cannot change where
        // the detail column starts - otherwise the whole table would shift
        // sideways as the cursor moved.
        var rows = Rows.Log(Opened(cursor: 1));
        var shown = Rows.Unwrapped(rows, selected: 1, width: 30);

        await Assert.That(Rows.DetailWidth(shown, available: 100))
            .IsEqualTo(Rows.DetailWidth(rows, available: 100));
    }

    // ---- the cursor ----

    [Test]
    public async Task The_cursor_moves_over_entries_while_the_modal_is_open()
    {
        var moved = Reducer.Reduce(Opened(), Command.SelectNext);

        await Assert.That(moved.LogSelected).IsEqualTo(1);
        await Assert.That(moved.FlightSelected).IsEqualTo(0)
            .Because("the flights list is BEHIND this modal. A log row that moved its cursor "
                   + "would leave the modal about one flight and the list under it pointing "
                   + "at another.");
    }

    [Test]
    public async Task And_is_clamped_to_the_entries_there_are()
    {
        var state = Opened(cursor: 2);

        await Assert.That(Reducer.Reduce(state, Command.SelectNext).LogSelected).IsEqualTo(2);
        await Assert.That(Reducer.Reduce(Opened(), Command.SelectPrevious).LogSelected)
            .IsEqualTo(0);
    }

    [Test]
    public async Task A_click_lands_on_the_entry_the_row_belongs_to()
    {
        var shown = Rows.Unwrapped(Rows.Log(Opened(cursor: 1)), selected: 1, width: 20);

        // A continuation row of entry one: clicking it means entry one, not
        // whatever number that row happens to be.
        var continuation = shown.Select((r, i) => (r, i)).Last(t => t.r.Entry == 1).i;

        await Assert.That(continuation).IsGreaterThan(1);
        await Assert.That(Reducer.Pointed(Opened(cursor: 1), shown[continuation].Entry)
            .LogSelected).IsEqualTo(1);
    }

    [Test]
    public async Task Opening_the_modal_starts_at_the_top_of_the_log()
    {
        var scrolled = Opened(cursor: 2) with { Mode = UiMode.Normal };

        await Assert.That(Reducer.FlightShown(scrolled).LogSelected).IsEqualTo(0)
            .Because("a cursor left where the last flight's log ended is a cursor pointing "
                   + "into a history it was never about.");
    }

    [Test]
    public async Task Nothing_behind_the_modal_moves_while_it_is_open()
    {
        foreach (var command in (Command[])[Command.SelectNext, Command.SelectPrevious])
        {
            var moved = Reducer.Reduce(Opened(cursor: 1), command);

            await Assert.That(moved.FlightSelected).IsEqualTo(0);
            await Assert.That(moved.SelectedRow).IsEqualTo(0);
            await Assert.That(moved.RunnerSelected).IsEqualTo(0);
            await Assert.That(moved.BrowseSelected).IsEqualTo(0);
            await Assert.That(moved.RepositorySelected).IsEqualTo(0);
        }
    }

    [Test]
    public async Task The_keys_that_move_it_are_offered_while_the_modal_is_open()
    {
        var context = new KeymapContext(UiMode.FlightDetail);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('j'), context))
            .IsEqualTo(Command.SelectNext);
        await Assert.That(Keymap.Resolve(KeyStroke.Char('k'), context))
            .IsEqualTo(Command.SelectPrevious);
    }

    // ---- the view binds it ----

    [Test]
    public async Task The_view_measures_and_maps_rather_than_deciding()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("Rows.DetailWidth")
            .Because("only the terminal knows how wide the column is, and only the model "
                   + "knows what goes in it.");
        await Assert.That(screen).Contains("Rows.Unwrapped");
        await Assert.That(screen).Contains("_flightLog.ValueChanged += OnLogRowPointedAt")
            .Because("the log has a cursor now, and it is its own subscription: "
                   + "OnRowPointedAt goes through Reducer.Pointed on the active TAB.");
    }

    /// <summary>
    /// The wrap happens again when the width it was made for changes.
    /// </summary>
    /// <remarks>
    /// <b>Found by running it, not by this.</b> A render happens BEFORE the
    /// layout does, so the widget's viewport is zero wide on the first pass -
    /// the first fill wrapped nothing, no second one followed, and the unwrap
    /// silently never happened while every unit test passed. The same event is
    /// the resize path: a narrower terminal is a narrower column, and text
    /// broken for the old one is text broken in the wrong places.
    /// </remarks>
    [Test]
    public async Task And_wraps_again_when_the_column_changes_width()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_flightLog.ViewportChanged += OnLogResized")
            .Because("a wrap made against a width of zero is no wrap at all, and the first "
                   + "render is always made against one.");
        await Assert.That(screen).Contains("_flightLog.ViewportChanged -= OnLogResized");
    }
}
