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
public readonly record struct KeyBinding(KeyStroke Key, Command Command, string Description);

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
            new(KeyStroke.Char('j'), Command.SelectNext, "down"),
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up"),
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
                    context.Frozen ? "unfreeze" : "freeze to copy")]
                : [],
            // THE SAME KEY, AND THEY CANNOT BOTH BE OFFERED. Browse and live
            // share one region and BrowseToggled turns the other off, so f is
            // free while browsing - and 'fly' is what it should mean there.
            // Asserted rather than reasoned about: a third pane over that
            // region would break this silently otherwise.
            .. context.BrowseVisible
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.FlyPicked, "fly this")]
                : [],
            // Only offered when there is something to take. A key advertised
            // against a flight with no held tree is a key that does nothing, and
            // the hints come from the same context dispatch does so the two
            // cannot drift.
            .. context.Takeable
                ? (KeyBinding[])[new(KeyStroke.Char('t'), Command.TakeFlight, "take over")]
                : [],
            // Only after somebody has taken it. Handing back a flight nobody
            // took is a key that does nothing, and the hints come from the same
            // context dispatch does.
            .. context.HandedBackable
                ? (KeyBinding[])[new(KeyStroke.Char('h'), Command.HandBack, "hand back")]
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
        string.Join(" · ", Bindings(context).Select(b => $"{b.Key.Name} {b.Description}"));
}
