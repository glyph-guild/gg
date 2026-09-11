using System.Diagnostics;

namespace Gg.Console;

/// <summary>
/// Puts what a modal is showing on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SHELL'S, BECAUSE A CLIPBOARD IS A CHILD PROCESS.</b>
/// <c>LiveStreamingTests</c> grants this console one clipboard exception and
/// states its limits: <i>"scoped to a clipboard READ, in one field, by one
/// key… and no second spawn: the console's other clipboard use — copying a
/// sign-in link — stays the shell's."</i> This is that second use, so it ends
/// the session and runs with the terminal free.
/// </para>
/// <para>
/// <b>Paying the round trip is cheap here and was not there.</b> The paste
/// exception exists because releasing the terminal mid-edit throws away a
/// half-typed field. A modal has nothing half-typed in it: the screen is
/// rebuilt from <c>AppState</c> and <c>Mode</c> is part of it, so the modal
/// comes back on its own and a copy costs a redraw.
/// </para>
/// <para>
/// <b>One platform table, and it is <see cref="ConsoleLink"/>'s.</b> A second
/// pbcopy/xclip/clip switch would be a copy of that to keep in agreement — the
/// same argument the paste makes for going through the driver rather than
/// rolling its own.
/// </para>
/// </remarks>
public static class ConsoleClipboard
{
    /// <param name="text">
    /// What the modal is showing, from <c>PaneText.Modal</c> — the producer the
    /// screen itself draws from.
    /// </param>
    /// <param name="start">
    /// Runs the child, writing the second argument to its standard input, and
    /// answers its exit code. A delegate so this can be asserted without
    /// spawning anything.
    /// </param>
    public static AppState Copied(
        AppState state, string text, Func<ProcessStartInfo, string?, int> start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(start);

        // AN EMPTY MODAL IS NOT A COPY. "Copied to the clipboard" about
        // nothing is a sentence describing an act that did not happen, and the
        // person would paste and find their previous clipboard.
        if (text.Trim().Length == 0)
        {
            return state with
            {
                LastEstate = "There is nothing on screen to copy.",
            };
        }

        return state with
        {
            LastEstate = ConsoleLink.Copied(text, start) switch
            {
                null => "On the clipboard.",
                var why => "The clipboard would not take it: " + why,
            },
        };
    }
}
