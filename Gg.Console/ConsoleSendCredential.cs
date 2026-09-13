using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// Puts a credential on the runner under the cursor.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one act this console could diagnose and not perform.</b> The queue
/// says a flight is blocked on a credential and names the machine, and the
/// runner modal is where that machine is on the screen. Until this, acting on
/// it meant quitting, finding the runner id again, and typing a command — which
/// is a person leaving the console at the moment it was most useful.
/// </para>
/// <para>
/// <b>It happens BETWEEN sessions, and this one needs the terminal more than
/// watching does.</b> Watching makes three network calls a session may not
/// make; this makes the same three AND reads a secret with the echo off, which
/// a Terminal.Gui session cannot turn off at all. So it takes the editor's slot,
/// with the terminal provably free, and what survives into the next session is
/// one sentence.
/// </para>
/// <para>
/// <b>The sender is the command line's, not a copy of it.</b> Where the secret
/// comes from — this machine's own store, or a prompt — is the question that
/// matters most on this path, and two implementations would be two answers to
/// it. <c>SendACredential</c> answers it once.
/// </para>
/// <para>
/// <b>Nothing here holds the secret beyond the call.</b> It is read, handed
/// over, and not kept in any field of this class or any member of
/// <c>AppState</c> — which is serialized, written to disk on a crash, and put
/// in a diagnostics bundle.
/// </para>
/// </remarks>
public static class ConsoleSendCredential
{
    /// <summary>
    /// Asks for a repository, reads the secret, and hands it to one runner.
    /// </summary>
    /// <remarks>
    /// The composition root's, for <c>ConsoleWatchRunner.Start</c>'s reason:
    /// the session token, the control plane, the pinned keys and the secret
    /// prompt are all things this assembly may not hold. It answers with the
    /// sentence to show.
    /// </remarks>
    public delegate string Send(string runnerId);

    /// <summary>Sends one, or says why it did not.</summary>
    public static AppState Give(AppState state, Send send)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(send);

        if (Rows.Selected(state) is not { } row || row.Id.Length == 0)
        {
            return state with
            {
                LastCredential = "There is no runner under the cursor to give one to.",
            };
        }

        // THE SAME RULE THE KEY OBEYS, checked again here rather than trusted -
        // ConsoleWatchRunner's argument, and it holds for the same mechanism.
        // The hint line is derived in one place and dispatch happens in
        // another, so a command that arrived anyway must not reach for a
        // channel that cannot exist and then blame the network for it.
        //
        // AND IT MATTERS MORE HERE, because the cost of getting it wrong is
        // that somebody is asked for a token and then told the machine was
        // never reachable. A secret typed for nothing is a secret that was
        // typed.
        if (row.State.StartsWith(RunnerStates.Offline, StringComparison.Ordinal))
        {
            return state with
            {
                LastCredential =
                    $"{row.Label} is not beating, so nothing can be handed to it. An "
                  + "introduction is picked up on a heartbeat. Start it and try again.",
            };
        }

        return state with { LastCredential = send(row.Id) };
    }
}
