namespace Gg.Console;

/// <summary>
/// The terminal-release loop. UI sessions are complete lifetimes: between
/// them the terminal belongs to whoever we spawn, and the model is the only
/// thing that survives.
/// </summary>
public sealed class ConsoleLoop(IUiSession ui, IEditorSession editor)
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

                default:
                    throw new InvalidOperationException(
                        $"UI session exited with {outcome.Exit}, which the shell does not handle");
            }
        }
    }
}
