using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A credential is named by the service it opens and what it grants there,
/// rather than by where gg keeps it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for on 2026-10-10</b>, before a SonarCloud token was handed to an
/// agent: every place gg named a credential printed its locator -
/// <c>local:tracker/ado</c>, <c>local:jdx/jdnext</c> - which says where a secret
/// is kept and nothing about what it opens.
/// </para>
/// <para>
/// <b>This file and <see cref="CredentialProviders"/> are the two that may name a
/// provider</b>, by the owner's decision; <c>ProviderNeutralityTests</c> names
/// both and nothing else.
/// </para>
/// </remarks>
public class ACredentialSaysWhatItGrantsTests
{
    private static CredentialReference AReference(
        string locator, params string[] scopes) => new()
    {
        Kind = CredentialKinds.Local,
        Locator = locator,
        Identity = "kevin",
        Scopes = scopes.Length == 0 ? [CredentialScopes.Read] : scopes,
    };

    private static RepositoryRegistered ARepository(string path, string provider) => new()
    {
        Name = path.Replace('/', '-'),
        Provider = provider,
        Id = "R_1",
        Path = path,
        Credential = RepositoryCredentialModes.Required,
        RegisteredAt = DateTimeOffset.UnixEpoch,
        RegisteredBy = "a-directory:ada",
    };

    private static CredentialPlaces Places(
        IEnumerable<RepositoryRegistered>? repositories = null,
        params (string Key, string Host)[] trackers) =>
        CredentialPlaces.From(repositories ?? [], trackers);

    // ---- what it is ----

    [Test]
    public async Task A_repositorys_credential_is_named_by_the_provider_it_was_registered_with()
    {
        var name = CredentialNames.Describe(
            AReference(CredentialLocator.ForRepo("JDX/JDNext"),
                CredentialScopes.Read, CredentialScopes.Write),
            "JDX/JDNext",
            Places([ARepository("JDX/JDNext", "ado")]));

        await Assert.That(name.Short).IsEqualTo("Azure DevOps · JDX/JDNext");
        await Assert.That(name.Grants).IsEqualTo("Code (Read & write), as kevin")
            .Because("write includes read on that service, and the words are the ones its own "
                   + "token page uses - so what a person reads here is what they ticked there.");
    }

    [Test]
    public async Task A_trackers_credential_is_named_by_the_host_this_machine_reads_it_at()
    {
        var name = CredentialNames.Describe(
            AReference(CredentialLocator.ForTracker("board")),
            "board",
            Places(trackers: ("board", "https://dev.azure.com/HRTMS/JDX")));

        await Assert.That(name.Short).IsEqualTo("Azure DevOps · HRTMS/JDX")
            .Because("the key is a name somebody chose; the host is where the token goes.");
        await Assert.That(name.Access).IsEqualTo("Work Items (Read)");
    }

    [Test]
    public async Task A_known_key_names_its_service_where_no_host_is_declared()
    {
        var name = CredentialNames.Describe(
            AReference(CredentialLocator.ForTracker("ado")), "ado", CredentialPlaces.None);

        await Assert.That(name.Service).IsEqualTo("Azure DevOps")
            .Because("the console declares no trackers, and `ado` is how this fleet already "
                   + "names that service in every profile.");
    }

    [Test]
    public async Task SonarCloud_is_known_by_its_host_and_by_its_name()
    {
        await Assert.That(CredentialProviders.Find("https://sonarcloud.io/organizations/jdx"))
            .IsEqualTo(CredentialProviders.SonarCloud);
        await Assert.That(CredentialProviders.Find("sonarcloud"))
            .IsEqualTo(CredentialProviders.SonarCloud);
        await Assert.That(CredentialProviders.SonarCloud.TokenPage)
            .IsEqualTo("https://sonarcloud.io/account/security")
            .Because("the wizard sends a person to the page that makes the token.");
    }

    // ---- and what it is not ----

