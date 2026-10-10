namespace Gg.Console;

/// <summary>Which of the sessions view's four acts (slice seventy-one).</summary>
public enum RemoteSessionKind
{
    /// <summary>A new session on the machine.</summary>
    Start,

    /// <summary>Attach to a running session, or resume an ended one.</summary>
    Open,

    /// <summary>Forget one ended session.</summary>
    Forget,

    /// <summary>Forget every ended session on the machine.</summary>
    ForgetEnded,
}

/// <summary>
/// One act on a machine's agent sessions, read off the model when its key was pressed.
/// </summary>
/// <param name="Kind">Which act.</param>
/// <param name="RunnerId">The machine, by the runner id the control plane knows it by.</param>
/// <param name="RunnerLabel">What the mux's column calls it.</param>
/// <param name="SessionId">The session acted on, or null for a new one and for forgetting every ended one.</param>
/// <param name="Alive">Whether that session's agent runs, which decides attach or resume.</param>
/// <param name="Directory">Where it worked, which a resume starts in again.</param>
public sealed record RemoteSessionAct(
    RemoteSessionKind Kind,
    string RunnerId,
    string RunnerLabel,
    string? SessionId = null,
    bool Alive = false,
    string? Directory = null);

/// <summary>
/// The runner modal's session acts, between UI sessions, and Remote Control's hand-off into
/// the modal (slice seventy-one, S71.4-02).
/// </summary>
/// <remarks>
/// <para>
/// <b>The commands carry nothing, so the act is read off the model</b>: the machine is the
/// runner the modal is open on, and the session is the one under the sessions view's cursor.
/// </para>
/// <para>
/// <b>The mux reaches; this says what came of it.</b> A started or opened session is put on a
/// row, which asks the shell to show it; the runner modal is left open, so ctrl-g 0 comes back
/// to it. A forget says so, and that the fleet's list follows the machine's next heartbeat.
/// </para>
/// </remarks>
public static class ConsoleRemoteSession
{
    /// <summary>The act a command asks for, or null when the model has nothing for it to act on.</summary>
    public static RemoteSessionAct? For(AppState state, Command command)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Rows.Selected(state) is not { } runner || EnvironmentRows.Runner(state) is not { } summary)
        {
            return null;
        }

        var session = RunnerActivity.SelectedSession(state);

        return command switch
        {
            Command.StartRemoteSession =>
                new(RemoteSessionKind.Start, summary.RunnerId, runner.Label),
            Command.ForgetEndedRemoteSessions =>
                new(RemoteSessionKind.ForgetEnded, summary.RunnerId, runner.Label),
            Command.OpenRemoteSession when session is not null =>
                new(RemoteSessionKind.Open, summary.RunnerId, runner.Label,
                    session.SessionId, session.Alive, session.Directory),
            Command.ForgetRemoteSession when session is { Alive: false } =>
                new(RemoteSessionKind.Forget, summary.RunnerId, runner.Label, session.SessionId),
            _ => null,
        };
    }

    /// <summary>Does the act through <paramref name="mux"/>, and says what came of it.</summary>
    public static AppState Act(Mux mux, AppState state, RemoteSessionAct act)
    {
        ArgumentNullException.ThrowIfNull(mux);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(act);

        var machine = new RemoteMachine(act.RunnerId, act.RunnerLabel);

        switch (act.Kind)
        {
            case RemoteSessionKind.Start:
            case RemoteSessionKind.Open:
                var opened = mux.OpenRemote(
                    machine, act.Kind == RemoteSessionKind.Start ? null : act.SessionId, act.Alive, act.Directory);

                // PLACING THE ROW ASKED THE SHELL TO SHOW IT, so the next turn of the loop is
                // the agent; only a refusal has anything to say here.
                return opened.Agent is null
                    ? state with { LastRunner = opened.Refused ?? $"{act.RunnerLabel} started nothing." }
                    : state with { LastRunner = null };

            default:
                var single = act.Kind == RemoteSessionKind.Forget;
                var refused = mux.ForgetRemote(machine, single ? act.SessionId : null);

                return state with
                {
                    LastRunner = refused
                        ?? (single
                            ? $"Session {Short(act.SessionId)} forgotten; the list updates on the machine's next heartbeat."
                            : $"Every ended session on {act.RunnerLabel} forgotten; the list updates on the machine's next heartbeat."),
                };
        }
    }

    /// <summary>
    /// The console with the runner modal open on <paramref name="runnerId"/>'s sessions, or
    /// null when the fleet the console holds does not list it.
    /// </summary>
    public static AppState? Opened(AppState state, string runnerId)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rows = Rows.Runners(state);
        var at = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.Equals(rows[i].Id, runnerId, StringComparison.Ordinal))
            {
                at = i;
                break;
            }
        }

        return at < 0
            ? null
            : state with
            {
                ActiveTab = TabId.Runners,
                RunnerSelected = at,
                Mode = UiMode.Runner,
                RunnerView = RunnerView.Sessions,
                RunnerSessionSelected = 0,
                ComposingFor = ComposingFor.Nothing,
            };
    }

    private static string Short(string? id) => id is null ? "" : id.Length <= 8 ? id : id[..8];
}
