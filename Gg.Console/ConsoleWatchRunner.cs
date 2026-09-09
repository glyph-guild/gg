namespace Gg.Console;

/// <summary>
/// Connects to the runner under the cursor and leaves the live pane on its flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>The connect happens BETWEEN sessions and the watching happens during
/// one.</b> Reaching a runner is three calls to the control plane and a WebRTC
/// handshake, which a UI session may not do — so it is done here, with the
/// terminal free, where its steps can also be read. What survives into the
/// session is a buffer, and draining a buffer on the tick is the same shape as
/// reading a file.
/// </para>
/// <para>
/// <b>Which is why this costs no exception.</b> The rule is that a session may
/// read a local file and nothing else, and the reason is that everything else
/// happens with the terminal provably free. Both halves of that still hold.
/// </para>
/// <para>
/// <b>The first version ran <c>gg runner watch</c> as a child for the whole
/// watch.</b> That works and is what the editor does, but it is a watch a
/// person has to leave the console to have — and it turned out to be
/// unnecessary, because <c>LiveTails</c> was already built to take a source it
/// does not choose.
/// </para>
/// </remarks>
public static class ConsoleWatchRunner
{
    /// <summary>
    /// Reaches one runner about one flight, saying what it is doing.
    /// </summary>
    /// <remarks>
    /// The composition root's: the session token, the control plane and the
    /// pinned keys are all things this assembly may not hold. It answers
    /// whether a channel is open, and everything it wants to say about how it
    /// went it says through <paramref name="say"/> while it happens.
    /// </remarks>
    public delegate bool Connect(string runnerId, string flightId, Action<string> say);

    /// <summary>Goes and watches, or says why it did not.</summary>
    public static AppState Watch(AppState state, Connect connect, Action<string> say)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(say);

        if (Rows.Selected(state) is not { } row || row.Id.Length == 0)
        {
            return state with { LastRunner = "There is no runner under the cursor to watch." };
        }

        // THE SAME RULE THE KEY OBEYS, checked again here rather than trusted.
        // The hint line is derived in one place and dispatch happens in
        // another; a command that arrived anyway - a rebind, a modal that
        // outlived the row it was over - must not reach for a channel that
        // cannot exist and then blame the network for it.
        if (row.Work is not { Length: > 0 } flying)
        {
            return state with
            {
                LastRunner =
                    $"{row.Label} is flying nothing, so there is nothing to watch: a channel "
                  + "to a runner exists only while a flight does, which is what stops it "
                  + "being a standing way in.",
            };
        }

        // THE ROW HAS A NUMBER AND THE PANE NEEDS AN ID, and the queue is where
        // the two meet. A runner flying something this console has not loaded
        // yet is a connect with nowhere to put the output, which is a refusal
        // rather than a blank box.
        var at = state.Queue
            .Select((flight, index) => (flight, index))
            .Where(both => string.Equals(both.flight.FlightNumber, flying, StringComparison.Ordinal))
            .Select(both => (int?)both.index)
            .FirstOrDefault();

        if (at is not { } cursor)
        {
            return state with
            {
                LastRunner =
                    $"{row.Label} is flying {flying}, and this console has not loaded that "
                  + "flight yet, so there is nowhere to show what it says. Refresh and try "
                  + "again.",
            };
        }

        if (!connect(row.Id, state.Queue[cursor].FlightId, say))
        {
            // NO PANE OVER A CONVERSATION THAT NEVER HAPPENED. An open live view
            // with nothing in it is the box that means "the agent is working",
            // and putting it over a failed connect is the silence this whole
            // path keeps producing. Whatever went wrong was said through `say`,
            // on the terminal that was free at the time.
            return state with
            {
                LastRunner = $"Could not reach {row.Label}. What it got as far as is above.",
            };
        }

        // THE CURSOR MOVES TO WHAT IS BEING WATCHED. The live pane is bound to
        // the queue cursor, so a watch that connected and did not move it draws
        // whatever flight happened to be under it before - somebody else's.
        return state with
        {
            // THE MODAL CLOSES, because the thing it was asked from is now
            // happening behind it and the pane it happens in is another tab.
            Mode = UiMode.Normal,
            SelectedRow = cursor,
            LiveVisible = true,
            ActiveTab = TabId.Live,
            LastRunner = $"Watching {flying} on {row.Label}. It ends when the flight does.",
        };
    }
}
