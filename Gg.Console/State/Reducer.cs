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
            Command.Quit => state,
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
    /// <summary>
    /// How many lines either buffer keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An unbounded list is also an unbounded <c>GG_STATE_DUMP</c>.</b>
    /// AppState is serialized when the terminal is released, so a pane left
    /// attached to a long flight would write every line it ever saw to disk on
    /// the way out.
    /// </para>
    /// <para>
    /// Five hundred is more scrollback than a terminal shows and, at the walk's
    /// measured mean of 69 characters a line, about 35 KB of state. <c>Held</c>
    /// gets the same cap for a different reason: a freeze somebody forgot is a
    /// buffer with no ceiling.
    /// </para>
    /// </remarks>
    private const int Keep = 500;

    public static AppState StreamArrived(AppState state, StreamLine line)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Frozen
            ? state with { Held = Capped(state.Held, line) }
            : state with { Live = Capped(state.Live, line) };
    }

    /// <summary>Appends, and drops the oldest first when full.</summary>
    /// <remarks>
    /// Oldest first because the newest is what a person is reading. Dropping
    /// the newest to protect the cap would be a pane that stops updating at
    /// exactly the moment it matters most.
    /// </remarks>
    private static IReadOnlyList<StreamLine> Capped(IReadOnlyList<StreamLine> lines, StreamLine line)
    {
        if (lines.Count < Keep)
        {
            return [.. lines, line];
        }

        return [.. lines.Skip(lines.Count - Keep + 1), line];
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
    /// <summary>
    /// What a reader answered, as the pane will draw it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a <c>Command</c>, for <c>StreamArrived</c>'s reason.</b> No
    /// keystroke caused this: a browse came back. Data that arrives from
    /// outside enters the model through its own door.
    /// </para>
    /// <para>
    /// <b>THE ONE PLACE FIVE ENDINGS BECOME A LISTING.</b> Every
    /// <see cref="BrowseOutcome"/> maps, and the switch is exhaustive on
    /// purpose - an outcome added without a mapping would arrive as a null
    /// <c>Browse</c>, which the pane renders as "no tracker is configured",
    /// the single most misleading sentence available. So the fallback is the
    /// outcome's own type name rather than silence: wrong, but loudly.
    /// </para>
    /// <para>
    /// <b>Replaces rather than appends.</b> A page is a page, not a log:
    /// appending would grow the state without bound while a person paged, and
    /// show them rows they had already scrolled past.
    /// </para>
    /// <para>
    /// <b>Nothing else moves.</b> A background answer that reset the selection
    /// or the focused pane would lose a person their place for a fetch they did
    /// not ask about.
    /// </para>
    /// </remarks>
    public static AppState Browsed(AppState state, string providerKey, BrowseOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(outcome);

        return state with
        {
            Browse = outcome switch
            {
                BrowseOutcome.Listed listed => new BrowseListing
                {
                    ProviderKey = providerKey,
                    // NO URL. A flight is opened from a provider and an id,
                    // never parsed out of a url, so carrying one would put a
                    // customer string in the dump that no reader of the screen
                    // asked for.
                    Items = [.. listed.Page.Items.Select(item => new BrowseRow
                    {
                        Id = item.Id,
                        Title = item.Title,
                        State = item.State,
                        Updated = item.Updated,
                    })],
                    NextCursor = listed.Page.NextCursor,
                },

                BrowseOutcome.NotBrowsable why => Absent(providerKey, why.Why),
                BrowseOutcome.Refused why => Absent(providerKey, why.Why),
                BrowseOutcome.Unintelligible why => Absent(providerKey, why.Why),
                BrowseOutcome.Silent why => Absent(providerKey, why.Why),

                _ => Absent(providerKey,
                    $"The reader for '{providerKey}' answered in a way this console does not "
                  + $"have a sentence for ({outcome.GetType().Name}). That is a gap here, not "
                  + "a problem with the tracker."),
            },
        };
    }

    /// <summary>A tracker that was asked and did not answer with work.</summary>
    private static BrowseListing Absent(string providerKey, string why) =>
        new() { ProviderKey = providerKey, Absence = why };

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
