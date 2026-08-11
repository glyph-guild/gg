using System.Reflection;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A scratch config root, so nothing here touches the developer's real store.
/// </summary>
internal sealed class TemporaryStore : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(), "gg-credential-tests", Guid.NewGuid().ToString("n"));

    public FileCredentialStore Store => new(Root);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

/// <summary>Answers the prompts a person would answer. Never a flag, never an argument.</summary>
internal sealed class ScriptedPrompt(string secret, string identity = "acme-bot") : ISecretPrompt
{
    public List<string> Prompts { get; } = [];

    public string ReadSecret(string prompt)
    {
        Prompts.Add(prompt);
        return secret;
    }

    public string ReadLine(string prompt)
    {
        Prompts.Add(prompt);
        return identity;
    }
}

internal sealed class HeldSessionStore(StoredSession? session) : ISessionStore
{
    public StoredSession? Read() => session;
    public void Write(StoredSession value) { }
    public void Clear() { }
}

/// <summary>
/// The local store: a mode-0600 file, in the same place the session already
/// lives.
/// </summary>
/// <remarks>
/// <para>
/// One store rather than two. Step 2b put the session token in a 0600 file and
/// left the keychain question to this step; the answer is that a second
/// mechanism for a second kind of secret is a component nobody asked for -
/// Article X, prefer fewer components.
/// </para>
/// <para>
/// <b>What this protects is stated rather than implied.</b> The security
/// property this slice delivers is that the secret never reaches the control
/// plane. It is not at-rest encryption on a laptop: anything running as this
/// uid can read the file, and <c>gg doctor</c> says so in those words.
/// </para>
/// </remarks>
public class LocalCredentialStoreTests
{
    private const string Locator = "local:github/acme-widgets";

    [Test]
    public async Task A_written_secret_reads_back()
    {
        using var temporary = new TemporaryStore();
        var store = temporary.Store;

        store.Write(Locator, "ghp-not-a-real-token");

        await Assert.That(store.Read(Locator)).IsEqualTo("ghp-not-a-real-token");
    }

    [Test]
    public async Task An_absent_secret_reads_as_nothing_rather_than_throwing()
    {
        using var temporary = new TemporaryStore();

        await Assert.That(temporary.Store.Read(Locator)).IsNull()
            .Because("a missing secret is a diagnosis the caller makes, not an exception from a file API.");
    }

    [Test]
    public async Task The_secret_file_is_readable_only_by_its_owner()
    {
        // POSIX modes only. Windows protects this directory by its ACL and the
        // assertion would be about a value the runtime invents there - but CI
        // is Linux, so this is checked on every push rather than never.
        if (OperatingSystem.IsWindows())
        {
            await Assert.That(FileCredentialStore.DefaultRoot()).IsNotEmpty();
            return;
        }

        using var temporary = new TemporaryStore();
        var store = temporary.Store;

        store.Write(Locator, "ghp-not-a-real-token");

        var mode = File.GetUnixFileMode(store.PathFor(Locator));

        await Assert.That(mode).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await Assert.That(File.GetUnixFileMode(Path.GetDirectoryName(store.PathFor(Locator))!))
            .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)
            .Because("a 0600 file inside a world-readable directory still tells everyone it exists.");
    }

    [Test]
    public async Task The_store_lives_beside_the_session_rather_than_somewhere_new()
    {
        // One store, not two. The session file's directory is the store's
        // directory, so there is one place to find, one place to back up, and
        // one place to get the permissions wrong.
        var sessionDirectory = Path.GetDirectoryName(FileSessionStore.DefaultPath())!;

        await Assert.That(FileCredentialStore.DefaultRoot()).StartsWith(sessionDirectory);
    }

    [Test]
    public async Task A_locator_cannot_escape_the_store()
    {
        // The charset the contract enforces makes this hard; asserting it makes
        // it checked. A locator is data from the control plane by the time the
        // runner sees it, and a path it can steer is a path it can steer
        // anywhere.
        using var temporary = new TemporaryStore();
        var store = temporary.Store;

        foreach (var hostile in (string[])
                 ["local:../../etc/passwd", "local:/etc/passwd", "local:..", "not-a-locator"])
        {
            await Assert.That(() => store.PathFor(hostile)).Throws<ArgumentException>()
                .Because($"'{hostile}' is not a locator, and treating it as one is a path this store chooses.");
        }
    }

    [Test]
    public async Task Removing_a_secret_removes_the_file_and_says_whether_there_was_one()
    {
        using var temporary = new TemporaryStore();
        var store = temporary.Store;

        store.Write(Locator, "ghp-not-a-real-token");

        await Assert.That(store.Remove(Locator)).IsTrue();
        await Assert.That(store.Read(Locator)).IsNull();
        await Assert.That(store.Remove(Locator)).IsFalse()
            .Because("removing nothing twice must not read as removing something.");
    }

    [Test]
    public async Task The_store_describes_its_own_protection_without_overstating_it()
    {
        using var temporary = new TemporaryStore();

        var protection = temporary.Store.Protection;

        await Assert.That(protection).Contains("0600");
        await Assert.That(protection.ToLowerInvariant()).DoesNotContain("keychain")
            .Because("this is a file with restrictive permissions and must not imply keychain-grade protection.");
        await Assert.That(protection.ToLowerInvariant()).DoesNotContain("encrypt")
            .Because("nothing here encrypts anything, and saying so would be the lie this slice exists to avoid.");
    }
}