    [Test]
    public async Task A_service_gg_does_not_know_is_said_with_the_locator_rather_than_guessed()
    {
        var locator = CredentialLocator.ForRepo("acme/payments");
        var name = CredentialNames.Describe(
            AReference(locator), "acme/payments",
            Places([ARepository("acme/payments", "forge")]));

        await Assert.That(name.Known).IsFalse();
        await Assert.That(name.Short).IsEqualTo("acme/payments");
        await Assert.That(name.Sentence).Contains("cannot tell which service")
            .Because("a wrong service on a credential is worse than the locator it replaced.");
        await Assert.That(name.Sentence).Contains(locator);
    }

    [Test]
    public async Task A_host_that_only_looks_like_a_known_one_is_not_it()
    {
        foreach (var lookalike in (string[])
                 ["https://dev.azure.com.attacker.example/x", "notsonarcloud.io",
                  "https://sonarcloud.io.example/x", "adoo"])
        {
            await Assert.That(CredentialProviders.Find(lookalike)).IsNull()
                .Because($"'{lookalike}' is not a service gg knows, and naming it as one would "
                       + "tell a person a token goes somewhere it does not.");
        }
    }

    [Test]
    public async Task A_vault_reference_names_no_subject_and_says_so()
    {
        var name = CredentialNames.Describe(
            AReference("keyvault://kv-example.vault.example/ado-workitem-read"),
            "ado-workitem-read", CredentialPlaces.None);

        await Assert.That(name.Known).IsFalse()
            .Because("a secret's name in a vault is somebody's label, not a declaration.");
    }

    // ---- where a person reads it ----

    [Test]
    public async Task The_list_leads_with_what_it_is_and_still_prints_where_it_is_kept()
    {
        var locator = CredentialLocator.ForRepo("JDX/JDNext");
        var summary = new CredentialSummary
        {
            CredentialId = "01a10ece-a9dc-77d7-a7d1-fd7e4b0b675b",
            For = "JDX/JDNext",
            AddedAt = DateTimeOffset.UnixEpoch,
            Reference = AReference(locator, CredentialScopes.Read, CredentialScopes.Write),
        };

        var text = VerbOutput.ToText(new VerbResult.Credentials(
            new CredentialList { Credentials = [summary] },
            [],
            [CredentialNames.Describe(
                summary.Reference, summary.For, Places([ARepository("JDX/JDNext", "ado")]))]));

        await Assert.That(text.Split('\n')[0].Trim()).IsEqualTo("Azure DevOps · JDX/JDNext");
        await Assert.That(text).Contains("Code (Read & write), as kevin");
        await Assert.That(text).Contains(locator)
            .Because("`gg credential trust-this-machine` and `gg doctor` still name it by its "
                   + "locator, so the list must still say it.");
    }

    [Test]
    public async Task Two_credentials_at_one_locator_each_keep_their_own_account()
    {
        // MEASURED ON THIS TENANT: two people registered `hrtms/jdx`, so two
        // credentials share local:hrtms/jdx - and matching names by locator
        // printed the first one's account on both lines. The account is the
        // whole point of the line.
        var locator = CredentialLocator.ForRepo("hrtms/jdx");
        CredentialSummary Registered(string id, string identity) => new()
        {
            CredentialId = id,
            For = "hrtms/jdx",
            AddedAt = DateTimeOffset.UnixEpoch,
            Reference = AReference(locator) with { Identity = identity },
        };

        var list = new CredentialList
        {
            Credentials =
            [
                Registered("01a0fe55-cefc-75d0-ae6a-5e65958a0a60", "pcarbone"),
                Registered("01a10ecb-aba0-71e4-bcff-bf7d38092185", "kdeenanauth"),
            ],
        };

        var text = VerbOutput.ToText(new VerbResult.Credentials(
            list, [],
            [.. list.Credentials.Select(
                c => CredentialNames.Describe(c.Reference, c.For, CredentialPlaces.None))]));

        await Assert.That(text).Contains("as pcarbone");
        await Assert.That(text).Contains("as kdeenanauth");
    }

    [Test]
    public async Task A_credential_gg_cannot_place_says_so_where_it_is_listed()
    {
        var name = CredentialNames.Describe(
            AReference(CredentialLocator.ForRepo("hrtms/jdx")), "hrtms/jdx", CredentialPlaces.None);

        await Assert.That(name.Grants).Contains("service not recognised")
            .Because("a list line that reads like every other one tells a person gg knows what "
                   + "it opens, and here it does not.");
    }
}
