namespace Gg.Runner.Vcs;

/// <summary>
/// How much of a repository's history a flight's tree carries: the tip alone,
/// or everything committed since a date.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tip, unless a loop asked for history.</b> A flight's tree is a
/// shallow fetch of exactly the pinned ref - the fastest thing, and the least
/// of somebody's code to have on disk - and nothing about editing, testing or
/// proposing needs more. Reading history does: GG-1084 (ado#18492,
/// 2026-10-11) was the first flight granted <c>read_history</c>, and every
/// <c>git log --grep</c> it ran printed nothing, because the tree was one
/// commit deep. So a loop that declares <c>history</c> is fetched back to a
/// date, and every other loop pays nothing.
/// </para>
/// <para>
/// <b>A date, not a count, and a year of it</b> (owner, 2026-10-11: "one year
/// is fine"). A commit count means a different span on every repository -
/// JDNext takes 1,367 commits in a quarter - where a date means the same
/// thing everywhere and is what a person asks for ("the story that closed in
/// July"). A year covers a closed story and its follow-ups at a fraction of
/// the whole: JDNext's full history is about 300 MB against 14 MB for the tip.
/// </para>
/// <para>
/// <b>Blobs and all.</b> A blobless fetch would be smaller and useless: the
/// history tool runs without the credential, so <c>show</c> and
/// <c>blame</c> could not fetch the blobs they need on demand.
/// </para>
/// </remarks>
public sealed record FetchDepth
{
    /// <summary>A year, which is the window a loop declaring <c>history</c> is fetched back to.</summary>
    public static readonly TimeSpan HistoryWindow = TimeSpan.FromDays(365);

    /// <summary>The pinned ref and nothing behind it.</summary>
    public static FetchDepth Tip { get; } = new() { Since = null };

    /// <summary>Everything committed since this date, or null for the tip alone.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Everything committed since <paramref name="since"/>.</summary>
    public static FetchDepth Back(DateTimeOffset since) => new() { Since = since };

    /// <summary>
    /// This many commits deep, counted every parent path, or null when the
    /// depth is a date or the tip.
    /// </summary>
    /// <remarks>
    /// <b>The fallback, never the ask.</b> Azure Repos refuses
    /// <c>--shallow-since</c> and <c>--deepen</c> (measured 2026-10-12,
    /// GG-1085) and supports only <c>--depth</c>, which re-fetched into an
    /// existing shallow clone deepens it. A count is blunt on a merge-heavy
    /// branch - JDNext at 1,500 is the whole repository - so it is asked for
    /// in the steps below, stopping as soon as the window is reached.
    /// </remarks>
    public int? Commits { get; init; }

    /// <summary>This many commits deep.</summary>
    public static FetchDepth Deepened(int commits) => new() { Commits = commits };

    /// <summary>
    /// The counts tried, in order, when a server refuses a date: measured on
    /// JDNext, 64 reached six months, 256 a year, 1024 the root.
    /// </summary>
    public static readonly IReadOnlyList<int> DeepeningSteps = [64, 256, 1024, 4096];

    /// <summary>
    /// What a loop with these moves is fetched to: a year back when it
    /// declares <c>history</c>, the tip otherwise.
    /// </summary>
    public static FetchDepth For(IEnumerable<string>? moves, DateTimeOffset now) =>
        moves is not null && moves.Contains(Gg.Contracts.LoopMoves.History, StringComparer.Ordinal)
            ? Back(now - HistoryWindow)
            : Tip;
}
