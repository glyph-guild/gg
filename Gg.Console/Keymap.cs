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
    TabId Showing = TabId.Queue,
    bool Frozen = false,
    bool Takeable = false,
    bool HandedBackable = false)
{
    /// <summary>
    /// Whether a code is already on the screen waiting to be approved.
    /// </summary>
    /// <remarks>
    /// The sign-in modal's two steps ask for opposite things - one asks the
    /// control plane for a code, the other says a person has approved it - and
    /// this is what tells them apart. It is in the CONTEXT rather than read off
    /// the model, like everything else here, so the hints and the dispatch
    /// cannot disagree about which step is showing.
    /// </remarks>
    public bool SignInStarted { get; init; }

    /// <summary>
    /// The context a model puts the console in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One derivation, because the screen kept a literal beside the
    /// model.</b> That literal carried the mode, the tab and the freeze, and
    /// not <see cref="Takeable"/>, <see cref="HandedBackable"/> or which step
    /// the sign-in modal is on - so <c>Catalogue</c> can name a key the screen
    /// would refuse to resolve. Two of those are invisible only because nothing
    /// in production sets <c>TakeableTree</c> or <c>TakenOver</c> yet, which
    /// makes them a trap rather than a defect: the day one is set is the worst
    /// day to find out.
    /// </para>
    /// <para>
    /// The mode comes from the model too, so a caller wanting the keys of a
    /// DIFFERENT mode says so with a <c>with</c> rather than by rebuilding
    /// this.
    /// </para>
    /// </remarks>
    public static KeymapContext For(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new KeymapContext(
            state.Mode,
            state.ActiveTab,
            state.Frozen,
            state.TakeableTree is not null,
            state.TakenOver)
        {
            // Which of the sign-in modal's two steps is showing. Both live in
            // one mode, so this is the only thing that tells them apart.
            SignInStarted = state.SignIn is not null,
        };
    }
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
    /// Bound, and kept off the hint line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE LINE IS ONE LINE, and every slot spent is one a key nobody knows
    /// could have had.</b> Three kinds of key are off it and each has somewhere
    /// else to be found: the six tab keys are printed on their own tabs, the
    /// two credential keys are in help and used about twice a year, and j and k
    /// are what the arrows already do.
    /// </para>
    /// <para>
    /// <b>Off the LINE, not out of the program.</b> <see cref="Keymap.Resolve"/>
    /// does not look at this, and <c>KeymapTests</c> still proves advertised
    /// keys and live keys are one set. The help page shows these; see
    /// <see cref="Untaught"/> for the only two it does not.
    /// </para>
    /// </remarks>
    public bool OffTheHintLine { get; init; }

    /// <summary>
    /// Not on the help page either.
    /// </summary>
    /// <remarks>
    /// <b>ONLY j AND k, and only because the arrows do the same thing.</b> Two
    /// properties rather than one because they are two different claims, and
    /// collapsing them cost something: when the six tab keys were marked as
    /// merely hidden, the help page - which exists to name EVERY key - stopped
    /// naming them, and the test that says it names every key was iterating the
    /// same flag, so it passed while the page was wrong. A key is taken off the
    /// line because it is advertised elsewhere; it is taken out of help only
    /// when there is another way to do the thing itself.
    /// </remarks>
    public bool Untaught { get; init; }
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

        // THE ONE MODAL A KEY DOES NOT OPEN, and it owns the keyboard exactly
        // like the ones that do. Without an arm here it fell through to Normal
        // mode and offered every key in the console - fly, take over, forget a
        // credential - to a person the control plane will refuse, over a queue
        // that is empty because nobody is signed in.
        //
        // Escaping is a real answer rather than a dismissal: somebody who wants
        // to look at an empty console, or who opened gg to read the help, is
        // allowed to.
        //
        // TWO STEPS, TWO KEYS, AND NOT THE SAME ONE TWICE. ConfirmFlight's
        // rule: the key that asks for a code is not the key that says the code
        // was approved, because one key for both is a double-press away from
        // waiting on something nobody was shown.
        UiMode.SignIn => context.SignInStarted
            ?
            [
                // WHEN, because it is not live in the plainest form of this
                // mode and the help page is a union over every context. Read
                // there without it, `y sign in` and `a I have approved it` sit
                // together as a contradiction rather than as two steps.
                new(KeyStroke.Char('a'), Command.SignIn, "I have approved it")
                {
                    When = "once a code is showing",
                },
                new(KeyStroke.Esc, Command.CloseModal, "give up"),
            ]
            :
            [
                new(KeyStroke.Char('y'), Command.SignIn, "sign in"),
                new(KeyStroke.Esc, Command.CloseModal, "carry on signed out"),
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
            new(KeyStroke.TabKey, Command.FocusNextPane, "next tab"),
            // BOUND AND NOT TAUGHT. See KeyBinding.Hidden: the arrows do this
            // through the list widget, so the hint line's slots go to keys a
            // person has no other way to find.
            new(KeyStroke.Char('j'), Command.SelectNext, "down")
                { OffTheHintLine = true, Untaught = true },
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up")
                { OffTheHintLine = true, Untaught = true },
            new(KeyStroke.Char('v'), Command.ToggleEvidence,
                Closes(context, TabId.Evidence, "evidence")) { OffTheHintLine = true },
            // WHAT A SECOND PRESS WILL DO, and under tabs that is "close" only
            // while you are looking at it. A key that said "hide" for an open
            // tab you had switched away from would advertise a close that does
            // not happen - the key brings it forward instead.
            //
            // HIDDEN BECAUSE THE TAB SAYS IT. Each of these six is on its own
            // tab in the bar, with its key on the label - so the hint line,
            // which is one line, keeps only the keys that have nowhere else to
            // be advertised. Bound, not advertised twice: the rule
            // KeyBinding.Hidden was written for.
            new(KeyStroke.Char('l'), Command.ToggleLive, Closes(context, TabId.Live, "live"))
                { OffTheHintLine = true },
            new(KeyStroke.Char('b'), Command.ToggleBrowse, Closes(context, TabId.Browse, "browse"))
                { OffTheHintLine = true },
            new(KeyStroke.Char('r'), Command.ToggleRepositories,
                Closes(context, TabId.Repositories, "repositories")) { OffTheHintLine = true },
            // `p` for plan, which is the verb it calls.
            new(KeyStroke.Char('p'), Command.ToggleChecklist,
                Closes(context, TabId.Checklist, "checklist")) { OffTheHintLine = true },
            // `e` for envelope, which is the noun and the verb it calls.
            new(KeyStroke.Char('e'), Command.ToggleEnvelope,
                Closes(context, TabId.Envelope, "envelope")) { OffTheHintLine = true },
            // ONE KEY, TWO MEANINGS, AND THE TAB DECIDES WHICH. This was two
            // booleans with an explicit precedence between them, because live
            // and browse shared a region: both flags on was a state the console
            // could not reach and the pure function could still be handed.
            // Exactly one tab is showing, so the ambiguity is gone by
            // construction rather than by a rule somebody has to maintain.
            .. context.Showing == TabId.Live
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.ToggleFreeze,
                    context.Frozen ? "unfreeze" : "freeze to copy")
                    { When = "while the live tab is showing" }]
                : [],
            .. context.Showing == TabId.Browse
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.FlyPicked, "fly this")
                    { When = "while the browse tab is showing" }]
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
            // THE TWO CREDENTIAL KEYS ARE IN HELP AND NOT ON THE LINE. Adding
            // and forgetting a credential is a thing a person does when they
            // set the tenant up and then about twice a year, and it was
            // spending two of the line's slots every second of every session.
            new(KeyStroke.Char('c'), Command.AddCredential, "add credential")
                { OffTheHintLine = true },
            // `x` for forget, because `f` is freeze and fly-this and `r` is
            // reject. A store you cannot clean is a store people work around.
            new(KeyStroke.Char('x'), Command.ForgetCredential, "forget credential")
                { OffTheHintLine = true },
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
            .Where(b => !b.OffTheHintLine)
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
        // the state it will change - "browse" and "close browse" are one
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
    /// Every tab crossed with the three flags that change what is bound. The
    /// union has to be complete rather than reachable: this is a pure function
    /// over a struct, and a shape left out shows up as a key missing from the
    /// help page, which is what HelpNamesEveryKeyTests asserts.
    /// </remarks>
    private static IEnumerable<KeymapContext> Shapes(UiMode mode) =>
        // ORDER IS THE PAGE'S ORDER. The plainest shape comes first - the queue
        // tab, nothing frozen, nothing to take - so the keys that always work
        // are listed first and in the order they are written.
        from showing in Enum.GetValues<TabId>()
        from frozen in (bool[])[false, true]
        from takeable in (bool[])[false, true]
        from handedBack in (bool[])[false, true]
        // THE SIGN-IN MODAL'S TWO STEPS, which are two sets of keys behind one
        // mode. Left out, `a` resolved in the running console and appeared on
        // no page - a key nobody could discover, which is the thing this
        // catalogue exists to prevent.
        from signInStarted in (bool[])[false, true]
        select new KeymapContext(mode, showing, frozen, takeable, handedBack)
        {
            SignInStarted = signInStarted,
        };

    /// <summary>
    /// A toggle's description: what a second press will do from here.
    /// </summary>
    /// <remarks>
    /// "close" only while the tab is the one showing, because that is the only
    /// place the key closes anything. From another tab it brings this one
    /// forward, and advertising a close that does not happen is how a person
    /// learns to stop trusting the line.
    /// </remarks>
    private static string Closes(KeymapContext context, TabId tab, string name) =>
        context.Showing == tab ? $"close {name}" : name;
}
