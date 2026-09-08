namespace Gg.Contracts;

/// <summary>
/// How fast a runner may be asked to come back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because <c>NextHeartbeatSeconds</c> is now load-bearing for two
/// things.</b> It was liveness — "the client respects a cadence rather than
/// inventing one, so the staleness threshold and the heartbeat rate cannot drift
/// apart across a deploy" — and it is now also how a waiting introduction gets
/// picked up quickly. A value the control plane chooses freely is a value that
/// can make a whole fleet chatty, and the runner is the side that pays.
/// </para>
/// <para>
/// <b>The floor is here rather than in the control plane, so the RUNNER can
/// hold it.</b> A bound only the sender enforces is a bound that disappears the
/// moment the sender is wrong — and the runner is the machine whose CPU and
/// egress this spends. Clamping on the receiving side means a control plane that
/// asked for a hundredth of a second gets one second and nobody has an outage
/// over it.
/// </para>
/// <para>
/// <b>One second is a handshake's cadence, not a poll's.</b> ICE gathering and a
/// DTLS handshake are seconds, so an introduction picked up within one is picked
/// up promptly; a fleet held at one second forever is thirty times its ordinary
/// rate, which is visible rather than ruinous.
/// </para>
/// </remarks>
public static class HeartbeatCadence
{
    /// <summary>The fastest a runner will be told to come back.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromSeconds(1);

    /// <summary>The slowest, so a bad value cannot silently retire a runner.</summary>
    /// <remarks>
    /// <b>The other direction matters too, and for a worse reason.</b> A
    /// heartbeat interval longer than the staleness bound makes a healthy runner
    /// read as offline, which takes it out of the fleet without anything
    /// failing. A ceiling here is what stops a configuration mistake looking
    /// like a dead machine.
    /// </remarks>
    public static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(5);

    /// <summary>
    /// What a runner will actually wait, whatever it was told.
    /// </summary>
    /// <remarks>
    /// <b>Clamped rather than refused.</b> A runner that stopped over an
    /// out-of-range cadence would turn a control plane's arithmetic mistake into
    /// a fleet outage — and the two loops this repository has already watched
    /// die on a refusal are the reason that is not a theoretical preference.
    /// </remarks>
    public static TimeSpan Respecting(int seconds) =>
        seconds <= Floor.TotalSeconds ? Floor
        : seconds >= Ceiling.TotalSeconds ? Ceiling
        : TimeSpan.FromSeconds(seconds);
}
