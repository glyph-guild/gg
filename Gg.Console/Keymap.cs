namespace Gg.Console;

/// <summary>
/// One keystroke, structurally. No Terminal.Gui types.
/// </summary>
/// <remarks>
/// The keymap stays a pure function testable without a terminal, and this is
/// the type that makes "the set of keys that do something" a set you can
/// actually compute - which is what turns the hints check from a sampling into
/// an equality.
/// </remarks>
public readonly record struct KeyStroke(char? Input, bool Ctrl = false, bool Escape = false, bool Tab = false)
{
    public static KeyStroke Char(char input) => new(input);

    public static KeyStroke Control(char input) => new(input, Ctrl: true);

    public static KeyStroke Esc { get; } = new(null, Escape: true);

    public static KeyStroke TabKey { get; } = new(null, Tab: true);

    /// <summary>How this key is written where a person will read it.</summary>
    public string Name =>
        Escape ? "esc"
        : Tab ? "tab"
        : Ctrl ? $"ctrl+{Input}"
        : Input?.ToString() ?? "?";
}

/// <summary>What a key means right now.</summary>
/// <remarks>
/// Everything the keymap dispatches on. The hints are generated from the SAME
/// value, so an advertised key cannot drift from a live one - there is no
/// second input to disagree about.
/// </remarks>
public readonly record struct KeymapContext(
    UiMode Mode,
    bool LiveVisible = false,
    bool Frozen = false,
    bool Takeable = false,
    bool HandedBackable = false)
{
    /// <summary>Whether the browse pane is already showing.</summary>
    /// <remarks>
    /// An init property rather than another positional parameter: the record
    /// already takes five, and a sixth bool that callers pass by position is a
    /// swap waiting to happen.
    /// </remarks>
    public bool BrowseVisible { get; init; }

    /// <summary>Whether the repositories pane is already showing.</summary>
    public bool RepositoriesVisible { get; init; }

    /// <summary>Whether the checklist pane has the region.</summary>
    public bool ChecklistVisible { get; init; }

    /// <summary>Whether the envelope pane has the region.</summary>
    public bool EnvelopeVisible { get; init; }
}

/// <summary>One binding: a key, what it does, and how to describe it.</summary>
public readonly record struct KeyBinding(KeyStroke Key, Command Command, string Description)
{
    /// <summary>
    /// When this key applies, for one that does not always.
    /// </summary>
    /// <remarks>
    /// <b>Because the help page is a union over every context.</b> Listed
    /// without this, <c>f</c> appears twice with two meanings and nothing
    /// saying which is which - a contradiction rather than a condition. Null
    /// for a key that is always live in its own mode, and
    /// <c>HelpNamesEveryKeyTests</c> asserts the two cannot be confused: a
    /// binding that does not resolve in the plainest form of its own mode has
    /// to say when it does.
    /// </remarks>
    public string? When { get; init; }

    /// <summary>
    /// Bound, and deliberately not advertised.
    /// </summary>
    /// <remarks>
    /// <b>ONLY j AND k, and only because there is a second way to do it.</b>
    /// The arrows move the queue through the list widget, so a person who never
    /// learned vim already has the key - and the hint line is one line, where
    /// every slot spent is one a key nobody knows could have had. Hidden is
    /// about the page, never about the keyboard: <see cref="Keymap.Resolve"/>
    /// does not look at this, and <c>KeymapTests</c> still proves advertised
    /// keys and live keys are the same set.
    /// </remarks>
    public bool Hidden { get; init; }
}

/// <summary>One catalogue entry: a binding and the mode it belongs to.</summary>
public readonly record struct KeyCatalogueEntry(UiMode Mode, KeyBinding Binding);

