namespace Gg.Console;

/// <summary>
/// Which views are open, which one has the screen, and how that reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, because "takes over all the panes" is an invariant rather than a
/// layout.</b> The view sets each pane's visibility from <see cref="Showing"/>
/// and nothing else, so the claim that exactly one is drawn is a claim about
/// this file - which is testable without a terminal, where the geometry is not.
/// </para>
/// <para>
/// <b>The open set is derived, never stored.</b> Six flags already say which
/// views are open and they are what the shell reads to decide whether to fetch
/// anything; a second list of the same fact is the drift this console keeps
/// finding one field at a time.
/// </para>
/// </remarks>
public static class Tabs
{
    /// <summary>
    /// Every open tab, in the order the bar shows them.
    /// </summary>
    /// <remarks>
    /// The queue is always in it. It is the view a console opens on and the one
    /// tab that cannot be closed, which is what makes "close the tab you are
    /// looking at" a move with somewhere to land.
    /// </remarks>
    public static IReadOnlyList<TabId> Open(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return [.. Enum.GetValues<TabId>().Where(tab => IsOpen(state, tab))];
    }

    /// <summary>Whether this view is open as a tab.</summary>
    public static bool IsOpen(AppState state, TabId tab)
    {
        ArgumentNullException.ThrowIfNull(state);

        return tab switch
        {
            // THE TWO THAT CANNOT BE CLOSED. What needs a person, and what has
            // happened - the second exists because the first is honest about
            // needing nobody and that answered nothing.
            TabId.Queue => true,
            TabId.Flights => true,
            TabId.Evidence => state.EvidenceVisible,
            TabId.Live => state.LiveVisible,
            TabId.Browse => state.BrowseVisible,
            TabId.Repositories => state.RepositoriesVisible,
            TabId.Checklist => state.ChecklistVisible,
            TabId.Envelope => state.EnvelopeVisible,

            // TOTAL, AND LOUD. A tab added to the enum and forgotten here would
            // otherwise be a tab that is never open and never drawn - a view
            // somebody built and nobody can reach.
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "unknown tab"),
        };
    }

    /// <summary>
    /// Whether this view has the screen right now.
    /// </summary>
    /// <remarks>
    /// <b>The whole layout, as one function.</b> Exactly one tab answers true,
    /// which is what "a view takes over all the panes" means concretely - and
    /// it reads the flag as well as the active tab, so a state naming a closed
    /// view cannot draw an empty pane under a tab nobody chose.
    /// </remarks>
    public static bool Showing(AppState state, TabId tab)
    {
        ArgumentNullException.ThrowIfNull(state);

        return IsOpen(state, state.ActiveTab)
            ? state.ActiveTab == tab
            : tab == TabId.Queue;
    }

    /// <summary>The tab after the one showing, wrapping.</summary>
    /// <remarks>
    /// Over the OPEN tabs only. Landing on a view nobody opened would render an
    /// empty pane, and a key that sometimes does nothing is a key people stop
    /// pressing.
    /// </remarks>
    public static TabId Next(AppState state)
    {
        var open = Open(state);
        var showing = -1;
        for (var i = 0; i < open.Count; i++)
        {
            if (open[i] == state.ActiveTab)
            {
                showing = i;
                break;
            }
        }

        return showing < 0 ? TabId.Queue : open[(showing + 1) % open.Count];
    }

    /// <summary>
    /// The bar, for the line the title is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always two cells at least.</b> It answered "" for a single tab, which
    /// was right when the queue was alone on it - a bar with one cell is
    /// decoration on the line a person reads without being asked to. The queue
    /// and the flights are both permanent now, so there is always somewhere to
    /// switch to and the guard had become unreachable.
    /// </para>
    /// <para>
    /// <b>Marked the way the help page marks its own pages.</b> Brackets rather
    /// than colour, because a console that has to render in a terminal with two
    /// colours renders the same thing in one with sixteen.
    /// </para>
    /// </remarks>
    public static string Bar(AppState state)
    {
        var open = Open(state);

        // JOINED WITH NOTHING, because each cell carries its own two columns
        // either side - brackets on the one showing, spaces on the rest. So the
        // names sit at the same place whichever one is marked, and the bar does
        // not shift under a person's eye when they press tab.
        return string.Concat(open.Select(tab =>
            Showing(state, tab) ? $"[ {Name(tab)} ]" : $"  {Name(tab)}  "));
    }

    /// <summary>What a tab is called where a person reads it.</summary>
    /// <remarks>
    /// The enum name, for now, and every one of them happens to be the word a
    /// person would use. A switch rather than <c>ToString</c> so the day one of
    /// them does not, the answer is a line here rather than a rename of the
    /// type.
    /// </remarks>
    public static string Name(TabId tab) => tab switch
    {
        TabId.Queue => "Queue",
        TabId.Flights => "Flights",
        TabId.Evidence => "Evidence",
        TabId.Live => "Live",
        TabId.Browse => "Browse",
        TabId.Repositories => "Repositories",
        TabId.Checklist => "Checklist",
        TabId.Envelope => "Envelope",
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "unknown tab"),
    };
}
