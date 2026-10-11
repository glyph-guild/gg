using System.Diagnostics;

namespace Gg.Cli;

/// <summary>One git command, run as a child in a directory, reported the way an airspace verb is.</summary>
public delegate PullReport RunGit(IReadOnlyList<string> arguments, string workingDirectory);

/// <summary>
/// Runs the git this machine has, as a child, in the repository's own
/// directory - for <see cref="PlatformToolServer"/>'s history tool.
/// </summary>
/// <remarks>
/// <para>
/// <b>A child, never a library</b>, for the reason <see cref="AirspacePullChild"/>
/// gives one tool over: the process whose job it is holds the behaviour, and
/// the agent gets git's own words rather than a reconstruction of them.
/// </para>
/// <para>
/// <b>No pager, no prompt.</b> There is no terminal on the far side of this,
/// so a pager would hang and a credential prompt would wait for ever. Neither
/// is given the chance. What is run is the caller's business - the server
/// decides which subcommands and which flags - and this does not second-guess
/// it, which is what makes the server's refusals the whole of the policy.
/// </para>
/// </remarks>
public static class GitHistoryChild
{
    private static readonly TimeSpan Longest = TimeSpan.FromMinutes(2);

    public static PullReport Run(IReadOnlyList<string> arguments, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_PAGER"] = "cat";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            using var child = Process.Start(start);
            if (child is null)
            {
                return new PullReport
                {
                    Started = false,
                    ExitCode = -1,
                    Said = "git did not start, and the operating system said nothing about why.",
                };
            }

            child.StandardInput.Close();
            var said = child.StandardOutput.ReadToEndAsync();
            var complained = child.StandardError.ReadToEndAsync();

            if (!child.WaitForExit(Longest))
            {
                child.Kill(entireProcessTree: true);
                return new PullReport
                {
                    Started = true,
                    ExitCode = -1,
                    Said = $"git was still running after {Longest.TotalMinutes:0} minutes and was "
                         + "stopped. Narrow the query.",
                };
            }

            return new PullReport
            {
                Started = true,
                ExitCode = child.ExitCode,
                Said = string.Join(
                    Environment.NewLine,
                    new[] { said.GetAwaiter().GetResult(), complained.GetAwaiter().GetResult() }
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Select(text => text.TrimEnd())),
            };
        }
        catch (Exception unstartable) when (
            unstartable is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or System.IO.IOException)
        {
            return new PullReport
            {
                Started = false,
                ExitCode = -1,
                Said = $"git did not start: {unstartable.Message}",
            };
        }
    }
}
