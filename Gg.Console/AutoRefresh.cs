using Gg.Client;

namespace Gg.Console;

/// <summary>
/// Brings the tab in front of somebody up to date, without the console going
/// away while it does it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SECOND EXCEPTION TO "A UI SESSION MAY NOT READ", and the argument for
/// it is that the session still does not.</b> Refreshing used to end the
/// session: the application is disposed, the alternate screen is left and
/// re-entered, and a new one is built from the model. That is right for handing
/// the terminal to an editor and it is the console vanishing every thirty
/// seconds on a timer.
/// <para>
/// What the rule protects is a session that blocks - a keyboard frozen for as
/// long as the control plane takes. So the request runs on a task owned outside
/// every UI lifetime, which is <see cref="LiveTails"/>' shape, and
/// <see cref="Advance"/> asks only whether it has finished. Every tick returns.
/// Nothing here waits, and the test beside it holds that over four ticks with
/// the read still in the air.
/// </para>
/// </para>
/// <para>
/// <b>What comes back is a patch, not a model.</b> A read answering with a
/// whole <see cref="AppState"/> would be a snapshot taken before the person
/// moved the cursor and applied after. The background half produces a function
/// and the function is applied to whatever is on screen when it lands, so
/// nothing has to list which fields are the read plane - the list
/// <c>ConsoleStart</c> exists to avoid.
/// </para>
/// <para>
/// <b>And only the tab in front of somebody.</b> Refreshing the envelope while
/// a person watches the fleet is a request nobody asked for, once every thirty
/// seconds, for as long as the console is open.
/// </para>
/// </remarks>
public sealed class AutoRefresh(
    Func<TabId, Task<Func<AppState, AppState>>> read, IClock clock, TimeSpan every)
{
    private Task<Func<AppState, AppState>>? _running;

    /// <summary>
    /// When the next one is due, counted from when this was built.
    /// </summary>
    /// <remarks>
    /// <b>Not from the first tick.</b> Set on first use it would measure from
    /// whenever the console got round to looking, which on a boot that took a
    /// second is a first refresh a second late and, in a test, a clock that has
    /// already moved.
    /// </remarks>
    private DateTimeOffset _due = clock.UtcNow + every;

    /// <summary>What the hint line shows while a read is in the air.</summary>
    private const string Working = "⟳";

    /// <summary>
    /// Start one if it is time, fold one if it has landed, and say where that
    /// leaves the countdown.
    /// </summary>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var now = clock.UtcNow;

        if (_running is { IsCompleted: true } finished)
        {
            _running = null;

            // THE CLOCK STARTS WHEN THE ANSWER LANDS, not when the request
            // left. Otherwise a control plane slower than the interval leaves a
            // countdown permanently at zero, which reads as broken.
            _due = now + every;

            state = Folded(state, finished);
        }

        if (_running is null && (state.Refresh.Wanted || now >= _due))
        {
            state = state with { Refresh = state.Refresh with { Wanted = false } };

            if (Reads(state.ActiveTab))
            {
                _running = read(state.ActiveTab);
            }
            else
            {
                // A TAB THAT ASKS NOBODY ANYTHING still resets the clock, or the
                // countdown sits at zero for as long as somebody reads a log.
                _due = now + every;
            }
        }

        return state with
        {
            Refresh = state.Refresh with
            {
                Busy = _running is not null,
                NextIn = _running is not null
                    ? 0
                    : (int)Math.Max(0, Math.Ceiling((_due - now).TotalSeconds)),
            },
        };
    }

    /// <summary>
    /// How the hint line says what this is doing.
    /// </summary>
    /// <remarks>
    /// <b>Nothing counted is nothing to say, not zero.</b> The line is drawn
    /// once before the first tick, and a console that opened with
    /// <c>g refresh 0s</c> read as a clock that had stopped - on the one frame
    /// a person looks at hardest. Busy still outranks the number, because the
    /// number is zero while a read is in the air and the mark is what that
    /// means.
    /// </remarks>
    public static string Says(RefreshState refresh)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        return refresh switch
        {
            { Busy: true } => Working,
            { NextIn: > 0 } counted => $"{counted.NextIn}s",
            _ => "",
        };
    }

    /// <summary>
    /// Whether this tab is one somebody has to go and ask about.
    /// </summary>
    /// <remarks>
    /// The live pane is a local file with a tick of its own and the browser is
    /// a child process this console already owns; neither is a thing to go and
    /// ask the control plane about. The evidence pane renders a flight the
    /// queue already carries.
    /// </remarks>
    private static bool Reads(TabId tab) =>
        tab is not (TabId.Live or TabId.Browse or TabId.Evidence);

    private static AppState Folded(AppState state, Task<Func<AppState, AppState>> finished)
    {
        try
        {
            return finished.Result(state);
        }
        catch (Exception failure) when (failure is AggregateException or InvalidOperationException)
        {
            // A REFRESH THAT FAILED COSTS A REFRESH. Rule 5's third sentence:
            // the rest of the model is still true, and emptying it because one
            // read failed is the shape that rule exists to stop. The reader
            // itself words the ordinary refusals; this is for the ones nobody
            // named.
            return state with
            {
                Diagnosis = "The last refresh did not finish: "
                          + (failure.InnerException ?? failure).Message,
            };
        }
    }
}
