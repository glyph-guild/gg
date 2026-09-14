namespace Gg.Console;

/// <summary>
/// A read a keypress asked for, made beside the console rather than instead of it.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="AutoRefresh"/>'s shape, for <see cref="AutoRefresh"/>'s
/// reason.</b> That is "THE SECOND EXCEPTION TO 'A UI SESSION MAY NOT READ',
/// and the argument for it is that the session still does not": the request
/// runs on a task owned outside every UI lifetime, and the tick asks only
/// whether it has finished. What the rule protects is a session that BLOCKS,
/// and nothing here blocks.
/// </para>
/// <para>
/// <b>What it replaces is a screen taken away and given back.</b> Opening a
/// flight's detail was declared as the shell's, so the session ended,
/// Terminal.Gui was disposed, the alternate screen was left, ONE request was
/// made, and a new session was built from the model. That teardown is right for
/// handing the terminal to <c>$EDITOR</c> and it is a blink for fetching a log.
/// </para>
/// <para>
/// <b>What comes back is a patch, not a model</b> — the same argument again. A
/// read answering with a whole <see cref="AppState"/> is a snapshot taken
/// before the person moved and applied after.
/// </para>
/// <para>
/// <b>One at a time, and the newest wins.</b> A person moving through flights
/// faster than the control plane answers would otherwise have several in the
/// air, landing in whatever order they finish — and the last to land would win
/// rather than the last asked for.
/// </para>
/// </remarks>
/// <param name="read">What to ask, and what comes back as a patch.</param>
/// <param name="ready">
/// Whether a command can be served WITHOUT starting anything, or null when
/// nothing this console reads needs starting.
/// </param>
public sealed class BackgroundReads(
    Func<Command, AppState, Task<Func<AppState, AppState>>> read,
    Func<Command, bool>? ready = null)
{
    private Task<Func<AppState, AppState>>? _running;

    /// <summary>
    /// Whether this can be answered beside the console, or needs the shell once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The spawn is the whole of it.</b> Four guards say browsing is the
    /// shell's and each gives one reason: a reader is an executable launched
    /// with a credential in its environment, and a session may start neither.
    /// They are right, and they are about STARTING one. Asking a reader that
    /// was running before the session existed is what <c>LiveTails</c> already
    /// does.
    /// </para>
    /// <para>
    /// <b>So the first browse of a console lifetime is still the shell's</b> -
    /// it has nothing to talk to, and making one is the act a session may not
    /// perform. Every one after it folds in. Nothing is started at launch: a
    /// reader nobody asked for is a child nobody asked for, and the console
    /// comes up without waiting on one.
    /// </para>
    /// <para>
    /// <b>No predicate means ready</b>, because its absence has to be the
    /// behaviour that was there before it - every console composed without a
    /// reader, and every test that builds a screen.
    /// </para>
    /// </remarks>
    public bool Ready(Command command) => ready is null || ready(command);

    /// <summary>
    /// Asks for one, replacing whatever was already in the air.
    /// </summary>
    /// <param name="landed">
    /// Called when the answer arrives, on whatever thread it arrives on.
    /// </param>
    /// <remarks>
    /// <b>Told rather than polled.</b> Terminal.Gui's own guidance is that all
    /// UI work happens on the main thread and a background result reaches it
    /// through <c>Invoke</c>; the first version of this asked every hundred and
    /// twenty milliseconds whether the task had finished, which is a timer
    /// spinning for something that can simply say so. The callback does no UI
    /// work itself — it hands back to whoever can.
    /// </remarks>
    public void Start(Command command, AppState state, Action? landed = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        // ABANDONED RATHER THAN CANCELLED. The old one will finish and its
        // answer is simply not folded; cancelling a request that is already on
        // the wire buys nothing a dropped result does not.
        var running = read(command, state);
        _running = running;

        if (landed is null)
        {
            return;
        }

        // NOTHING MAY THROW OUT OF THIS. It runs on a thread pool thread beside
        // a console somebody is using, and an exception escaping here would
        // take the process down over a read.
        _ = running.ContinueWith(
            _ =>
            {
                try
                {
                    landed();
                }
                catch (Exception)
                {
                    // Said by the absence, the way a failed read already is.
                }
            },
            TaskScheduler.Default);
    }

    /// <summary>
    /// Folds one if it has landed, and says whether anything is still coming.
    /// </summary>
    /// <remarks>
    /// <b>It never waits and never throws.</b> A read that failed must not take
    /// a UI session down mid-render — the terminal would be left in a state
    /// nothing is holding — so a failure lands as no patch at all and the pane
    /// says which absence it is.
    /// </remarks>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_running is not { IsCompleted: true } finished)
        {
            return state with { ReadInFlight = _running is not null };
        }

        _running = null;

        try
        {
            state = finished.Result(state);
        }
        catch (Exception)
        {
            // Said by the absence rather than by a crash: a story nobody
            // fetched and one a read failed to bring back are the same thing
            // to a person, and neither is worth a dead console.
        }

        return state with { ReadInFlight = false };
    }
}
