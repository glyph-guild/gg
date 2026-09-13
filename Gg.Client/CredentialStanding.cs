using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Whether a registered repository has the credential it needs, on this
/// machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>One answer assembled from three places, because no one place has it.</b>
/// The registry says whether a credential is needed at all; the control plane
/// says whether a reference was registered for it; this machine says whether
/// the secret is actually here. A person reading a repository list wants the
/// conclusion, and the three sources are exactly why they could not previously
/// reach one.
/// </para>
/// <para>
/// <b>Four words, and none of them collapses into another.</b> Each names a
/// different remedy — add the credential on this machine, register one at all,
/// or nothing because the repository authenticates to nothing — and a list
/// that said only "ok" and "not ok" would send half the people reading it to
/// the wrong one.
/// </para>
/// <para>
/// <b>Presence is asked of the store and never resolved.</b> The predicate
/// takes a locator and answers a bool, which is <c>ICredentialStore.Holds</c>'
/// whole reason for existing: this answer is rendered on a screen and written
/// into a state dump, and a secret must be in neither. Taking a predicate
/// rather than a store also leaves this pure, so every branch is tested
/// without a filesystem.
/// </para>
/// <para>
/// <b>Joined through the locator.</b> <see cref="CredentialLocator.ForRepo"/>
/// decides how a repository's name becomes a credential's address, lowercasing
/// and reducing on the way; comparing the spellings instead would answer
/// differently from the runner that goes looking, on exactly the repositories
/// whose owners typed a capital letter.
/// </para>
/// </remarks>
public static class CredentialStanding
{
    /// <summary>The repository authenticates to nothing, and none is wanted.</summary>
    public const string NotNeeded = "not needed";

    /// <summary>Registered, and the secret is on this machine.</summary>
    public const string Here = "here";

    /// <summary>
    /// Registered, and the secret is not on this machine — the one that fails
    /// at the runner.
    /// </summary>
    public const string MissingHere = "missing here";

    /// <summary>Needed, and nobody has registered one for this tenant.</summary>
    public const string NoneRegistered = "none registered";

    /// <summary>
    /// Nothing was said about it, which is not the same as nothing being wrong.
    /// </summary>
    /// <remarks>
    /// Rendered when a standing never arrived — an older control plane, a read
    /// that half-failed. The failure this project keeps finding is silence
    /// reading as agreement, and a repository list is a place somebody decides
    /// to fly.
    /// </remarks>
    public const string Unknown = "not known";

    /// <summary>
    /// The standing of one repository.
    /// </summary>
    /// <param name="repository">The registration, which says whether one is needed.</param>
    /// <param name="credentials">What the control plane holds references for.</param>
    /// <param name="heldHere">Whether a locator's secret is on this machine.</param>
    public static string For(
        RepositoryRegistered repository,
        IReadOnlyList<CredentialSummary> credentials,
        Func<string, bool> heldHere)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(heldHere);

        // `none` is the only value that means no credential. Absence means
        // required - RepositoryCredentialModes says so, and a reader that
        // treated an empty string as "nothing needed" would render every
        // registration written before the member existed as reachable.
        if (string.Equals(repository.Credential, RepositoryCredentialModes.None, StringComparison.Ordinal))
        {
            return NotNeeded;
        }

        var locator = CredentialLocator.ForRepo(repository.Path);

        var registered = credentials.Any(c => string.Equals(
            c.Reference.Locator, locator, StringComparison.Ordinal));

        return registered
            ? heldHere(locator) ? Here : MissingHere
            : NoneRegistered;
    }
}
