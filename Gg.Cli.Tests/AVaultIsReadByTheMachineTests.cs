using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// A <c>keyvault://</c> reference is read by the machine's own identity, held
/// in memory, and never written anywhere - slice forty-three, rules 23 and 24.
/// </summary>
/// <remarks>
/// <para>
/// <b>Adapter three of a port that has had one since it was written.</b>
/// <c>ICredentialStore</c> said so - "Keychain and Key Vault are adapter two and
/// adapter three" - and a fleet machine nobody signs in on is the case that
/// needs it: its credentials cannot be typed at it, so they have to be somewhere
/// its identity can read.
/// </para>
/// <para>
/// <b>Runner-side, and only runner-side.</b> The control plane's own program
/// document ruled a vault out as a control-plane source, because it would put
/// the control plane back in the business of reaching customer secrets. The reading that document endorses is this one: the
/// runner's identity reads the customer's vault, and the control plane holds, at
/// most, the reference.
/// </para>
/// </remarks>
public class AVaultIsReadByTheMachineTests
{
    private const string Token = "eyJ0eXAi-the-machine-identity-token";
    private const string Secret = "ghp_the-secret-in-the-vault";

    /// <summary>Answers the metadata service and the vault, and remembers what it was asked.</summary>
    private sealed class Cloud(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? identity = null,
        Func<HttpRequestMessage, HttpResponseMessage>? vault = null) : HttpMessageHandler
    {
        internal List<HttpRequestMessage> Asked { get; } = [];

        protected override HttpResponseMessage Send(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request);

            return request.RequestUri!.Host == "169.254.169.254"
                ? (identity ?? AToken)(request, cancellationToken)
                : (vault ?? TheSecret)(request);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));

        private static HttpResponseMessage AToken(HttpRequestMessage _, CancellationToken __) =>
            Json(HttpStatusCode.OK,
                $$$"""{"access_token":"{{{Token}}}","expires_on":"1790000000","resource":"https://vault.example.net","token_type":"Bearer"}""");

        private static HttpResponseMessage TheSecret(HttpRequestMessage _) =>
            Json(HttpStatusCode.OK,
                $$$"""{"value":"{{{Secret}}}","id":"https://acme-fleet.vault.example.net/secrets/repo-read/0f1e","attributes":{"enabled":true}}""");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Refused(HttpStatusCode status) =>
        Json(status, """{"error":{"code":"Forbidden","message":"The user, group or application does not have secrets get permission"}}""");

    private sealed class ScratchStore : IDisposable
    {
        internal string Root { get; } = Path.Combine(
            Path.GetTempPath(), "gg-vault-tests", Guid.NewGuid().ToString("n"));

        internal FileCredentialStore Store => new(Root);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static KeyVaultCredentialSource Reading(Cloud cloud) =>
        new(new HttpClient(cloud), identityPatience: TimeSpan.FromMilliseconds(200));

    [Test]
    public async Task A_vault_reference_is_read_with_two_calls_and_the_machines_identity()
    {
        var cloud = new Cloud();

        var secret = Reading(cloud).Read("keyvault://acme-fleet.vault.example.net/repo-read");

        await Assert.That(secret).IsEqualTo(Secret);
        await Assert.That(cloud.Asked.Count).IsEqualTo(2)
            .Because("the token, then the secret - and nothing an SDK would add on top.");

        var identity = cloud.Asked[0];
        await Assert.That(identity.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(identity.RequestUri!.GetLeftPart(UriPartial.Path))
            .IsEqualTo("http://169.254.169.254/metadata/identity/oauth2/token");
        await Assert.That(identity.RequestUri.Query).Contains("api-version=2018-02-01");
        await Assert.That(Uri.UnescapeDataString(identity.RequestUri.Query))
            .Contains("resource=https://vault.example.net")
            .Because("the token is minted for the domain the vault is served from, and so is only "
                   + "ever sent to a host inside the audience it was minted for.");
        await Assert.That(identity.Headers.TryGetValues("Metadata", out var metadata)
                && metadata.Single() == "true").IsTrue()
            .Because("the metadata service refuses a request without it, which is its SSRF guard.");

        var read = cloud.Asked[1];
        await Assert.That(read.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(read.RequestUri!.ToString())
            .IsEqualTo("https://acme-fleet.vault.example.net/secrets/repo-read?api-version=7.4");
        await Assert.That(read.Headers.Authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(read.Headers.Authorization?.Parameter).IsEqualTo(Token);
    }

    [Test]
    public async Task The_secret_never_reaches_the_disk()
    {
        using var scratch = new ScratchStore();
        var store = new MachineCredentialStore(scratch.Store, Reading(new Cloud()));

        var secret = store.Read("keyvault://acme-fleet.vault.example.net/repo-read");

        await Assert.That(secret).IsEqualTo(Secret);
        await Assert.That(Directory.Exists(scratch.Root)
                && Directory.EnumerateFileSystemEntries(scratch.Root, "*", SearchOption.AllDirectories).Any())
            .IsFalse()
            .Because("a vault is where this secret lives; a copy under the credential store is a "
                   + "second home for it that nobody rotates.");
    }

    [Test]
    public async Task A_vault_reference_is_never_written_by_this_machine()
    {
        using var scratch = new ScratchStore();
        var store = new MachineCredentialStore(scratch.Store, Reading(new Cloud()));

        await Assert.That(() => store.Write("keyvault://acme-fleet.vault.example.net/repo-read", Secret))
            .Throws<ArgumentException>();
        await Assert.That(() => store.Remove("keyvault://acme-fleet.vault.example.net/repo-read"))
            .Throws<ArgumentException>();
        await Assert.That(Directory.Exists(scratch.Root)).IsFalse();
    }

    [Test]
    public async Task A_local_locator_is_still_the_file_and_asks_nobody()
    {
        using var scratch = new ScratchStore();
        var cloud = new Cloud();
        var store = new MachineCredentialStore(scratch.Store, Reading(cloud));
        store.Write("local:acme/widgets", "from-the-file");

        await Assert.That(store.Read("local:acme/widgets")).IsEqualTo("from-the-file");
        await Assert.That(store.Holds("local:acme/widgets")).IsTrue();
        await Assert.That(cloud.Asked).IsEmpty();
    }

    [Test]
    public async Task A_vault_that_refuses_the_machine_says_which_vault_and_what_to_grant()
    {
        var cloud = new Cloud(vault: _ => Refused(HttpStatusCode.Forbidden));

        var refused = await Assert.That(() => Reading(cloud).Read("keyvault://acme-fleet.vault.example.net/repo-read"))
            .Throws<CredentialUnavailableException>();

        await Assert.That(refused!.Message).Contains("keyvault://acme-fleet.vault.example.net/repo-read");
        await Assert.That(refused.Message).Contains("managed identity");
        await Assert.That(refused.Message).Contains("get")
            .Because("the remedy is a permission on the vault, and the sentence should name it.");
    }

    [Test]
    public async Task A_secret_the_vault_does_not_have_says_so()
    {
        var cloud = new Cloud(vault: _ => Json(HttpStatusCode.NotFound,
            """{"error":{"code":"SecretNotFound","message":"A secret with (name/id) repo-read was not found in this key vault."}}"""));

        var missing = await Assert.That(() => Reading(cloud).Read("keyvault://acme-fleet.vault.example.net/repo-read"))
            .Throws<CredentialUnavailableException>();

        await Assert.That(missing!.Message).Contains("no secret named 'repo-read'");
        await Assert.That(missing.Message).Contains("acme-fleet");
    }

    [Test]
    public async Task A_machine_with_no_identity_says_so_and_never_asks_the_vault()
    {
        var cloud = new Cloud(identity: (_, _) =>
            throw new HttpRequestException("No route to host (169.254.169.254:80)"));

        var none = await Assert.That(() => Reading(cloud).Read("keyvault://acme-fleet.vault.example.net/repo-read"))
            .Throws<CredentialUnavailableException>();

        await Assert.That(none!.Message).Contains("no managed identity");
        await Assert.That(cloud.Asked.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_metadata_service_that_never_answers_is_given_up_on()
    {
        // NOT A CLOUD MACHINE AT ALL is the commonest way to have no identity, and there
        // the link-local address is not refused - it is silent. A read that
        // waited the client's full timeout would hold a flight for it.
        var cloud = new Cloud(identity: (_, cancellation) =>
        {
            if (!cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("the read gave the metadata service no deadline.");
            }

            throw new OperationCanceledException(cancellation);
        });

        var none = await Assert.That(() => Reading(cloud).Read("keyvault://acme-fleet.vault.example.net/repo-read"))
            .Throws<CredentialUnavailableException>();

        await Assert.That(none!.Message).Contains("no managed identity");
    }

    [Test]
    [Arguments("keyvault://acme-fleet.vault.example.net@evil.example.com/repo-read")]
    [Arguments("keyvault://acme-fleet.vault.example.net:8443/repo-read")]
    [Arguments("keyvault://acme-fleet.vault.example.net/repo-read/../other")]
    [Arguments("keyvault://acme-fleet.vault.example.net/repo-read?x=1")]
    [Arguments("keyvault://acme-fleet.vault.example.net/")]
    [Arguments("keyvault://acme-fleet.vault.example.net")]
    [Arguments("keyvault://acme-fleet/repo-read")]
    [Arguments("keyvault://acme-fleet.net/repo-read")]
    [Arguments("keyvault://169.254.169.254/repo-read")]
    [Arguments("keyvault://ab.vault.example.net/repo-read")]
    [Arguments("keyvault:///repo-read")]
    public async Task A_reference_that_could_steer_the_token_is_refused_before_any_call(string locator)
    {
        // THE VAULT'S NAME BECOMES A HOST, and the machine's token goes to that
        // host. A name that is anything but a vault name is a way to send this
        // machine's identity somewhere else, so the charset is the control.
        var cloud = new Cloud();

        await Assert.That(() => Reading(cloud).Read(locator)).Throws<ArgumentException>();
        await Assert.That(cloud.Asked).IsEmpty();
    }

    [Test]
    public async Task The_resolver_turns_a_vault_it_cannot_read_into_a_diagnosis()
    {
        using var scratch = new ScratchStore();
        var store = new MachineCredentialStore(
            scratch.Store, Reading(new Cloud(vault: _ => Refused(HttpStatusCode.Forbidden))));

        var resolution = await new LocalCredentialResolver(store).ResolveAsync(new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = "keyvault://acme-fleet.vault.example.net/repo-read",
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        });

        await Assert.That(resolution).IsTypeOf<CredentialResolution.Unresolvable>()
            .Because("a runner that cannot read a vault must say so on the flight log, not stall "
                   + "or throw halfway through a claim - ADR-0004 named exactly this failure.");
    }

    [Test]
    public async Task What_travels_names_the_reference_and_never_the_token_or_the_secret()
    {
        // RULE 24. The diagnosis is the one thing about a vault read that leaves
        // this machine - it becomes a CredentialResolutionFailure on the flight
        // log - so it may carry the reference and nothing the read held.
        using var scratch = new ScratchStore();
        var store = new MachineCredentialStore(scratch.Store, Reading(new Cloud(
            vault: request => Json(HttpStatusCode.Forbidden,
                $$$"""{"error":{"code":"Forbidden","message":"caller {{{request.Headers.Authorization}}} lacks get"}}"""))));

        var resolution = await new LocalCredentialResolver(store).ResolveAsync(new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = "keyvault://acme-fleet.vault.example.net/repo-read",
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        });

        var problem = ((CredentialResolution.Unresolvable)resolution).Problem;
        await Assert.That(problem).Contains("keyvault://acme-fleet.vault.example.net/repo-read");
        await Assert.That(problem).DoesNotContain(Token)
            .Because("a vault's error body can echo the caller, and the caller is a bearer token.");
        await Assert.That(problem).DoesNotContain(Secret);
    }

    [Test]
    public async Task A_tool_server_without_its_vault_credential_is_not_told_to_add_a_file()
    {
        var sentence = IntentConfiguration.Unresolvable(
            new IntentReader("ado", "ado-mcp", [], "ADO_TOKEN", "keyvault://acme-fleet.vault.example.net/tracker-read"),
            secret: null);

        await Assert.That(sentence).IsNotNull();
        await Assert.That(sentence!).Contains("vault");
        await Assert.That(sentence).DoesNotContain("gg credential add")
            .Because("a file on this machine is not where a vault reference is kept, and the "
                   + "advice would put a second copy of the secret there.");
    }

    [Test]
    public async Task Every_runner_read_goes_through_the_machines_store()
    {
        // A PORT NOTHING CALLS is the defect this repository keeps finding: an
        // adapter tested to the hilt and reachable from no product code. The
        // composition root is where a vault reference either reaches the vault
        // or reaches a file store that throws on anything but local:.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        var program = File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));

        var direct = Regex.Matches(
                program,
                @"new FileCredentialStore\(\)\s*\.Read\(|new LocalCredentialResolver\(\s*new FileCredentialStore\(\)\s*\)")
            .Select(m => m.Value)
            .ToList();

        await Assert.That(direct).IsEmpty()
            .Because("each of these reads a keyvault:// reference as a malformed local locator. "
                   + "Found: " + string.Join(" | ", direct));
    }
}
