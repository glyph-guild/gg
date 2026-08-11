using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// The seam between the runner's resolver port and the local store.
/// </summary>
/// <remarks>
/// <para>
/// It lives in this project because this is the only one that sees both
/// halves: <c>Gg.Runner</c> deliberately cannot reference <c>Gg.Client</c>, so
/// a runner is structurally unable to hold a developer's session, and
/// <c>Gg.Client</c> has no business knowing what a runner is.
/// </para>
/// <para>
/// Small, and worth its own tests anyway: every way resolution can fail passes
/// through here, and each one has to come out as a diagnosis rather than as an
/// exception or - worse - as an empty secret.
/// </para>
/// </remarks>
public class LocalCredentialResolverTests
{
    private sealed class ScratchStore : IDisposable
    {
        internal string Root { get; } = Path.Combine(
            Path.GetTempPath(), "gg-resolver-tests", Guid.NewGuid().ToString("n"));

        internal FileCredentialStore Store => new(Root);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static CredentialReference AReference(string locator = "local:acme/widgets") => new()
    {
        Kind = CredentialKinds.Local,
        Locator = locator,
        Identity = "acme-bot",
        Scopes = [CredentialScopes.Read],
    };

    [Test]
    public async Task A_stored_secret_resolves()
    {
        using var scratch = new ScratchStore();
        var store = scratch.Store;
        store.Write("local:acme/widgets", "not-a-real-value");

        var resolution = await new LocalCredentialResolver(store).ResolveAsync(AReference());

        await Assert.That(resolution).IsTypeOf<CredentialResolution.Resolved>();
        await Assert.That(((CredentialResolution.Resolved)resolution).Secret).IsEqualTo("not-a-real-value");
    }

    [Test]
    public async Task A_missing_secret_is_a_diagnosis_naming_the_locator_and_the_remedy()
    {
        using var scratch = new ScratchStore();

        var resolution = await new LocalCredentialResolver(scratch.Store).ResolveAsync(AReference());

        var problem = ((CredentialResolution.Unresolvable)resolution).Problem;
        await Assert.That(problem).Contains("local:acme/widgets");
        await Assert.That(problem).Contains("gg credential add")
            .Because("this sentence ends up on a flight log, and a flight log nobody can act on is logging.");
    }

    [Test]
    public async Task An_empty_secret_is_refused_as_loudly_as_a_missing_one()
    {
        // Article XI. An empty secret fetches nothing and fails at the provider
        // with an authentication error, which is a very long way from here and
        // indistinguishable from a revoked token.
        using var scratch = new ScratchStore();
        var store = scratch.Store;
        store.Write("local:acme/widgets", "");

        var resolution = await new LocalCredentialResolver(store).ResolveAsync(AReference());

        await Assert.That(resolution).IsTypeOf<CredentialResolution.Unresolvable>();
        await Assert.That(((CredentialResolution.Unresolvable)resolution).Problem).Contains("empty");
    }

    [Test]
    public async Task A_locator_the_store_refuses_is_a_diagnosis_rather_than_a_crash()
    {
        // By the time a runner sees a locator it is data that came back from
        // the control plane. A malformed one must produce a flight-log event
        // naming it, not an unhandled exception halfway through a claim.
        using var scratch = new ScratchStore();

        var resolution = await new LocalCredentialResolver(scratch.Store)
            .ResolveAsync(AReference(locator: "local:../../etc/passwd"));

        await Assert.That(resolution).IsTypeOf<CredentialResolution.Unresolvable>();
    }

    [Test]
    public async Task The_resolver_reads_the_same_place_the_verb_writes()
    {
        // The whole mechanism in one assertion: a person runs gg credential add
        // on this machine, and the runner process - a child of the same binary
        // - reads what it wrote. Nothing in between is the control plane.
        await Assert.That(new FileCredentialStore().PathFor("local:acme/widgets"))
            .StartsWith(FileCredentialStore.DefaultRoot());
    }
}
