using Gg.Local;

namespace Gg.Console;

/// <summary>
/// What a tracker answered, fetched beside the console rather than instead of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The spawn and the asking are different acts, and only the first was ever
/// forbidden.</b> Four guards said browsing is the shell's, each giving one
/// reason: an <c>IntentReader</c> is an executable launched with a credential
/// in its environment, and a session may start neither. Every word of that is
/// about STARTING one. <see cref="ReaderSessions"/> starts a reader once per
/// console lifetime and caches it — <i>"a reader asked for twice is the same
/// reader"</i> — so the first browse is still the shell's and does the spawn
/// there, and everything after it talks to a process that was running before
/// the session existed. That is the shape <c>LiveTails</c> already has.
/// </para>
/// <para>
/// <b>Patches, not models</b>, which is <see cref="BackgroundReads"/>' own
/// rule: an answer that arrives as a whole <see cref="AppState"/> is a snapshot
/// taken before the person moved and applied after they did.
/// </para>
/// <para>
/// <b>And already held is already paid for</b>, the rule
/// <c>ConsoleFlightLog</c> keeps one file over. Reopening a pane or an item
/// that has not changed asked the tracker again every time, which is what made
/// the old teardown happen far more often than anything needed it to.
/// </para>
/// </remarks>
public static class ConsoleBrowsing
{
    /// <summary>What the tracker has, or what is already held.</summary>
    /// <remarks>
    /// <b>Nothing when the pane is closing.</b> A toggle that shut the pane and
    /// then fetched what to put in it is a request nobody asked for — the same
    /// sentence the other toggles' read function already keeps.
    /// </remarks>
    public static Func<AppState, AppState> Patch(IWorkBrowser? browser, AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (browser is null || !state.BrowseVisible)
        {
            return current => current;
        }

        var key = browser.Key ?? "the reader";

        try
        {
            var listing = browser.BrowseAsync(
                cursor: null,
                limit: 50,
                Narrowing(state),
                CancellationToken.None).GetAwaiter().GetResult();

            var said = BrowseFilters.Said(state);

            return current => Reducer.Browsed(current, key, listing, said);
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            var failed = new BrowseOutcome.Unintelligible(
                $"Browsing '{key}' failed inside this console rather than at the tracker: "
              + problem.Message);

            return current => Reducer.Browsed(current, key, failed);
        }
    }

    /// <summary>One item: what it says, and what has happened to it.</summary>
    /// <remarks>
    /// <b>Both, and the item first.</b> What it IS comes before what has
    /// happened to it, because the second only means anything once you know the
    /// first — <c>ConsoleLoop</c>'s own sentence, kept here rather than
    /// restated differently.
    /// </remarks>
    public static Func<AppState, AppState> ItemPatch(IWorkBrowser? browser, AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Under(state) is not { } item)
        {
            return current => current;
        }

        if (browser is null)
        {
            return current => current with
            {
                Mode = UiMode.WorkItemDetail,
                WorkItemSaid = "This console is not configured to read work items.",
            };
        }

        var said = Words(browser, item.Id);
        var happened = Happened(browser, item.Id);

        return current => current with
        {
            Mode = UiMode.WorkItemDetail,
            WorkItemSaid = said,
            WorkItemChanges = happened.Rows,
            WorkItemHistorySaid = happened.Said,
            WorkItemSelected = 0,
            WorkItemTab = WorkItemTab.Details,
        };
    }

    /// <summary>What there is to narrow by.</summary>
    public static Func<AppState, AppState> FacetsPatch(IWorkBrowser? browser, AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (browser is null)
        {
            return current => current with { Mode = UiMode.BrowseFilter };
        }

        try
        {
            // THE LOOP'S OWN MAPPING, because a second one is a second answer
            // to what a tracker offered.
            var offered = browser.FacetsAsync(CancellationToken.None).GetAwaiter().GetResult()
                switch
            {
                FacetOutcome.Offered(var facets) => new BrowseFacets
                {
                    AreaPaths = facets.AreaPaths,
                    Iterations = facets.Iterations,
                    States = facets.States,
                },
                FacetOutcome.Nothing(var why) => new BrowseFacets { Why = why },
                _ => new BrowseFacets
                {
                    Why = "The reader answered in a way this console has no sentence for.",
                },
            };

            return current => Reducer.FilterOffered(current, offered);
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            var failed = new BrowseFacets
            {
                Why = "Asking what there is to filter by failed inside this console rather "
                    + $"than at the tracker: {problem.Message}",
            };

            return current => Reducer.FilterOffered(current, failed);
        }
    }

    /// <summary>
    /// What the person has narrowed to, or nothing when they have not.
    /// </summary>
    /// <remarks>
    /// The loop's own, kept identical: a second way of building this is a
    /// second answer to what the person asked for.
    /// </remarks>
    private static WorkItemFilter? Narrowing(AppState state)
    {
        var filter = new WorkItemFilter(
            state.ChosenAreaPath, state.ChosenIteration,
            state.ChosenStates.Count > 0 ? state.ChosenStates : null);

        return filter.Narrows ? filter : null;
    }

    /// <summary>The row the cursor is on, or nothing.</summary>
    private static BrowseRow? Under(AppState state) =>
        state.Browse is { Items.Count: > 0 } listing
        && state.BrowseSelected >= 0
        && state.BrowseSelected < listing.Items.Count
            ? listing.Items[state.BrowseSelected]
            : null;

    private static string Words(IWorkBrowser browser, string id)
    {
        try
        {
            return browser.ReadAsync(id, CancellationToken.None).GetAwaiter().GetResult()
                switch
            {
                ItemOutcome.Read(var text) => text,
                ItemOutcome.Nothing(var why) => why,
                _ => "The reader answered in a way this console has no sentence for.",
            };
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            return "This item could not be read: " + problem.Message;
        }
    }

    private static (IReadOnlyList<WorkItemChangeRow> Rows, string? Said) Happened(
        IWorkBrowser browser, string id)
    {
        try
        {
            return browser.HistoryAsync(id, CancellationToken.None).GetAwaiter().GetResult()
                switch
            {
                HistoryOutcome.Read(var rows) => (rows, null),
                HistoryOutcome.Nothing(var why) => ([], why),
                _ => ([], "The reader answered nothing about this item's history."),
            };
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            return ([], "This item's history could not be read: " + problem.Message);
        }
    }
}
