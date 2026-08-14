namespace Gg.Console;

/// <summary>
/// Every change to the model, as a pure function.
/// </summary>
/// <remarks>
/// Nothing here touches a terminal, a clock or a socket, which is why the
/// interaction disciplines can be tested at all. "Arrivals do not move the
/// cursor" is a claim about this file and nowhere else.
/// </remarks>
public static class Reducer
{
    public static AppState Reduce(AppState state, Command command)
    {
        ArgumentNullException.ThrowIfNull(state);

        return command switch
        {
            Command.ToggleHelp => Modal(state, UiMode.Help),
            Command.ToggleFlightActions => Modal(state, UiMode.FlightActions),
            Command.OpenGate => Modal(state, UiMode.GateDecision),

            // ANSWERING POSTS; IT DOES NOT DECIDE. Both answers leave the state exactly as
            // it is: the loop sends the decision, the control plane records it, the Engine
            // re-evaluates, and what comes back is what closes this modal. A reducer that
            // closed it here would be the console deciding - Article IX in its softest
            // clothing, which is the dangerous kind, because the demo works.
            Command.ApproveGate => state,
            Command.RejectGate => state,
            Command.CloseModal => state with { Mode = UiMode.Normal },

            Command.FocusNextPane => state with { FocusedPane = NextVisible(state) },

            Command.SelectNext => Select(state, state.SelectedRow + 1),
            Command.SelectPrevious => Select(state, state.SelectedRow - 1),

            Command.ToggleEvidence => Reveal(state with { EvidenceVisible = !state.EvidenceVisible }),
            Command.ToggleLive => ToggleLive(state),
            Command.ToggleFreeze => ToggleFreeze(state),

            // Quit and OpenEditor end the UI session; the shell handles them.
            Command.Quit or Command.OpenEditor => state,
            _ => state,
        };
    }

    /// <summary>
    /// A modal is entered, or left by pressing the same key again.
    /// </summary>
    /// <remarks>
    /// Opening a second modal over the first would create a stack, and a stack
    /// needs as many escapes as it has depth. One at a time keeps "exactly one
    /// escape hatch" true rather than aspirational.
    /// </remarks>
    private static AppState Modal(AppState state, UiMode mode) =>
        state with { Mode = state.Mode == mode ? UiMode.Normal : mode };

    /// <summary>
    /// The next pane that is actually on screen.
    /// </summary>
    /// <remarks>
    /// Focus never lands on a hidden pane. Somewhere invisible holding the
    /// focus ring reads exactly like a frozen keyboard, and the person's next
    /// move is to kill the terminal.
    /// </remarks>
    private static PaneId NextVisible(AppState state)
    {
        var panes = Enum.GetValues<PaneId>();
        var start = Array.IndexOf(panes, state.FocusedPane);

        for (var step = 1; step <= panes.Length; step++)
        {
            var candidate = panes[(start + step) % panes.Length];
            if (IsVisible(state, candidate))
            {
                return candidate;
            }
        }

        return PaneId.Queue;
    }

    private static bool IsVisible(AppState state, PaneId pane) => pane switch
    {
        PaneId.Evidence => state.EvidenceVisible,
        PaneId.Live => state.LiveVisible,
        _ => true,
    };

    /// <summary>Pulls focus back out of a pane that has just been hidden.</summary>
    private static AppState Reveal(AppState state) =>
        IsVisible(state, state.FocusedPane) ? state : state with { FocusedPane = NextVisible(state) };

    /// <summary>
    /// Moves the cursor, and marks what it lands on as read.
    /// </summary>
    /// <remarks>
    /// Clamped rather than wrapped: running off the end of a short queue and
    /// arriving at the top is a way to act on the wrong flight while believing
    /// you moved one row.
    /// </remarks>
    private static AppState Select(AppState state, int index)
    {
        if (state.Queue.Count == 0)
        {
            return state with { SelectedRow = 0 };
        }

        var landed = Math.Clamp(index, 0, state.Queue.Count - 1);

        var moved = state with
        {
            SelectedRow = landed,
            Queue = [.. state.Queue.Select((row, i) => i == landed ? row with { UnreadArrivals = 0 } : row)],
        };

        // Moving the cursor while the live view is open IS watching the flight
        // you moved to. Counting only the keypress that opened the pane would
        // measure how often somebody presses `l`, which is not the number we
        // want to fall.
        return moved.LiveVisible ? RecordAttach(moved, attached: true) : moved;
    }

