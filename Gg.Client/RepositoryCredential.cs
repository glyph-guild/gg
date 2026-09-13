namespace Gg.Client;

/// <summary>
/// One repository's credential standing, as this machine sees it.
/// </summary>
/// <remarks>
/// <b>Keyed by the repository's path, and carrying no secret and no
/// locator.</b> This travels into a console's state, onto a screen and into a
/// state dump: what belongs in all three is the conclusion, and nothing that
/// could be used to find a secret belongs in any of them.
/// </remarks>
/// <param name="Repo">The registry path this is about.</param>
/// <param name="Standing">One of <see cref="CredentialStanding"/>.</param>
public sealed record RepositoryCredential(string Repo, string Standing);

/// <summary>
/// Reading a standing out of a list of them.
/// </summary>
/// <remarks>
/// <b>By name, never by position.</b> The registry and the standings are
/// assembled from two reads, so a list indexed alongside another is a list
/// that labels every row with somebody else's answer the moment the two
/// disagree about order or length — and the disagreement would be invisible,
/// because both lists render fine.
/// </remarks>
public static class RepositoryCredentials
{
    /// <summary>
    /// The standing recorded for a path, or <see cref="CredentialStanding.Unknown"/>.
    /// </summary>
    /// <remarks>
    /// An absent entry is not good news. A read that half-failed, or a control
    /// plane with no credentials door, leaves this empty — and a caller that
    /// rendered that as reachable would be this project's recurring failure in
    /// a pane where somebody decides to fly.
    /// </remarks>
    public static string StandingOf(IReadOnlyList<RepositoryCredential> credentials, string path)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return credentials.FirstOrDefault(
            c => string.Equals(c.Repo, path, StringComparison.Ordinal))?.Standing
            ?? CredentialStanding.Unknown;
    }
}
