using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The browse that restores a remembered filter, and the one that writes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Before the query, never after it.</b> Restoring afterwards lists the
/// whole backlog once and then narrows it, which is a screen changing under
/// somebody for no reason they can see - and on a tracker with thousands of
/// items it is also the expensive way round.
/// </para>
/// <para>
/// <b>Once per reader per console lifetime.</b> Re-reading the file on every
/// browse would undo a filter the moment somebody cleared it, because the file
/// still says what it said until the browse that rewrites it.
/// </para>
/// </remarks>
public class ABrowseRestoresWhatWasNarrowedTests
{
    private sealed class Root : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "gg-restore-" + Guid.NewGuid().ToString("n"));

        internal Root() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    /// <summary>A tracker that records what it was asked to narrow by.</summary>
    private sealed class Listing(WorkItemFacets? facets = null) : IWorkBrowser
    {
        internal List<WorkItemFilter?> Narrowings { get; } = [];

        internal int FacetsAsked { get; private set; }

        public string? Key => "a-tracker";

        public Task<BrowseOutcome> BrowseAsync(
            string? cursor, int limit, WorkItemFilter? filter, CancellationToken token)
        {
            Narrowings.Add(filter);

            return Task.FromResult<BrowseOutcome>(new BrowseOutcome.Listed(
                new WorkItemPage(
                    [new WorkItemSummary("1", "A thing", "Active", "https://invalid/1", null, null)],
                    null)));
        }

        public Task<FacetOutcome> FacetsAsync(CancellationToken token)
        {
            FacetsAsked++;

            return Task.FromResult<FacetOutcome>(
                new FacetOutcome.Offered(facets ?? new WorkItemFacets([], [], [])));
        }

        public Task<ItemOutcome> ReadAsync(string id, CancellationToken token) =>
            Task.FromResult<ItemOutcome>(new ItemOutcome.Nothing("not asked here"));

        public Task<HistoryOutcome> HistoryAsync(string id, CancellationToken token) =>
            Task.FromResult<HistoryOutcome>(new HistoryOutcome.Nothing("not asked here"));

        public Task<FieldsOutcome> FieldsAsync(string id, CancellationToken token) =>
            Task.FromResult<FieldsOutcome>(new FieldsOutcome.Nothing("not asked here"));
    }

    private static AppState Browsing() => new()
    {
        ActiveTab = TabId.Browse,
        BrowseVisible = true,
    };

    private static AppState Browsed(IWorkBrowser browser, AppState state, string root) =>
        ConsoleBrowsing.Patch(browser, state, root)(state);

    [Test]
    public async Task What_was_narrowed_last_time_narrows_this_time()
    {
        using var root = new Root();

        BrowseFilterStore.Write("a-tracker", new RememberedFilters
        {
            AreaPath = @"Widgets\Platform",
            States = ["Active"],
        }, root.Path);

        var browser = new Listing();
        var after = Browsed(browser, Browsing(), root.Path);

        await Assert.That(after.ChosenAreaPath).IsEqualTo(@"Widgets\Platform");
        await Assert.That(after.ChosenStates).IsEquivalentTo((string[])["Active"]);

        await Assert.That(browser.Narrowings.Single()?.AreaPath).IsEqualTo(@"Widgets\Platform")
            .Because("restored BEFORE the query. Doing it after lists the whole backlog once "
                   + "and then narrows it, which is a screen changing for no visible reason.");
    }

    [Test]
    public async Task A_tracker_nobody_has_filtered_asks_for_everything()
    {
        using var root = new Root();

        var browser = new Listing();
        var after = Browsed(browser, Browsing(), root.Path);

        await Assert.That(after.ChosenAreaPath).IsNull();
        await Assert.That(browser.Narrowings.Single()).IsNull();

        await Assert.That(browser.FacetsAsked).IsEqualTo(0)
            .Because("nothing was remembered, so there is nothing to check against anything - "
                   + "and a call nobody needs is one every browse would pay for.");
    }

    [Test]
    public async Task A_remembered_value_the_tracker_dropped_does_not_narrow_anything()
    {
        // THE CASE THIS EXISTS FOR, end to end. Sprint 42 finished while the
        // console was closed. Restoring it would narrow the first listing to
        // nothing, which reads exactly like a backlog with no work in it.
        using var root = new Root();

        BrowseFilterStore.Write("a-tracker", new RememberedFilters
        {
            AreaPath = @"Widgets\Platform",
            Iteration = @"Widgets\Sprint 42",
        }, root.Path);

        var browser = new Listing(new WorkItemFacets(
            [@"Widgets\Platform"], [@"Widgets\Sprint 43"], ["Active"]));

        var after = Browsed(browser, Browsing(), root.Path);

        await Assert.That(after.ChosenIteration).IsNull();
        await Assert.That(after.ChosenAreaPath).IsEqualTo(@"Widgets\Platform")
            .Because("the team is still real, and losing it because a sprint ended would be "
                   + "the same defect one dimension over.");

        await Assert.That(browser.Narrowings.Single()?.Iteration).IsNull();
        await Assert.That(browser.FacetsAsked).IsEqualTo(1)
            .Because("asked once, and only because something was remembered to check.");
    }

    [Test]
    public async Task What_was_browsed_is_what_comes_back_next_time()
    {
        using var root = new Root();

        var after = Browsed(
            new Listing(),
            Browsing() with { ChosenAreaPath = "Widgets", ChosenStates = ["Active", "New"] },
            root.Path);

        var remembered = BrowseFilterStore.Read("a-tracker", root.Path);

        await Assert.That(remembered.AreaPath).IsEqualTo("Widgets");
        await Assert.That(remembered.States).IsEquivalentTo((string[])["Active", "New"]);
        await Assert.That(after.ChosenAreaPath).IsEqualTo("Widgets");
    }

    [Test]
    public async Task Clearing_and_browsing_forgets_it()
    {
        // THE HALF A MERGE WOULD BREAK. Somebody takes the filter off and lists
        // the work again; the next session must start where they left it, not
        // where they left it two sessions ago.
        using var root = new Root();

        BrowseFilterStore.Write(
            "a-tracker", new RememberedFilters { AreaPath = "Widgets" }, root.Path);

        var restored = Browsed(new Listing(), Browsing(), root.Path);
        var cleared = Reducer.Reduce(restored, Command.ClearFilter);

        Browsed(new Listing(), cleared, root.Path);

        await Assert.That(BrowseFilterStore.Read("a-tracker", root.Path).AreaPath).IsNull();
    }

    [Test]
    public async Task The_file_is_read_once_and_not_on_every_browse()
    {
        // A SECOND READ WOULD UNDO A CLEAR. The file still says what it said
        // until the browse that rewrites it, so re-reading on the next press
        // would put the filter somebody just took off straight back on.
        using var root = new Root();

        BrowseFilterStore.Write(
            "a-tracker", new RememberedFilters { AreaPath = "Widgets" }, root.Path);

        var browser = new Listing();
        var first = Browsed(browser, Browsing(), root.Path);

        await Assert.That(first.FiltersRestoredFor).IsEqualTo("a-tracker");

        var cleared = Reducer.Reduce(first, Command.ClearFilter);
        var second = Browsed(browser, cleared, root.Path);

        await Assert.That(second.ChosenAreaPath).IsNull()
            .Because("the clear stands. A browse that re-read the file would restore what "
                   + "was just taken off and look like a key that does nothing.");
    }
}
