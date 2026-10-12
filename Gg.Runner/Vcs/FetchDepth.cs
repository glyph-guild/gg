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
    /// What a loop with these moves is fetched to: a year back when it
    /// declares <c>history</c>, the tip otherwise.
    /// </summary>
    public static FetchDepth For(IEnumerable<string>? moves, DateTimeOffset now) =>
        moves is not null && moves.Contains(Gg.Contracts.LoopMoves.History, StringComparer.Ordinal)
            ? Back(now - HistoryWindow)
            : Tip;
}
