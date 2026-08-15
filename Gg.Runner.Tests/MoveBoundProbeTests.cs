using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The runner proves the tool bound holds before it takes work, and refuses to
/// take any if it does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Verified, not configured.</b> The bound rests on <c>--setting-sources ""</c>,
/// whose mechanism is uncharacterised and whose failure is silent: with settings
/// active, an allow-list omitting <c>Write</c> did not stop a write, and passing
/// <c>--permission-mode default</c> did not bring the bound back. A check that
/// asserts the flag is present would be asserting the thing we do not understand.
/// This one denies a tool, attempts it, and looks at the disk - which works
/// without knowing why the flag works.
/// </para>
/// <para>
/// <b>What it does on failure is refuse to lease.</b> If moves are not enforceable
/// on this machine then a governed flight here is not governed, and flying one
/// anyway is the claim this product is sold on being false on a customer's laptop.
/// </para>
/// <para>
/// <b>And an inconclusive probe refuses too.</b> Unknown is not false - the rule
/// this project applies to a missing fact applies to a missing measurement.
/// </para>
/// </remarks>
public class MoveBoundProbeTests
{
    /// <summary>An executor that does whatever the test says, without a network.</summary>
    private sealed class StubExecutor(Func<ExecutorRequest, ExecutorRun> respond) : IExecutorPort
    {
        internal List<ExecutorRequest> Requests { get; } = [];

        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static ExecutorRun Finished(string outcome = LoopOutcomes.Completed) => new()
    {
        LoopId = "probe",
        Outcome = outcome,
        Reason = "done",
        Attempts = 1,
        DurationMs = 10,
        MovesUsed = [],
    };

    /// <summary>An executor that respects the bound: it changes nothing it was not allowed to.</summary>
    private static StubExecutor Bound() => new(_ => Finished());

    /// <summary>An executor that does not: it writes despite the move not being declared.</summary>
    private static StubExecutor Unbound() => new(request =>
    {
        File.WriteAllText(
            Path.Combine(request.WorkingDirectory, MoveBoundProbe.Canary), "written anyway");
        return Finished();
    });

    // ---- the probe itself ----

    [Test]
    public async Task A_bound_executor_passes_and_says_what_it_proved()
    {
        var result = await MoveBoundProbe.RunAsync(Bound(), CancellationToken.None);

        await Assert.That(result.Bound).IsTrue();
        await Assert.That(result.Diagnosis).Contains("Write")
            .Because("the diagnosis names the move that was denied, or it proves nothing "
                   + "anybody can check.");
    }

    [Test]
    public async Task An_executor_that_writes_anyway_fails_the_probe()
    {
        // THE LIVENESS TWIN, and the one that matters: without it the probe would
        // pass on a machine where nothing is enforced and on a machine where
        // nothing ran.
        var result = await MoveBoundProbe.RunAsync(Unbound(), CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Diagnosis).Contains(MoveBoundProbe.Canary)
            .Because("it names the file that appeared, so an operator can see the evidence.");
    }

    [Test]
    public async Task The_probe_denies_the_move_it_is_testing_and_declares_the_rest()
    {
        // Otherwise it would be measuring nothing: an executor granted Write and
        // not using it looks exactly like one that was bound.
        var executor = Bound();
        await MoveBoundProbe.RunAsync(executor, CancellationToken.None);

        var request = executor.Requests.Single();

        await Assert.That(request.Moves).DoesNotContain(LoopMoves.Edit);
        await Assert.That(request.Moves).Contains(LoopMoves.Read)
            .Because("the agent has to be able to look, or a refusal to write is "
                   + "indistinguishable from an agent that never got started.");
    }

