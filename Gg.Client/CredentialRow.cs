using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// One row of the credentials list: a credential, what it is for, how it rests
/// here, and whether anything is left to do about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Credential-first, and the repository is what it serves.</b> The owner's
/// call for slice sixty: the question a person brings to this list is about a
/// credential, and the repository is context. The repositories pane retires into
/// this one.
/// </para>
/// <para>
/// <b>A row can exist with no credential, and that is the point.</b>
/// <see cref="Locator"/> is null for a repository nobody has registered one for —
/// the only row in the list that predicts a flight failing. Inventing the locator
/// such a credential *would* have would make a row claiming a credential nobody
/// registered.
/// </para>
/// <para>
/// <b>Nothing here is a secret.</b> This reaches a screen, a state dump and a
/// diagnostics bundle. A locator belongs in all three — it is a reference the
/// control plane already holds and `gg credential list` prints on purpose — and a
/// value, an envelope and key material belong in none of them. The shape is
/// asserted, not assumed.
/// </para>
/// </remarks>
/// <param name="Locator">The credential's locator, or null where none is registered.</param>
/// <param name="For">What it serves: a repository path, or what the locator names.</param>
/// <param name="Identity">The account it acts as, or null with no credential.</param>
/// <param name="Scopes">What it may do, empty with no credential.</param>
/// <param name="CredentialId">The registration's id, or null with no credential.</param>
/// <param name="AddedAt">When it was registered, or null with no credential.</param>
/// <param name="Whose">
/// The subject it was registered by, as a person is named outside the control
/// plane. Null on an older control plane, which is not "somebody else's".
/// </param>
/// <param name="Resting">How it rests here — one of <see cref="CredentialResting"/>.</param>
/// <param name="Standing">
/// What is left to do — one of <see cref="CredentialStanding"/>, and the only one
/// of the two words that can be ranked.
/// </param>
/// <param name="Holders">
/// Who can open the envelope on THIS machine, named where gg can say. Empty where
/// there is no envelope here, which is not a claim that nobody holds it.
/// </param>
public sealed record CredentialRow(
    string? Locator,
    string For,
    string? Identity,
    IReadOnlyList<string> Scopes,
    string? CredentialId,
    DateTimeOffset? AddedAt,
    string? Whose,
    string Resting,
    string Standing,
    IReadOnlyList<CredentialHolder> Holders);

/// <summary>
/// Building the credentials list out of three readings, and ordering it into a
/// worklist.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed on repositories UNION credentials.</b> A projection over registered
/// credentials alone is simpler and drops the row that matters: a repository with
/// no credential is where a flight fails, and it has no <c>CredentialSummary</c>
/// to be derived from. A projection over repositories alone drops the other half —
/// an agent's token and a vault reference serve no repository at all.
/// </para>
/// <para>
/// <b>It takes no store and no delegate, deliberately.</b> The resting shapes are
/// gathered once on a read task by <see cref="CredentialsAtRest"/> and handed over
/// as data. A builder that could be given either is one a later change can make
/// resolve a credential on a render path — and `gg doctor` shows how easily that
/// happens, since it answers this same question by decrypting everything.
/// </para>
/// </remarks>
public static class CredentialRows
{
    /// <summary>
    /// Every row, ordered with what is unfinished first and then by name.
    /// </summary>
    /// <param name="credentials">What the control plane holds references for.</param>
    /// <param name="repositories">What the tenant has registered, so a gap is visible.</param>
    /// <param name="resting">How each locator rests here, from this machine's store.</param>
    /// <param name="keys">What this tenant's people have registered, for naming holders.</param>
    /// <param name="thisMachine">This machine's own public key, when it has one.</param>
    /// <param name="pinned">The runner keys this console has pinned.</param>
    public static IReadOnlyList<CredentialRow> For(
        IReadOnlyList<CredentialSummary> credentials,
        IReadOnlyList<RepositoryRegistered> repositories,
        IReadOnlyList<CredentialAtRest> resting,
        IReadOnlyList<PrincipalKeySummary> keys,
        string? thisMachine,
        IReadOnlyList<PinnedKey> pinned)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(resting);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(pinned);

