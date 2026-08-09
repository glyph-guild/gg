using System.Diagnostics;

namespace Gg.Console;

public interface IEditorSession
{
    string Edit(string initialText);
}

/// <summary>
/// Spawns $EDITOR as a separate process with the terminal inherited, waits
/// for it to exit, and returns the edited text. Only ever called while no UI
/// session is running — the terminal must be free.
/// </summary>
public sealed class EditorSession : IEditorSession
{
    private readonly string _editorCommand;

    public EditorSession(string? editorCommand = null)
        => _editorCommand = editorCommand
            ?? Environment.GetEnvironmentVariable("EDITOR")
            ?? "vi";

    public string Edit(string initialText)
    {
        throw new NotImplementedException();
    }
}
