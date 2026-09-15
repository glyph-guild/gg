using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The work item modal shows every field the tracker sent, not the seven the
/// listing was built to choose by.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "we're missing point and other fields".</b> Story
/// points, assignee, priority — a tracker holds dozens and this console showed
/// seven. Those seven were chosen for a LISTING, where the argument is right:
/// <i>"enough to choose by, and no more"</i>, because a list that carried
/// everything would make every page as expensive as reading everything on it.
/// A modal about ONE item is the other case entirely.
/// </para>
/// <para>
/// <b>The named seven stay named.</b> They are a contract a third-party reader
/// implements, and <c>Filters</c> narrows on two of them. What arrives beside
/// them is an OPEN list the reader may send and this console renders verbatim
/// — so the vocabulary is not dissolved to carry a story point, and a reader
/// that sends nothing extra is unchanged.
/// </para>
/// <para>
/// <b>Everything, under the tracker's own names.</b> The owner chose this over
/// a curated set: a field this console decided not to show is one nobody can
/// discover is there, and the names a tracker uses are the names a person will
/// search its UI for.
/// </para>
/// <para>
/// <b>And values are not all strings.</b> Story points is a NUMBER, which is
/// why naming it would not have been enough on its own —
/// <c>WiqlWorkItemSource.Field</c> reads <c>JsonValueKind.String</c> and
/// answers null for everything else, so the field would have arrived empty and
/// looked like a tracker that said nothing.
/// </para>
/// </remarks>
public class EveryFieldTheTrackerSentTests
{
    private static BrowseRow Item() => new()
    {
        Id = "17864",
        Title = "Sonar cleanup: correctness risks",
        State = "Active",
        Where = "Platform",
        Sprint = "Sprint 142",
        Updated = "2026-09-14T18:17Z",
        Url = "https://dev.azure.com/HRTMS/JDX/_workitems/edit/17864",
    };

    private static AppState Showing() => new()
    {
        ActiveTab = TabId.Browse,
        BrowseVisible = true,
        Mode = UiMode.WorkItemDetail,
        BrowseSelected = 0,
        Browse = new BrowseListing { ProviderKey = "ado", Items = [Item()] },
        WorkItemId = "17864",
        WorkItemFields =
        [
            new("Microsoft.VSTS.Scheduling.StoryPoints", "5"),
            new("System.AssignedTo", "Kevin Deenanauth"),
            new("System.Rev", "47"),
        ],
    };

    [Test]
    public async Task The_modal_has_a_tab_for_them()
    {
        await Assert.That(Enum.GetValues<WorkItemTab>()).Contains(WorkItemTab.Fields)
            .Because("a third tab, because what an item IS, what has happened to it and what "
                   + "the tracker records about it are three different questions.");
    }

    [Test]
    public async Task The_standard_seven_come_first_and_in_contract_order()
    {
        // NAMED THINGS FIRST, because they are the ones a person came for and
        // the ones this console can promise. The tracker's own follow.
        var rows = WorkItemDetails.AllFields(Showing());
        var names = rows.Select(r => r.Name).ToList();

        await Assert.That(names.Take(7).ToList()).IsEquivalentTo((string[])
            ["id", "title", "state", "url", "updated", "areaPath", "iteration"])
            .Because("BrowseTool.Fields declares them in that order and this is the same "
                   + "seven, so a reader of both sees one list rather than two orderings. "
                   + "Found: " + string.Join(", ", names.Take(7)));
    }

    [Test]
    public async Task And_then_everything_else_the_tracker_sent()
    {
        var names = WorkItemDetails.AllFields(Showing()).Select(r => r.Name).ToList();

        await Assert.That(names).Contains("Microsoft.VSTS.Scheduling.StoryPoints")
            .Because("the point is the field the owner went looking for.");
        await Assert.That(names).Contains("System.AssignedTo");

        await Assert.That(names).Contains("System.Rev")
            .Because("everything, including what looks like noise - a field this console "
                   + "decided not to show is one nobody can discover is there.");
    }

    [Test]
    public async Task A_value_the_tracker_sent_is_shown_as_the_tracker_spells_it()
    {
        var rows = WorkItemDetails.AllFields(Showing());

        await Assert.That(rows.Single(r => r.Name == "Microsoft.VSTS.Scheduling.StoryPoints").Value)
            .IsEqualTo("5");
    }

