namespace Gg.Local;

/// <summary>
/// What is already installed on this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>A look, not a launch.</b> It reads <c>PATH</c> and checks for a file. It
/// does not run anything, ask anything what version it is, or care whether it
/// works — that would be a child process to answer a question about advice.
/// </para>
/// <para>
/// <b>Here because both halves may want it.</b> The doctor names what it found;
/// a seed could offer it. <c>Gg.Local</c>'s charter is exactly this — a fact
/// about the local machine, with no transport and no credential.
/// </para>
/// </remarks>
public static class OnPath
{
    /// <summary>Where a command is, or null when it is not on PATH.</summary>
    /// <param name="path">
    /// The search path, for a caller that has one. Null reads the process — the
    /// override every reader here carries, because the environment is
    /// process-global and a suite that runs four-wide cannot have one test
    /// setting it while another reads it.
    /// </param>
    public static string? Find(string command, string? path = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        // AN ABSOLUTE NAME IS NOT A SEARCH. Somebody asking about /usr/bin/x is
        // asking whether that file is there, and walking PATH for it would
        // answer a different question.
        if (Path.IsPathRooted(command))
        {
            return File.Exists(command) ? command : null;
        }

        var searched = path ?? Environment.GetEnvironmentVariable("PATH") ?? "";

        foreach (var directory in searched.Split(
            Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries
                              | StringSplitOptions.TrimEntries))
        {
            string candidate;

            try
            {
                candidate = Path.Combine(directory, command);
            }
            catch (ArgumentException)
            {
                // A PATH ENTRY THAT IS NOT A PATH. People have these, and one
                // of them must not stop the search at the entry before the
                // answer.
                continue;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
