namespace Gg.Console;

/// <summary>
/// When the console covers itself with the mark, and when it stops.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stated here so both ways in agree.</b> Five minutes of nobody touching
/// it, or <c>ctrl+g</c> outright - and the second one exists because the first
/// cannot be demonstrated without waiting five minutes for it.
/// </para>
/// <para>
/// <b>It draws <see cref="LoadingArt"/> and nothing of its own.</b> The
/// letters, the cosine that fades them and the shimmer that changes them are
/// already there and already tested; a screensaver with its own copy would be
/// a second thing to keep in agreement with the first. What is different is
/// only how much of the screen it takes.
/// </para>
/// </remarks>
public static class Screensaver
{
    /// <summary>
    /// How many idle seconds bring it up.
    /// </summary>
    /// <remarks>
    /// <b>Counted on the refresh tick.</b> That timer runs once a second
    /// whatever else is happening, so this is a number of ticks rather than a
    /// duration - and the model stays something that can be written down and
    /// read back rather than something holding a clock.
    /// </remarks>
    public static int After => 300;

    /// <summary>
    /// How many ticks one of this mark's breaths takes.
    /// </summary>
    /// <remarks>
    /// <b>Twice the loading mark's, and for a different job.</b> That one
    /// answers "is this coming?" for a second or two and wants to look busy.
    /// This is what a room looks at for an hour, and at the loading pace it
    /// reads as a thing pulsing at somebody rather than a thing at rest.
    /// <para>
    /// The same curve stretched, never a second curve - and both halves of it,
    /// because the ink settles as the mark brightens and a shimmer on the old
    /// pace under a glow on this one would come apart.
    /// </para>
    /// </remarks>
    public static int Breath => LoadingArt.Breath * 2;

    /// <summary>Whether the mark is covering the screen right now.</summary>
    public static bool Showing(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Screening;
    }

    /// <summary>Whether this many idle seconds is enough.</summary>
    public static bool Due(int idleTicks) => idleTicks >= After;
}
