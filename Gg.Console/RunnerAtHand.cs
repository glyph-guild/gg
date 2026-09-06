using System.Diagnostics;

namespace Gg.Console;

/// <summary>
/// The runner process this console started, and the two things that can be
/// done to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It holds the handle, because a model may not.</b> <c>AppState</c> is
/// serialized under <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle,
/// so what crosses is a pid, an exit code and the tail of a log. This is
/// <c>ReaderSessions</c>' shape for the same reason: a session must retain
/// nothing across a rebuild, so whoever composed the console owns the child and
/// stops it at the end.
/// </para>
/// <para>
/// <b>Not the same interface the screen holds.</b> Starting spawns and stopping
/// signals, neither of which a UI session may do; the screen is given
/// <see cref="IRunnerLog"/>, which reads a file and nothing else. Two
/// interfaces rather than one is what keeps that difference structural.
/// </para>
/// </remarks>
public sealed class RunnerAtHand(IRunnerLog log, Func<Process?> spawn) : IDisposable
{
    private Process? _child;

    /// <summary>
    /// Fold what is known about the child into the model.
    /// </summary>
    /// <remarks>
    /// <b>Between sessions, because it touches a process.</b> The log is read on
    /// the session's own tick; whether the child is still up is asked here,
    /// where spawning is allowed and the terminal is free.
    /// </remarks>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_child is null)
        {
            return state;
        }

        return state with
        {
            Here = new RunnerHere
            {
                Pid = _child.Id,
                Exit = _child.HasExited ? _child.ExitCode : null,
                LogPath = log.Path,
                Log = log.Read(),
            },
        };
    }

    /// <summary>Start one, unless one is already up.</summary>
    /// <remarks>
    /// <b>Refused rather than doubled.</b> A second runner registered from one
    /// machine is litter in the fleet, and the key that reaches this is meant to
    /// be gone while one is running - this is the half that holds when it is
    /// not, which is every race between the fleet's view and the child's.
    /// </remarks>
    public AppState Start(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_child is { HasExited: false })
        {
            return state with
            {
                LastRunner = $"A runner is already running here as process {_child.Id}.",
            };
        }

        _child?.Dispose();
        _child = spawn();

        return Advance(state) with
        {
            LastRunner = _child is null
                ? "Nothing was started: the runner process would not start."
                : $"A runner is starting here as process {_child.Id}.",
        };
    }

    /// <summary>
    /// Ask it to stop, and wait a moment for it to.
    /// </summary>
    /// <remarks>
    /// <b>Asked, then told.</b> A runner holding a lease should release it, so
    /// the polite signal goes first; a child that has not gone after the grace
    /// below is one that cannot, and leaving it running would leave the fleet
    /// with a runner nobody in this console can reach.
    /// </remarks>
    public AppState Stop(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_child is not { HasExited: false } child)
        {
            return state with
            {
                LastRunner = "No runner started from this console is running.",
            };
        }

        try
        {
            child.CloseMainWindow();
            child.Kill(entireProcessTree: true);
            child.WaitForExit(Grace);
        }
        catch (Exception failure) when (failure is InvalidOperationException
                                            or NotSupportedException
                                            or System.ComponentModel.Win32Exception)
        {
            return state with { LastRunner = "The runner would not stop: " + failure.Message };
        }

        return Advance(state) with
        {
            LastRunner = child.HasExited
                ? "The runner on this machine has been shut down."
                : "The runner was asked to stop and has not yet.",
        };
    }

    /// <summary>How long a shutdown waits before saying it did not happen.</summary>
    private const int Grace = 5000;

    public void Dispose()
    {
        // NOT KILLED HERE. The console exiting is not a reason to take a runner
        // down: somebody who started one and closed the console meant to leave
        // it working, and the flight it holds outlives this process by design.
        _child?.Dispose();
        _child = null;
    }
}
