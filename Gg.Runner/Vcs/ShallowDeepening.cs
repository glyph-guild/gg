using System.Globalization;

namespace Gg.Runner.Vcs;

/// <summary>
/// Deepening a tree by count when the server will not take a date (slice
/// seventy-four, after GG-1085).
/// </summary>
/// <remarks>
/// <para>
/// <b>The date is asked for first; this is the fallback.</b> Azure Repos
/// refuses <c>--shallow-since</c> (no <c>deepen-since</c>) and hangs up on
/// <c>--deepen</c> (no <c>deepen-relative</c>); what it supports is
/// re-fetching an existing shallow clone with a larger <c>--depth</c>, which
/// deepens it. Measured on JDNext, 2026-10-12: 64 reached six months, 256 a
/// year, 1024 the root.
/// </para>
/// <para>
/// <b>A count is blunt</b> - depth counts every parent path, so on a
/// merge-heavy branch 1,500 is the whole repository - which is why it is
/// asked for in steps and stopped the moment the oldest shallow-boundary
/// commit is older than the window, or the history is exhausted. On such a
/// server the window is a floor rather than a ceiling.
/// </para>
/// </remarks>
public static class ShallowDeepening
{
    /// <summary>Whether a failed fetch failed because the server has no <c>deepen-since</c>.</summary>
    /// <remarks>
    /// <b>git's own words, and only those.</b> A credential failure, a missing
    /// ref or a network fault must not be answered by fetching more.
    /// </remarks>
    public static bool RefusesShallowSince(string message) =>
        message.Contains("does not support --shallow-since", StringComparison.Ordinal);

    /// <summary>
    /// Deepens an initialised, empty-or-shallow tree by count, in
    /// <see cref="FetchDepth.DeepeningSteps"/>, until the oldest shallow
    /// boundary commit is older than <paramref name="since"/> or the history
    /// is exhausted. Returns the step it stopped at.
    /// </summary>
    public static async Task<int> DeepenUntilAsync(
        string url, string resolvedRef, string intoDirectory, string? secret, DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var floor = since.ToUnixTimeSeconds();
        var reached = 0;

        foreach (var step in FetchDepth.DeepeningSteps)
        {
            await GitInvocation.Fetch(url, resolvedRef, secret, FetchDepth.Deepened(step))
                .RunAsync(intoDirectory, cancellationToken);
            reached = step;

            // THE HISTORY IS EXHAUSTED: nothing deeper exists, whatever the window.
            var shallow = (await GitInvocation.Plain("rev-parse", "--is-shallow-repository")
                .RunAsync(intoDirectory, cancellationToken)).Trim();
            if (!string.Equals(shallow, "true", StringComparison.Ordinal))
            {
                break;
            }

            // THE BOUNDARY IS OLD ENOUGH: the parentless commits of a shallow
            // clone are its boundary, and the oldest of them says how far back
            // the tree reaches.
            var boundary = await GitInvocation.Plain("log", "--max-parents=0", "--format=%ct", "FETCH_HEAD")
                .RunAsync(intoDirectory, cancellationToken);
            var oldest = boundary
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => long.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out var at) ? at : long.MaxValue)
                .DefaultIfEmpty(long.MaxValue)
                .Min();
            if (oldest < floor)
            {
                break;
            }
        }

        return reached;
    }
}
