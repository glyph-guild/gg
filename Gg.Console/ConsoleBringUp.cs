using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// A machine saying what it lacks, as the console reads it (slice forty-six,
/// step 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Not an agent login, and the difference is the act.</b> Both are runner
/// incidents that reach one person. Signing an agent in is something this
/// console does, over a channel, on a key. A machine that does not declare the
/// agent its profile names, cannot resolve a credential reference, or cannot
/// reach its forge needs something done somewhere else - and offering the
/// ceremony for any of those would be the wrong act on the right machine.
/// </para>
/// <para>
/// <b>And neither answer fits.</b> Approving a bring-up ask ends the flight
/// with the item still missing, and the next reading opens another; rejecting
/// it ends it without the machine ever having been fixed. What clears it is
/// the machine's own next reading, which is what this says rather than
/// offering two keys that do not answer.
/// </para>
/// </remarks>
public static class ConsoleBringUp
{
    /// <summary>The bring-up ask on the selected gate, or null when it is not one.</summary>
    /// <remarks>
    /// <b>Closed on the kind</b>, as the agent login's own reader is: a kind
    /// this build does not know is a gate to read rather than an act to guess.
    /// </remarks>
    public static GateMaintenance? Asking(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.SelectedGate?.Maintenance is
        { Kind: GateMaintenanceKinds.BringUp, Runner.Length: > 0 } asked
            ? asked
            : null;
    }

    /// <summary>
    /// What this machine lacks and where it is answered, or empty when the gate
    /// is not a bring-up ask.
    /// </summary>
    /// <remarks>
    /// <b>Where, not how.</b> Two of the three items are answered on the
    /// machine itself and one is answered at another machine's console, and a
    /// sentence that pretended any of them could be answered here would be the
    /// approve key's problem in prose.
    /// </remarks>
    public static string Said(AppState state)
    {
        if (Asking(state) is not { } asked)
        {
            return "";
        }

        var machine = asked.RunnerLabel is { Length: > 0 } label ? label : asked.Runner;
        var subject = asked.Subject is { Length: > 0 } named ? named : "it";

        var where = asked.Item switch
        {
            ReadinessKinds.Agent =>
                $"{machine} does not declare the agent its profile names ({subject}). The "
              + "binary is the machine's to have: give its path to `gg service install "
              + "--agent-binary <path>` when the machine is built, or set executor-binary on "
              + "it. Nothing here can do that.",

            ReadinessKinds.Credential =>
                $"{machine} cannot resolve the credential {subject}. Send it one with "
              + $"`gg credential send --runner {asked.Runner}`, or put it where that machine's "
              + "own store looks - the modal for it is on the machine's row, not on this gate.",

            ReadinessKinds.Forge =>
                $"{machine} cannot reach its forge '{subject}'. That is a route between that "
              + "machine and that host, and nothing a console does opens one.",

            _ => $"{machine} does not meet its profile, and this build does not know the item "
               + $"'{asked.Item}'. Its own words are below.",
        };

        return asked.Diagnosis is { Length: > 0 } why
            ? $"{where}\n\nWhat the machine said: {why}"
            : where;
    }
}
