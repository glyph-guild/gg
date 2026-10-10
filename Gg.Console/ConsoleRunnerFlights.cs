namespace Gg.Console;

/// <summary>
/// Reading what a runner has flown, beside the console rather than instead of it
/// (slice seventy-one).
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ConsoleFacts</c>' shape, one modal over, for its reasons.</b> A pane that
/// is shown wants filling; this returns a PATCH so the fold happens on a tick
/// inside <c>Invoke</c> and the session never waits; and it is asked for when the
/// pane is shown, not at boot, because a request per runner at launch would be
/// paid by everybody for a pane almost nobody opens.
/// </para>
/// <para>
/// <b>A failed read leaves nothing held.</b> The pane then says it could not be
/// read and why, which is a different sentence from a runner that flew nothing.
/// </para>
/// </remarks>
public static class ConsoleRunnerFlights
{
    /// <summary>What to fold, once the read has landed.</summary>
    public static Func<AppState, AppState> Patch(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        var read = Read(data, state);

        return current => current with
        {
            RunnerFlights = read.RunnerFlights,
            RunnerFlightsFor = read.RunnerFlightsFor,
            RunnerFlightSelected = read.RunnerFlightSelected,
            Diagnosis = read.Diagnosis,
        };
    }

    /// <summary>The read itself, so a test can hold it without a screen.</summary>
    /// <remarks>
    /// <b>The runner the modal is open on, and nothing if the fleet has not heard
    /// of it</b> - a child this console just started has a row before it has a
    /// registration, and there is nothing to ask about yet.
    /// </remarks>
    public static AppState Read(ConsoleData data, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        if (EnvironmentRows.Runner(state) is not { } runner)
        {
            return state;
        }

        try
        {
            // THROUGH THE ONE PROJECTION, so how a result becomes state is
            // written once: ConsoleProjection.Apply has the arm.
            return ConsoleProjection.Apply(
                state, data.RunnerFlightsAsync(runner.RunnerId).GetAwaiter().GetResult());
        }
        catch (Exception failed)
        {
            // SAID BY THE PANE, never by a throw: this runs on a task beside a
            // console somebody is using.
            return state with { Diagnosis = failed.Message };
        }
    }
}
