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
    /// <summary>
    /// Begins watching one runner's flight, and returns without waiting.
    /// </summary>
    /// <remarks>
    /// <b>It answers whether it STARTED, never whether it connected.</b>
    /// Reaching a runner takes up to a heartbeat interval and its steps are
    /// worth watching, so waiting for it here would hold the console down over
    /// exactly the thing a person wants to see happening. Everything it has to
    /// say - each step, and why it stopped if it did - arrives in the pane.
    /// </remarks>
    public delegate bool Start(string runnerId, string flightId);

    /// <summary>Goes and watches, or says why it did not.</summary>
    public static AppState Watch(AppState state, Start start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(start);

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

        // THE ROW HAS A NUMBER AND THE PANE NEEDS AN ID, and the FLEET ANSWER is
        // where the two meet. It was the queue once, which is why watching drew
        // nothing: the queue holds flights that need somebody, and a flight
        // being watched is usually just flying.
        if (state.Runners?.Runners
                .FirstOrDefault(r => string.Equals(r.RunnerId, row.Id, StringComparison.Ordinal))
                ?.CurrentFlightId is not { Length: > 0 } flightId)
        {
            return state with
            {
                LastRunner =
                    $"{row.Label} is flying {flying} and the fleet does not say which flight "
                  + "that is, so there is nowhere to put what it says. Refresh and try again.",
            };
        }

        // STARTED AND NOT WAITED FOR. What the connect has to say - each step,
        // and why it stopped if it does - goes into the buffer the pane drains,
        // so a person watches it happen instead of watching a bare terminal
        // that stops existing the moment this returns.
        if (!start(row.Id, flightId))
        {
            return state with
            {
                LastRunner = $"This console is not configured to watch {row.Label}.",
            };
        }

        // THE PANE IS TOLD WHICH FLIGHT rather than left to infer it from a
        // cursor that cannot point at this one. Live is cleared because what
        // follows is one flight's account from its first step, and lines left
        // over from whatever was drawn before would sit above it unlabelled.
        return state with { Live = [] } with
        {
            // THE MODAL CLOSES, because the thing it was asked from is now
            // happening behind it and the pane it happens in is another tab.
            Mode = UiMode.Normal,
            WatchedFlightId = flightId,
            LiveVisible = true,
            ActiveTab = TabId.Live,
            LastRunner = $"Watching {flying} on {row.Label}. The pane says how it is going.",
        };
    }
}
