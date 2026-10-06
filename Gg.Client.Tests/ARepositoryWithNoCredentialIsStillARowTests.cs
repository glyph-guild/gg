using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A repository nobody has registered a credential for is a row with an empty
/// credential cell, not a row that is missing.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.2-02, and it is the row this pane exists for.</b> Every other row says
/// something is fine. This one says a flight against that repository will fail
/// at the forge, and it is the only row on the screen that predicts a failure.
/// </para>
/// <para>
/// <b>Written to fail against the obvious implementation, deliberately.</b> A
/// projection keyed on registered credentials — one row per
/// <c>CredentialSummary</c> — is simpler, renders faster, and cannot produce this
/// row at all. It would pass a criterion that asked only for correct rows. So
/// what is asked for here is a row whose credential is <b>absent</b>, which
/// forces the row set to be keyed on repositories ∪ credentials rather than on
/// credentials alone.
/// </para>
/// <para>
/// <b>This is also what retires the repositories pane without losing it.</b> That
/// pane's whole job was to list what a tenant can fly against; if a repository
/// could vanish from this one, retiring it would be a removal rather than a move.
/// </para>
/// </remarks>
public class ARepositoryWithNoCredentialIsStillARowTests
{
    private static CredentialSummary ACredential(string repo, string locator) => new()
    {
        CredentialId = "01a0a21c-a32c-76e1-a716-ccbb19dda796",
        For = repo,
        AddedAt = DateTimeOffset.UnixEpoch,
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = locator,
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        },
    };

    private static RepositoryRegistered ARepository(
        string path, string credential = RepositoryCredentialModes.Required) => new()
    {
        Name = path.Replace('/', '-'),
        Provider = "forge",
        Id = "R_" + path.GetHashCode(StringComparison.Ordinal),
        Path = path,
        Credential = credential,
        RegisteredAt = DateTimeOffset.UnixEpoch,
        RegisteredBy = "a-directory:ada",
    };

    [Test]
    public async Task It_is_a_row()
    {
        var rows = CredentialRows.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            [ARepository("acme/widgets"), ARepository("acme/orphan")],
            [new CredentialAtRest("local:acme/widgets", CredentialResting.Sealed, [])],
            keys: [], thisMachine: null, pinned: []);

        await Assert.That(rows.Count).IsEqualTo(2)
            .Because("a projection keyed on registered credentials would answer 1 here, and the "
                   + "row it dropped is the only one that predicts a failure. Saw: "
                   + string.Join(", ", rows.Select(r => r.For)));

        await Assert.That(rows.Select(r => r.For)).Contains("acme/orphan");
    }

    [Test]
    public async Task Its_credential_cell_is_empty_rather_than_invented()
    {
        var rows = CredentialRows.For(
            [],
            [ARepository("acme/orphan")],
            [],
            keys: [], thisMachine: null, pinned: []);

        var row = rows.Single();

        await Assert.That(row.Locator).IsNull()
            .Because("there is no locator, and putting the one a credential WOULD have - "
                   + "`local:acme/orphan` - in that cell would make a row claiming a credential "
                   + "nobody registered.");

        await Assert.That(row.Identity).IsNull();
        await Assert.That(row.CredentialId).IsNull();
        await Assert.That(row.AddedAt).IsNull();
        await Assert.That(row.Scopes).IsEmpty();
    }

    [Test]
    public async Task It_says_none_is_registered_rather_than_that_one_is_missing_here()
    {
        // THE TWO ARE DIFFERENT REMEDIES AND THE WORDS ALREADY EXIST.
        // `missing here` sends somebody to push one from the machine that holds
        // it; `none registered` sends them to `gg credential add`, because there
        // is nothing anywhere to push.
        var rows = CredentialRows.For([], [ARepository("acme/orphan")], [],
            keys: [], thisMachine: null, pinned: []);

        await Assert.That(rows.Single().Standing).IsEqualTo(CredentialStanding.NoneRegistered);
    }

    [Test]
    public async Task A_repository_that_authenticates_to_nothing_is_not_pending_work()
    {
        // `none` IS THE ONLY VALUE THAT MEANS NO CREDENTIAL, and absence means
        // required - which RepositoryCredentialModes says and CredentialStanding
        // already honours. A reader treating an empty string as "nothing needed"
        // would render every registration written before that member existed as
        // fine.
        var rows = CredentialRows.For(
            [],
            [ARepository("acme/public", credential: RepositoryCredentialModes.None)],
            [],
            keys: [], thisMachine: null, pinned: []);

        await Assert.That(rows.Single().Standing).IsEqualTo(CredentialStanding.NotNeeded);
    }

    [Test]
    public async Task A_repository_whose_credential_is_registered_is_not_doubled()
    {
        // THE JOIN IS ON THE LOCATOR, NOT ON THE NAME. A repository and the
        // credential for it are one row; two rows for one subject would make a
        // list that double-counts the work left.
        var rows = CredentialRows.For(
            [ACredential("acme/widgets", CredentialLocator.ForRepo("acme/widgets"))],
            [ARepository("acme/widgets")],
            [new CredentialAtRest(CredentialLocator.ForRepo("acme/widgets"), CredentialResting.Sealed, [])],
            keys: [], thisMachine: null, pinned: []);

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows.Single().Locator).IsEqualTo(CredentialLocator.ForRepo("acme/widgets"));
    }
}
