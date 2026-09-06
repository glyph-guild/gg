using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The three views that are lists of the same shape of thing are tables, with
/// columns a person can read down.
/// </summary>
/// <remarks>
/// <para>
/// <b>THEY WERE PRE-FORMATTED TEXT IN A LABEL</b>, aligned by counting
/// characters into a format string - so every column was as wide as the widest
/// value anybody imagined, a long repository path pushed the name off the
/// screen, and nothing said what a column was. A table has headers, its own
/// idea of how wide a column should be, and a cursor the widget maintains.
/// </para>
/// <para>
/// <b>The rows stay pure, and that is the whole point of the split.</b> What
/// goes in each cell is a function of the model and is tested without a
/// terminal; <c>Terminal.Gui</c>'s job is to draw it and to say which row the
/// person is on. A renderer that formatted its own columns was doing both, and
/// only one of them can be checked.
/// </para>
/// <para>
/// <b>Not every tab.</b> The envelope is a document a person copies and applies
/// back, so columns would break the thing that makes it useful; the live view is
/// a stream; evidence is prose with a voice attached, where "said" and
/// "measured" have to stay distinguishable. Those three stay text.
/// </para>
/// </remarks>
public class TheTablesAreTablesTests
{
    private static FlightSummary AFlight(string number, DateTimeOffset created) => new()
    {
        FlightId = "f-" + number,
        FlightNumber = number,
        Name = "work for " + number,
        Intent = new FlightIntent { Kind = FlightIntentKinds.Text, Text = "work" },
        CreatedAt = created,
        RunnerProtocolVersion = 1,
        FactVocabularyVersion = "0.25.0",
        ConstitutionVersion = "1.0.0",
        EnvelopeVersion = "v6",
        Attempts = 1,
        State = FlightStates.Landed,
        Facts = [],
    };

    private static AppState Listing() => new()
    {
        ActiveTab = TabId.Flights,
        Flights = new FlightList
        {
            Flights =
            [
                AFlight("GG-50", new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero)),
                AFlight("GG-52", new DateTimeOffset(2026, 9, 6, 15, 0, 0, TimeSpan.Zero)),
                AFlight("GG-51", new DateTimeOffset(2026, 9, 6, 5, 0, 0, TimeSpan.Zero)),
            ],
        },
    };

    [Test]
    public async Task Every_row_is_the_flight_it_says_it_is()
    {
        var rows = Rows.Flights(Listing());

        await Assert.That(rows.Select(r => r.Number).ToList())
            .IsEquivalentTo((string[])["GG-52", "GG-51", "GG-50"])
            .Because("newest first, which is the order the cursor indexes and therefore the "
                   + "only order a row on a screen can mean.");

        await Assert.That(rows[0].FlightId).IsEqualTo("f-GG-52")
            .Because("the row carries the id, so what enter opens is decided by the row a "
                   + "person is on rather than by counting back through the list.");
    }

    [Test]
    public async Task A_column_is_named_where_a_person_reads_it()
    {
        // The headers are the thing a Label could not have. Asserted as a set
        // so the order stays the view's business and the NAMES stay the model's.
        await Assert.That(Rows.FlightColumns).IsEquivalentTo((string[])
            ["flight", "state", "loop", "age", "work"]);
        await Assert.That(Rows.BrowseColumns).IsEquivalentTo((string[])
            ["item", "state", "title"]);
        await Assert.That(Rows.RepositoryColumns).IsEquivalentTo((string[])
            ["", "path", "name"]);
    }

    [Test]
    public async Task Pointing_at_a_row_moves_the_cursor_of_the_list_on_the_screen()
    {
        // WHAT A CLICK NEEDS AND THE KEYS DID NOT. QueueSelection.Wanted turns
        // any jump into one step - it can only answer SelectNext or
        // SelectPrevious - so clicking the fifth row moved the cursor to the
        // second. A table hands over a row number, and this is the entry point
        // that takes one.
        var pointed = Reducer.Pointed(Listing(), 2);

        await Assert.That(pointed.FlightSelected).IsEqualTo(2);

        var clamped = Reducer.Pointed(Listing(), 99);

        await Assert.That(clamped.FlightSelected).IsEqualTo(2)
            .Because("a row past the end points at no flight, and enter would then have to "
                   + "decide what to do about that.");
    }

    [Test]
    public async Task Pointing_somewhere_leaves_the_other_lists_where_they_were()
    {
        var state = Listing() with { SelectedRow = 1, BrowseSelected = 3, RepositorySelected = 2 };

        var pointed = Reducer.Pointed(state, 1);

        await Assert.That(pointed.FlightSelected).IsEqualTo(1);
        await Assert.That(pointed.SelectedRow).IsEqualTo(1);
        await Assert.That(pointed.BrowseSelected).IsEqualTo(3);
        await Assert.That(pointed.RepositorySelected).IsEqualTo(2)
            .Because("four lists, four cursors, and pointing moves whichever has the screen.");
    }

    [Test]
    public async Task A_table_with_nothing_in_it_is_not_a_table()
    {
        // A HEADER OVER NO ROWS SAYS A READ SUCCEEDED AND FOUND NOTHING, which
        // is one of three things it could mean. The pane keeps its sentence for
        // the empty cases and the table is only drawn when there is something
        // to put in it.
        await Assert.That(Rows.Flights(new AppState())).IsEmpty();
        await Assert.That(PaneText.Flights(new AppState()))
            .Contains("could not", StringComparison.OrdinalIgnoreCase)
            .Because("null is a request that did not answer, and the pane still says so.");

        await Assert.That(Rows.Browse(new AppState())).IsEmpty();
        await Assert.That(Rows.Repositories(new AppState())).IsEmpty();
    }
}