    /// <summary>
    /// Shows or hides the live view, and records that it happened.
    /// </summary>
    /// <remarks>
    /// The fact is written on ATTACH only. Counting detaches too would double
    /// every number and make a rate that should fall look like it doubled.
    /// </remarks>
    private static AppState ToggleLive(AppState state)
    {
        var showing = !state.LiveVisible;
        return RecordAttach(Reveal(state with { LiveVisible = showing }), showing);
    }

    /// <summary>
    /// Writes the fact that somebody watched this flight.
    /// </summary>
    /// <remarks>
    /// The count goes up on ATTACH only. Counting detaches would double every
    /// number and make a rate that should fall look like it doubled.
    /// </remarks>
    private static AppState RecordAttach(AppState state, bool attached)
    {
        if (state.Selected is not { } row)
        {
            return state;
        }

        var existing = state.AttachFacts.FirstOrDefault(f => f.FlightId == row.FlightId);
        var updated = new LiveAttachFact
        {
            FlightId = row.FlightId,
            Attached = attached,
            AttachCount = (existing?.AttachCount ?? 0) + (attached ? 1 : 0),
        };

        return state with
        {
            AttachFacts = existing is null
                ? [.. state.AttachFacts, updated]
                : [.. state.AttachFacts.Select(f => f.FlightId == row.FlightId ? updated : f)],
        };
    }

    /// <summary>
    /// Holds the screen still, and lets it go again.
    /// </summary>
    /// <remarks>
    /// Thawing flushes what arrived rather than discarding it. A copy that
    /// works, over output with a hole in it that nobody can see, is worse than
    /// a screen that moved while they were selecting.
    /// </remarks>
    private static AppState ToggleFreeze(AppState state) =>
        state.Frozen
            ? state with { Frozen = false, Live = [.. state.Live, .. state.Held], Held = [] }
            : state with { Frozen = true };

    /// <summary>
    /// A line of runner output arrived.
    /// </summary>
    /// <remarks>
    /// While frozen it is held, not shown and not dropped.
    /// </remarks>
    public static AppState StreamArrived(AppState state, StreamLine line)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Frozen
            ? state with { Held = [.. state.Held, line] }
            : state with { Live = [.. state.Live, line] };
    }

    /// <summary>
    /// A flight started needing attention.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Arrivals queue; they do not preempt.</b> The row appears, or its
    /// unread count goes up, and the cursor stays where the person left it.
    /// </para>
    /// <para>
    /// The cursor follows the FLIGHT rather than the index, which matters more
    /// than the obvious half: an arrival that sorts above the selection would
    /// otherwise move somebody to a different flight while the cursor appeared
    /// not to move at all. That is worse than moving it, because nothing on
    /// screen says it happened.
    /// </para>
    /// <para>
    /// One exception: a flight you just started or took. Going there is the
    /// answer to what you just did.
    /// </para>
    /// </remarks>
    public static AppState Arrived(AppState state, QueueRow row, bool startedByMe)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(row);

        var selectedFlight = state.Selected?.FlightId;

        var existing = state.Queue.FirstOrDefault(r => r.FlightId == row.FlightId);
        var merged = existing is null
            ? row with { UnreadArrivals = startedByMe ? 0 : 1 }
            : existing with
            {
                Reason = row.Reason,
                Since = row.Since,
                UnreadArrivals = startedByMe ? 0 : existing.UnreadArrivals + 1,
            };

        var queue = QueueSort.Default.Order(
            existing is null
                ? [.. state.Queue, merged]
                : [.. state.Queue.Select(r => r.FlightId == row.FlightId ? merged : r)]);

        var landOn = startedByMe ? row.FlightId : selectedFlight;
        var index = queue.ToList().FindIndex(r => r.FlightId == landOn);

        return state with
        {
            Queue = queue,
            SelectedRow = index < 0 ? 0 : index,
        };
    }
}
