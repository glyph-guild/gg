using Porta.Pty;

namespace Gg.Console;

/// <summary>The options every pseudo terminal the console starts is built with.</summary>
/// <remarks>
/// <b>Asynchronous IO on Windows, so the read ends with the child.</b> A Windows pseudo console
/// keeps the child's output open after the child exits, until the pseudo console is closed, and
/// Porta.Pty closes it on exit only in this mode. Without it the host, which reads to the end
/// before asking for the exit code, waits for ever. Elsewhere the read already ends with the
/// child, and on Linux the mode needs a kernel feature (pidfd_open) gg does not otherwise ask for.
/// </remarks>
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
            UseAsyncIo = windows,
        };
}
