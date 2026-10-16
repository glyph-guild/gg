using Porta.Pty;

namespace Gg.Console;

/// <summary>The options every pseudo terminal the console starts is built with.</summary>
public static class ChildPty
{
    public static PtyOptions Options(
        int columns, int rows, string workingDirectory, string command, IEnumerable<string> arguments, bool windows) =>
        new()
        {
            Name = "gg",
            Cols = columns,
            Rows = rows,
            Cwd = workingDirectory,
            App = command,
            CommandLine = [.. arguments],
            Environment = new Dictionary<string, string> { ["TERM"] = "xterm-256color" },
        };
}
