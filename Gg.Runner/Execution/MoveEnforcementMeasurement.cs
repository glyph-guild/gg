using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// What enforcement level a session's probe result honestly supports.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule slice two's moves row took three corrections to reach:</b> the
/// wire value derives from the measurement, never from a capability constant.
/// A held bound derives <c>per-tool</c> - each denied tool refused at the
/// call, while Read and Bash stay measured-unbound. A broken or unmeasured
/// bound derives NOTHING: that lease is released with the diagnosis, so
/// <c>none</c> never crosses from a working runner, and <c>full</c> is never
/// derivable because a probe cannot prove a universal from finitely many
/// refusals. Both stay in the vocabulary as the values whose absence is the
/// finding.
/// </para>
/// </remarks>
public static class MoveEnforcementMeasurement
{
    /// <summary>The enforcement the probe proved, or null when it proved none.</summary>
    public static string? Of(ProbeResult? probe) =>
        probe is { Bound: true } ? MoveEnforcements.PerTool : null;
}
