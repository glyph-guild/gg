using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Console;

/// <summary>One flight the runner under the cursor claimed, as its table draws it.</summary>
/// <param name="FlightId">The whole id, carried for whatever opens it; no column draws it.</param>
/// <param name="Flight">Its tenant-wide number, or the short id when it has none.</param>
/// <param name="Kind">Its work kind, or empty.</param>
/// <param name="State">Where it is now, in the words a flight summary uses.</param>
/// <param name="Claimed">When this runner claimed it, or empty.</param>
/// <param name="Ended">When it reached an exit, or empty while it flies.</param>
public sealed record RunnerFlightRow(
    string FlightId,
    string Flight,
    string Kind,
    string State,
    string Claimed,
    string Ended);

/// <summary>One ad hoc agent session the runner under the cursor holds.</summary>
/// <param name="SessionId">The whole id, which the session actions will need.</param>
/// <param name="Session">Enough of it to tell two apart.</param>
/// <param name="State">running or ended.</param>
/// <param name="Started">When it started.</param>
/// <param name="Ended">When its agent ended, or empty while it runs.</param>
/// <param name="Directory">Where it works, or empty when the machine did not say.</param>
public sealed record RunnerSessionRow(
    string SessionId,
    string Session,
    string State,
    string Started,
    string Ended,
    string Directory);

/// <summary>What the runner modal's sessions view is over, for its keys (slice seventy-one).</summary>
public enum RunnerSessionPane
{
    /// <summary>A machine that takes sessions, with no row under the cursor.</summary>
    NoneSelected,

    /// <summary>A session whose agent runs: enter attaches.</summary>
    OverALiveOne,

    /// <summary>A session whose agent ended: enter resumes, d forgets.</summary>
    OverAnEndedOne,
}

/// <summary>
/// What the runner under the cursor has done and is holding: the runner modal's
/// flights and sessions views (slice seventy-one, S71.4-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure, for <c>EnvironmentRows</c>' reason</b>: the screen cannot be built
/// without a terminal, so what a cell says and which absence a pane is in are
/// answered here, where a test can ask.
/// </para>
/// <para>
/// <b>Two sources.</b> The sessions come with the fleet read, on the row the
/// modal is open on, already cleaned by <c>Rows</c>. The flights are a read of
/// their own, folded by <c>ConsoleProjection</c> and held with the runner they
/// are about.
/// </para>
/// <para>
/// <b>Times are absolute</b>, like the fleet's last-heard: nothing on the model
/// carries a clock, so an age would be computed in the view and two panes would
/// disagree about what "now" is.
/// </para>
/// </remarks>
public static class RunnerActivity
{
    public static IReadOnlyList<string> FlightColumns { get; } =
        ["flight", "kind", "state", "claimed", "ended"];

    public static IReadOnlyList<string> SessionColumns { get; } =
        ["session", "state", "started", "ended", "directory"];

    /// <summary>What the session state cell says of one whose agent still runs.</summary>
    public const string Running = "running";

    /// <summary>And of one whose agent ended, which can be resumed.</summary>
    public const string Ended = "ended";

    /// <summary>
    /// The runner whose flights the pane owes a read, or null when it owes none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SHOWING THE PANE IS THE ASKING</b> - <c>FlightDetails.FactsOwed</c>'s
    /// rule, because the facts tab learned what a read only a keypress started
    /// costs: no key sends it, so nothing ever showed.
    /// </para>
    /// <para>
    /// <b>Only a runner the fleet has heard of.</b> A child this console just
    /// started has a row before it has a registration, and its id there is a
    /// file's, not the control plane's.
    /// </para>
    /// <para>
    /// <b>NOT WHILE ANOTHER READ IS IN THE AIR.</b> One read runs at a time and
    /// starting a second abandons the first.
    /// </para>
    /// </remarks>
    public static string? FlightsOwed(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state is { Mode: UiMode.Runner, RunnerView: RunnerView.Flights, ReadInFlight: false }
            && EnvironmentRows.Runner(state) is { } runner
            && FlightsHeld(state) is null
                ? runner.RunnerId
                : null;
    }

    /// <summary>The flights held, when they are this runner's.</summary>
    public static RunnerFlightList? FlightsHeld(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.RunnerFlights is { } flights
            && EnvironmentRows.Runner(state) is { } runner
            && string.Equals(state.RunnerFlightsFor, ControlText.Strip(runner.RunnerId), StringComparison.Ordinal)
                ? flights
                : null;
    }

    /// <summary>One row per flight this runner claimed, newest first as the control plane sent them.</summary>
    public static IReadOnlyList<RunnerFlightRow> Flights(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return FlightsHeld(state) is not { } held
            ? []
            : [.. held.Flights.Select(f => new RunnerFlightRow(
                FlightId: f.FlightId,
                Flight: f.Number is { } number ? FlightRef.Format(number) : Short(f.FlightId),
                Kind: f.Kind ?? "",
                State: f.State,
                Claimed: f.ClaimedAt is { } claimed ? claimed.ToString("u") : "",

                // EMPTY WHILE IT FLIES. A dash would read as an ending nobody
                // recorded, and a flight in the air has not ended.
                Ended: f.EndedAt is { } ended ? ended.ToString("u") : ""))];
    }

