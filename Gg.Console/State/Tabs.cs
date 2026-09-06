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
    /// Every tab, in the order the bar shows them.
    /// </summary>
    /// <remarks>
    /// <b>ALL OF THEM, ALWAYS.</b> The first version listed the tabs whose view
    /// had been opened, so the bar could only tell a person about views they had
    /// already found - a repositories tab appeared once you knew how to reach
    /// the repositories. A bar's whole job is to say what there is.
    /// </remarks>
    public static IReadOnlyList<TabId> All { get; } = [.. Enum.GetValues<TabId>()];

    /// <summary>
    /// Whether this view holds anything yet.
    /// </summary>
    /// <remarks>
    /// Not about the BAR any more - every tab is on that. This is what the
    /// pane behind it has to say for itself: the four read-backed views are
    /// empty until the shell has fetched them, and each one says so in its own
    /// words rather than rendering blank.
    /// </remarks>
    public static bool HasRead(AppState state, TabId tab)
    {
        ArgumentNullException.ThrowIfNull(state);

        return tab switch
        {
            TabId.Queue => true,
            TabId.Flights => state.Flights is not null,
            TabId.Evidence => state.EvidenceVisible,
            TabId.Live => state.LiveVisible,
            TabId.Browse => state.BrowseVisible,
            TabId.Repositories => state.RepositoriesVisible,
            TabId.Checklist => state.ChecklistVisible,
            TabId.Envelope => state.EnvelopeVisible,
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "unknown tab"),
        };
    }

    /// <summary>
    /// The key that jumps to this tab, or null for the two that are always
    /// where a person already is.
    /// </summary>
    /// <remarks>
    /// <b>Read off this rather than typed onto a label.</b> The title is built
    /// from it and <c>EveryTabIsOnTheBarTests</c> checks the keymap resolves the
    /// same key to the same tab, so a tab cannot advertise a key that does
    /// nothing - which is worse than a tab with no key on it.
    /// </remarks>
    public static KeyStroke? KeyFor(TabId tab) => tab switch
    {
        TabId.Queue or TabId.Flights => null,
        TabId.Evidence => KeyStroke.Char('v'),
        TabId.Live => KeyStroke.Char('l'),
        TabId.Browse => KeyStroke.Char('b'),
        TabId.Repositories => KeyStroke.Char('r'),
        TabId.Checklist => KeyStroke.Char('p'),
        TabId.Envelope => KeyStroke.Char('e'),
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "unknown tab"),
    };

    /// <summary>
    /// What clicking this tab asks for, or null when it needs nothing asked.
    /// </summary>
    /// <remarks>
    /// <b>One path, whether a person clicked or typed.</b> Four of these are
    /// SHELL commands - showing that view is a read, and a UI session may not
    /// make one - so a click ends the session exactly as the key does, and the
    /// next session renders what came back. A tab bar that changed the model
    /// itself would be a second way to do one thing.
    /// </remarks>
    public static Command? CommandFor(TabId tab) => tab switch
    {
        TabId.Queue or TabId.Flights => null,
        TabId.Evidence => Command.ToggleEvidence,
        TabId.Live => Command.ToggleLive,
        TabId.Browse => Command.ToggleBrowse,
        TabId.Repositories => Command.ToggleRepositories,
        TabId.Checklist => Command.ToggleChecklist,
        TabId.Envelope => Command.ToggleEnvelope,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "unknown tab"),
    };

    /// <summary>
    /// What goes on the tab: its name, its key, and a mark when it is holding
    /// nothing yet.
    /// </summary>
    /// <remarks>
    /// <b>The key is on the tab because the hint line is one line.</b> Six of
    /// these keys were on it, saying what the bar now says, and the line is
    /// where a person finds the keys that have nowhere else to live.
    /// </remarks>
    public static string Title(AppState state, TabId tab)
    {
        ArgumentNullException.ThrowIfNull(state);

        var key = KeyFor(tab) is { } stroke ? $" {stroke.Name}" : "";

        // A DOT FOR A VIEW NOBODY HAS FETCHED, and nothing louder. It is a tab
        // that will read something when you go there, not a fault.
        var unread = HasRead(state, tab) ? "" : " ·";

        return $"{Name(tab)}{key}{unread}";
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

        return state.ActiveTab == tab;
    }

    /// <summary>The tab after the one showing, wrapping.</summary>
    /// <remarks>
    /// Over every tab, because every tab is on the bar. It used to walk the
    /// open ones so it could not land on an empty pane; a pane that says what
    /// it is waiting for is a better answer than a tab a person cannot reach.
    /// </remarks>
    public static TabId Next(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var open = All;
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
