using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Whether anything can be handed to a runner, read off the state the fleet
/// derived for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beating, not "not offline".</b> An introduction is picked up on a
/// heartbeat, so only a runner that beats can be reached - and busy and idle
/// are the two states a heartbeat is behind. <c>maintaining</c> made the
/// difference real: a pool maintainer is alive, reports every few seconds, and
/// never beats, so a check for <c>offline</c> alone waved it through to twenty
/// seconds of silence.
/// </para>
/// <para>
/// <b>One rule for both surfaces.</b> The command line and the console each
/// refuse before minting anything; stated here once, the two cannot drift.
/// </para>
/// </remarks>
public static class RunnerReach
{
    /// <summary>The state word, with anything a surface wrote after the mark removed.</summary>
    /// <remarks>The console writes <c>idle · parked</c>; the state is the word in front.</remarks>
    public static string Derived(string? cell) => (cell ?? "").Split('·', 2)[0].Trim();

    /// <summary>Whether a runner in this state is beating, and so can be reached.</summary>
    /// <remarks>
    /// A state this build does not know is not assumed to beat: waiting out an
    /// introduction to learn otherwise is the cost this exists to avoid.
    /// </remarks>
    public static bool Beats(string? cell) =>
        Derived(cell) is RunnerStates.Busy or RunnerStates.Idle;

    /// <summary>Whether this runner is a pool maintainer, alive and never beating.</summary>
    public static bool Maintains(string? cell) =>
        string.Equals(Derived(cell), RunnerStates.Maintaining, StringComparison.Ordinal);

    /// <summary>
    /// What to say instead of reaching a maintainer. Not "start it": it is
    /// running, and doing its job.
    /// </summary>
    public static string MaintainerSaid(string label) =>
        $"{label} maintains a pool and never beats, so there is nothing to reach there: an "
      + "introduction is picked up on a heartbeat. It takes no flights and holds no agent - "
      + "the runners it keeps warm are the ones to reach.";
}
