namespace Gg.Console;

/// <summary>
/// The two board rows a watch stands about itself when it cannot do its job.
/// </summary>
/// <remarks>
/// <para>
/// <b>A failure is a nomination with no work in it.</b> When a sweep comes back
/// saying it could not sweep, or no sweep comes back at all, the control plane
/// stands a gated <c>sweep</c> row so that somebody sees it. On the board that
/// row reads exactly like a request to start work - "sweep · nominated · open
/// it?" - and a credential that broke on Monday is twenty-four of them by
/// Tuesday, every one asking to be opened.
/// </para>
/// <para>
/// <b>The subject is what tells them apart, and it is the control plane's
/// spelling.</b> <c>WatchMisses</c> in good-grief writes these two prefixes and
/// is the only author; this reads them back. A third prefix there is a row
/// this console draws as an ordinary nomination, which is today's behaviour
/// and not a wrong one - so the reading is open-ended on purpose rather than
/// throwing on what it does not know.
/// </para>
/// </remarks>
public static class WatchFailures
{
    /// <summary>A sweep came back and said it failed.</summary>
    public const string Unreachable = "watch-unreachable:";

    /// <summary>Nothing came back for longer than twice the watch's period.</summary>
    public const string Missed = "watch-missed:";

    /// <summary>
    /// What a failure row is called in the queue, or null for real work.
    /// </summary>
    /// <remarks>
    /// <b>The watch's name first</b>, because that is what a person goes and
    /// fixes, and the two endings say which kind of fixing: a sweep that failed
    /// said why on the row, and a watch that went quiet has no runner taking it.
    /// </remarks>
    public static string? Named(string? subject) =>
        subject switch
        {
            { } s when s.StartsWith(Unreachable, StringComparison.Ordinal)
                       && s.Length > Unreachable.Length
                => $"{s[Unreachable.Length..]} · sweep failed",
            { } s when s.StartsWith(Missed, StringComparison.Ordinal)
                       && s.Length > Missed.Length
                => $"{s[Missed.Length..]} · not reporting",
            _ => null,
        };
}
