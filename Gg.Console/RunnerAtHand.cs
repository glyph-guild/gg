using System.Diagnostics;

namespace Gg.Console;

/// <summary>
/// The runner on this machine, whoever started it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pid file is the source, not the handle.</b> This held a handle on the
/// child it spawned, so a console that had not spawned one could see nothing
/// and stop nothing - and a runner a few minutes old has usually outlived the
/// console that started it. The handle is kept only so a freshly started runner
/// is visible before it has written its own file.
/// </para>
/// <para>
/// <b>What crosses into the model is a pid.</b> <c>AppState</c> is serialized
/// under <c>GG_STATE_DUMP</c> and handed to the diagnostics bundle, so a
/// process handle there is both unserializable and a live resource in a
/// document.
/// </para>
/// <para>
/// <b>Not the same interface the screen holds.</b> Starting spawns and stopping
/// signals, neither of which a UI session may do; the screen is given
/// <see cref="IRunnerLog"/>, which reads a file and nothing else.
/// </para>
/// </remarks>
public sealed class RunnerAtHand(
    IRunnerLog log,
    RunnerPidFile pidFile,
    IRunnerProcesses processes,
    Func<Process?> spawn) : IDisposable
{
    private Process? _child;

    /// <summary>
    /// The pid of the runner running here, or null if none is.
    /// </summary>
    /// <remarks>
    /// <b>Checked, and the file corrected when it lies.</b> Pids are reused, so
    /// a stale file naming one that now belongs to something else would have
    /// this console report a stranger as its runner and then kill it.
    /// </remarks>
    private int? Running()
    {
        if (_child is { HasExited: false } child)
        {
            return child.Id;
        }

        if (pidFile.Read() is not { } pid)
        {
            return null;
        }

        if (processes.Alive(pid))
        {
            return pid;
        }

        pidFile.Clear();

        return null;
    }

    /// <summary>
    /// Fold what is known about the runner here into the model.
    /// </summary>
    /// <remarks>
    /// Between sessions, because it looks at processes. The log is read on the
    /// session's own tick, where reading a file is what the exception allows.
    /// </remarks>
    public AppState Advance(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state with
        {
            Here = new RunnerHere
            {
                Pid = Running(),
                LogPath = log.Path,
                Log = log.Read(),
            },
        };
    }

    /// <summary>Start one, unless one is already up.</summary>
    /// <remarks>
    /// <b>Refused rather than doubled, whoever started the first.</b> A second
    /// runner registered from one machine is litter in the fleet, and which
    /// console asked for the first one does not change that.
    /// </remarks>
    public AppState Start(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Running() is { } already)
        {
            return state with
            {
                LastRunner = $"A runner is already running here as process {already}.",
            };
        }

        _child?.Dispose();
        _child = spawn();

        if (_child is not null)
        {
            // WRITTEN HERE TOO, and the child writes the same number a moment
            // later. Without it the modal that opens on the keypress would have
            // nothing to show until the runner got round to saying where it is.
            pidFile.Write(_child.Id);
        }

        return Advance(state) with
        {
            LastRunner = _child is null
                ? "Nothing was started: the runner process would not start."
                : $"A runner is starting here as process {_child.Id}.",
        };
    }

    /// <summary>
    /// Shut the runner on this machine down.
    /// </summary>
    /// <remarks>
    /// <b>Through the pid, so it reaches one this console did not start.</b>
    /// That is the ordinary case: `x' did nothing for a whole slice because it
    /// signalled a handle, and the runner it was aimed at had outlived the
    /// console that spawned it.
    /// </remarks>
    public AppState Stop(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Running() is not { } pid)
        {
            return state with { LastRunner = "No runner is running on this machine." };
        }

        var went = processes.Stop(pid);

        // CLEARED EITHER WAY. A pid file outliving the process it names is the
        // next console's stale answer, and one naming a process that would not
        // stop is a file this console will keep offering to kill.
        pidFile.Clear();
        _child?.Dispose();
        _child = null;

        return Advance(state) with
        {
            LastRunner = went
                ? "The runner on this machine has been shut down."
                : $"Process {pid} was asked to stop and has not.",
        };
    }

    public void Dispose()
    {
        // NOT STOPPED HERE, and the pid file is left where it is. The console
        // exiting is not a reason to take a runner down: somebody who started
        // one and closed the console meant to leave it working, and the next
        // console reads the file and finds it.
        _child?.Dispose();
        _child = null;
    }
}
