using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;
using Gg.Runner.Sweeps;

namespace Gg.Runner.Tests;

/// <summary>
/// What an agent's sweep concluded, read from what it did: a sweep that never
/// read its tracker did not find nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 11: the board must never look quiet when it is blind.</b> An agent
/// whose every query was refused - an expired credential, a tracker serving a
/// sign-in page - finishes its turn cleanly having nominated nothing, and a
/// runner that reported that as swept would tell the board the backlog was
/// empty. So a sweep counts as having looked only when a query came back, and
/// the call's answer is the record of that, as a nomination's is.
/// </para>
/// <para>
/// <b>The tracker's words stay here.</b> A refused query's text can quote the
/// page that came back, and the report crosses to the control plane - so the
/// diagnosis says what happened in this platform's words and points at the
/// transcript on this machine.
/// </para>
/// <para>
/// <b>The bound is measured before a sweep runs, as before a flight.</b> A
/// sweep is an agent on this machine, and a machine where withholding a move
/// does not withhold it is not one to start an agent on.
/// </para>
/// </remarks>
public class ASweepThatNeverReadItsTrackerDidNotSweepTests
{
    private const string Declared = "board=https://tracker.example/acme|local:acme/board";

    private static readonly SelfInvocation Self = SelfInvocation.For("/opt/gg/gg", null)!;

    private static readonly string QueryName = $"mcp__board__{QueryTool.Name}";

    private sealed class Scripted(
        Func<ExecutorRequest, (ExecutorRun Run, string Transcript)> sweep,
        bool breaksTheBound = false) : IExecutorPort
    {
        internal List<ExecutorRequest> Asked { get; } = [];

        internal bool DirectoryExistedWhileRunning { get; private set; }

        public ExecutorCapabilities Capabilities { get; } = new() { Rung = ExecutorRungs.Frontier };

        public async Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request);

