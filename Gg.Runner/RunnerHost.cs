namespace Gg.Runner;

/// <summary>
/// The runner role, hosted by the `gg` binary as a separate child process
/// (`gg runner up` spawns `gg runner serve`). No Whizbang here — the runner
/// needs an HTTP client, git, the filesystem, credential resolution, and a
/// small append-only spool, none of which is built yet.
/// </summary>
public static class RunnerHost
{
    public static int Run()
    {
        System.Console.WriteLine(
            $"gg-runner (pid {Environment.ProcessId}): idle — the work loop is not built yet. Terminate with SIGTERM.");
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }
}
