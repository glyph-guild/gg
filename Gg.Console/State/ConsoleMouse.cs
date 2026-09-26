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
    /// text selection was the terminal taking its own back. A click that misses
    /// the modal is dropped instead; see <see cref="SwallowedWhile"/>.
    /// </remarks>
    public static bool OursWhile(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return !state.Frozen;
    }

    /// <summary>Whether a click at this point is dropped rather than routed,
    /// because a modal is open and the click missed it.</summary>
    /// <remarks>
    /// <para>
    /// <b>Because focusing a covered view ends the process.</b> A click on what
    /// a modal is drawn over asks Terminal.Gui to focus a view nobody can see,
    /// and its own post-condition throws: <c>FocusChanging was not cancelled
    /// and the HasFocus value did not change</c>. Reported from a live console,
    /// reproduced in a pty at the exact cell, and still present in 2.5.0.
    /// </para>
    /// <para>
    /// <b>Dropped before routing, rather than disabling what it covers.</b>
    /// Two other mechanisms were tried against the live console and both are
    /// wrong, each for its own reason:
    /// </para>
    /// <para>
    /// <c>Enabled</c> reads like the answer - the library's hit test declines
    /// to descend into a subview that is not enabled, which looks like a
    /// subtree exclusion in one assignment. It is not one assignment. The
    /// setter walks every descendant itself, and on the way it grants or drops
    /// focus, and focus moves a view within its parent's list. So the setter
    /// throws out of its own <c>foreach</c>, from inside the library, and the
    /// console goes with it. Snapshotting our own walk does not help: the walk
    /// that throws is theirs.
    /// </para>
    /// <para>
    /// <c>ViewportSettingsFlags.TransparentMouse</c> reads like the answer too,
    /// and is not: it is applied per view rather than per subtree, and per CELL
    /// at that - a view that drew at the point keeps the click, and its
    /// children are never excluded.
    /// </para>
    /// <para>
    /// What is left is the application's own pre-routing event, which is raised
    /// with a screen position before any view is consulted and stops when it is
    /// marked handled. It changes nothing about any view, so nothing is dimmed,
    /// no focus moves, and there is no list to invalidate.
    /// </para>
    /// </remarks>
    /// <param name="state">The console's state, which says whether a modal is drawn.</param>
    /// <param name="overTheModal">Whether the point is inside the modal's own frame.</param>
    public static bool SwallowedWhile(AppState state, bool overTheModal)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Modals.IsDrawn(state.Mode) && !overTheModal;
    }
}