/// <summary>
/// <c>gg credential add</c>: prompt, store locally, register a reference.
/// </summary>
/// <remarks>
/// The claim the whole slice exists for, from gg's side. The secret is read
/// from a prompt and written to a file; what crosses the wire is
/// <c>{kind, locator, identity, scopes}</c> and nothing else - and the request
/// type has no field that could carry more.
/// </remarks>
public class CredentialCommandTests
{
    private const string TheSecret = "ghp-THE-SECRET-VALUE-nobody-should-see";

    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static CredentialCommands Build(
        StubControlPlane stub, FileCredentialStore store, ISecretPrompt prompt, ISessionStore? sessions = null) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            sessions ?? new HeldSessionStore(ASession()),
            store,
            prompt);

    [Test]
    public async Task Add_prompts_for_the_secret_stores_it_locally_and_registers_a_reference()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        var store = temporary.Store;
        var prompt = new ScriptedPrompt(TheSecret);

        var result = await Build(stub, store, prompt).AddAsync("github/acme-widgets", [CredentialScopes.Read]);

        await Assert.That(prompt.Prompts).IsNotEmpty()
            .Because("the secret is prompted for. There is no other way in.");

        var registered = ((VerbResult.CredentialAdded)result).Value;
        await Assert.That(registered.Reference.Kind).IsEqualTo(CredentialKinds.Local);
        await Assert.That(registered.Reference.Scopes).IsEquivalentTo((string[])[CredentialScopes.Read]);

        await Assert.That(store.Read(registered.Reference.Locator)).IsEqualTo(TheSecret)
            .Because("the runner resolves the secret from the store, so the store is where it has to be.");
    }

    [Test]
    public async Task The_secret_is_in_no_request_body_gg_sends()
    {
        // Observed on the wire rather than read off the client's source. The
        // stub records every body it receives.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret))
            .AddAsync("github/acme-widgets", [CredentialScopes.Read]);

        foreach (var body in stub.ObservedBodies)
        {
            await Assert.That(body).DoesNotContain(TheSecret);
        }
    }

    [Test]
    public async Task The_poison_twin_the_body_recorder_really_does_see_what_gg_sent()
    {
        // The absence above passes on a recorder that captured nothing, which
        // is exactly what a stub returning early would do. So: the locator was
        // sent, and the recorder has it.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret))
            .AddAsync("github/acme-widgets", [CredentialScopes.Read]);

        await Assert.That(stub.ObservedBodies).IsNotEmpty();
        await Assert.That(string.Join("\n", stub.ObservedBodies)).Contains("acme-widgets")
            .Because("if the recorder cannot see the locator, its silence about the secret means nothing.");
    }

    [Test]
    public async Task Nothing_but_the_prompt_can_supply_the_secret()
    {
        // Structural. AddAsync takes a repository, scopes and an identity - all
        // facts - and gets the secret from the prompt port. A parameter here
        // would be a parameter a caller could fill from a flag, and a flag is
        // shell history and ps output.
        var parameters = typeof(CredentialCommands)
            .GetMethod(nameof(CredentialCommands.AddAsync))!
            .GetParameters()
            .Select(p => p.Name!)
            .ToList();

        string[] forbidden = ["secret", "token", "password", "value"];
        var offenders = parameters
            .Where(name => forbidden.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a secret parameter is a secret flag one refactor later. Found: " + string.Join(", ", offenders));
        await Assert.That(parameters).IsNotEmpty();
    }

    [Test]
    public async Task Scopes_wider_than_read_are_refused_before_anything_is_stored()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        var store = temporary.Store;

        await Assert.That(async () => await Build(stub, store, new ScriptedPrompt(TheSecret))
                .AddAsync("github/acme-widgets", ["write"]))
            .Throws<CredentialScopeException>();

        await Assert.That(Directory.Exists(temporary.Root) && Directory.EnumerateFiles(
                temporary.Root, "*", SearchOption.AllDirectories).Any()).IsFalse()
            .Because("a refused registration must not leave a secret on disk with nothing pointing at it.");
    }

    [Test]
    public async Task A_refused_registration_leaves_no_secret_behind()
    {
        // The control plane is the other gate, and it can refuse for reasons gg
        // does not know. Either way there must be no orphan.
        await using var stub = new StubControlPlane { RefuseCredential = "kind 'local' is not accepted here" };
        using var temporary = new TemporaryStore();

        await Assert.That(async () => await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret))
                .AddAsync("github/acme-widgets", [CredentialScopes.Read]))
            .Throws<CredentialRefusedException>();

        await Assert.That(Directory.Exists(temporary.Root) && Directory.EnumerateFiles(
                temporary.Root, "*", SearchOption.AllDirectories).Any()).IsFalse();
    }

    [Test]
    public async Task List_returns_the_references_the_control_plane_holds()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret))
            .AddAsync("github/acme-widgets", [CredentialScopes.Read]);

        var listed = ((VerbResult.Credentials)await Build(
            stub, temporary.Store, new ScriptedPrompt(TheSecret)).ListCredentialsAsync()).Value;

        await Assert.That(listed.Credentials).IsNotEmpty();
        await Assert.That(listed.Credentials[0].Reference.Identity).IsNotEmpty();
    }

    [Test]
    public async Task Remove_deregisters_first_and_then_deletes_the_local_secret()
    {
        // The order is the point, and it is the same order logout uses.
        // Deleting locally first and failing to deregister leaves the control
        // plane pointing at a secret that is not there - which is a flight that
        // stalls for a reason nobody can see.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        var store = temporary.Store;

        var added = ((VerbResult.CredentialAdded)await Build(stub, store, new ScriptedPrompt(TheSecret))
            .AddAsync("github/acme-widgets", [CredentialScopes.Read])).Value;

        var removed = ((VerbResult.CredentialRemoved)await Build(stub, store, new ScriptedPrompt(TheSecret))
            .RemoveCredentialAsync(added.CredentialId)).Value;

        await Assert.That(removed.CredentialId).IsEqualTo(added.CredentialId);
        await Assert.That(store.Read(added.Reference.Locator)).IsNull()
            .Because("a store you cannot clean is a store people work around.");
    }

    [Test]
    public async Task Removing_something_that_is_not_there_says_so()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();

        await Assert.That(async () => await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret))
                .RemoveCredentialAsync("019fe815-6136-7518-bb57-b06d6d3f411a"))
            .Throws<CredentialNotFoundException>();
    }

    [Test]
    public async Task Every_credential_verb_needs_a_session()
    {
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        var signedOut = new HeldSessionStore(null);

        await Assert.That(async () => await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret), signedOut)
            .ListCredentialsAsync()).Throws<NotSignedInException>();
        await Assert.That(async () => await Build(stub, temporary.Store, new ScriptedPrompt(TheSecret), signedOut)
            .AddAsync("github/acme-widgets", [CredentialScopes.Read])).Throws<NotSignedInException>();
    }

    [Test]
    public async Task Signing_out_before_prompting_means_no_secret_was_ever_typed()
    {
        // Small, and it matters: asking somebody for a token and then telling
        // them to log in first has taken a secret into a process for nothing.
        await using var stub = new StubControlPlane();
        using var temporary = new TemporaryStore();
        var prompt = new ScriptedPrompt(TheSecret);

        try
        {
            await Build(stub, temporary.Store, prompt, new HeldSessionStore(null))
                .AddAsync("github/acme-widgets", [CredentialScopes.Read]);
        }
        catch (NotSignedInException)
        {
            // Expected; the assertion is about what happened before it.
        }

        await Assert.That(prompt.Prompts).IsEmpty();
    }
}

