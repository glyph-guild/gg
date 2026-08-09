namespace Gg.Console;

/// <summary>
/// The terminal-release loop. UI sessions are complete lifetimes: between
/// them the terminal belongs to whoever we spawn, and the model is the only
/// thing that survives.
/// </summary>
public sealed class ConsoleLoop(IUiSession ui, IEditorSession editor)
{
    private readonly IUiSession _ui = ui;
    private readonly IEditorSession _editor = editor;

    public AppState Run(AppState initial)
    {
        throw new NotImplementedException();
    }
}
