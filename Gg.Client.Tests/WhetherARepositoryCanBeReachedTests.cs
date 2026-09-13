using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// What a person looking at a registered repository needs to know before they
/// fly against it, and the one question the console may not answer by
/// resolving a secret.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four states, and three of them are invisible today.</b> A registry entry
/// carries a credential mode, and the tenant separately registers credential
/// references, and this machine separately holds the secrets. So a repository
/// is reachable, unreachable here, unreachable anywhere, or needs nothing —
/// and the repositories tab shows a path and a name, which cannot distinguish
/// any of them. The one that costs the most is a flight that claims fine and
/// fails at the runner, where nobody is looking: <c>gg doctor</c> calls that
/// state blocking for exactly that reason.
/// </para>
/// <para>
/// <b>Presence, never the secret.</b> This console may not resolve a
/// credential — CLAUDE.md states it as a rule about what a UI session may
/// reach — and "is there one" does not need one resolved. <c>Holds</c> answers
/// from the filesystem without reading the file, so the answer can travel into
/// <c>AppState</c>, onto a screen and into a state dump without a secret ever
/// being in the process to leak.
/// </para>
/// <para>
/// <b>Joined on the locator rather than on the slug.</b>
/// <c>CredentialLocator.ForRepo</c> already decides how a repository's name
/// becomes a credential's address — it lowercases and reduces — so comparing
/// slugs directly would answer differently from the runner that actually goes
/// looking. One computation per kind of question.
/// </para>
/// </remarks>
public class WhetherARepositoryCanBeReachedTests
{
    private static RepositoryRegistered Repository(string path, string credential) => new()
    {
        Name = "widgets",
        Provider = "a-forge",
        Id = "42",
        Path = path,
        Credential = credential,
        RegisteredBy = "somebody",
        RegisteredAt = DateTimeOffset.UnixEpoch,
    };

    private static CredentialSummary Registered(string repo) => new()
    {
        CredentialId = "cred-1",
        Repo = repo,
        Reference = new CredentialReference
        {
            Kind = "local",
            Locator = CredentialLocator.ForRepo(repo),
            Identity = "somebody",
            Scopes = ["read"],
        },
        AddedAt = DateTimeOffset.UnixEpoch,
    };

    [Test]
    public async Task A_repository_needing_no_credential_says_so_rather_than_looking_unconfigured()
    {
        // `none` is a real answer and not an omission: a mirror reached over
        // file:// has nothing to authenticate to, and demanding a credential
        // for it produced a flight that could not be flown.
        var standing = CredentialStanding.For(
            Repository("acme/mirror", RepositoryCredentialModes.None),
            credentials: [],
            heldHere: _ => false);

        await Assert.That(standing).IsEqualTo(CredentialStanding.NotNeeded)
            .Because("rendering this the same as a missing credential would send somebody to add "
                   + "one that the claim would then refuse.");
    }

    [Test]
    public async Task A_credential_registered_and_present_here_is_the_only_reachable_state()
    {
        var standing = CredentialStanding.For(
            Repository("acme/widgets", RepositoryCredentialModes.Required),
            [Registered("acme/widgets")],
            heldHere: locator => locator == CredentialLocator.ForRepo("acme/widgets"));

        await Assert.That(standing).IsEqualTo(CredentialStanding.Here);
    }

    [Test]
    public async Task Registered_but_not_on_this_machine_is_the_one_that_fails_at_the_runner()
    {
        // The doctor's blocking case, said where somebody is looking BEFORE
        // they fly rather than in a report they ran last week.
        var standing = CredentialStanding.For(
            Repository("acme/widgets", RepositoryCredentialModes.Required),
            [Registered("acme/widgets")],
            heldHere: _ => false);

        await Assert.That(standing).IsEqualTo(CredentialStanding.MissingHere);
    }

    [Test]
    public async Task A_required_credential_nobody_registered_is_a_different_fault()
    {
        // Distinct from the case above on purpose: the fix for one is `gg
        // credential add` on THIS machine, and for the other it is registering
        // the credential at all. One word for both would send half the people
        // reading it to the wrong remedy.
        var standing = CredentialStanding.For(
            Repository("acme/widgets", RepositoryCredentialModes.Required),
            credentials: [],
            heldHere: _ => true);

        await Assert.That(standing).IsEqualTo(CredentialStanding.NoneRegistered);
    }

    [Test]
    public async Task An_absent_mode_means_required_here_exactly_as_it_does_everywhere_else()
    {
        // RepositoryCredentialModes says absence and `required` are the same
        // fact. A console that read absence as "nothing needed" would render
        // every pre-slice registration as reachable.
        var standing = CredentialStanding.For(
            Repository("acme/widgets", credential: ""),
            credentials: [],
            heldHere: _ => false);

        await Assert.That(standing).IsEqualTo(CredentialStanding.NoneRegistered);
    }

    [Test]
    public async Task The_join_is_the_locator_and_not_the_spelling()
    {
        // A registry path and a credential's repo are both "as that provider
        // spells it", and ForRepo lowercases and reduces before either reaches
        // a filesystem. Comparing the spellings would answer differently from
        // the runner that goes looking, on exactly the repositories whose
        // owners typed a capital letter.
        var standing = CredentialStanding.For(
            Repository("JDX/Agile-Cortex", RepositoryCredentialModes.Required),
            [Registered("jdx/agile-cortex")],
            heldHere: locator => locator == CredentialLocator.ForRepo("jdx/agile-cortex"));

        await Assert.That(standing).IsEqualTo(CredentialStanding.Here)
            .Because("the runner resolves through the locator, so a console comparing slugs would "
                   + "report a working repository as missing its credential.");
    }

    [Test]
    public async Task The_store_answers_whether_it_holds_one_without_handing_it_over()
    {
        // THE WHOLE REASON THIS METHOD EXISTS. Read returns the secret, and a
        // console that called it to render a column would have pulled every
        // credential this machine holds into the process that draws the screen
        // and writes state dumps.
        var root = Path.Combine(Path.GetTempPath(), $"gg-holds-{Guid.NewGuid():N}");
        var store = new FileCredentialStore(root);

        try
        {
            await Assert.That(store.Holds("local:acme/widgets")).IsFalse()
                .Because("nothing has been written, and a store that answered true would make "
                       + "every repository look reachable on a fresh machine.");

            store.Write("local:acme/widgets", "a-secret");

            await Assert.That(store.Holds("local:acme/widgets")).IsTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
