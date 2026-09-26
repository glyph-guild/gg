namespace Gg.Console;

/// <summary>
/// Whose the mouse is: this console's, or the terminal it is running in.
/// </summary>
/// <remarks>
/// <para>
/// <b>A modal owns the keyboard, and this is the rest of that sentence.</b>
/// While one is open, a click lands on whatever the modal is drawn over -
/// and asking Terminal.Gui to focus a view that is covered ends the process:
/// <c>FocusChanging was not cancelled and the HasFocus value did not change</c>,
/// out of the library's own title command, reported from a live console and
/// reproduced in a pty at the exact cell.
/// </para>
/// <para>
/// <b>Stated here rather than in the view, so it is one answer.</b> The screen
/// hands the mouse over and takes it back; what decides is a function of the
/// model, like every other question this console answers.
/// </para>
/// <para>
/// <b>And the frozen screen keeps its own reason.</b> Freezing was the first
/// thing that needed the terminal's selection back; a modal needs it for a
/// different reason and gets it the same way.
/// </para>
/// </remarks>
public static class ConsoleMouse
{
    /// <summary>Whether gg should be receiving mouse events at all.</summary>
    /// <remarks>
    /// <b>Freezing only, now.</b> A modal used to take the mouse away too, and
    /// that is what made every modal unclickable - the pointer turning into a
    /// text selection was the terminal taking its own back. What a modal covers
    /// is disabled instead; see <see cref="CoveredWhile"/>.
    /// </remarks>
    public static bool OursWhile(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return !state.Frozen;
    }

    /// <summary>Whether the panes behind a modal must stop taking clicks.</summary>
    /// <remarks>
    /// <para>
    /// <b>Because focusing a covered view ends the process.</b> A click on what
    /// a modal is drawn over asks Terminal.Gui to focus a view nobody can see,
    /// and its own post-condition throws: <c>FocusChanging was not cancelled
    /// and the HasFocus value did not change</c>. Reported from a live console,
    /// reproduced in a pty at the exact cell, and still present in 2.5.0.
    /// </para>
    /// <para>
    /// <b>Disabled rather than transparent.</b> The library's hit test declines
    /// to descend into a subview that is not <c>Enabled</c>, which takes the
    /// whole subtree out in one place. <c>ViewportSettingsFlags.TransparentMouse</c>
    /// reads like the right answer and is not: it is applied per view rather
    /// than per subtree, and per CELL at that - a view that drew at the point
    /// keeps the click, and its children are never excluded.
    /// </para>
    /// <para>
    /// <b>It dims what it disables</b>, because Terminal.Gui draws a disabled
    /// view in <c>VisualRole.Disabled</c>. That is a visible change and it is
    /// the price of the modal being clickable at all.
    /// </para>
    /// </remarks>
    public static bool CoveredWhile(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Modals.IsDrawn(state.Mode);
    }
}
