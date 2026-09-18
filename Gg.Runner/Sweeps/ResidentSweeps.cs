using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Sweeps;

/// <summary>
/// Whether a resident runner asks for sweeps, and with what.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner's two sentences, and this is all of both.</b> <i>"Today a
/// watch sweeps only while someone keeps that process running - we need to have
/// the runner automatically check."</i> Then: <i>"we should be able to turn off
/// the runner automated sweep as well. it's on by default."</i>
/// </para>
/// <para>
/// <b>Pure, so the whole decision is testable without a runner.</b> What a
/// machine asks for is a function of its operator's setting and what its
/// operator declared it can reach - nothing on the wire decides it, which is
/// what keeps a control plane from switching sweeping on for a machine whose
/// operator turned it off.
/// </para>
/// </remarks>
public static class ResidentSweeps
{
    /// <summary>The setting's variable, beside its <c>gg config</c> key.</summary>
    public const string Variable = "GG_RUNNER_SWEEPS";

    /// <summary>The setting's key, as <c>gg config set</c> spells it.</summary>
    public const string Key = "runner-sweeps";

    public const string On = "on";

    public const string Off = "off";

    /// <summary>
    /// The claim to make, or null when this runner should not ask at all.
    /// </summary>
    /// <param name="setting">
    /// <c>on</c>, <c>off</c>, or null for unset - which is ON.
    /// </param>
    /// <param name="trackers">What this machine's operator declared it can reach.</param>
    /// <remarks>
    /// <para>
    /// <b>Unset is on.</b> A runner stood up from nothing sweeps without anybody
    /// remembering to switch it on, which is what "on by default" has to mean
    /// for a setting nobody will think to write.
    /// </para>
    /// <para>
    /// <b>Off never asks</b>, so it spends none of the machine's allowance on
    /// sweeps. And off is still a state rather than a silence: a watch that no
    /// runner will sweep is named <c>watch-missed</c> at twice its period by
    /// rule 11, so the board never reads as quiet because the machines stopped
    /// looking.
    /// </para>
    /// <para>
    /// <b>A machine with nothing to sweep with does not ask.</b> The contract
    /// refuses an empty claim, and a runner sending one every idle cycle would
    /// leave a 400 in its journal forever. A tracker declared with no
    /// credential is left out for the same reason: half a pair matches nothing
    /// rule 18 would accept.
    /// </para>
    /// <para>
    /// <b>Anything but <c>on</c> or <c>off</c> is refused by name.</b> Reading
    /// anything but <c>on</c> as off would turn a misspelt <c>of</c> into a
    /// silent decision to stop sweeping - the one outcome nobody would notice.
    /// </para>
    /// </remarks>
    public static SweepClaim? ClaimFor(string? setting, IReadOnlyList<ServedTracker> trackers)
    {
        ArgumentNullException.ThrowIfNull(trackers);

        var said = setting?.Trim();

        if (said is { Length: > 0 }
            && !string.Equals(said, On, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(said, Off, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(
                nameof(setting), setting,
                $"'{Key}' is '{On}' or '{Off}'. '{setting}' is neither, and reading it as either "
              + $"would be deciding for the operator - set {Variable} or `gg config set {Key}` "
              + "to one of the two.");
        }

        if (string.Equals(said, Off, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var serves = trackers
            .Where(t => !string.IsNullOrWhiteSpace(t.Host) && t.Locator is { Length: > 0 })
            .Select(t => new SweepServes { Host = t.Host, Credential = t.Locator! })
            .ToList();

        return serves.Count == 0 ? null : new SweepClaim { Serves = serves };
    }
}
