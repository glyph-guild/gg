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
public sealed class BackgroundReads(Func<Command, AppState, Task<Func<AppState, AppState>>> read)
{
    private Task<Func<AppState, AppState>>? _running;

    /// <summary>Asks for one, replacing whatever was already in the air.</summary>
    public void Start(Command command, AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // ABANDONED RATHER THAN CANCELLED. The old one will finish and its
        // answer is simply not folded; cancelling a request that is already on
        // the wire buys nothing a dropped result does not.
        _running = read(command, state);
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
