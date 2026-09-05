using Gg.Client;
namespace Gg.Console;

/// <summary>
/// The terminal-release loop. UI sessions are complete lifetimes: between
/// them the terminal belongs to whoever we spawn, and the model is the only
/// thing that survives.
/// </summary>
public sealed class ConsoleLoop(
    IUiSession ui,
    IEditorSession editor,
    ITakeSession? take = null,
    IHandSession? hand = null,
    IConsoleActions? actions = null,
    Func<string, ILiveSource>? liveSource = null)
{
    /// <summary>
    /// One tail per flight, kept because a tail without its offset re-reads.
    /// </summary>
    /// <remarks>
    /// <b>Held by the LOOP and never by a session.</b> <c>IUiSession</c> must
    /// not retain anything across calls - it builds views FROM the given state -
    /// and a file offset is exactly the sort of thing that would rot there. The
    /// loop is the lifetime that spans sessions, so it is the one that may
    /// remember where it had read to.
    /// </remarks>
    private readonly Dictionary<string, ILiveSource> _tails = new(StringComparer.Ordinal);

    /// <summary>
    /// Reads whatever the watched flight has said since the last look.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Between sessions, which is a still picture rather than a stream.</b>
    /// The pane advances when the person presses a key, because that is when a
    /// session ends and this runs. It is deliberately step three of five: it
    /// proves the producer, the path and the consumer end to end without the
    /// architectural change streaming needs, and it is worth having on its own.
    /// </para>
    /// <para>
    /// <b>It follows the selection.</b> A tail is kept per flight so moving the
    /// cursor away and back does not replay what was already seen, and so two
    /// flights watched in one sitting do not share an offset.
    /// </para>
    /// </remarks>
    private AppState Tailed(AppState state)
    {
        if (!state.LiveVisible || liveSource is null || state.Selected is not { } row)
        {
            return state with { Silence = LiveSilence.NotAttached };
        }

        if (!_tails.TryGetValue(row.FlightId, out var tail))
        {
            tail = liveSource(row.FlightId);
            _tails[row.FlightId] = tail;
        }

        foreach (var line in tail.Read())
        {
            state = Reducer.StreamArrived(state, line);
        }

        // WHICH SILENCE, recorded here because this is the only place that knows.
        // The pane sees state and a state that cannot tell "nothing is writing"
        // from "nothing has been said yet" shows one empty box for both.
        return state with
        {
            Silence = state.Live.Count > 0 ? LiveSilence.Speaking
                : tail.Exists ? LiveSilence.NothingYet
                : LiveSilence.NotStarted,
        };
    }

    public AppState Run(AppState initial)
    {
        var state = initial;
        while (true)
        {
            state = Tailed(state);

            var outcome = ui.Run(state);
            state = outcome.State;

            var before = state;

            switch (outcome.Exit)
            {
                case Command.Quit:
                    return state;

                case Command.HandBack:
                    // The same shape again: the session ends, an agent reads the
                    // tree, a person answers, and the model is the only thing
                    // that crosses back.
                    state = HandedBack(state, hand);
                    break;

                case Command.OpenFlight:
                    state = Opened(state, actions, editor);
                    break;

                case Command.AddCredential:
                    // The value is read by CredentialCommands, inside the action.
                    // Nothing here holds it, which is the point: this record is
                    // serialized to disk under GG_STATE_DUMP.
                    state = state with
                    {
                        LastCredential = actions is null
                            ? "This console is not configured to register credentials."
                            : actions.AddCredential(),
                    };
                    break;

                case Command.Invite:
                    state = state with
                    {
                        LastInvite = actions is null
                            ? "This console is not configured to issue invitations."
                            : actions.Invite(),
                    };
                    break;

                case Command.ApproveGate:
                case Command.RejectGate:
                    // THE WRITE THE REDUCER DELIBERATELY DOES NOT DO. It returns the
                    // state unchanged for both answers and says why: answering posts,
                    // and what closes the modal is what the control plane sends back.
                    // That was right and the loop never saw the command, so nothing
                    // posted at all - the console had no write path.
                    state = Decided(
                        state, actions, editor, outcome.Exit == Command.ApproveGate);
                    break;

                case Command.TakeFlight:
                    // The same shape, against something much larger: a person
                    // holds the terminal for minutes rather than an editor for
                    // seconds. It works for the same reason - the session is
                    // over before the child starts, so the terminal is provably
                    // free, and the next session is rebuilt from the model
                    // alone.
                    state = Took(state, take);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"UI session exited with {outcome.Exit}, which the shell does not handle");
            }

            // WHAT JUST HAPPENED, in one slot, derived rather than set six times.
            // Each arm above already records its outcome per kind; this is the line a
            // person reads, and taking it from whichever field the arm changed means a
            // new arm cannot forget to say anything.
            state = state with { LastAction = Said(before, state) };
        }
    }

    /// <summary>
    /// The outcome this pass produced, whichever field carried it.
    /// </summary>
    /// <remarks>
    /// Compared rather than assigned, so an arm that records an outcome cannot also
    /// forget to surface it. Quit and the editor change nothing a person needs told.
    /// </remarks>
    private static string? Said(AppState before, AppState after) =>
        after.LastFlightOpened != before.LastFlightOpened ? after.LastFlightOpened
        : after.LastCredential != before.LastCredential ? after.LastCredential
        : after.LastInvite != before.LastInvite ? after.LastInvite
        : after.LastDecision != before.LastDecision ? after.LastDecision
        : after.LastTakeover != before.LastTakeover ? after.LastTakeover
        : after.LastHandBack != before.LastHandBack ? after.LastHandBack
        : before.LastAction;

    /// <summary>
    /// Takes the intent and opens a flight, or opens nothing and says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A prompt with an escape to <c>$EDITOR</c>, and it is ONE field.</b> The
    /// prompt's answer seeds the editor rather than competing with it, so there is a
    /// single value with somewhere to grow - two input paths to one field is how they
    /// drift, and an intent that came from either is the same intent.
    /// </para>
    /// <para>
    /// <b>Nothing typed opens nothing.</b> A flight opened by accident is a record
    /// somebody has to explain and a number that is now taken, so an empty answer
    /// says it changed nothing rather than falling silent.
    /// </para>
    /// </remarks>
    private static AppState Opened(
        AppState state, IConsoleActions? actions, IEditorSession editor)
    {
        if (actions is null)
        {
            return state with
            {
                LastFlightOpened = "This console is not configured to open flights.",
            };
        }

        // The editor IS the prompt at this cardinality: it opens on an empty buffer,
        // a person types a line or a paragraph, and saving is the answer. Adding a
        // separate one-line reader would be the second path this comment warns about.
        var intent = editor.Edit("").Trim();

        return state with
        {
            LastFlightOpened = intent.Length == 0
                ? "Nothing was opened: no intent was written."
                : actions.Fly(intent),
        };
    }

    /// <summary>
    /// Sends the answer, and folds what was sent into the model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The modal is not closed here.</b> What a gate BECAME is the control
    /// plane's answer - the decision is recorded, the Engine re-evaluates, and the
    /// next load carries the result. A console that closed the modal on the
    /// keystroke would be reporting its own optimism, which is Article IX in its
    /// softest clothing because the demo works.
    /// </para>
    /// <para>
    /// <b>Everything that can go wrong leaves the model intact and says so</b>, the
    /// same shape the takeover has: no actions configured, no gate on this row, or a
    /// refusal from the far side.
    /// </para>
    /// </remarks>
    private static AppState Decided(
        AppState state, IConsoleActions? actions, IEditorSession editor, bool approved)
    {
        if (actions is null || state.SelectedGate is not { } gate)
        {
            return state with
            {
                LastDecision = actions is null
                    ? "This console is not configured to answer gates."
                    : "Nothing on this flight is waiting on a decision.",
            };
        }

        // A REJECTION NEEDS A REASON, and the verb refuses one without it - the loop
        // runs again with the reason, so a rejection that says nothing sends the work
        // back to be done the same way. The console has no text field, and the answer
        // is the one it has always used for text: release the terminal to $EDITOR.
        //
        // Refused HERE as well as there, so a person who changes their mind by
        // saving an empty buffer has not answered a gate by accident.
        string? reason = null;
        if (!approved)
        {
            reason = editor.Edit("").Trim();

            if (reason.Length == 0)
            {
                return state with
                {
                    LastDecision = $"{gate.FlightNumber}: nothing was sent. Rejecting needs a "
                                 + "reason - the loop runs again with it.",
                };
            }
        }

        return state with
        {
            LastDecision = actions.Decide(gate.FlightNumber, gate.ObligationId, approved, reason),
        };
    }

    /// <summary>
    /// Runs the takeover and folds what came back into the model.
    /// </summary>
    /// <remarks>
    /// <b>Everything that can go wrong ends with the model intact.</b> No
    /// takeover configured, a child that would not start, a return file that
    /// cannot be trusted - each leaves the flight exactly as it was and says so
    /// on the state the next session renders from.
    /// </remarks>
    private static AppState Took(AppState state, ITakeSession? take)
    {
        if (take is null || state.Selected is not { } row || state.TakeableTree is not { } tree)
        {
            return state with
            {
                LastTakeover = take is null
                    ? "This console is not configured to take flights over."
                    : "There is no held tree for this flight, so there is nothing to take over.",
            };
        }

        var result = take.Take(new TakeRequest
        {
            FlightId = row.FlightId,
            FlightNumber = row.FlightNumber,
            TreePath = tree,
            Seed = state.TakeSeed!,
        });

        return state with
        {
            LastTakeover = result switch
            {
                { Diagnosis: { Length: > 0 } diagnosis } => diagnosis,
                { Decision: { } decision } =>
                    $"{row.FlightNumber}: {decision.Outcome}"
                  + (decision.Note is { Length: > 0 } note ? $" — {note}" : ""),
                _ => $"{row.FlightNumber}: taken over for {result.Held.TotalMinutes:F0} minute(s), "
                   + "and no decision was written.",
            },
            LastTakeoverHeld = result.Held,
        };
    }

    /// <summary>
    /// Runs the hand-back and folds what the person confirmed into the model.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is recorded unless they answered.</b> A walk-away leaves no
    /// account and the state says so, because an unconfirmed proposal stored as
    /// somebody's words attributes a guess to them.
    /// </remarks>
    private static AppState HandedBack(AppState state, IHandSession? hand)
    {
        if (hand is null || state.Selected is not { } row || state.TakeSeed is not { } seed)
        {
            return state with
            {
                LastHandBack = hand is null
                    ? "This console is not configured to hand flights back."
                    : "There is nothing to hand back: this flight has not been taken over.",
            };
        }

        var outcome = hand.Hand(new HandRequest
        {
            FlightId = row.FlightId,
            FlightNumber = row.FlightNumber,
            TreePath = state.TakeableTree ?? "",
            By = state.Principal,
            PriorAccount = seed.Account,
            Measurements = seed.Measurements,
        });

        return state with
        {
            LastHandBack = outcome.Detail,
            // Recorded whichever way it went, INCLUDING the walk-away: a rate
            // that only counted the answers would be a rate of answers.
            HandConfirmations =
            [
                .. state.HandConfirmations.Where(f => f.FlightId != row.FlightId),
                new HandConfirmationFact { FlightId = row.FlightId, Choice = outcome.Choice },
            ],
            // The account joins the seed, so the next person to take this flight
            // over finds it where a resuming reader looks.
            TakeSeed = outcome.Account is { } account
                ? seed with { PriorHuman = account }
                : seed,
            TakenOver = false,
        };
    }
}
