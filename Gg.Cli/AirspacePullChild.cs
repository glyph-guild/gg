using System.Diagnostics;
using Gg.Local;

namespace Gg.Cli;

/// <summary>How the tool server renders an airspace: by running the verb.</summary>
/// <remarks>
/// A port, so the server can be asked what it does with an exit code without a
/// control plane to answer one. The tool is the only caller and the default is
/// the real child.
/// </remarks>
/// <param name="root">The working copy to render into.</param>
public delegate PullReport RunPull(string root);

/// <summary>What running the verb came to.</summary>
/// <remarks>
/// <b>Three facts, because they are three different answers.</b> A child that
/// never started is gg's own failure; a non-zero exit is the verb refusing,
/// which is an ordinary outcome with a reason worth reading; and the text is
/// the verb's own words. Collapsing any two would make a refusal look like a
/// crash or a crash look like a refusal.
/// </remarks>
public sealed record PullReport
{
    /// <summary>Whether the child ran at all.</summary>
    public required bool Started { get; init; }

    /// <summary>What it exited with. Meaningless unless it started.</summary>
    public required int ExitCode { get; init; }

    /// <summary>Everything it printed, out and err together, in order enough.</summary>
    public required string Said { get; init; }
}

/// <summary>
/// Runs <c>gg airspace pull</c> as a child of the tool server.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SAME BINARY UNDER A DIFFERENT VERB, through the one function that
/// knows how.</b> <c>SelfInvocation.Under</c> answers the bootstrap question —
/// whether this deployment needs its own assembly handed back to it — and the
/// failure it records is exactly the one a second caller re-deriving it would
/// reproduce: a server that never started and nothing that said so.
/// </para>
/// <para>
/// <b>The credential and the network are the child's, deliberately.</b> The
/// tool server holds neither and must go on holding neither: it is a child of
/// a process the threat model treats as compromised. A verb resolving a
/// session and calling the control plane is the process whose job that is.
/// </para>
/// <para>
/// <b>Both streams, and the exit code separately.</b> A pull prints its
/// refusals to stderr and its report to stdout, and an agent needs whichever
/// happened — so they are read together and the code is what decides whether
/// this was a refusal or a result.
/// </para>
/// <para>
/// <b>It cannot wait for ever.</b> A child that hangs holds the tool call
/// open, and the agent has no way to abandon it — so the wait is bounded and a
/// timeout is reported as itself rather than as an empty success. Generous
/// rather than tight: this is two HTTP calls and a tree render, and killing a
/// pull that was about to finish would leave a half-written working copy.
/// </para>
/// </remarks>
public static class AirspacePullChild
{
    /// <summary>How long a pull may take before it is given up on.</summary>
    private static readonly TimeSpan Longest = TimeSpan.FromMinutes(3);

    public static PullReport Run(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (SelfInvocation.Current is not { } self)
        {
            // SAYS SO RATHER THAN GUESSING, for the reason SelfInvocation
            // itself answers null: a path that is not this binary is a child
            // that fails at startup, and the agent has already been told the
            // tool exists.
            return new PullReport
            {
                Started = false,
                ExitCode = -1,
                Said = "gg cannot name its own executable here, so it cannot run the pull.",
            };
        }

        var start = new ProcessStartInfo(self.Command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in self.Under("airspace", "pull"))
        {
            start.ArgumentList.Add(argument);
        }

        // FORCED, NEVER INFERRED. The verb falls back to the current directory,
        // and this process's directory is whatever the MCP client chose - so a
        // pull left to the fallback succeeds and writes a tree nowhere anybody
        // is looking.
        start.Environment[AirspacePullTool.RootVariable] = root;

        try
        {
            using var child = Process.Start(start);

            if (child is null)
            {
                return new PullReport
                {
                    Started = false,
                    ExitCode = -1,
                    Said = "The pull did not start, and the operating system said nothing "
                         + "about why.",
                };
            }

            // READ BEFORE WAITING, both of them, because a child that fills a
            // pipe nobody is draining blocks for ever - and a pull that prints
            // a long refusal is exactly the case that would.
            var said = child.StandardOutput.ReadToEndAsync();
            var complained = child.StandardError.ReadToEndAsync();

            if (!child.WaitForExit(Longest))
            {
                child.Kill(entireProcessTree: true);

                return new PullReport
                {
                    Started = true,
                    ExitCode = -1,
                    Said = $"The pull was still running after {Longest.TotalMinutes:0} "
                         + "minutes and was stopped. The working copy may be half written; "
                         + "a person should look before anything is drafted into it.",
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
                        or IOException)
        {
            return new PullReport
            {
                Started = false,
                ExitCode = -1,
                Said = $"The pull could not be started: {unstartable.Message}",
            };
        }
    }
}