/// <summary>
/// The keymap. Pure, total, and the only place bindings live.
/// </summary>
/// <remarks>
/// <see cref="Bindings"/> is the single source: <see cref="Resolve"/> looks up
/// in it and <see cref="Hints"/> renders it. Written as two lists that agreed
/// with each other, they would agree until somebody added a key to one - which
/// is the drift the discipline exists to prevent, so there is one list.
/// </remarks>
public static class Keymap
{
    /// <summary>Quits from anywhere, including a modal. The last resort, not the escape hatch.</summary>
    public static KeyStroke Interrupt { get; } = KeyStroke.Control('c');

    /// <summary>
    /// Every binding live in this context.
    /// </summary>
    /// <remarks>
    /// A modal returns ONLY its own bindings, which is what "modals own the
    /// keyboard" means concretely: nothing underneath is reachable while one is
    /// open, so no key can act on a flight the person cannot currently see.
    /// </remarks>
    public static IReadOnlyList<KeyBinding> Bindings(KeymapContext context) => context.Mode switch
    {
        UiMode.Help =>
        [
            // TURNS THE PAGE, and it has to be BOUND rather than merely handled
            // by the reducer. The pane's own text tells a person to press it;
            // for one release it resolved to nothing, because the reducer had
            // an arm and this list did not, and every test called the reducer
            // directly. A key advertised and bound to nothing is the shape
            // ShellHandledTests exists for, one modal down.
            new(KeyStroke.TabKey, Command.FocusNextPane, "keys / environment"),
            new(KeyStroke.Esc, Command.CloseModal, "close help"),
        ],

        // OWNS THE KEYBOARD. Only these three resolve while it is open, so a key that
        // means something in Normal mode cannot reach through and act on the flight
        // behind the modal - and exactly one of them is a way out, which decides nothing.
        UiMode.GateDecision =>
        [
            new(KeyStroke.Char('a'), Command.ApproveGate, "approve"),
            new(KeyStroke.Char('r'), Command.RejectGate, "reject"),
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        // A CONFIRMATION IS A MODAL LIKE ANY OTHER: it captures the keyboard,
        // it has exactly one escape hatch, and escaping is a real answer. The
        // confirming key is deliberately NOT the key that opened it - f twice
        // in quick succession is the accident this whole question exists to
        // catch.
        UiMode.ConfirmFlight =>
        [
            new(KeyStroke.Char('y'), Command.FlyPicked, "open a second flight"),
            new(KeyStroke.Esc, Command.CloseModal, "leave it alone"),
        ],

        UiMode.FlightActions =>
        [
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        _ =>
        [
            new(KeyStroke.Char('q'), Command.Quit, "quit"),
            // `g` for "get again". `r` is reject inside the gate modal and `R`
            // would be the only capital in the map, which is a shape somebody
            // has to learn rather than read.
            new(KeyStroke.Char('g'), Command.Refresh, "refresh"),
            new(KeyStroke.Char('?'), Command.ToggleHelp, "help"),
            new(KeyStroke.Char('a'), Command.ToggleFlightActions, "actions"),
            new(KeyStroke.Char('d'), Command.OpenGate, "decide"),
            new(KeyStroke.TabKey, Command.FocusNextPane, "switch pane"),
            // BOUND AND NOT TAUGHT. See KeyBinding.Hidden: the arrows do this
            // through the list widget, so the hint line's slots go to keys a
            // person has no other way to find.
            new(KeyStroke.Char('j'), Command.SelectNext, "down") { Hidden = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up") { Hidden = true },
            new(KeyStroke.Char('v'), Command.ToggleEvidence, "evidence"),
            new(KeyStroke.Char('l'), Command.ToggleLive, context.LiveVisible ? "hide live" : "live"),
            new(KeyStroke.Char('b'), Command.ToggleBrowse,
                context.BrowseVisible ? "hide browse" : "browse"),
            new(KeyStroke.Char('r'), Command.ToggleRepositories,
                context.RepositoriesVisible ? "hide repositories" : "repositories"),
            // `p` for plan, which is the verb it calls.
            new(KeyStroke.Char('p'), Command.ToggleChecklist,
                context.ChecklistVisible ? "hide checklist" : "checklist"),
            // `e` for envelope, which is the noun and the verb it calls.
            new(KeyStroke.Char('e'), Command.ToggleEnvelope,
                context.EnvelopeVisible ? "hide envelope" : "envelope"),
            // TOTAL RATHER THAN TRUSTING THE REDUCER. BrowseToggled turns live
            // off, so both flags on is a state the console cannot reach - but
            // Bindings is a pure function that can be handed any context, and a
            // key with two meanings in one list resolves to whichever was
            // written first. So browse wins here explicitly, and a test over
            // every combination says it stays that way.
            .. context.LiveVisible && !context.BrowseVisible
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.ToggleFreeze,
                    context.Frozen ? "unfreeze" : "freeze to copy")
                    { When = "while the live pane is showing" }]
                : [],
            // THE SAME KEY, AND THEY CANNOT BOTH BE OFFERED. Browse and live
            // share one region and BrowseToggled turns the other off, so f is
            // free while browsing - and 'fly' is what it should mean there.
            // Asserted rather than reasoned about: a third pane over that
            // region would break this silently otherwise.
            .. context.BrowseVisible
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.FlyPicked, "fly this")
                    { When = "while browsing" }]
                : [],
            // Only offered when there is something to take. A key advertised
            // against a flight with no held tree is a key that does nothing, and
            // the hints come from the same context dispatch does so the two
            // cannot drift.
            .. context.Takeable
                ? (KeyBinding[])[new(KeyStroke.Char('t'), Command.TakeFlight, "take over")
                    { When = "when the flight has a tree somebody is holding" }]
                : [],
            // Only after somebody has taken it. Handing back a flight nobody
            // took is a key that does nothing, and the hints come from the same
            // context dispatch does.
            .. context.HandedBackable
                ? (KeyBinding[])[new(KeyStroke.Char('h'), Command.HandBack, "hand back")
                    { When = "after you have taken it" }]
                : [],

            // TENANT-LEVEL WRITES, in Normal mode only. A modal holds the keyboard
            // while it is open, and one of these reachable from a gate decision
            // would be a key doing something unrelated to the question on screen.
            new(KeyStroke.Char('n'), Command.OpenFlight, "new flight"),
            new(KeyStroke.Char('c'), Command.AddCredential, "add credential"),
            // `x` for forget, because `f` is freeze and fly-this and `r` is
            // reject. A store you cannot clean is a store people work around.
            new(KeyStroke.Char('x'), Command.ForgetCredential, "forget credential"),
            new(KeyStroke.Char('i'), Command.Invite, "invite"),
            // `y` because every letter in `fly by hand` is taken: f is freeze
            // and fly-this, l is nothing yet but reads as live, b is browse, h
            // is hand back, a and n and d are taken. A key chosen for its
            // mnemonic and then silently shadowing another is worse than one
            // chosen for being free and said to be.
            new(KeyStroke.Char('y'), Command.FlyByHand, "fly by hand"),
        ],
    };

    /// <summary>
    /// What a keystroke does here, or nothing.
    /// </summary>
    /// <remarks>
    /// Ctrl-C is handled ahead of the table and in every mode. A modal that
    /// could swallow it would be a modal that can trap the terminal, which is
    /// the failure the escape hatch exists to make impossible - this is the
    /// belt to that braces.
    /// </remarks>
    public static Command? Resolve(KeyStroke key, KeymapContext context)
    {
        if (key == Interrupt)
        {
            return Command.Quit;
        }

        foreach (var binding in Bindings(context))
        {
            if (binding.Key == key)
            {
                return binding.Command;
            }
        }

        return null;
    }

    /// <summary>
    /// The one key that leaves the modal in this context, or null outside one.
    /// </summary>
    /// <remarks>
    /// Named rather than assumed, so the property test can ask the keymap what
    /// its escape hatch is instead of hard-coding a guess and proving the
    /// guess.
    /// </remarks>
    public static KeyStroke? EscapeHatch(KeymapContext context) =>
        context.Mode == UiMode.Normal ? null : KeyStroke.Esc;

    /// <summary>
    /// The status line, rendered from the bindings that are live.
    /// </summary>
    /// <remarks>
    /// Generated from <see cref="Bindings"/> rather than written alongside it.
    /// A hand-written hint string is a second list, and a second list drifts.
    /// </remarks>
    public static string Hints(KeymapContext context) =>
        string.Join(" · ", Bindings(context)
            .Where(b => !b.Hidden)
            .Select(b => $"{b.Key.Name} {b.Description}"));

    /// <summary>
    /// Every key this console answers, in any context, once each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE HELP PAGE ASKS A DIFFERENT QUESTION FROM THE HINT LINE.</b> The
    /// line shows what is live, which is right: an advertised key that does
    /// nothing teaches a person the console is broken. The page is where
    /// somebody looks for a key they do not know, and it was showing one
    /// context's bindings - so <c>f</c> was absent whenever neither the live
    /// pane nor browse was showing, and the gate modal's <c>a</c> and <c>r</c>
    /// were never on it at all.
    /// </para>
    /// <para>
    /// <b>A union over the contexts rather than a second list.</b> Written out
    /// by hand this would be the third list of keys in the program, after
    /// <see cref="Bindings"/> and the hint line - and the one people read when
    /// they are already confused, so the one that must not drift. The contexts
    /// enumerated here are every mode crossed with every flag that changes what
    /// is bound; a flag that changed the set and was left out would show up as
    /// a key missing from the page, which is what
    /// <c>HelpNamesEveryKeyTests</c> asserts.
    /// </para>
    /// <para>
    /// Ordered by mode, then by the order <see cref="Bindings"/> writes them,
    /// so the page reads in the order somebody wrote it rather than in
    /// dictionary order.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<KeyCatalogueEntry> Catalogue()
    {
        var entries = new List<KeyCatalogueEntry>();

        // BY WHAT IT DOES, NOT BY WHAT IT SAYS. A toggle's description reads
        // the state it will change - "browse" and "hide browse" are one
        // binding - so keying this on the description put every toggle on the
        // page twice with the two halves of a sentence. Keyed on the command,
        // the first context wins, and the first context is the plainest one.
        //
        // `f` still appears twice, and should: it is two commands over one key,
        // and each says when it applies.
        var seen = new HashSet<(UiMode Mode, KeyStroke Key, Command Command)>();

        foreach (var mode in Enum.GetValues<UiMode>())
        {
            foreach (var context in Shapes(mode))
            {
                foreach (var binding in Bindings(context))
                {
                    if (seen.Add((mode, binding.Key, binding.Command)))
                    {
                        entries.Add(new KeyCatalogueEntry(mode, binding));
                    }
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Every shape of context that can change what is bound in one mode.
    /// </summary>
    /// <remarks>
    /// The five flags are not independent - browse and live share a region, so
    /// both on is a state the console cannot reach - but this is a pure
    /// function over a struct and the union has to be complete rather than
    /// reachable. Enumerating the product costs thirty-two calls to a
    /// list-builder, once, when somebody opens help.
    /// </remarks>
    private static IEnumerable<KeymapContext> Shapes(UiMode mode) =>
        // ORDER IS THE PAGE'S ORDER. The all-false shape comes first, so the
        // keys that always work are listed first and in the order they are
        // written; the flags after it are ordered so the conditional keys read
        // in the order somebody meets them - browse and live before the two
        // that depend on what a flight is doing.
        from browse in (bool[])[false, true]
        from live in (bool[])[false, true]
        from frozen in (bool[])[false, true]
        from takeable in (bool[])[false, true]
        from handedBack in (bool[])[false, true]
        select new KeymapContext(mode, live, frozen, takeable, handedBack)
        {
            BrowseVisible = browse,
        };
}
