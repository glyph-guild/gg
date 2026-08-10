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
public readonly record struct KeymapContext(UiMode Mode, bool LiveVisible = false, bool Frozen = false);

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
            new(KeyStroke.Esc, Command.CloseModal, "close help"),
        ],

        UiMode.FlightActions =>
        [
            new(KeyStroke.Esc, Command.CloseModal, "close"),
        ],

        _ =>
        [
            new(KeyStroke.Char('q'), Command.Quit, "quit"),
            new(KeyStroke.Char('?'), Command.ToggleHelp, "help"),
            new(KeyStroke.Char('a'), Command.ToggleFlightActions, "actions"),
            new(KeyStroke.TabKey, Command.FocusNextPane, "switch pane"),
            new(KeyStroke.Char('j'), Command.SelectNext, "down"),
            new(KeyStroke.Char('k'), Command.SelectPrevious, "up"),
            new(KeyStroke.Char('v'), Command.ToggleEvidence, "evidence"),
            new(KeyStroke.Char('l'), Command.ToggleLive, context.LiveVisible ? "hide live" : "live"),
            .. context.LiveVisible
                ? (KeyBinding[])[new(KeyStroke.Char('f'), Command.ToggleFreeze,
                    context.Frozen ? "unfreeze" : "freeze to copy")]
                : [],
            new(KeyStroke.Char('e'), Command.OpenEditor, "edit notes"),
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
