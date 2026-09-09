using System.Diagnostics;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Hands the terminal to <c>gg runner watch</c> for the runner under the cursor.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a child and not a pane.</b> Reaching a runner is three calls to the
/// control plane — mint an introduction, leave the sealed offer, collect the
/// answer — and then a WebRTC socket carrying the conversation. A UI session
/// may make none of those: <c>LiveStreamingTests</c> scans the session sources
/// and fails on an <c>HttpClient</c> or a <c>Socket</c>, and that rule is the
/// reason the console can promise the terminal is free between sessions. So
/// this takes the slot <c>$EDITOR</c> takes.
/// </para>
/// <para>
/// <b>The live pane in this console is not a precedent for doing it inline.</b>
/// That pane reads a LOCAL file — the live view a runner on this machine wrote
/// — which is the narrowest exception available and the only one a session
/// has. A fleet runner's output is on the fleet runner; that is the whole
/// premise of the feature.
/// </para>
/// <para>
/// <b>Through <see cref="SelfInvocation"/>, so the child is THIS gg.</b> A bare
/// <c>gg</c> off PATH is whichever one is installed, which on a developer's
/// machine is routinely not the one they are running — <c>ConsoleHandFlight</c>
/// refuses for the same reason and this refuses the same way.
/// </para>
/// </remarks>
public static class ConsoleWatchRunner
{
    /// <summary>How to start the watch, as a child of this process.</summary>
    public static ProcessStartInfo StartInfoFor(SelfInvocation self, string runnerId)
    {
        ArgumentNullException.ThrowIfNull(self);

        var info = new ProcessStartInfo(self.Command)
        {
            // NOTHING REDIRECTED. What the agent is saying has to reach the
            // screen; a pipe here is a tail nobody reads, and stdin has to
            // reach the child so Ctrl-C stops the watch rather than the console.
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
        };

        // THROUGH `Under`, which already answers whether this process needs its
        // own assembly handed back to it - `dotnet Gg.Cli.dll` rather than `gg`.
        // SelfInvocation's remark says a second verb must ask rather than
        // re-derive it, and the failure it records is a child that never
        // started with nothing that said so.
        foreach (var argument in self.Under("runner", "watch"))
        {
            info.ArgumentList.Add(argument);
        }

        // THE WHOLE ID. The grid shows eight characters and the verb takes all
        // of it, which is why the row carries both.
        info.ArgumentList.Add(runnerId);

        return info;
    }

    /// <summary>
    /// Goes and watches, or says why it did not.
    /// </summary>
    /// <param name="start">
    /// Runs the child and answers its exit code. The composition root's,
    /// because starting a process is not something this assembly may do to a
    /// terminal it does not know it has.
    /// </param>
    public static AppState Watch(
        AppState state, SelfInvocation? self, Func<ProcessStartInfo, int> start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(start);

        if (Rows.Selected(state) is not { } row || row.Id.Length == 0)
        {
            return state with
            {
                LastRunner = "There is no runner under the cursor to watch.",
            };
        }

        // THE SAME RULE THE KEY OBEYS, checked again here rather than trusted.
        // The hint line is derived in one place and dispatch happens in
        // another; a command that arrived anyway - a rebind, a modal that
        // outlived the row it was over - must not start a child that cannot
        // work and then blame the network for it.
        if (row.Work is not { Length: > 0 } flying)
        {
            return state with
            {
                LastRunner =
                    $"{row.Label} is flying nothing, so there is nothing to watch: a channel "
                  + "to a runner exists only while a flight does, which is what stops it "
                  + "being a standing way in.",
            };
        }

        if (self is null)
        {
            return state with
            {
                LastRunner =
                    "Nothing was watched: this gg cannot work out how to re-run itself, so "
                  + "the watch would be handed to whichever gg is on PATH.",
            };
        }

        var exit = start(StartInfoFor(self, row.Id));

        // WHAT CAME BACK, and a zero is not a promise that anybody was reached.
        // `watch` answers with a sentence of its own on the terminal the child
        // owned - which this console has just taken back and cannot reprint -
        // so the receipt says where to look rather than inventing a verdict.
        return state with
        {
            LastRunner = exit == 0
                ? $"Watched {flying} on {row.Label}. The channel closes with the flight."
                : $"Watching {flying} on {row.Label} ended with exit {exit}. Its own sentence "
                + "was on the screen the watch had.",
        };
    }
}
