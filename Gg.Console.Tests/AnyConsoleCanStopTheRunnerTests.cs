namespace Gg.Console.Tests;

/// <summary>
/// The runner on this machine can be stopped from any console, not only the
/// one that started it.
/// </summary>
/// <remarks>
/// <para>
/// <b>`x' did nothing, and the reason was structural.</b> RunnerAtHand held a
/// handle on the child it spawned, so a console that had not spawned one had
/// nothing to signal. Observed: a runner whose parent console had exited, still
/// running, reparented to init - which is the ordinary state of a runner a few
/// minutes after you start it and close the window. The handle answers "did I
/// start this", and the question a person is asking is "is one running here".
/// </para>
/// <para>
/// <b>A file on disk is what makes it the machine's rather than the
/// process's.</b> `gg runner up' writes its pid where any console can read it
/// and removes it on the way out - the same shape the runner store beside it
/// already has, and the reason both are files is that the thing they describe
/// outlives whoever asked for it.
/// </para>
/// <para>
/// <b>A pid alone is not a promise.</b> Pids are reused, and a stale file
/// naming one that now belongs to something else would have this console kill a
/// stranger. Liveness is asked of the port that owns processes, and the file is
/// cleared the moment it is found to be lying.
/// </para>
/// </remarks>
public class AnyConsoleCanStopTheRunnerTests
{
    private sealed class Processes : IRunnerProcesses
    {
        internal HashSet<int> Living { get; } = [];

        internal List<int> Stopped { get; } = [];

        public bool Alive(int pid) => Living.Contains(pid);

        public bool Stop(int pid)
        {
            Stopped.Add(pid);
            return Living.Remove(pid);
        }
    }

    private sealed class Log : IRunnerLog
    {
        public string Path => "/tmp/gg/runner.log";

        public IReadOnlyList<string> Read() => ["nothing ready"];
    }

    private static (RunnerAtHand Runner, Processes Processes, RunnerPidFile File) Console()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-{Guid.NewGuid():N}.pid");
        var file = new RunnerPidFile(path);
        var processes = new Processes();

        return (new RunnerAtHand(new Log(), file, processes, () => null), processes, file);
    }

    [Test]
    public async Task A_pid_written_by_one_process_is_read_by_another()
    {
        var (_, _, file) = Console();

        try
        {
            file.Write(4242);

            await Assert.That(new RunnerPidFile(file.Path).Read()).IsEqualTo(4242)
                .Because("`gg runner up' writes it and a console that never met that process "
                       + "reads it - which is the whole of what a file is for here.");

            file.Clear();

            await Assert.That(new RunnerPidFile(file.Path).Read()).IsNull();
        }
        finally
        {
            file.Clear();
        }
    }

    [Test]
    public async Task Nothing_written_and_nothing_readable_are_both_nothing()
    {
        var missing = new RunnerPidFile(
            Path.Combine(Path.GetTempPath(), $"gg-absent-{Guid.NewGuid():N}.pid"));

        await Assert.That(missing.Read()).IsNull()
            .Because("no file is no runner, and it must not be an exception on a boot path.");

        var nonsense = new RunnerPidFile(
            Path.Combine(Path.GetTempPath(), $"gg-junk-{Guid.NewGuid():N}.pid"));

        try
        {
            File.WriteAllText(nonsense.Path, "not a number");

            await Assert.That(nonsense.Read()).IsNull()
                .Because("and neither is a file somebody has been editing.");
        }
        finally
        {
            File.Delete(nonsense.Path);
        }
    }

    [Test]
    public async Task A_console_that_started_nothing_still_sees_the_runner()
    {
        var (runner, processes, file) = Console();

        try
        {
            file.Write(4242);
            processes.Living.Add(4242);

            var state = runner.Advance(new AppState());

            await Assert.That(state.Here?.Pid).IsEqualTo(4242);
            await Assert.That(state.Here?.Up).IsTrue()
                .Because("this is the ordinary state of a runner a few minutes after you "
                       + "start it: still up, and its console gone.");
        }
        finally
        {
            file.Clear();
        }
    }

    [Test]
    public async Task And_can_stop_it()
    {
        var (runner, processes, file) = Console();

        try
        {
            file.Write(4242);
            processes.Living.Add(4242);

            var state = runner.Stop(new AppState());

            await Assert.That(processes.Stopped).IsEquivalentTo(new[] { 4242 });
            await Assert.That(file.Read()).IsNull()
                .Because("a pid file outliving the process it names is the next console's "
                       + "stale answer.");
            await Assert.That(state.LastRunner!).Contains("shut down");
        }
        finally
        {
            file.Clear();
        }
    }

    [Test]
    public async Task A_pid_that_is_no_longer_alive_is_not_a_runner_and_is_not_killed()
    {
        // PIDS ARE REUSED. A stale file naming one that now belongs to something
        // else would have this console kill a stranger, so liveness is asked
        // before anything is signalled and the file is cleared when it lies.
        var (runner, processes, file) = Console();

        try
        {
            file.Write(4242);

            var seen = runner.Advance(new AppState());

            await Assert.That(seen.Here?.Pid).IsNull();
            await Assert.That(file.Read()).IsNull()
                .Because("found to be lying, so cleared where it was found.");

            var stopped = runner.Stop(new AppState());

            await Assert.That(processes.Stopped).IsEmpty()
                .Because("nothing was signalled, because nothing there was ours.");
            await Assert.That(stopped.LastRunner!).Contains("No runner");
        }
        finally
        {
            file.Clear();
        }
    }

    [Test]
    public async Task Starting_is_refused_while_one_is_up_whoever_started_it()
    {
        var (runner, processes, file) = Console();

        try
        {
            file.Write(4242);
            processes.Living.Add(4242);

            var state = runner.Start(new AppState());

            await Assert.That(state.LastRunner!).Contains("already running")
                .Because("a second runner registered from one machine is litter in the fleet, "
                       + "and which console started the first one does not change that.");
        }
        finally
        {
            file.Clear();
        }
    }

    [Test]
    public async Task The_runner_writes_its_own_pid_and_takes_it_back()
    {
        var program = Sources.Read("Gg.Cli", "Program.cs");

        await Assert.That(program).Contains("RunnerPidFile")
            .Because("`gg runner up' is the one process that always knows a runner is "
                   + "running here, whoever asked for it.");
        await Assert.That(program).Contains(".Clear()")
            .Because("and it takes the file back on the way out, or the next console reads a "
                   + "runner that stopped hours ago.");
    }
}
