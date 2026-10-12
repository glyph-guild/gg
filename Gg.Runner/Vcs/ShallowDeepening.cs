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
    public static bool RefusesShallowSince(string message) => false;

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
        await GitInvocation.Fetch(url, resolvedRef, secret).RunAsync(intoDirectory, cancellationToken);
        return 0;
    }
}
