using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// `moveEnforcement` on the wire is the session's measured result, dated -
/// never a capability's claim.
/// </summary>
/// <remarks>
/// <para>
/// Slice four shipped it as the executor's compile-time constant with only
/// `movesProbed` measured; slice two's moves row has been corrected twice
/// already, and the difference that made the first correction wrong was a
/// flag standing where a measurement was needed. The derivation: a session
/// whose probe held every denied tool ships `per-tool`; a session whose probe
/// broke or could not measure ships NOTHING, because the lease was released
/// with the diagnosis instead - so `none` never crosses from a working
/// runner, and `full` is never derivable (Read and Bash are measured
/// unbound, and a probe cannot prove a universal from finitely many
/// refusals). Its absence is the finding.
/// </para>
/// </remarks>
public class MeasuredEnforcementTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    private static ProbeResult Probe(bool bound) => new()
    {
        Bound = bound,
        Diagnosis = bound ? "held" : "broke",
        Took = TimeSpan.FromSeconds(17),
        MeasuredAt = T0,
        Workspace = "/tmp/probe",
        Held = bound ? ["Edit", "Write"] : ["Edit"],
        Broke = bound ? [] : ["Write"],
    };

    [Test]
    public async Task A_held_bound_derives_per_tool_and_nothing_else_ever_derives()
    {
        await Assert.That(MoveEnforcementMeasurement.Of(Probe(bound: true)))
            .IsEqualTo(MoveEnforcements.PerTool)
            .Because("per-tool is what the probe can prove: each denied tool refused at "
                   + "the call, while Read and Bash stay measured-unbound.");

        await Assert.That(MoveEnforcementMeasurement.Of(Probe(bound: false))).IsNull()
            .Because("a broken or unmeasured bound ships no enforcement claim at all - "
                   + "the lease was released with the diagnosis, so none never crosses "
                   + "from a working runner and full is never derivable.");
    }

    [Test]
    public async Task The_fact_carries_what_the_probe_held_and_when_it_measured()
    {
        var identity = EnvironmentSurvey.Observe(
            treePath: null, provenance: EnvironmentProvenance.Fresh, probe: Probe(bound: true));

        await Assert.That(identity.MoveEnforcement).IsEqualTo(MoveEnforcements.PerTool);
        await Assert.That(identity.MovesProbed).IsEquivalentTo((string[])["Edit", "Write"])
            .Because("movesProbed is the set the session's probe actually held - it was a "
                   + "hardcoded Write from startup, which measured one tool once.");
        await Assert.That(identity.ProbedAt).IsEqualTo(T0)
            .Because("the timestamp is what makes 'a measurement of this session' "
                   + "auditable rather than asserted.");
    }

    [Test]
    public async Task A_runner_with_no_executor_still_ships_nothing_about_moves()
    {
        var identity = EnvironmentSurvey.Observe(
            treePath: null, provenance: EnvironmentProvenance.Fresh, probe: null);

        await Assert.That(identity.MoveEnforcement).IsNull();
        await Assert.That(identity.MovesProbed).IsEmpty();
        await Assert.That(identity.ProbedAt).IsNull()
            .Because("null and none are different: this runner has no executor, so no "
                   + "session existed for a probe to measure.");
    }
}
