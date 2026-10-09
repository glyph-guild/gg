using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The browse tab is the <b>intents</b> tab: it lists intent sources down a strip on its left - a
/// tracker today, a directory of documents later - and the work items of the one chosen (owner,
/// 2026-10-08). On <c>[</c>, which is punctuation beside the other list tabs' keys.
/// </summary>
public class TheIntentsTabHoldsSourcesTests
{
    private static AppState TwoTrackers(string? chosen = null) => new()
    {
        ActiveTab = TabId.Intents,
        BrowseVisible = true,
        ReaderKeys = ["ado", "jira"],
        IntentSource = chosen,
    };

    [Test]
    public async Task It_is_called_intents_and_opens_on_the_bracket()
    {
        await Assert.That(Tabs.Name(TabId.Intents)).IsEqualTo("intents");
        await Assert.That(Tabs.KeyFor(TabId.Intents)).IsEqualTo(KeyStroke.Char('['));
        await Assert.That(Keymap.Resolve(KeyStroke.Char('['), KeymapContext.For(new AppState())))
            .IsEqualTo(Command.ToggleIntents);
        await Assert.That(Tabs.Title(new AppState(), TabId.Intents)).Contains("intents [");
    }

    [Test]
    public async Task B_no_longer_opens_it()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('b'), KeymapContext.For(new AppState()))).IsNull()
            .Because("the tab moved to `[`, and a letter left behind would be a second door.");
    }

    [Test]
    public async Task Each_declared_tracker_is_a_source()
    {
        var sources = IntentSources.All(TwoTrackers());

        await Assert.That(sources).IsEquivalentTo((IntentSource[])
        [
            new("ado", IntentSourceKind.Tracker, "ado"),
            new("jira", IntentSourceKind.Tracker, "jira"),
        ]);
        await Assert.That(IntentSources.Shown(TwoTrackers())?.Key).IsEqualTo("ado")
            .Because("nothing chosen is the first source.");
        await Assert.That(IntentSources.Shown(TwoTrackers("jira"))?.Key).IsEqualTo("jira");
        await Assert.That(IntentSources.Shown(new AppState())).IsNull();
    }

    [Test]
    public async Task V_on_the_tab_steps_through_the_sources_and_reads_again()
    {
        await Assert.That(Keymap.Resolve(KeyStroke.Char('v'), KeymapContext.For(TwoTrackers())))
            .IsEqualTo(Command.NextIntentSource);

        var next = Reducer.Reduce(TwoTrackers() with { Browse = Listing("ado") }, Command.NextIntentSource);
        await Assert.That(next.IntentSource).IsEqualTo("jira");
        await Assert.That(next.Browse).IsNull()
            .Because("the rows on screen are the last source's, so the tab reads again.");
        await Assert.That(Reducer.Reduce(next, Command.NextIntentSource).IntentSource).IsEqualTo("ado");

        await Assert.That(ShellCommands.Reads).Contains(Command.NextIntentSource);
    }

    [Test]
    public async Task The_listing_is_read_from_the_chosen_source()
    {
        var browser = new Keyed();

        var patched = ConsoleBrowsing.Patch(browser, TwoTrackers("jira"))(TwoTrackers("jira"));

        await Assert.That(browser.Asked).IsEquivalentTo(["jira"]);
        await Assert.That(patched.Browse?.ProviderKey).IsEqualTo("jira");
    }

    [Test]
    public async Task A_ticket_from_a_flight_is_read_from_its_own_tracker()
    {
        var browser = new Keyed();
        var state = TwoTrackers("ado") with { WorkItemId = "42", WorkItemProvider = "jira" };

        _ = ConsoleBrowsing.TicketPatch(browser, state)(state);

        await Assert.That(browser.Asked.Distinct()).IsEquivalentTo(["jira"])
            .Because("the flight names its provider, which need not be the source on screen.");
    }

    [Test]
    public async Task The_pane_is_titled_by_its_source()
    {
        await Assert.That(PaneText.BrowseTitle(TwoTrackers("jira") with { Browse = Listing("jira") }))
            .Contains("Intents — jira");
    }

    [Test]
    public async Task The_column_reads_left_to_right_with_the_shown_source_marked()
    {
        // OWNER, 2026-10-08: "is it possible for the tab on the left to have its text left to
        // right?" Terminal.Gui draws a side tab's title top to bottom and offers no way round it, so
        // the strip is a list column: one label per row, the shown one selected.
        var (labels, shown) = IntentSources.Column(TwoTrackers("jira"));

        await Assert.That(labels).IsEquivalentTo((string[])["ado", "jira"]);
        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(IntentSources.Column(TwoTrackers()).Shown).IsEqualTo(0);
        await Assert.That(IntentSources.Column(new AppState()).Labels).IsEmpty();
    }

    private static BrowseListing Listing(string key) => new() { ProviderKey = key, Items = [] };

    /// <summary>A browser per key, recording which keys it was asked to read.</summary>
    private sealed class Keyed(string? key = null, List<string>? asked = null) : IWorkBrowser
    {
        private readonly List<string> _asked = asked ?? [];

        public List<string> Asked => _asked;

        public string? Key => key ?? "ado";

        public IWorkBrowser For(string? chosen) => new Keyed(chosen ?? "ado", _asked);

        private T Said<T>(Func<string, T> outcome)
        {
            _asked.Add(Key!);
            return outcome(Key!);
        }

        public Task<ItemOutcome> ReadAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Said<ItemOutcome>(k => new ItemOutcome.Nothing(k)));

        public Task<HistoryOutcome> HistoryAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Said<HistoryOutcome>(k => new HistoryOutcome.Nothing(k)));

        public Task<FieldsOutcome> FieldsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Said<FieldsOutcome>(k => new FieldsOutcome.Nothing(k)));

        public Task<BrowseOutcome> BrowseAsync(
            string? cursor, int limit, WorkItemFilter? filter, CancellationToken cancellationToken) =>
            Task.FromResult(Said<BrowseOutcome>(k => new BrowseOutcome.Silent(k)));

        public Task<FacetOutcome> FacetsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Said<FacetOutcome>(k => new FacetOutcome.Nothing(k)));
    }
}