/// <summary>
/// Credential results render both ways, through the one path every verb uses.
/// </summary>
public class CredentialVerbOutputTests
{
    private static CredentialSummary ASummary() => new()
    {
        CredentialId = "019fe815-6136-7518-bb57-b06d6d3f411a",
        Repo = "github/acme-widgets",
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = "local:github/acme-widgets",
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        },
        AddedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero),
    };

    [Test]
    public async Task A_credential_list_renders_and_round_trips()
    {
        var result = new VerbResult.Credentials(new CredentialList { Credentials = [ASummary()] });

        var json = VerbOutput.ToJson(result);
        var again = VerbOutput.Parse(result.Kind, json);

        await Assert.That(VerbOutput.ToText(again)).IsEqualTo(VerbOutput.ToText(result))
            .Because("the human rendering is a rendering of the JSON, with no second source.");
        await Assert.That(VerbOutput.ToText(result)).Contains("acme-bot");
        await Assert.That(VerbOutput.ToText(result)).Contains(CredentialScopes.Read);
    }

    [Test]
    public async Task An_empty_credential_list_says_so_rather_than_printing_nothing()
    {
        var text = VerbOutput.ToText(new VerbResult.Credentials(new CredentialList { Credentials = [] }));

        await Assert.That(text).IsNotEmpty()
            .Because("nothing found and nothing printed look identical, and one of them is a bug.");
    }

    [Test]
    public async Task Every_credential_result_kind_can_be_read_back()
    {
        // The practical form of "a diagnosis they can send us": we cannot look
        // at their terminal, so a --json payload has to be enough to re-render.
        VerbResult[] results =
        [
            new VerbResult.Credentials(new CredentialList { Credentials = [ASummary()] }),
            new VerbResult.CredentialAdded(new CredentialRegistered
            {
                CredentialId = ASummary().CredentialId,
                Reference = ASummary().Reference,
                AddedAt = ASummary().AddedAt,
            }),
            new VerbResult.CredentialRemoved(new Gg.Contracts.CredentialRemoved
            {
                CredentialId = ASummary().CredentialId,
                Reference = ASummary().Reference,
            }),
        ];

        foreach (var result in results)
        {
            var again = VerbOutput.Parse(result.Kind, VerbOutput.ToJson(result));
            await Assert.That(VerbOutput.ToText(again)).IsEqualTo(VerbOutput.ToText(result));
        }
    }

    [Test]
    public async Task No_rendering_of_a_credential_result_could_print_a_secret()
    {
        // There is nothing in the result to print, which is the point - but the
        // renderer is the last code before a screen, and a screen is a place
        // people screenshot into tickets.
        var rendered = VerbOutput.ToText(new VerbResult.Credentials(new CredentialList { Credentials = [ASummary()] }));

        await Assert.That(rendered).DoesNotContain("secret");
        await Assert.That(rendered).Contains("local:github/acme-widgets")
            .Because("the locator is what a person needs to see; it is a place, not a value.");
    }
}