    [Test]
    public async Task An_item_the_reader_sent_nothing_extra_for_still_shows_the_seven()
    {
        // THE ORDINARY READER, and the reason the whole thing is optional: one
        // that answers the declared contract and no more must be unchanged by
        // this.
        var bare = Showing() with { WorkItemFields = [] };

        var rows = WorkItemDetails.AllFields(bare);

        await Assert.That(rows).Count().IsEqualTo(7);
    }

    [Test]
    public async Task A_reader_that_cannot_be_asked_says_so_rather_than_going_blank()
    {
        // THE THREE-ABSENCE RULE, which this modal already keeps about a
        // history. A reader that does not declare the tool, a tracker that
        // records nothing beyond the seven, and an inventory with rows in it
        // are three different facts. An empty table under the seven claims the
        // second when it may be the first.
        var unasked = Showing() with
        {
            WorkItemFields = [],
            WorkItemFieldsSaid = "The reader for 'ado' does not declare 'get_work_item_fields'.",
        };

        await Assert.That(WorkItemDetails.FieldsAbsence(unasked))
            .Contains("get_work_item_fields")
            .Because("the person reading it is usually the operator who installed the reader, "
                   + "and the missing tool name is the one thing they can act on.");
    }

    [Test]
    public async Task A_reader_that_put_them_on_the_listing_row_is_read_that_way_too()
    {
        // TWO CONFORMANT SHAPES, because the contract offers both. A reader
        // that finds the extras cheap may hang them on a listed item; one that
        // does not answers the per-item verb. The per-item answer wins where
        // there is one, because it is the one asked about THIS item.
        var listed = Showing() with
        {
            WorkItemFields = [],
            Browse = new BrowseListing
            {
                ProviderKey = "ado",
                Items = [Item() with { Fields = [new("System.Tags", "sonar; debt")] }],
            },
        };

        await Assert.That(WorkItemDetails.AllFields(listed).Select(r => r.Name).ToList())
            .Contains("System.Tags");
    }

    [Test]
    public async Task A_number_survives_the_crossing()
    {
        // THE LATENT BUG THAT WOULD HAVE MADE NAMING THE FIELD USELESS.
        // WiqlWorkItemSource.Field answers null for anything that is not a JSON
        // string, and a story point is a number - so it would have arrived
        // empty and read as a tracker that recorded nothing.
        var said = WorkItemFields.Text(
            System.Text.Json.JsonDocument.Parse("5").RootElement);

        await Assert.That(said).IsEqualTo("5");
    }

    [Test]
    public async Task What_the_reader_sent_reaches_the_row_it_is_about()
    {
        // THE MIDDLE OF THE SLICE, and it was missing. Both ends were built and
        // tested - the reader parses the extras, the modal renders whatever a
        // row carries - and the step between them, where a listed item becomes
        // a BrowseRow, dropped them on the floor. Every test above passes with
        // that step broken, because every one of them builds the row by hand.
        //
        // FOUND BY DRIVING THE BINARY, not by the suite: the tab came up
        // showing the seven and saying the tracker had offered nothing else,
        // about a reader that had just sent eight.
        var listed = new BrowseOutcome.Listed(new WorkItemPage(
            [
                new WorkItemSummary(
                    Id: "17864",
                    Title: "Sonar cleanup: correctness risks",
                    State: "Active",
                    Url: "https://tracker.example/acme/_workitems/edit/17864",
                    Updated: "2026-09-14T18:17Z",
                    AreaPath: "Platform",
                    Iteration: @"Widgets\Sprint 142",
                    Fields: [new("Microsoft.VSTS.Scheduling.StoryPoints", "5")]),
            ],
            NextCursor: null));

        var after = Reducer.Browsed(new AppState(), "a-tracker", listed);

        await Assert.That(after.Browse!.Items[0].Fields.Select(f => f.Name).ToList())
            .Contains("Microsoft.VSTS.Scheduling.StoryPoints")
            .Because("a field the reader sent and the row did not carry is a field the modal "
                   + "cannot show, and nothing anywhere would say it had been lost.");
    }

    [Test]
    public async Task A_person_the_tracker_wrote_as_an_object_reads_as_their_name()
    {
        // ADO SENDS System.AssignedTo AS AN OBJECT. Rendered as raw JSON it is
        // a wall of urls and descriptors with the one useful word buried in it.
        var said = WorkItemFields.Text(System.Text.Json.JsonDocument.Parse(
            """{"displayName":"Kevin Deenanauth","uniqueName":"kdeenanauth@jdxpert.com"}""")
            .RootElement);

        await Assert.That(said).IsEqualTo("Kevin Deenanauth");
    }
}
