using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// The act behind an agent-login gate's <c>s</c>: what the console decides
/// before the terminal is handed over.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gate says which runner, so the cursor does not.</b> The queue's
/// cursor is on a FLIGHT - the maintenance flight the incident minted - and
/// the machine that needs the login is named in the gate's own maintenance
/// ask. Reading the runner off a cursor would log in whatever row happened to
/// be selected on a different pane.
/// </para>
/// <para>
/// <b>It refuses here rather than at the channel</b> -
/// <see cref="ConsoleSendCredential"/>'s argument, and it matters for the same
/// reason with one more step in it: the cost of getting this wrong is that
/// somebody opens a browser, signs in, and types a code for a machine that was
/// never reachable.
/// </para>
/// <para>
/// <b>And it never decides the gate.</b> What clears a runner's agent-login
/// gate is the runner reporting <c>ready</c> - the incident recovers and the
/// control plane withdraws the flight - so an approval typed here would answer
/// a question nobody asked and leave the machine still unable to fly. That is
/// asserted over this file's source in <c>AgentLoginFromTheConsoleTests</c>.
/// </para>
/// <para>
/// <b>What it says lands in <c>LastCredential</c></b>, not
/// <c>LastDecision</c>: the ceremony ends with a credential on a machine, which
/// is the field the send already uses, and a login outcome under a name that
/// says decision would read as an answer to the gate.
/// </para>
/// </remarks>
public static class ConsoleAgentLogin
{
    /// <summary>Runs the ceremony for one runner and one agent, and says how it went.</summary>
    public delegate string Ceremony(string runnerId, string provider);

    public static AppState Begin(AppState state, Ceremony ceremony)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ceremony);

        // A GATE THAT ASKS FOR NOTHING IS AN ORDINARY GATE. The key is not
        // offered over one, and a command that arrived anyway must not start a
        // program for a question about something else - the hint line is
        // derived in one place and dispatch happens in another, so what the
        // key obeys is checked again here.
        if (Asking(state) is not { } asked)
        {
            return state with
            {
                LastCredential =
                    "This gate is not asking for an agent login, so there is nothing to log "
                  + "in. Answer it with a or r, or read it and leave it open.",
            };
        }

        if (Rows.Runners(state).FirstOrDefault(row => string.Equals(
                row.Id, asked.Runner, StringComparison.OrdinalIgnoreCase)) is not { } runner)
        {
            // A GATE CAN OUTLIVE THE FLEET READ UNDER IT, and the id is what
            // somebody takes to `gg runners` to find out what happened to it.
            return state with
            {
                LastCredential =
                    $"The gate names runner {asked.Runner}, which is not in the fleet this "
                  + "console can see. It may have been retired since the gate opened. "
                  + "`gg runners` lists the ones that are left.",
            };
        }

        if (runner.State.StartsWith(RunnerStates.Offline, StringComparison.Ordinal))
        {
            return state with
            {
                LastCredential =
                    $"{runner.Label} is not beating, so the ceremony cannot be started on it. "
                  + "The login runs on that machine and an introduction is picked up on a "
                  + "heartbeat. Start it and try again.",
            };
        }

        return Spent(state) with { LastCredential = ceremony(runner.Id, asked.Provider) };
    }

    /// <summary>
    /// The maintenance ask on the selected gate, when it is one this build
    /// knows how to act on.
    /// </summary>
    /// <remarks>
    /// <b>Closed on the kind, so a newer control plane cannot steer this.</b>
    /// <see cref="GateMaintenanceKinds"/> is a closed vocabulary; a kind this
    /// build does not know is a gate to read rather than an act to offer, and
    /// offering the key for one would start the wrong ceremony for the right
    /// reason. The keymap derives its own dimension from this same method, so
    /// the button and the act cannot disagree about whether there is one.
    /// </remarks>
    public static GateMaintenance? Asking(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.SelectedGate?.Maintenance is
        { Kind: GateMaintenanceKinds.AgentLogin, Runner.Length: > 0, Provider.Length: > 0 } asked
            ? asked
            : null;
    }

    /// <summary>The modal is over: the ceremony took the terminal.</summary>
    /// <remarks>
    /// Coming back to a modal over a gate that is about to be withdrawn is a
    /// screen to dismiss, and what happened is on the activity line.
    /// </remarks>
    public static AppState Spent(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state with { Mode = UiMode.Normal };
    }
}