    [Test]
    public async Task An_executor_that_could_not_run_is_inconclusive_and_that_is_not_a_pass()
    {
        // UNKNOWN IS NOT FALSE. A probe that could not measure has not measured a
        // bound, and treating a failed measurement as a passed one is the exact
        // shape of the defect this whole step exists to remove.
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => throw new InvalidOperationException("the binary is not there")),
            CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Diagnosis).Contains("could not be measured");
    }

    [Test]
    public async Task The_probe_leaves_nothing_behind()
    {
        // It runs on a customer's machine, at startup, every time. A probe that
        // accumulated scratch directories would be a disk leak with a schedule.
        // The DIFFERENCE rather than the count, because other tests in this file
        // run concurrently and have probe directories of their own in flight. A
        // count would make this test about the scheduler.
        var before = Directory
            .GetDirectories(Path.GetTempPath(), "gg-move-probe*")
            .ToHashSet(StringComparer.Ordinal);

        await MoveBoundProbe.RunAsync(Bound(), CancellationToken.None);
        await MoveBoundProbe.RunAsync(Unbound(), CancellationToken.None);

        await Assert.That(Directory
                .GetDirectories(Path.GetTempPath(), "gg-move-probe*")
                .Except(before, StringComparer.Ordinal))
            .IsEmpty();
    }

    [Test]
    [Category("RealAgent")]
    public async Task What_the_probe_costs_against_the_real_binary()
    {
        // PROBE COST, measured rather than estimated, because it is spent on every
        // runner start on somebody else's machine and "an agent invocation is not
        // free" is not a number.
        var binary = Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
            ?? throw new InvalidOperationException("GG_EXECUTOR_BINARY is not set.");

        var result = await MoveBoundProbe.RunAsync(
            new ClaudeCodeExecutor(binary), CancellationToken.None);

        Console.WriteLine($"probe took {result.Took.TotalSeconds:F1}s, bound={result.Bound}");

        if (Environment.GetEnvironmentVariable("GG_PROBE_COST") is { Length: > 0 } into)
        {
            File.WriteAllText(into, $"{result.Took.TotalSeconds:F1}s bound={result.Bound}\n{result.Diagnosis}\n");
        }

        await Assert.That(result.Took).IsLessThan(TimeSpan.FromMinutes(3))
            .Because("the budget it is given, so a probe that reached it would be a runner "
                   + "held up for three minutes rather than one told something.");
    }

    // ---- and it runs in the product, not only here ----

    [Test]
    public async Task The_runner_probes_before_it_claims_anything()
    {
        // THE ASSERTION THIS FILE EXISTS FOR. A probe that runs in the suite and
        // not in the product is this project's most-repeated failure, and it is
        // repeated most easily by a check that was only ever written for a test.
        // Asserted over the source of the host that production starts, and paired
        // below with the fact that production reaches that host.
        var host = SourceOf("RunnerHost.cs");

        await Assert.That(host).Contains("MoveBoundProbe")
            .Because("the host is what `gg runner serve` calls, so this is the probe being "
                   + "on the path a customer takes.");
        await Assert.That(host.IndexOf("MoveBoundProbe", StringComparison.Ordinal))
            .IsLessThan(host.IndexOf("loop.RunAsync", StringComparison.Ordinal))
            .Because("before the loop starts is the only place it means anything - after it "
                   + "would be a check on a runner that had already taken work.");
    }

    [Test]
    public async Task The_command_a_person_runs_reaches_that_host_with_an_executor()
    {
        // The other half, and the half that was missing entirely: the host can
        // only probe an executor it was given, and nothing outside the test
        // assemblies constructed one. A runner with no executor ran no loop, so
        // the moves question never arose - and neither did the answer.
        var program = SourceOf("Program.cs");

        await Assert.That(program).Contains("RunnerHost.RunAsync");
        await Assert.That(program).Contains("ExecutorConfiguration.FromEnvironment")
            .Because("configured like the vcs and destination adapters beside it, because "
                   + "which executor a machine has is deployment knowledge.");
    }

    [Test]
    public async Task A_runner_with_no_executor_does_not_refuse_to_start()
    {
        // An honest state rather than a degraded one, and it must not be swept up
        // by the refusal: a runner that cannot invoke an agent cannot break a move
        // bound either, and refusing to start would take away the observe-only
        // runner every slice before this one had.
        var refusal = MoveBoundProbe.Required(executor: null);

        await Assert.That(refusal).IsNull();
    }

    private static string SourceOf(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return string.Join('\n', Directory
            .EnumerateFiles(root, file, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .Select(File.ReadAllText));
    }
}
