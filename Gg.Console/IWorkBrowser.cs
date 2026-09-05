using Gg.Local;

namespace Gg.Console;

/// <summary>
/// Whatever a tracker offers to pick from.
/// </summary>
/// <remarks>
/// <para>
/// <b>An interface so the loop can be tested without a process</b>, and so the
/// one implementation that starts a child stays the only thing that does.
/// <see cref="ILiveSource"/> is the precedent and the sentence is deliberately
/// the same: the difference is a child rather than a file.
/// </para>
/// <para>
/// <b>It answers with an outcome and never throws</b> — see
/// <see cref="BrowseOutcome"/>. The caller is a redraw, and a redraw that has
/// to catch is a console that dies because a tracker did.
/// </para>
/// </remarks>
public interface IWorkBrowser
{
    /// <summary>Which tracker this browses, or null when none is configured.</summary>
    string? Key { get; }

    Task<BrowseOutcome> BrowseAsync(string? cursor, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// The first configured reader, as something the loop can ask.
/// </summary>
/// <remarks>
/// <para>
/// <b>One tracker for now, and the shape says which.</b> <see cref="Key"/> is
/// on the screen precisely because a tenant may configure several and this will
/// have to choose; until there is a way to choose, taking the first declared is
/// honest and the pane names it. Picking silently among several would be the
/// bug this interface exists to make visible.
/// </para>
/// <para>
/// <b>It owns nothing it did not start.</b> <see cref="ReaderSessions"/> holds
/// the processes and is disposed by whoever composed the console.
/// </para>
/// </remarks>
public sealed class ConfiguredWorkBrowser(ReaderSessions readers) : IWorkBrowser
{
    private readonly ReaderSessions _readers = readers;

    public string? Key => _readers.Keys.Count > 0 ? _readers.Keys[0] : null;

    public async Task<BrowseOutcome> BrowseAsync(
        string? cursor, int limit, CancellationToken cancellationToken)
    {
        if (Key is not { } key || _readers.For(key) is not { } reader)
        {
            return new BrowseOutcome.Silent(
                "No tracker is configured to browse on this machine.");
        }

        return await reader.BrowseAsync(cursor, limit, cancellationToken);
    }
}
