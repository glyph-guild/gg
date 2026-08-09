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
        var file = Path.Combine(Path.GetTempPath(), $"gg-notes-{Guid.NewGuid():N}.md");
        File.WriteAllText(file, initialText);
        try
        {
            var parts = _editorCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var startInfo = new ProcessStartInfo(parts[0]) { UseShellExecute = false };
            foreach (var argument in parts.Skip(1))
            {
                startInfo.ArgumentList.Add(argument);
            }
            startInfo.ArgumentList.Add(file);

            using var editor = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"failed to start editor '{_editorCommand}'");
            editor.WaitForExit();

            return File.ReadAllText(file);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
