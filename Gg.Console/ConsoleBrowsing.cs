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
    /// <param name="stateHome">
    /// Where the remembered filter is kept, for a caller that has its own.
    /// Production passes nothing - the override exists because
    /// <c>XDG_STATE_HOME</c> is process-global and a suite that runs four-wide
    /// cannot have one test setting it while another reads it, which is the
    /// reason every path in <c>LocalPaths</c> takes one.
    /// </param>
    public static Func<AppState, AppState> Patch(
        IWorkBrowser? browser, AppState state, string? stateHome = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (browser is null || !state.BrowseVisible)
        {
            return current => current;
        }

        var key = browser.Key ?? "the reader";

        try
        {
            // WHAT THEY NARROWED TO LAST TIME, before the query rather than
            // after it. Restoring afterwards would list the whole backlog once
            // and then narrow it, which is a screen that changes under somebody
            // for no reason they can see.
            var restored = Restored(browser, key, state, stateHome);

            var listing = browser.BrowseAsync(
                cursor: null,
                limit: 50,
                Narrowing(restored),
                CancellationToken.None).GetAwaiter().GetResult();

            var said = BrowseFilters.Said(restored);

            // WRITTEN FROM WHAT WAS ACTUALLY BROWSED. A pick that never reached
            // a listing is a person still deciding; what comes back next time
            // is what they last looked at.
            Remember(key, restored, stateHome);

            return current => Reducer.Browsed(Carried(current, restored), key, listing, said);
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

        // ALREADY HELD, ALREADY PAID FOR - ConsoleFlightLog's rule one file
        // over. Leaving an item and coming back to it cost a request every
        // time, which is most of what browsing used to spend. `g` drops what
        // is held, so there is always a way past this.
        if (string.Equals(state.WorkItemId, item.Id, StringComparison.Ordinal)
            && state.WorkItemSaid is { Length: > 0 })
        {
            return current => current with { Mode = UiMode.WorkItemDetail };
        }

        var said = Words(browser, item.Id);
        var happened = Happened(browser, item.Id);

        return current => current with
        {
            Mode = UiMode.WorkItemDetail,
            WorkItemId = item.Id,
            WorkItemSaid = said,
            WorkItemChanges = happened.Rows,
            WorkItemHistorySaid = happened.Said,
            WorkItemSelected = 0,
            WorkItemTab = WorkItemTab.Details,
        };
    }

    /// <summary>
    /// This session's state with the tracker's remembered filter folded in, or
    /// unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Once per reader, and the facets are asked for while it happens.</b>
    /// A remembered value the tracker no longer offers narrows every listing to
    /// nothing, which reads exactly like a backlog with no work in it - so the
    /// one browse that restores also pays for the one call that says what is
    /// still on offer, and <c>Reducer.FilterOffered</c> drops what went. Every
    /// browse after it costs nothing extra.
    /// </para>
    /// <para>
    /// <b>Nothing remembered is the common case and costs no call at all.</b>
    /// A tracker nobody has filtered reads back an empty filter, and there is
    /// nothing to check against anything.
    /// </para>
    /// </remarks>
    private static AppState Restored(
        IWorkBrowser browser, string key, AppState state, string? stateHome)
    {
        if (string.Equals(state.FiltersRestoredFor, key, StringComparison.Ordinal))
        {
            return state;
        }

        // WHAT THEY JUST PICKED BEATS WHAT THEY PICKED LAST WEEK. Somebody can
        // open the filter and narrow before the first listing of a session, and
        // restoring over that would wipe the choice between pressing pick and
        // pressing browse - a modal that appears to do nothing.
        if (Narrowing(state) is not null)
        {
            return state with { FiltersRestoredFor = key };
        }

        var remembered = Gg.Local.BrowseFilterStore.Read(key, stateHome);

        var carrying = state with
        {
            FiltersRestoredFor = key,
            ChosenAreaPath = remembered.AreaPath,
            ChosenIteration = remembered.Iteration,
            ChosenStates = remembered.States,
        };

        if (!remembered.Narrows)
        {
            return carrying;
        }

        // ASKED ONCE, TO CHECK WHAT WAS RESTORED. This is the call FacetsPatch
        // makes when somebody opens the modal; making it here means the first
        // listing is already narrowed by things that still exist, and the
        // modal's own read finds the answer held.
        var offered = Offered(browser);

        return offered is null
            ? carrying
            : Reducer.FilterOffered(carrying, offered) with { Mode = state.Mode };
    }

    /// <summary>Puts the restored filter on whatever state the tick folds into.</summary>
    /// <remarks>
    /// <b>Patches, not models</b> - <c>BackgroundReads</c>' own rule. The state
    /// this read started from is a snapshot taken before the person moved, so
    /// only the members this read is about may cross into the current one.
    /// </remarks>
    private static AppState Carried(AppState current, AppState restored) => current with
    {
        FiltersRestoredFor = restored.FiltersRestoredFor,
        ChosenAreaPath = restored.ChosenAreaPath,
        ChosenIteration = restored.ChosenIteration,
        ChosenStates = restored.ChosenStates,
        Facets = restored.Facets ?? current.Facets,
    };

    /// <summary>Remembers what this tracker is narrowed to, for the next session.</summary>
    private static void Remember(string key, AppState state, string? stateHome) =>
        Gg.Local.BrowseFilterStore.Write(key, new Gg.Local.RememberedFilters
        {
            AreaPath = state.ChosenAreaPath,
            Iteration = state.ChosenIteration,
            States = state.ChosenStates,
        }, stateHome);

    /// <summary>What the tracker offers to narrow by, or null when it could not say.</summary>
    /// <remarks>
    /// Null rather than a <c>Why</c>, because this caller is checking a
    /// remembered filter rather than drawing a modal: a reader that could not
    /// answer has said nothing about whether a sprint still exists, and
    /// dropping the filter on that would lose it to a bad minute at the tracker.
    /// </remarks>
    private static BrowseFacets? Offered(IWorkBrowser browser)
    {
        try
        {
            return browser.FacetsAsync(CancellationToken.None).GetAwaiter().GetResult() switch
            {
                FacetOutcome.Offered(var facets) => new BrowseFacets
                {
                    AreaPaths = facets.AreaPaths,
                    Iterations = facets.Iterations,
                    States = facets.States,
                },
                _ => null,
            };
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>What there is to narrow by.</summary>
    public static Func<AppState, AppState> FacetsPatch(IWorkBrowser? browser, AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (browser is null)
        {
            return current => current with { Mode = UiMode.BrowseFilter };
        }

        // THE SAME RULE, one read over. What a tracker offers to narrow by
        // changes far less often than the work does, and `g` drops it.
        if (state.Facets is not null)
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
