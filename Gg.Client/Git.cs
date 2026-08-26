using System.Diagnostics;

namespace Gg.Client;

/// <summary>
/// The one git question the working copy asks: what is uncommitted here.
/// </summary>
/// <remarks>
/// <para>
/// <b>A deliberate crossing, and its blast radius is stated.</b> Until slice
/// thirteen no git ran in <c>Gg.Client</c> at all — it lived in
/// <c>Gg.Runner</c>, in a separate process, against repositories treated as
/// hostile. This is a different thing: a read of the person's own working copy,
/// on their own machine, with no network, no credentials, no writes, and no
/// customer repository anywhere near it.
/// </para>
/// <para>
/// <b>It exists because the distinction it draws cannot be drawn any other
/// way.</b> Pull overwrites files with canonical renderings, which is the point;
/// what it must not overwrite is an edit nobody committed. Comparing bytes
/// against the stream cannot tell a colleague's committed change from your own
/// unfinished one — only git knows that, so git is asked.
/// </para>
/// <para>
/// <b>The credential-helper discipline comes with it.</b> <c>Gg.Runner</c> clears
/// the machine's helper list so a developer's keychain cannot silently
/// authenticate; there is nothing to authenticate here, and the flag is set
/// anyway, because a read that grew a network call later would grow it with the
/// guard already in place.
/// </para>
/// </remarks>
public static class Git
{
    /// <summary>How long a local status read may take before it is a hang.</summary>
    /// <remarks>
    /// Bounded because a git that never returns would hang the verb with no
    /// output — and the failure a person actually hits is a lock file left by an
    /// editor, which resolves in milliseconds or not at all.
    /// </remarks>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Whether this directory is inside a git working tree.</summary>
    /// <remarks>
    /// Nothing forces a working copy to be a repository — ADR-0016 is explicit
    /// that the repository is convenience and the stream is law. A plain
    /// directory gets no git opinion rather than a refusal it cannot act on.
    /// </remarks>
    public static bool IsRepository(string directory) =>
        Try(directory, out _, "rev-parse", "--is-inside-work-tree");

    /// <summary>
    /// The porcelain status of one path, as lines. Empty when clean or not a
    /// repository.
    /// </summary>
    public static IReadOnlyList<string> Status(string directory, string? path = null)
    {
        if (!IsRepository(directory))
        {
            return [];
        }

        string[] arguments = path is null
            ? ["status", "--porcelain"]
            : ["status", "--porcelain", "--", path];

        // NOT TrimEntries. Porcelain v1 is two fixed status columns then a
        // space, and a modified-but-unstaged file's first column is a SPACE -
        // trimming it shifts every such path left by one and the slice that
        // follows cuts into the filename. An untracked file starts '??' and
        // survives the same bug, which is how it hides: the test that only
        // adds a file passes.
        return Try(directory, out var output, arguments)
            ? [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))]
            : [];
    }

    /// <summary>Runs git and throws if it fails. For tests and setup, never for a verb.</summary>
    public static void Run(string directory, params string[] arguments)
    {
        if (!Try(directory, out var output, arguments))
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed in {directory}: {output}");
        }
    }

    /// <summary>
    /// Runs git, answering whether it succeeded and what it said.
    /// </summary>
    /// <remarks>
    /// <b>Failure is an answer, not an exception</b>, for the same reason the
    /// runner's invocation is a plan: the caller decides what a missing git or a
    /// locked index means. Here both mean "no git opinion", which is the safe
    /// reading — a person whose repository is mid-rebase should get a pull that
    /// works, not a tool that refuses because it could not ask.
    /// </remarks>
    private static bool Try(string directory, out string output, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // NO CREDENTIAL HELPER, though nothing here authenticates. The runner
        // clears the list so a developer's keychain cannot silently answer for
        // a flight; carrying the same flag means a read that ever grew a remote
        // would grow it with the guard already on.
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("credential.helper=");

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                output = string.Empty;
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(Patience))
            {
                process.Kill(entireProcessTree: true);
                output = "git did not answer in time";
                return false;
            }

            output = process.ExitCode == 0 ? stdout : stderr;
            return process.ExitCode == 0;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No git on the machine. The working copy still works; it simply
            // has no opinion about what is uncommitted.
            output = string.Empty;
            return false;
        }
    }
}
