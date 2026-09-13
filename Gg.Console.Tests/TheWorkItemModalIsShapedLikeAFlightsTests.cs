using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A work item is read the way a flight is: a document, its scalars, its history.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is already written down one file over.</b> <c>FlightDetails</c>
/// says it: <i>"split by what the content IS rather than by where it lands. An
/// identity is a heading, an intent is a document somebody wrote, the scalars
/// are fields and the history is a table - and each of those is a different
/// widget."</i> The work item modal had all four of those things and drew them
/// as one Label with a rule of dashes in the middle.
/// </para>
/// <para>
/// <b>So the same three regions, in the same order.</b> What the reader said
/// goes in the pane the intent uses, because both are prose somebody else
/// wrote. The scalars become fields a cursor can enter, because the id is the
/// value most often wanted out of this modal and a Label cannot be copied from.
/// The history becomes a table, because it is one.
/// </para>
/// <para>
/// <b>Which means the history has to arrive as rows.</b> It crossed as one
/// rendered string, which is a table flattened at the only point where the
/// shape was still known - the same defect this repository has now named twice
/// as prose asserting what the data no longer has.
/// </para>
/// </remarks>
public class TheWorkItemModalIsShapedLikeAFlightsTests
{
    private static AppState Reading() => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.WorkItemDetail,
        BrowseSelected = 0,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items =
            [
                new BrowseRow
                {
                    Id = "18515",
                    Title = "Oz asks guided questions",
                    State = "Active",
                    Updated = "2026-09-05T01:06:13Z",
                    Url = "https://tracker.example/acme/_workitems/edit/18515",
                    Where = "Platform",
                    Sprint = @"Widgets\Sprint 42",
                },
            ],
        },
        WorkItemSaid = "## Oz asks guided questions\n\nA body somebody wrote.",
        WorkItemChanges =
        [
            new WorkItemChangeRow { When = "2026-09-05 01:06", Who = "Kevin", What = "State: New → Active" },
            new WorkItemChangeRow { When = "2026-09-04 11:20", Who = "Sam", What = "Title changed" },
        ],
    };

    [Test]
    public async Task The_modal_is_titled_by_the_item_it_is_about()
    {
        // THE FLIGHT NAMES ITSELF UP THERE and every other mode keeps the title
        // written for it - a document about one subject gets the subject. Two
        // items with similar names behind a modal are otherwise told apart by
        // nothing.
        var title = PaneText.ModalTitle(Reading());

        await Assert.That(title).Contains("18515");
        await Assert.That(title).Contains("Oz asks guided questions");
    }

    [Test]
    public async Task The_scalars_are_fields_rather_than_lines_of_a_paragraph()
    {
        var fields = WorkItemDetails.Fields(Reading());

        await Assert.That(fields.Select(f => f.Label)).Contains("id");
        await Assert.That(fields.Single(f => f.Label == "id").Value).IsEqualTo("18515");

        await Assert.That(fields.Select(f => f.Label)).Contains("state");
        await Assert.That(fields.Select(f => f.Label)).Contains("where");
        await Assert.That(fields.Select(f => f.Label)).Contains("sprint");
        await Assert.That(fields.Select(f => f.Label)).Contains("updated");

        await Assert.That(fields.Single(f => f.Label == "link").Value)
            .IsEqualTo("https://tracker.example/acme/_workitems/edit/18515")
            .Because("the row carried a url all along and the modal offered to open it "
                   + "without ever showing it - a value a person cannot read is one they "
                   + "cannot send to anybody else.");
    }

    [Test]
    public async Task They_come_from_the_row_rather_than_from_the_prose()
    {
        // NOT PARSED BACK OUT OF WHAT THE READER WROTE. The listing carried
        // every one of these, and pulling them out of a rendering would be
        // parsing something nobody promised - the same argument the browser key
        // already makes about the url.
        var fields = WorkItemDetails.Fields(Reading() with { WorkItemSaid = "" });

        await Assert.That(fields).IsNotEmpty()
            .Because("a reader that could not be asked still leaves a row that was chosen "
                   + "from, and what a person picked is worth showing them.");
    }

    [Test]
    public async Task What_the_reader_said_is_the_document_region()
    {
        await Assert.That(WorkItemDetails.Said(Reading()))
            .IsEqualTo("## Oz asks guided questions\n\nA body somebody wrote.")
            .Because("it is prose somebody else wrote, which is what the intent pane is for "
                   + "- and re-sorting it here would be a second opinion about the same "
                   + "bytes.");
    }

    [Test]
    public async Task The_history_is_a_table_with_three_columns()
    {
        await Assert.That(Rows.WorkItemColumns)
            .IsEquivalentTo((string[])["when", "who", "what"]);

        var changes = WorkItemDetails.Changes(Reading());

        await Assert.That(changes).Count().IsEqualTo(2);
        await Assert.That(changes[0].Who).IsEqualTo("Kevin");
    }

    [Test]
    public async Task A_reader_with_no_history_says_so_instead_of_an_empty_table()
    {
        // AN EMPTY TABLE CLAIMS NOTHING HAS HAPPENED TO THE ITEM, which is a
        // different sentence from a reader that cannot be asked - the
        // distinction every pane in this console draws between empty and
        // absent.
        var quiet = Reading() with
        {
            WorkItemChanges = [],
            WorkItemHistorySaid = ItemTool.NoHistory("a-tracker"),
        };

        await Assert.That(WorkItemDetails.HistoryAbsence(quiet)).Contains("a-tracker");

        await Assert.That(WorkItemDetails.HistoryAbsence(Reading() with { WorkItemChanges = [] }))
            .Contains("Nothing")
            .Because("a tracker that answered and had nothing to say is not a reader that "
                   + "could not be asked.");

        await Assert.That(WorkItemDetails.HistoryAbsence(Reading())).IsEmpty()
            .Because("there are rows, so the table is what speaks.");
    }

    [Test]
    public async Task The_history_cursor_moves_inside_the_modal()
    {
        // THE MODAL WITH A LIST IN IT OWNS THE CURSOR, because it owns the
        // keyboard - and the tab under this one is Browse, so without an arm
        // the work list would scroll behind a modal about one of its rows.
        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('j'), new KeymapContext(UiMode.WorkItemDetail)))
            .IsEqualTo(Command.SelectNext);

        var moved = Reducer.Reduce(Reading(), Command.SelectNext);

        await Assert.That(moved.WorkItemSelected).IsEqualTo(1);
        await Assert.That(moved.BrowseSelected).IsEqualTo(0)
            .Because("the work list is behind this modal and is not what is being walked.");
    }

    [Test]
    public async Task A_history_crosses_as_rows_rather_than_as_a_rendered_block()
    {
        // THE SHAPE WAS KNOWN AND WAS THROWN AWAY. WorkItemChange has three
        // fields; the server joined them with two spaces and the console
        // received a paragraph, which is a table flattened at the last point
        // anybody could still see it was one.
        await Assert.That(ItemTool.History.Changes).IsEqualTo("changes");
        await Assert.That(ItemTool.History.When).IsEqualTo("when");
        await Assert.That(ItemTool.History.Who).IsEqualTo("who");
        await Assert.That(ItemTool.History.What).IsEqualTo("what");
    }

    [Test]
    public async Task The_screen_draws_the_flights_three_regions()
    {
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("_itemSaid")
            .Because("prose somebody else wrote goes in the pane the intent uses.");
        await Assert.That(screen).Contains("_itemFields")
            .Because("scalars are fields a cursor can enter, or the id cannot be copied.");
        await Assert.That(screen).Contains("_itemHistory")
            .Because("a history is a table, which is the whole of this change.");

        await Assert.That(screen).Contains("Lay(_itemFields")
            .Because("one implementation of the field column, because two modals wanted it "
                   + "and now three do.");
    }
}
