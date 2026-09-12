using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Which kind of share a window has, decided once for every surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four surfaces render this and each had its own copy of the rule.</b>
/// <c>gg allowance</c>, <c>gg allowances</c>, the allowance column on the
/// runners pane and the fleet pane. When the meter arrived only the first
/// learned about it, so one reading showed a percentage in one place and "no
/// ceiling set" in another.
/// </para>
/// <para>
/// <b>The decision is here; the formatting stays where it belongs.</b> A pane
/// fits a share into a column and a verb into a sentence, and those are
/// genuinely different. Which share to show is not.
/// </para>
/// </remarks>
public static class AllowanceShare
{
    /// <summary>
    /// Whether the meter's number is about a window that has already ended.
    /// </summary>
    /// <remarks>
    /// The meter keeps FIXED windows. When one rolls over and the executor has
    /// not asked again, its share is a true statement about a finished window
    /// and says nothing about the one a person is in — so it is withheld
    /// rather than restated.
    /// </remarks>
    public static bool RolledOver(AllowanceWindow window, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.ResetsAt is { } ended && ended <= asOf;
    }

    /// <summary>
    /// The provider's own share, when it is about the window a person is in.
    /// </summary>
    public static double? Live(AllowanceWindow window, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.Reported is { } share && !RolledOver(window, asOf) ? share : null;
    }

    /// <summary>
    /// The share of a ceiling somebody typed, which is a guess at a number the
    /// provider knows.
    /// </summary>
    /// <remarks>
    /// Still the answer on every machine with no meter, which is most of them.
    /// </remarks>
    public static double? Typed(AllowanceWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return window.Limit is { } ceiling and > 0
            ? window.Tokens / (double)ceiling
            : null;
    }

    /// <summary>A share as whole percent, floored — never rounded up.</summary>
    /// <remarks>
    /// <b>Floored, so 99.6% of a plan does not read as spent</b> and, more to
    /// the point, so nothing below one percent reads as one.
    /// </remarks>
    public static int Percent(double share) => (int)Math.Floor(share * 100);
}
