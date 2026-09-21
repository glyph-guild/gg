namespace Gg.Contracts;

/// <summary>
/// How a list is asked for a page, so both sides agree what a page is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared in the contract because both sides need the same answer.</b> The
/// control plane clamps a limit and composes the cursor; gg asks with a limit
/// and hands the cursor back untouched. Two spellings of a page size is two
/// pages of different length depending on who asked - the argument
/// <see cref="DestinationBranch"/> already makes for a rule that crosses.
/// </para>
/// <para>
/// <b>Not a wire type - nothing serializes it.</b> It is a RULE that crosses,
/// which is the other thing this package is for.
/// </para>
/// <para>
/// <b>The cursor is opaque, and it belongs to the answer.</b> What a page
/// stopped at is composed by the side that ordered the rows; a client that
/// parsed it would be a second implementation of an ordering it does not own,
/// and the two would agree until the ordering changed.
/// </para>
/// </remarks>
public static class Paging
{
    /// <summary>The page a reader gets when it names no size.</summary>
    /// <remarks>
    /// <b>A hundred, decided 2026-09-20.</b> Big enough that the first page of
    /// a tenant's flights is the whole of what anybody scrolls in a sitting -
    /// the dev tenant has flown about two hundred and twenty - and small enough
    /// that the answer is bounded whatever a tenant's history grows to.
    /// </remarks>
    public const int DefaultLimit = 100;

    /// <summary>The most a reader may ask for in one page.</summary>
    /// <remarks>
    /// <b>A ceiling rather than a promise.</b> A reader that asked for
    /// everything by naming a huge limit would be the unbounded read this
    /// exists to stop, arriving as a parameter.
    /// </remarks>
    public const int MaxLimit = 1000;

    /// <summary>Why this limit may not be served, or null when it may.</summary>
    /// <remarks>
    /// <b>Refused rather than clamped silently.</b> A door that answered a
    /// different question from the one asked - a hundred rows to a caller who
    /// asked for five thousand - reads as an answer about the tenant rather
    /// than about the request, and a caller paging on it would loop.
    /// </remarks>
    public static string? Validate(int limit) =>
        limit < 1 || limit > MaxLimit
            ? $"{limit} is not a page size. A page is between 1 and {MaxLimit} rows, and "
            + $"omitting it answers with {DefaultLimit}."
            : null;
}
