namespace Gg.Console.Views;

/// <summary>What should hold the keyboard focus after a render.</summary>
public enum FocusTarget
{
    /// <summary>Whoever has it now. A person put it there.</summary>
    LeaveAlone,

    /// <summary>The modal, which owns the keyboard while it is open.</summary>
    Modal,

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
    public static FocusTarget Wanted(
        UiMode mode, TabId showing, TabId? landed, bool modalHasFocus) => (mode, landed) switch
    {
        (not UiMode.Normal, _) => modalHasFocus ? FocusTarget.LeaveAlone : FocusTarget.Modal,
        (_, { } already) when already == showing => FocusTarget.LeaveAlone,
        _ => FocusTarget.Tab,
    };
}
