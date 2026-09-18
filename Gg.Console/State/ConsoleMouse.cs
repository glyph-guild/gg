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
    public static bool OursWhile(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return !state.Frozen && !Modals.IsDrawn(state.Mode);
    }
}