    /// <summary>
    /// What the flights view says when it has no rows to say it with.
    /// </summary>
    /// <remarks>
    /// <b>THREE ABSENCES, THREE SENTENCES</b>, <c>FlightDetails.FactsAbsence</c>'s
    /// rule: a read in the air, a read that failed, and a runner that genuinely
    /// flew nothing. The first drawn as the last would say something false about
    /// a machine.
    /// </remarks>
    public static string FlightsAbsence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (EnvironmentRows.Runner(state) is null)
        {
            return "The control plane has not heard from this runner yet, so it has flown nothing "
                 + "anyone recorded.";
        }

        if (FlightsHeld(state) is not { } held)
        {
            if (state.ReadInFlight)
            {
                return "What this runner has flown is still coming.";
            }

            return "What this runner has flown could not be read"
                 + (state.Diagnosis is { Length: > 0 } why ? $": {why}" : ".")
                 + " Turn away from this view and back to try again.";
        }

        return held.Flights.Count == 0
            ? "This runner has flown nothing."
            : "";
    }

    /// <summary>
    /// The sessions the runner under the cursor holds: the live ones first, then
    /// the newest.
    /// </summary>
    /// <remarks>
    /// <b>The order the cursor indexes</b>, so <see cref="SelectedSession"/> reads
    /// through this rather than through the wire's list.
    /// </remarks>
    public static IReadOnlyList<RunnerSessionRow> Sessions(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return [.. Ordered(state).Select(s => new RunnerSessionRow(
            SessionId: s.SessionId,
            Session: Short(s.SessionId),
            State: s.Alive ? Running : Ended,
            Started: s.StartedAt.ToString("u"),
            Ended: s.EndedAt is { } ended ? ended.ToString("u") : "",
            Directory: s.Directory ?? ""))];
    }

    /// <summary>The session under the cursor, or null when there is none.</summary>
    /// <remarks>
    /// <b>Null rather than a fallback</b>, <c>Rows.Selected</c>'s rule: the list is
    /// a heartbeat's and shrinks under a refresh, so an index past the end is
    /// reachable, and answering some other session there would act on one
    /// nobody chose.
    /// </remarks>
    public static AgentSessionStanding? SelectedSession(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var sessions = Ordered(state);

        return state.RunnerSessionSelected >= 0 && state.RunnerSessionSelected < sessions.Count
            ? sessions[state.RunnerSessionSelected]
            : null;
    }

    /// <summary>
    /// What the sessions view says when it has no rows to say it with.
    /// </summary>
    /// <remarks>
    /// <b>Two absences.</b> A machine whose file does not opt in will refuse
    /// every session, and its remedy is that file; one that opts in and holds
    /// none has nothing wrong with it. The sentence names no key, because none
    /// is bound yet - a sentence asking for a press nobody can make is the facts
    /// tab's old defect.
    /// </remarks>
    public static string SessionsAbsence(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Rows.Selected(state) is not { } row)
        {
            return "The control plane has not heard from this runner yet, so it holds no sessions.";
        }

        if (Sessions(state).Count > 0)
        {
            return "";
        }

        // NULL AND FALSE ARE ONE ANSWER, the contract's own words: a gg older
        // than the setting says nothing, and says nothing because it takes none.
        // The machine's own refusal is worded the same way.
        return row.AcceptsAgentSessions is true
            ? "No sessions on this machine yet."
            : "This machine's configuration does not say `accept-agent-sessions`, so it runs no "
            + "agent sessions.";
    }

    /// <summary>
    /// What the sessions view's keys are over, or null where they do not apply (slice
    /// seventy-one).
    /// </summary>
    /// <remarks>
    /// <b>Null off the view and over a machine that does not take sessions</b>, whose every
    /// act the machine would refuse; otherwise no row, a live one or an ended one.
    /// </remarks>
    public static RunnerSessionPane? Pane(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state is not { Mode: UiMode.Runner, RunnerView: RunnerView.Sessions }
            || Rows.Selected(state) is not { AcceptsAgentSessions: true })
        {
            return null;
        }

        return SelectedSession(state) switch
        {
            null => RunnerSessionPane.NoneSelected,
            { Alive: true } => RunnerSessionPane.OverALiveOne,
            _ => RunnerSessionPane.OverAnEndedOne,
        };
    }

    private static IReadOnlyList<AgentSessionStanding> Ordered(AppState state) =>
        Rows.Selected(state)?.AgentSessions is not { } sessions
            ? []
            : [.. sessions
                .OrderBy(s => s.Alive ? 0 : 1)
                .ThenByDescending(s => s.StartedAt)];

    private static string Short(string id) => id.Length <= 8 ? id : id[..8];
}
