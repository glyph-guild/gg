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
    public static bool MustKeep(bool landed, ExposureServed? serving) =>
        !landed || serving is { Url.Length: > 0 };
}
