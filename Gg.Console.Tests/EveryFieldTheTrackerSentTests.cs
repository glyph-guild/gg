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
        Fields =
        [
            new("Microsoft.VSTS.Scheduling.StoryPoints", "5"),
            new("System.AssignedTo", "Kevin Deenanauth"),
            new("System.Rev", "47"),
        ],
    };

    private static AppState Showing() => new()
    {
        ActiveTab = TabId.Browse,
        BrowseVisible = true,
        Mode = UiMode.WorkItemDetail,
        BrowseSelected = 0,
        Browse = new BrowseListing { ProviderKey = "ado", Items = [Item()] },
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
        // THE ORDINARY READER, and the reason the extras are optional: one that
        // answers the declared contract and no more must be unchanged by this.
        var bare = Showing() with
        {
            Browse = new BrowseListing
            {
                ProviderKey = "ado",
                Items = [Item() with { Fields = [] }],
            },
        };

        var rows = WorkItemDetails.AllFields(bare);

        await Assert.That(rows).Count().IsEqualTo(7);
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
