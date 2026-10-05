using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A credential's row is built from the registry, the repositories and how each
/// one rests — and nothing on the way to a row opens a credential.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.2-01.</b> The row is what a pane draws and what `gg credential list`
/// already half-draws: a credential, what it is for, and how it rests here. All
/// three are facts somebody may read. The value is not, and the way this stays
/// true is that no code on the path to a row can ask for one.
/// </para>
/// <para>
/// <b>Why it is asserted rather than intended.</b> `gg doctor`'s credentials row
/// answers a near-identical question — is this one here — by calling
/// <c>Read</c>, which decrypts every credential on the machine and reseals the
/// plaintext ones as it goes. That is a defensible choice for a diagnostic run
/// by hand. A row is drawn on every refresh of a pane, so the same choice here
/// would mean every secret on the machine passing through the process that
/// paints the screen, thirty seconds at a time.
/// </para>
/// <para>
/// <b>The builder takes the resting shapes rather than a store</b>, which is
/// <c>CredentialStanding.For</c>'s shape and <c>CredentialsAtRest.For</c>'s. It
/// keeps the filesystem out of a render path, and it keeps the word
/// <c>CredentialStore</c> out of anything the console's structural scan reads.
/// </para>
/// </remarks>
public class ACredentialRowOpensNothingTests
{
    private static CredentialSummary ACredential(
        string repo, string locator, string identity = "acme-bot") => new()
    {
        CredentialId = "01a0a21c-a32c-76e1-a716-ccbb19dda796",
        Repo = repo,
        AddedAt = DateTimeOffset.UnixEpoch,
        ReferencedBySubject = "a-directory:ada",
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = locator,
            Identity = identity,
            Scopes = [CredentialScopes.Read, CredentialScopes.Write],
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
        Ref = "main",
        Narrowings = "two",
    };

    [Test]
    public async Task A_row_carries_the_credential_what_it_is_for_and_how_it_rests()
    {
        var rows = CredentialRows.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            [ARepository("acme/widgets")],
            [new CredentialAtRest("local:acme/widgets", CredentialResting.Sealed)]);

        var row = rows.Single();

        await Assert.That(row.Locator).IsEqualTo("local:acme/widgets");
        await Assert.That(row.For).IsEqualTo("acme/widgets");
        await Assert.That(row.Identity).IsEqualTo("acme-bot");
        await Assert.That(row.Resting).IsEqualTo(CredentialResting.Sealed);
        await Assert.That(row.Standing).IsEqualTo(CredentialStanding.Here);
    }

    [Test]
    public async Task Nothing_on_the_path_to_a_row_can_ask_for_a_value()
    {
        // STRUCTURAL, because the shape is the guarantee. The builder's only
        // inputs are two contract lists and a list of locator/word pairs. There
        // is no store, no delegate that could reach one, and therefore nothing
        // a later change could quietly route through.
        var parameters = typeof(CredentialRows)
            .GetMethod(nameof(CredentialRows.For))!
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToList();

        await Assert.That(parameters.Any(t => t.Contains("CredentialStore", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a builder that could be handed a store is one a later change can make "
                   + $"resolve a credential. Takes: {string.Join(", ", parameters)}");

        await Assert.That(parameters.Any(t => t.StartsWith("Func", StringComparison.Ordinal)))
            .IsFalse()
            .Because("and a delegate is a store with the name filed off - the resting shapes are "
                   + "gathered on the read task and handed over as data. Takes: "
                   + string.Join(", ", parameters));
    }

    [Test]
    public async Task A_row_carries_whose_the_credential_is()
    {
        // REFERENCEDBYSUBJECT REACHES A PERSON FOR THE FIRST TIME. It is on the
        // wire, Doctor.IsYours reads it, and nothing has ever displayed it - so
        // "all 5 registered credentials resolve" could be said on a machine that
        // checked three.
        var rows = CredentialRows.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            [],
            [new CredentialAtRest("local:acme/widgets", CredentialResting.Sealed)]);

        await Assert.That(rows.Single().Whose).IsEqualTo("a-directory:ada");
    }

    [Test]
    public async Task A_credential_for_no_registered_repository_is_still_a_row()
    {
        // AN AGENT TOKEN IS THE EVERYDAY CASE. local:agent/claude authenticates
        // an executor rather than a forge, so no repository names it - and it is
        // the credential a resident runner most depends on.
        var rows = CredentialRows.For(
            [ACredential("", CredentialLocator.ForAgent("claude"))],
            [],
            [new CredentialAtRest(CredentialLocator.ForAgent("claude"), CredentialResting.Sealed)]);

        var row = rows.Single();

        await Assert.That(row.Locator).IsEqualTo(CredentialLocator.ForAgent("claude"));
        await Assert.That(row.For).IsNotEmpty()
            .Because("a row whose `for` cell is blank tells a reader nothing; a credential that "
                   + "serves no repository still serves something, and the locator says what.");
    }

    [Test]
    public async Task A_resting_shape_nobody_reported_is_not_read_as_here()
    {
        // ABSENCE IS NOT GOOD NEWS, in the one list that is about secrets.
        var rows = CredentialRows.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            [ARepository("acme/widgets")],
            []);

        await Assert.That(rows.Single().Resting).IsEqualTo(CredentialResting.NotKnown);
        await Assert.That(rows.Single().Standing).IsNotEqualTo(CredentialStanding.Here)
            .Because("a read that half-failed must not render as a credential this machine holds.");
    }
}
