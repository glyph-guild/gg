namespace Gg.Contracts;

/// <summary>
/// How two <c>gg</c> versions compare, decided once for both sides.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here because both ends have to agree.</b> The control plane decides
/// whether a caller is behind and says so in a notice; <c>gg</c> decides the
/// same thing for <c>doctor</c> and <c>update</c> from the same two strings. A
/// second copy of the rule is a second answer waiting to happen, on a question
/// where disagreement looks like a bug in whichever side the reader is not
/// looking at.
/// </para>
/// <para>
/// <b>Build metadata is dropped, and dropping it is the whole subtlety.</b> The
/// informational version carries the commit — <c>0.4.0+9f144b8…</c> — so a text
/// comparison is never equal to anything and every install in a fleet reports
/// itself behind for ever. Prerelease labels go with it: <c>gg</c> has never
/// shipped one, and a comparison that pretends to order them without a test
/// saying how is worse than one that plainly does not try.
/// </para>
/// </remarks>
public static class VersionOrder
{
    /// <summary>
    /// The release part of a version string, or null when there is not one.
    /// </summary>
    /// <remarks>
    /// Null means "cannot be compared", which every caller must render as an
    /// absence of knowledge rather than as agreement — the failure this whole
    /// area exists to avoid.
    /// </remarks>
    public static Version? Release(string? version)
    {
        if (version is not { Length: > 0 })
        {
            return null;
        }

        var release = version.Split('+', 2)[0].Split('-', 2)[0];

        return Version.TryParse(release, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Whether <paramref name="installed"/> is older than <paramref name="current"/>.
    /// </summary>
    /// <remarks>
    /// False when either side cannot be read, because "not comparable" is not
    /// "behind" — a caller that cannot be placed must not be told to update to
    /// something on the strength of a string nobody could parse.
    /// </remarks>
    public static bool IsBehind(string? installed, string? current)
    {
        var mine = Release(installed);
        var theirs = Release(current);

        return mine is not null && theirs is not null && mine < theirs;
    }
}
