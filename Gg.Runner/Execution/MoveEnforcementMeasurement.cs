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
/// <para>
/// <b><c>none</c> now has one caller, and it is not a broken bound.</b> A loop
/// declaring <c>LoopMoves.Anything</c> withholds nothing, which is the
/// vocabulary's own definition of the value - <i>"nothing declared is withheld.
/// A move is an observation only"</i> - so it derives here rather than being
/// left as null. Null would say UNMEASURED, which is the attended flight's
/// answer and a weaker claim: unknown is not none any more than unknown is
/// false.
/// </para>
/// </remarks>
public static class MoveEnforcementMeasurement
{
    /// <summary>The enforcement the probe proved, or null when it proved none.</summary>
    /// <param name="probe">What this session's probe measured, when one ran.</param>
    /// <param name="unbounded">
    /// Whether the envelope declined to bound this loop, in which case no probe
    /// ran and none is what crossed.
    /// </param>
    public static string? Of(ProbeResult? probe, bool unbounded = false) =>
        unbounded ? MoveEnforcements.None
        : probe is { Bound: true } ? MoveEnforcements.PerTool
        : null;
}
