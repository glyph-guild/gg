using Gg.Contracts;

namespace Gg.Client;

/// <summary>Where this binary stands against what the control plane published.</summary>
public enum VersionStandingKind
{
    /// <summary>The oracle could not be asked, or said nothing usable.</summary>
    /// <remarks>
    /// Its own state on purpose. Folding it into <see cref="Current"/> is the
    /// defect this whole check exists to avoid: a person told nothing concludes
    /// they were told everything is fine, and never updates.
    /// </remarks>
    Unknown,

    /// <summary>Running what the control plane says is current.</summary>
    Current,

    /// <summary>Older than what the control plane says is current.</summary>
    Behind,

    /// <summary>
    /// Newer than anything the control plane has published.
    /// </summary>
    /// <remarks>
    /// <b>Not called "Ahead", and the name is the point.</b> This is the shape a
    /// package pushed with a stolen nuget.org key takes when seen from a host:
    /// the feed offered a version the control plane has never heard of, and a
    /// fleet on <i>latest</i> took it. "Ahead" is a compliment; this is a thing
    /// to look at.
    /// </remarks>
    Unrecognised,
}

/// <summary>
/// The comparison, and the one reading of it that must never be available.
/// </summary>
/// <remarks>
/// <para>
/// <b>A report, never a refusal.</b> Rule 6: being behind does not block. The
/// protocol floor already refuses with a 426 and that stays the only thing that
/// does — so nothing here carries a way to say stop, and
/// <c>VersionCheckTests</c> asserts over this type's shape that nothing ever
/// does.
/// </para>
/// <para>
/// <b>Build metadata is not part of the comparison.</b> The informational
/// version carries the commit — <c>0.4.0+9f144b8…</c> — so a text comparison is
/// never equal to anything and every install in the fleet reports itself behind
/// forever.
/// </para>
/// </remarks>
/// <param name="Kind">Where this binary stands.</param>
/// <param name="Installed">The version running, as reported.</param>
/// <param name="Current">What the control plane published, or null if it did not say.</param>
public sealed record VersionStanding(
    VersionStandingKind Kind,
    string Installed,
    string? Current)
{
    /// <summary>
    /// Whether this is a state a person may take as fine.
    /// </summary>
    /// <remarks>
    /// Exactly one kind is. Read by every renderer so that "nothing to say"
    /// cannot be spelled the same way as "nothing is wrong".
    /// </remarks>
    public bool IsReassuring => Kind == VersionStandingKind.Current;

    /// <summary>
    /// Where <paramref name="installed"/> stands against <paramref name="current"/>.
    /// </summary>
    /// <param name="installed">This binary's own version.</param>
    /// <param name="current">What the control plane published, or null.</param>
    public static VersionStanding For(string? installed, string? current)
    {
        // ONE COMPARISON, SHARED WITH THE CONTROL PLANE. It decides the same
        // thing from the same two strings when it raises a notice, and two
        // copies of that rule is two answers on a question where disagreement
        // looks like a bug in whichever side the reader is not looking at.
        var mine = VersionOrder.Release(installed);
        var theirs = VersionOrder.Release(current);

        if (theirs is null || mine is null)
        {
            // EITHER SIDE MISSING IS UNKNOWN. An unreadable local version is as
            // much an absence of knowledge as an unreachable control plane, and
            // guessing which way it falls is guessing.
            return new VersionStanding(VersionStandingKind.Unknown, installed ?? "", null);
        }

        var kind = mine.CompareTo(theirs) switch
        {
            0 => VersionStandingKind.Current,
            < 0 => VersionStandingKind.Behind,
            _ => VersionStandingKind.Unrecognised,
        };

        return new VersionStanding(kind, installed ?? "", current);
    }

}