        var rows = new List<CredentialRow>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        // A REGISTERED CREDENTIAL IS A ROW, whether or not a repository names it.
        foreach (var credential in credentials)
        {
            var locator = credential.Reference.Locator;
            var repository = repositories.FirstOrDefault(
                r => string.Equals(
                    CredentialLocator.ForRepo(r.Path), locator, StringComparison.Ordinal));

            if (repository is not null)
            {
                // JOINED ON THE LOCATOR RATHER THAN THE NAME, so the pair is one
                // row. Two rows for one subject would double-count the work left.
                _ = claimed.Add(repository.Path);
            }

            rows.Add(new CredentialRow(
                Locator: locator,
                For: repository?.Path is { Length: > 0 } path ? path : Serves(credential, locator),
                Identity: credential.Reference.Identity,
                Scopes: credential.Reference.Scopes,
                CredentialId: credential.CredentialId,
                AddedAt: credential.AddedAt,
                Whose: credential.ReferencedBySubject,
                Resting: CredentialsAtRest.RestingOf(resting, locator),
                Standing: StandingOf(repository, CredentialsAtRest.RestingOf(resting, locator)),
                Holders: CredentialHolders.Of(
                    CredentialsAtRest.HoldersOf(resting, locator), keys, thisMachine, pinned)));
        }

        // AND A REPOSITORY WITH NO CREDENTIAL IS ALSO A ROW. This is the one the
        // list exists for; everything above says something is fine.
        foreach (var repository in repositories.Where(r => !claimed.Contains(r.Path)))
        {
            rows.Add(new CredentialRow(
                Locator: null,
                For: repository.Path,
                Identity: null,
                Scopes: [],
                CredentialId: null,
                AddedAt: null,
                Whose: null,
                Resting: CredentialResting.NotHere,
                Standing: Needed(repository)
                    ? CredentialStanding.NoneRegistered
                    : CredentialStanding.NotNeeded,
                // NOBODY, because there is no credential to be sealed to anyone.
                Holders: []));
        }

        // ORDERED BY WHAT IS LEFT TO DO, THEN BY NAME. A list in registry order
        // makes somebody read past everything already done to find the row they
        // came for - and that row is the whole reason this list has a cursor.
        return
        [
            .. rows
                .OrderBy(r => CredentialStanding.Rank(r.Standing))
                .ThenBy(r => r.For, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// What a credential serving no registered repository is for.
    /// </summary>
    /// <remarks>
    /// <b>A blank cell tells a reader nothing.</b> An agent's token and a vault
    /// reference serve something real; the locator says what, and the
    /// registration's own repo field says it when it has one.
    /// </remarks>
    private static string Serves(CredentialSummary credential, string locator) =>
        credential.Repo is { Length: > 0 } repo ? repo : locator;

    /// <summary>
    /// Whether this repository needs a credential at all.
    /// </summary>
    /// <remarks>
    /// <b><c>none</c> is the only value that means no credential, and absence
    /// means required.</b> <see cref="RepositoryCredentialModes"/> says so, and a
    /// reader treating an empty string as "nothing needed" would render every
    /// registration written before that member existed as fine.
    /// </remarks>
    private static bool Needed(RepositoryRegistered repository) =>
        !string.Equals(
            repository.Credential, RepositoryCredentialModes.None, StringComparison.Ordinal);

    /// <summary>
    /// What is left to do about a registered credential, from how it rests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two vocabularies meet here and nowhere else.</b> Resting is a fact
    /// about this machine's disk; standing is what a person can act on. A vault
    /// reference rests nowhere here and still needs nothing done — the machine
    /// reads it with its own identity when a flight asks — so it is
    /// <see cref="CredentialStanding.Here"/> reached differently.
    /// </para>
    /// <para>
    /// <b>Plaintext is not pending work.</b> It reseals on read with nobody
    /// present, so ranking it as something to do would put rows at the top of a
    /// worklist that no person can clear, which is how a worklist stops being
    /// read.
    /// </para>
    /// </remarks>
    private static string StandingOf(RepositoryRegistered? repository, string resting)
    {
        if (repository is not null && !Needed(repository))
        {
            return CredentialStanding.NotNeeded;
        }

        return resting switch
        {
            CredentialResting.Sealed or CredentialResting.Plaintext or CredentialResting.InAVault =>
                CredentialStanding.Here,
            CredentialResting.NotHere => CredentialStanding.MissingHere,

            // UNPLACEABLE AND NOT KNOWN BOTH LAND HERE, and the resting column
            // carries the difference. Neither is "missing here", because pasting
            // a value on this machine fixes neither a broken locator nor a read
            // that did not finish.
            _ => CredentialStanding.Unknown,
        };
    }
}