            if (request.LoopId != SweepLauncher.LoopId)
            {
                if (breaksTheBound)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(request.WorkingDirectory, MoveBoundProbe.Canary),
                        "unbound", cancellationToken);
                }

                return ExecutorRun.Completed(request.LoopId, "did nothing", 1, TimeSpan.Zero, []);
            }

            DirectoryExistedWhileRunning = Directory.Exists(request.WorkingDirectory);

            var (run, transcript) = sweep(request);
            Directory.CreateDirectory(Path.GetDirectoryName(request.TranscriptPath)!);
            await File.WriteAllTextAsync(request.TranscriptPath, transcript, cancellationToken);
            return run;
        }
    }

    private static string Call(string id, string name, string input, bool? refused)
    {
        var call =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          + "\"id\":\"" + id + "\",\"name\":\"" + name + "\",\"input\":" + input + "}]}}";

        if (refused is not { } error)
        {
            return call;
        }

        return call + "\n"
             + "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\","
             + "\"tool_use_id\":\"" + id + "\""
             + (error ? ",\"is_error\":true" : "")
             + ",\"content\":[{\"type\":\"text\",\"text\":\""
             + (error ? "https://tracker.example/acme answered, and the answer was not data - "
                      + "what arrived begins: <html>Sign in to Acme Payroll" : "{\\\"items\\\":[]}")
             + "\"}]}]}}";
    }

    private static string Queried(string id, bool refused = false) =>
        Call(id, QueryName, "{}", refused);

    private static string Nominated(string id, string subject) =>
        Call(id, NominationTool.Qualified,
            "{\"subject\":\"" + subject + "\",\"version\":\"4\",\"work_kind\":\"review\","
          + "\"reason\":\"names a customer\"}",
            refused: false);

    private static (ExecutorRun, string) Finished(params string[] lines) =>
        (ExecutorRun.Completed(SweepLauncher.LoopId, "done", 1, TimeSpan.FromSeconds(40), []),
         string.Join('\n', lines));

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "gg-sweep-scratch", Guid.NewGuid().ToString("n"));

    private static (AgentSweepExecutor Executor, Scripted Port) Executor(
        Func<ExecutorRequest, (ExecutorRun Run, string Transcript)> sweep,
        string declared = Declared,
        bool breaksTheBound = false,
        string? scratch = null)
    {
        var port = new Scripted(sweep, breaksTheBound);

        return (new AgentSweepExecutor(
            executorFor: (_, _) => port,
            trackers: IntentConfiguration.ServedTrackers(declared),
            self: Self,
            scratch: scratch ?? Scratch(),
            wallClock: TimeSpan.FromMinutes(15)), port);
    }

    private static SweepRequest ASweep() =>
        TheSweepLauncherAttachesTheWatchsServersTests.ASweep() with
        {
            TranscriptPath = Path.Combine(
                Path.GetTempPath(), "gg-sweep-transcripts", Guid.NewGuid().ToString("n"),
                "a.ndjson"),
        };

    [Test]
    public async Task A_sweep_that_read_its_tracker_reports_what_it_nominated()
    {
        var (executor, _) = Executor(_ => Finished(
            Queried("q1"), Nominated("n1", "4242"), Nominated("n2", "4243")));

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Ran>();
        await Assert.That(((SweepExecution.Ran)ran).Nominated.Select(n => n.Subject))
            .IsEquivalentTo((string[])["4242", "4243"]);
    }

    [Test]
    public async Task A_sweep_that_read_its_tracker_and_found_nothing_swept()
    {
        var (executor, _) = Executor(_ => Finished(Queried("q1")));

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Ran>()
            .Because("found-nothing is a result, once something was looked at.");
        await Assert.That(((SweepExecution.Ran)ran).Nominated).IsEmpty();
    }

    [Test]
    public async Task A_tracker_that_refused_every_query_is_not_a_sweep_that_found_nothing()
    {
        var (executor, _) = Executor(_ => Finished(
            Queried("q1", refused: true), Queried("q2", refused: true)));
        var sweep = ASweep();

        var ran = await executor.RunAsync(sweep);

        await Assert.That(ran).IsTypeOf<SweepExecution.Failed>();

        var diagnosis = ((SweepExecution.Failed)ran).Diagnosis;
        await Assert.That(diagnosis).Contains("https://tracker.example/acme");
        await Assert.That(diagnosis).Contains("2");
        await Assert.That(diagnosis).Contains(sweep.TranscriptPath)
            .Because("the tracker's own sentence is in the transcript on this machine, and the "
                   + "person reading the report needs to know where.");
        await Assert.That(diagnosis).DoesNotContain("Acme Payroll")
            .Because("a refused query can quote the page that came back, and the report "
                   + "crosses to the control plane.");
    }

    [Test]
    public async Task An_agent_that_never_queried_did_not_sweep()
    {
        var (executor, _) = Executor(_ => Finished(Nominated("n1", "4242")));

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Failed>()
            .Because("a nomination made without reading the tracker was made from nothing the "
                   + "watch reviewed.");
        await Assert.That(((SweepExecution.Failed)ran).Diagnosis).Contains(QueryName);
    }

    [Test]
    public async Task A_sweep_that_ran_out_of_time_says_so()
    {
        var (executor, _) = Executor(request => (
            ExecutorRun.Exhausted(request.LoopId, request.WallClock, []),
            Queried("q1")));

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Failed>();
        await Assert.That(((SweepExecution.Failed)ran).Diagnosis).Contains("15 minutes");
    }

    [Test]
    public async Task A_crashed_agent_is_a_failed_sweep_with_its_reason()
    {
        var (executor, _) = Executor(request => (
            ExecutorRun.Failed(request.LoopId, "the agent exited: rate limited", 1,
                TimeSpan.FromSeconds(3), []),
            Queried("q1")));

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Failed>();
        await Assert.That(((SweepExecution.Failed)ran).Diagnosis).Contains("rate limited");
    }

    [Test]
    public async Task A_bound_that_does_not_hold_launches_no_sweep()
    {
        var (executor, port) = Executor(_ => Finished(Queried("q1")), breaksTheBound: true);

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Failed>();
        await Assert.That(((SweepExecution.Failed)ran).Diagnosis).Contains(MoveBoundProbe.Canary);
        await Assert.That(port.Asked.Select(a => a.LoopId)).DoesNotContain(SweepLauncher.LoopId)
            .Because("a machine where withholding a move does not withhold it is not one to "
                   + "start an agent on.");
    }

    [Test]
    public async Task A_refused_plan_starts_nothing_at_all()
    {
        var (executor, port) = Executor(_ => Finished(Queried("q1")), declared: "");

        var ran = await executor.RunAsync(ASweep());

        await Assert.That(ran).IsTypeOf<SweepExecution.Failed>();
        await Assert.That(port.Asked).IsEmpty()
            .Because("the probe is an agent invocation too, and is not spent on a sweep that "
                   + "was never going to run.");
    }

    [Test]
    public async Task The_sweeps_own_directory_is_there_while_it_runs_and_gone_after()
    {
        var scratch = Scratch();
        var (executor, port) = Executor(_ => Finished(Queried("q1")), scratch: scratch);

        await executor.RunAsync(ASweep());

        var ran = port.Asked.Single(a => a.LoopId == SweepLauncher.LoopId);
        await Assert.That(port.DirectoryExistedWhileRunning).IsTrue();
        await Assert.That(ran.WorkingDirectory.StartsWith(scratch, StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(Directory.Exists(ran.WorkingDirectory)).IsFalse();
    }
}
