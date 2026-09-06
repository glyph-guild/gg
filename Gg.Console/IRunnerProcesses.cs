using System.Diagnostics;

namespace Gg.Console;

/// <summary>
/// The processes on this machine, as far as the console needs them.
/// </summary>
/// <remarks>
/// <b>A port, because a pid from a file is not a promise.</b> Pids are reused,
/// so every one read off disk is checked before it is reported and before it is
/// signalled - and both of those are things a test has to be able to answer
/// without there being a real process to answer about.
/// </remarks>
public interface IRunnerProcesses
{
    /// <summary>Whether a runner is still running under this pid.</summary>
    bool Alive(int pid);

    /// <summary>Stop it, and say whether it went.</summary>
    bool Stop(int pid);
}
