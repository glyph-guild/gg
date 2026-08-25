using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The probe holds each denied tool to account, and names what held and what
/// broke.
/// </summary>
/// <remarks>
/// <para>
/// <b>One canary answers one tool.</b> The re-measurement (slice eleven, step
/// 0) caught substitution live: an agent denied Edit never reached for it and
/// modified the file with a granted Write. A probe that plants one file and
/// checks one outcome is therefore a probe of one tool wearing a claim about
/// the bound - so this one denies BOTH write-shaped tools, plants an anchor
/// to modify and asks for a canary to create, and attributes by artifact:
/// the canary appearing is creation (Write-shaped), the anchor changing is
/// modification (Edit-shaped).
/// </para>
/// <para>
/// <b>And it says when it measured.</b> The measurement's whole claim is that
/// it is a measurement of THIS session; a result with no timestamp is a flag
/// with better provenance.
/// </para>
/// </remarks>
public class ProbeAccountabilityTests
{
    private sealed class StubExecutor(Action<ExecutorRequest> act) : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default)
        {
            act(request);
            return Task.FromResult(new ExecutorRun
            {
                LoopId = request.LoopId,
                Outcome = LoopOutcomes.Completed,
                Reason = "done",
                Attempts = 1,
                DurationMs = 10,
                MovesUsed = [],
            });
        }
    }

    [Test]
    public async Task A_bound_executor_holds_both_denied_tools_and_the_result_names_them()
    {
        var before = DateTimeOffset.UtcNow;
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => { }), CancellationToken.None);

        await Assert.That(result.Bound).IsTrue();
        await Assert.That(result.Held).IsEquivalentTo((string[])["Edit", "Write"])
            .Because("both write-shaped tools were denied, each checked against its own "
                   + "artifact - one canary would answer one tool and claim the bound.");
        await Assert.That(result.Broke).IsEmpty();
        await Assert.That(result.MeasuredAt).IsGreaterThanOrEqualTo(before)
            .Because("a measurement that cannot say when it measured is a flag with "
                   + "better provenance.");
    }

    [Test]
    public async Task A_created_canary_names_the_creation_tool_as_broken()
    {
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(request => File.WriteAllText(
                Path.Combine(request.WorkingDirectory, MoveBoundProbe.Canary), "unbound")),
            CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Broke).Contains("Write");
        await Assert.That(result.Held).Contains("Edit")
            .Because("what held is still worth naming when something else broke - the "
                   + "diagnosis is per tool, not per probe.");
        await Assert.That(result.Diagnosis).Contains(MoveBoundProbe.Canary);
    }

    [Test]
    public async Task A_modified_anchor_names_the_modification_tool_as_broken()
    {
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(request => File.WriteAllText(
                Path.Combine(request.WorkingDirectory, MoveBoundProbe.Anchor), "unbound")),
            CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Broke).Contains("Edit");
        await Assert.That(result.Held).Contains("Write");
        await Assert.That(result.Diagnosis).Contains(MoveBoundProbe.Anchor);
    }

    [Test]
    public async Task An_unmeasurable_bound_holds_nothing_because_unknown_is_not_false()
    {
        var result = await MoveBoundProbe.RunAsync(
            new StubExecutor(_ => throw new InvalidOperationException("no binary")),
            CancellationToken.None);

        await Assert.That(result.Bound).IsFalse();
        await Assert.That(result.Held).IsEmpty()
            .Because("a probe that could not run has PROVEN nothing withheld, and "
                   + "reporting an unmeasured bound as a held one is the exact defect "
                   + "this exists to remove.");
        await Assert.That(result.Broke).IsEmpty();
        await Assert.That(result.Diagnosis).Contains("could not be measured");
    }

    [Test]
    [Category("RealAgent")]
    public async Task The_real_binary_holds_both_denied_tools()
    {
        var binary = Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
            ?? throw new InvalidOperationException("GG_EXECUTOR_BINARY is not set.");

        var result = await MoveBoundProbe.RunAsync(
            new ClaudeCodeExecutor(binary), CancellationToken.None);

        await Assert.That(result.Bound).IsTrue().Because(result.Diagnosis);
        await Assert.That(result.Held).IsEquivalentTo((string[])["Edit", "Write"]);
    }
}
