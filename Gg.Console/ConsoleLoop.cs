namespace Gg.Console;

/// <summary>
/// The terminal-release loop. UI sessions are complete lifetimes: between
/// them the terminal belongs to whoever we spawn, and the model is the only
/// thing that survives.
/// </summary>
public sealed class ConsoleLoop(IUiSession ui, IEditorSession editor, ITakeSession? take = null)
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
}
