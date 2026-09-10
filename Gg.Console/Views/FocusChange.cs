namespace Gg.Console.Views;

/// <summary>What should hold the keyboard focus after a render.</summary>
public enum FocusTarget
{
    /// <summary>Whoever has it now. A person put it there.</summary>
    LeaveAlone,

    /// <summary>The modal, which owns the keyboard while it is open.</summary>
    Modal,

    /// <summary>
    /// The log inside the runner modal, which is the part of it somebody
    /// scrolls.
    /// </summary>
    /// <remarks>
    /// The flight log's reason, one modal over: a modal made of widgets has to
    /// say WHICH widget, and what a person opened this to watch is a runner
    /// coming up - which is the log, live, while it does.
    /// </remarks>
    RunnerLog,

    /// <summary>
    /// The log inside the flight modal, which is the part of it with a cursor.
    /// </summary>
    /// <remarks>
    /// A modal made of widgets has to say WHICH widget, and the frame is not an
    /// answer - Terminal.Gui would pick the first focusable child, which is the
    /// intent, and an arrow key there scrolls a document instead of walking the
    /// history a person opened the modal to walk.
    /// </remarks>
    FlightLog,

    /// <summary>
    /// The airspace path field, which is not a modal and still owns the
    /// keyboard.
    /// </summary>
    /// <remarks>
    /// A field that accepts keystrokes has to hold the focus for as long as
    /// somebody is typing into it, and it sits inside a tab rather than in a
    /// dialog - so it is neither <see cref="Modal"/> nor <see cref="Tab"/>.
    /// </remarks>
    AirspacePath,

    /// <summary>The tab on screen, wherever that tab says focus lands.</summary>
    Tab,
}

/// <summary>
/// Whether a render should move the focus, and where.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, because the screen cannot be constructed and this has been wrong
/// twice.</b> First it moved focus on every render, which a once-a-second
/// countdown turned into a button that could be reached and not held. Then it
/// left focus alone whenever the tab's pane already had it - which is true the
/// moment a pane is shown, so arriving on a tab landed nowhere and Terminal.Gui
/// picked the first focusable child instead of the one the tab means.
/// </para>
/// <para>
/// <b>The question is whether the tab is new, not who holds the focus.</b> The
/// two readings agree on the render case and disagree on the arrival, which is
/// why the second version passed the first version's tests.
/// </para>
/// </remarks>
public static class FocusChange
{
    /// <param name="landed">
    /// The tab focus was last placed on, or null if it has not been placed -
    /// which is also how a modal holding it is recorded, so that closing one
    /// counts as a change again.
    /// </param>
    /// <param name="modalHasFocus">
    /// Whether the modal already holds it. A modal owns the keyboard, and
    /// re-asserting that every second would move a cursor inside it exactly as
    /// it moved one behind it.
    /// </param>
    /// <param name="pathHasFocus">
    /// Whether the airspace field already holds it. The same argument as its
    /// neighbour and a sharper case: re-asserting focus on a field somebody is
    /// typing into puts the cursor back at the start of the line, once a
    /// second, while they type.
    /// </param>
    public static FocusTarget Wanted(
        UiMode mode,
        TabId showing,
        TabId? landed,
        bool modalHasFocus,
        bool pathHasFocus = false) => (mode, landed) switch
    {
        // THE FIELD FIRST, because it is not a modal and the arms below would
        // hand it to one that is not on screen.
        (UiMode.AirspacePath, _) when pathHasFocus => FocusTarget.LeaveAlone,
        (UiMode.AirspacePath, _) => FocusTarget.AirspacePath,

        (not UiMode.Normal, _) when modalHasFocus => FocusTarget.LeaveAlone,

        // WHICH WIDGET, for the two modals that are made of several. The rest
        // are a few lines and two keys, and the frame is the whole of them.
        (UiMode.FlightDetail, _) => FocusTarget.FlightLog,
        (UiMode.Runner, _) => FocusTarget.RunnerLog,
        (not UiMode.Normal, _) => FocusTarget.Modal,
        (_, { } already) when already == showing => FocusTarget.LeaveAlone,
        _ => FocusTarget.Tab,
    };
}
