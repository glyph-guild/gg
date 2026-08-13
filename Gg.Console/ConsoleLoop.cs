using Gg.Client;
namespace Gg.Console;

/// <summary>
/// The terminal-release loop. UI sessions are complete lifetimes: between
/// them the terminal belongs to whoever we spawn, and the model is the only
/// thing that survives.
/// </summary>
public sealed class ConsoleLoop(
    IUiSession ui, IEditorSession editor, ITakeSession? take = null, IHandSession? hand = null)
{
    public AppState Run(AppState initial)
    {
        var state = initial;
        while (true)
        {
            var outcome = ui.Run(state);
            state = outcome.State;

            switch (outcome.Exit)
            {
                case Command.Quit:
                    return state;

                case Command.OpenEditor:
                    // The UI session has ended; the terminal is free for the
                    // child process. Its result lands in the model, and the
                    // next session is rebuilt from that model alone.
                    state = state with { Notes = editor.Edit(state.Notes) };
                    break;

                case Command.HandBack:
                    // The same shape again: the session ends, an agent reads the
                    // tree, a person answers, and the model is the only thing
                    // that crosses back.
                    state = HandedBack(state, hand);
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
        }
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
