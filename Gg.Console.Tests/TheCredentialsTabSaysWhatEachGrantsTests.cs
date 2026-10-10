using Gg.Client;
using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The credentials tab names each credential by the service it opens and says
/// what it grants, the way `gg credential list` does.
/// </summary>
/// <remarks>
/// <b>And the send still names the locator.</b> The first cell used to BE the
/// locator, and <see cref="AudienceReview.Chosen"/> read it straight off the row
/// to decide what to send - so the cell becoming a sentence would have sent a
/// sentence. The locator is its own field now.
/// </remarks>
public class TheCredentialsTabSaysWhatEachGrantsTests
{
    private const string Locator = "local:jdx/jdnext";

    private static AppState OnTheTab() => new()
    {
        ActiveTab = TabId.Credentials,
        CredentialsVisible = true,
        Credentials = new CredentialList
        {
            Credentials =
            [
                new CredentialSummary
                {
                    CredentialId = "01a10ece-a9dc-77d7-a7d1-fd7e4b0b675b",
                    For = "JDX/JDNext",
                    AddedAt = DateTimeOffset.UnixEpoch,
                    Reference = new CredentialReference
                    {
                        Kind = CredentialKinds.Local,
                        Locator = Locator,
                        Identity = "kdeenanauth",
                        Scopes = [CredentialScopes.Read, CredentialScopes.Write],
                    },
                },
            ],
        },
        Repositories = new RegisteredRepositories
        {
            Repositories =
            [
                new RepositoryRegistered
                {
                    Name = "jdnext",
                    // THE KEY THIS FLEET'S PROFILES ALREADY USE for the service.
                    Provider = "ado",
                    Id = "R_1",
                    Path = "JDX/JDNext",
                    Credential = RepositoryCredentialModes.Required,
                    RegisteredAt = DateTimeOffset.UnixEpoch,
                    RegisteredBy = "a-directory:ada",
                },
            ],
        },
    };

    [Test]
    public async Task The_first_cell_is_the_service_and_the_repository()
    {
        // FROM THE CATALOG RATHER THAN SPELLED OUT, because only the catalog and
        // its own test may name a provider.
        var service = CredentialProviders.Find("ado")?.Name;
        var row = Rows.Credentials(OnTheTab()).Single();

        await Assert.That(service).IsNotNull();
        await Assert.That(row.Credential).IsEqualTo($"{service} · JDX/JDNext");
        await Assert.That(row.Grants).Contains("as kdeenanauth");
        await Assert.That(Rows.CredentialColumns[1]).IsEqualTo("grants");
    }

    [Test]
    public async Task And_what_a_send_names_is_still_the_locator()
    {
        await Assert.That(Rows.Credentials(OnTheTab()).Single().Locator).IsEqualTo(Locator);
        await Assert.That(AudienceReview.Chosen(OnTheTab() with { CredentialsSelected = 0 }))
            .IsEqualTo(Locator)
            .Because("a send that took the first cell would send a sentence.");
    }

    /// <summary>
    /// Arrows and clicks move the credentials table's own cursor (owner, 2026-10-10: "i cannot
    /// click or select rows with arrow keys on the credentials tab").
    /// </summary>
    /// <remarks>
    /// The table is painted from <c>CredentialsSelected</c>, and both paths moved
    /// <c>RepositorySelected</c> - the compose flow's repository - so the highlight never moved
    /// and every press changed what the next flight would fly against.
    /// </remarks>
    [Test]
    public async Task Arrows_and_clicks_move_the_credentials_cursor_and_nothing_else()
    {
        var two = OnTheTab() with
        {
            Credentials = OnTheTab().Credentials! with
            {
                Credentials =
                [
                    .. OnTheTab().Credentials!.Credentials,
                    OnTheTab().Credentials!.Credentials[0] with
                    {
                        CredentialId = "01a10ece-a9dc-77d7-a7d1-fd7e4b0b675c",
                        For = "JDX/Other",
                        Reference = OnTheTab().Credentials!.Credentials[0].Reference with { Locator = "local:jdx/other" },
                    },
                ],
            },
        };
        var rows = Rows.Credentials(two).Count;
        await Assert.That(rows).IsGreaterThanOrEqualTo(2);

        var down = Reducer.Reduce(two, Command.SelectNext);
        await Assert.That(down.CredentialsSelected).IsEqualTo(1);
        await Assert.That(down.RepositorySelected).IsEqualTo(two.RepositorySelected)
            .Because("the compose flow's repository is not this table's cursor.");

        var past = Reducer.Reduce(Reducer.Reduce(down, Command.SelectNext), Command.SelectNext);
        await Assert.That(past.CredentialsSelected).IsEqualTo(rows - 1);

        var clicked = Reducer.Pointed(two, 1);
        await Assert.That(clicked.CredentialsSelected).IsEqualTo(1);
        await Assert.That(clicked.RepositorySelected).IsEqualTo(two.RepositorySelected);
    }
}
