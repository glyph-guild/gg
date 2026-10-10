using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The intents tab's work items scroll without end, as flights and the board do: reaching the last
/// row asks the tracker for the next page, with the same narrowing the first page was asked with
/// (owner, 2026-10-09: "the ado list in gg for the agentic log looks short - there are 132
/// stories" - the tab showed the first fifty and stopped).
/// </summary>
public class TheIntentsListLoadsMoreAtTheEndTests
{
    private static BrowseRow Row(int n) => new() { Id = $"{n}", Title = $"item {n}", State = "New" };

    private static AppState AtTheEnd(string? next, int selected = 1) => new()
    {
        ActiveTab = TabId.Intents,
        Browse = new BrowseListing
        {
            ProviderKey = "ado",
            Items = [Row(1), Row(2)],
            NextCursor = next,
            Asked = new BrowseAsked(AreaPath: @"JDX\JDNext\Agentic"),
        },
        BrowseSelected = selected,
    };

    [Test]
    public async Task The_last_row_of_a_paged_listing_asks_for_more_and_no_other_does()
    {
        await Assert.That(Reducer.WantsMore(AtTheEnd("2"))).IsEqualTo(Command.LoadMoreIntents);
        await Assert.That(Reducer.WantsMore(AtTheEnd("2", selected: 0))).IsNull();
        await Assert.That(Reducer.WantsMore(AtTheEnd(null))).IsNull()
            .Because("no cursor is the whole list.");
        await Assert.That(Reducer.WantsMore(AtTheEnd("2") with { ReadInFlight = true })).IsNull();
        await Assert.That(Reducer.Reduce(AtTheEnd("2"), Command.LoadMoreIntents).ReadInFlight).IsTrue();
    }

    [Test]
    public async Task The_next_page_is_added_under_with_the_first_page_s_narrowing()
    {
        var browser = new Paging(new WorkItemPage(
            [Summary(2), Summary(3), Summary(4)], NextCursor: "5"));

        var folded = ConsoleBrowsing.MorePatch(browser, AtTheEnd("2"))(AtTheEnd("2"));

        await Assert.That(browser.Cursor).IsEqualTo("2");
        await Assert.That(browser.Filter?.AreaPath).IsEqualTo(@"JDX\JDNext\Agentic")
            .Because("the next page answers the same question as the first, or the rows under are another list's.");
        await Assert.That(folded.Browse!.Items.Select(r => r.Id)).IsEquivalentTo(
            ["1", "2", "3", "4"], TUnit.Assertions.Enums.CollectionOrdering.Matching)
            .Because("added under, and a row already held is not held twice.");
        await Assert.That(folded.Browse.NextCursor).IsEqualTo("5");
        await Assert.That(folded.BrowseSelected).IsEqualTo(1)
            .Because("the cursor stays on the row that asked.");
    }

    [Test]
    public async Task The_first_page_records_what_it_was_asked()
    {
        var home = Directory.CreateTempSubdirectory("gg-intents-more-");
        try
        {
            var browser = new Paging(new WorkItemPage([Summary(1)], NextCursor: "1"));
            var state = new AppState { ActiveTab = TabId.Intents, BrowseVisible = true, ChosenAreaPath = @"JDX\JDNext\Agentic" };

            var listed = ConsoleBrowsing.Patch(browser, state, home.FullName)(state);

            await Assert.That(listed.Browse?.Asked?.AreaPath).IsEqualTo(@"JDX\JDNext\Agentic");
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    private static WorkItemSummary Summary(int n) => new($"{n}", $"item {n}", "New", $"https://example.test/{n}", null);

    private sealed class Paging(WorkItemPage page) : IWorkBrowser
    {
        public string? Cursor { get; private set; }

        public WorkItemFilter? Filter { get; private set; }

        public string? Key => "ado";

        public Task<BrowseOutcome> BrowseAsync(
            string? cursor, int limit, WorkItemFilter? filter, CancellationToken cancellationToken)
        {
            Cursor = cursor;
            Filter = filter;
            return Task.FromResult<BrowseOutcome>(new BrowseOutcome.Listed(page));
        }

        public Task<ItemOutcome> ReadAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<HistoryOutcome> HistoryAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FieldsOutcome> FieldsAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FacetOutcome> FacetsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
