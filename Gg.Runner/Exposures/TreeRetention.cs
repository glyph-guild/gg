namespace Gg.Runner.Exposures;

/// <summary>
/// Whether a flight's working tree must outlive the flight.
/// </summary>
/// <remarks>
/// <para>
/// <b>A preview is served from inside the tree, after the flight ends.</b> The
/// ui-preview work kind tells an agent to leave the server running, because the
/// person who asked for the preview opens the address afterwards. Releasing the
/// tree then leaves a process the kernel reports as having a <c>(deleted)</c>
/// working directory: still listening, still answering anything already open,
/// and failing every lazy import.
/// </para>
/// <para>
/// <b>Up and gutted is worse than down.</b> A dead address answers 502 and a
/// person knows to wait. GG-303's answered 500 with a stack trace naming paths
/// that no longer existed, which reads as a broken application rather than a
/// reclaimed directory.
/// </para>
/// <para>
/// <b>Pure, so the rule can be asserted without a filesystem</b> — which is
/// what the two flights that hit this could not do.
/// </para>
/// </remarks>
public static class TreeRetention
{
    /// <summary>
    /// Whether this flight's tree must be kept rather than released.
    /// </summary>
    /// <param name="landed">Whether the flight landed. One that did not is already held.</param>
    /// <param name="serving">What this machine is serving at, or null for nothing.</param>
    /// <remarks>
    /// <b>An address is the test, not the grant.</b> A machine holding a slot
    /// whose connector would not start reports a diagnosis and no url — there is
    /// nothing running, so there is nothing to protect and the tree goes back.
    /// </remarks>
    /// <param name="produces">
    /// What this flight's kind declares it produces, or null when the document
    /// said nothing. <b>The discriminator this rule was missing.</b> A tree kept
    /// for a preview nobody asked for is disk nothing reclaims.
    /// </param>
    public static bool MustKeep(
        bool landed, ExposureServed? serving, IReadOnlyList<string>? produces) =>
        !landed || (ServesAnAddress(serving) && Previews(produces));

    /// <summary>
    /// Whether this machine must hold its lease rather than go back for work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A flight gated on a preview stays alive, and its runner takes no
    /// work</b> (owner's decision, 2026-09-25). The two are one requirement: the
    /// address points at a server inside that flight's tree, on that flight's
    /// port, so a second flight on this machine would want both — and the first
    /// person's preview would go out from under them while they were looking at
    /// it.
    /// </para>
    /// <para>
    /// <b>Bounded by the lease, not by a timer.</b> The hold already ends when a
    /// renewal comes back fenced or gone, which is what happens once the gate is
    /// answered and the flight lands. So the machine is released by the same
    /// event that makes it free, and nothing here has to poll or be told.
    /// </para>
    /// <para>
    /// <b>The same fact that keeps the tree</b>, deliberately. Two rules that
    /// could disagree would leave a held machine serving from a released tree,
    /// which is exactly the state GG-303 was found in.
    /// </para>
    /// <para>
    /// <b>AND THE KIND HAS TO WANT ONE, which this rule did not ask.</b>
    /// <c>_served</c> is the MACHINE's: set once, on the beat, when it learned its
    /// slot, and true for every flight that lands there afterwards. So a learning
    /// rehearsal on a slot-holding machine held it for twelve hours and took no
    /// other work — measured on GG-327, where grounding the flight did not release
    /// it because the hold is not the flight's state. The paragraph above always
    /// said "a flight gated on a preview"; this is that sentence enforced.
    /// </para>
    /// </remarks>
    public static bool HoldsItsMachine(
        ExposureServed? serving, IReadOnlyList<string>? produces) =>
        ServesAnAddress(serving) && Previews(produces);

    /// <summary>Whether this flight's kind asked for a preview at all.</summary>
    /// <remarks>
    /// <b>The kind's own declaration rather than a new flag.</b> A work kind that
    /// wants a preview already says <c>preview.url</c> in <c>produces:</c>, and one
    /// that does not already says nothing — and null is silence rather than
    /// emptiness, which is not a claim to anything.
    /// </remarks>
    private static bool Previews(IReadOnlyList<string>? produces) =>
        produces is not null
        && produces.Contains(Gg.Contracts.FactKinds.PreviewUrl, StringComparer.Ordinal);

    /// <summary>Whether anything is actually answering at this machine's slot.</summary>
    /// <remarks>
    /// <b>An address, not a grant.</b> A machine holding a slot whose connector
    /// never started reports a diagnosis and no url: nothing is running, nobody
    /// was told to go and look at anything, and there is no reason to keep
    /// either the tree or the machine.
    /// </remarks>
    private static bool ServesAnAddress(ExposureServed? serving) =>
        serving is { Url.Length: > 0 };
}
