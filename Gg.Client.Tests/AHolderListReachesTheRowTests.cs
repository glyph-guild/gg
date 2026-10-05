using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The holders of a sealed credential reach the row that will be drawn, and the
/// envelope does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wiring half of step 3, and the reason it is its own change.</b>
/// <c>CredentialHolders</c> resolves a list of public keys into names; this is how
/// that list gets out of a sealed file and onto a row. Without it the join is a
/// port nothing calls — the exact shape S60.1-03's ratchet polices one layer over.
/// </para>
/// <para>
/// <b>The store answers with KEYS, not with the envelope.</b> Putting
/// <c>SealedFor</c> on <c>ICredentialStore</c> would hand every caller a
/// <see cref="SealedCredential"/> — ciphertext and all — and the model must not be
/// able to hold one (slice sixty, rule 7). <c>HoldersOf</c> is the narrow answer:
/// public keys, which the contract says a refusal may name because saying so gives
/// nothing away.
/// </para>
/// <para>
/// <b>Nothing here opens anything.</b> Deserialising an envelope to read whose
/// keys it names is not decrypting it, and the ciphertext is never touched.
/// </para>
/// </remarks>
public class AHolderListReachesTheRowTests
{
    private static FileCredentialStore AScratchStore(out MachineKey machine)
    {
        machine = MachineKey.LoadOrCreate(
            Path.Combine(Path.GetTempPath(), "gg-holders-k-" + Guid.NewGuid().ToString("N"), "k"));

        return new FileCredentialStore(
            Path.Combine(Path.GetTempPath(), "gg-holders-" + Guid.NewGuid().ToString("N")),
            machine);
    }

    private static CredentialSummary ACredential(string repo, string locator) => new()
    {
        CredentialId = "01a0a21c-a32c-76e1-a716-ccbb19dda796",
        Repo = repo,
        AddedAt = DateTimeOffset.UnixEpoch,
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = locator,
            Identity = "acme-bot",
            Scopes = [CredentialScopes.Read],
        },
    };

    [Test]
    public async Task The_store_says_whose_keys_a_sealed_credential_names()
    {
        var store = AScratchStore(out var machine);
        ICredentialStore asked = store;

        store.Write("local:acme/widgets", "a-secret");

        await Assert.That(asked.HoldersOf("local:acme/widgets")).IsEquivalentTo(
            (List<string>)[machine.PublicKey])
            .Because("a credential this machine sealed is sealed to this machine's own key, and "
                   + "that is the first thing a reader wants to know about it.");
    }

    [Test]
    public async Task A_credential_that_is_not_sealed_here_names_no_holders()
    {
        var store = AScratchStore(out _);
        ICredentialStore asked = store;

        await Assert.That(asked.HoldersOf("local:acme/absent")).IsEmpty()
            .Because("absent is not an error and not a holder; the resting word already says "
                   + "which it is.");

        var plaintext = store.PathFor("local:acme/legacy");
        Directory.CreateDirectory(Path.GetDirectoryName(plaintext)!);
        File.WriteAllText(plaintext, "ghp-from-before-sealing");

        await Assert.That(asked.HoldersOf("local:acme/legacy")).IsEmpty()
            .Because("a plaintext credential is sealed to nobody, and answering with a holder "
                   + "would claim a protection the file does not have.");
    }

    [Test]
    public async Task A_vault_reference_names_no_holders_and_does_not_throw()
    {
        ICredentialStore machine = new MachineCredentialStore(
            AScratchStore(out _), new KeyVaultCredentialSource(new HttpClient()));

        await Assert.That(machine.HoldersOf("keyvault://acme-vault/jdapp-01")).IsEmpty()
            .Because("how it rests there is the vault's to say, and this machine did not seal it. "
                   + "The locator never validates locally, so the bare store would throw.");
    }

    [Test]
    public async Task Asking_for_holders_opens_nothing()
    {
        // THE ENVELOPE IS READ AND THE CIPHERTEXT IS NOT TOUCHED. Deserialising
        // to see whose keys are named is not a decrypt - and if this ever became
        // one, every refresh of a pane would decrypt every credential.
        var store = AScratchStore(out var machine);

        store.Write("local:acme/widgets", "a-secret-nobody-should-see");

        var holders = ((ICredentialStore)store).HoldersOf("local:acme/widgets");

        await Assert.That(holders).IsNotEmpty();

        // A SECOND MACHINE'S STORE, which could not decrypt even if it tried, and
        // must still be able to say who the holders are.
        var stranger = new FileCredentialStore(
            store.Root,
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), "gg-stranger-" + Guid.NewGuid().ToString("N"), "k")));

        await Assert.That(((ICredentialStore)stranger).HoldersOf("local:acme/widgets"))
            .IsEquivalentTo((List<string>)[machine.PublicKey])
            .Because("naming the holders of an envelope you cannot open is the whole reason the "
                   + "refusal sentence can say who to ask.");
    }

    [Test]
    public async Task The_gathered_resting_shape_carries_the_holders_with_it()
    {
        // ONE READING, for AirspaceRepositories' reason. The word and the holders
        // are two facts about the same file at the same moment, and two lists a
        // caller had to pair are two that come to disagree.
        var store = AScratchStore(out var machine);
        ICredentialStore asked = store;

        store.Write("local:acme/widgets", "a-secret");

        var resting = CredentialsAtRest.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            asked.RestingOf,
            asked.HoldersOf);

        await Assert.That(resting.Single().Resting).IsEqualTo(CredentialResting.Sealed);
        await Assert.That(resting.Single().Holders).IsEquivalentTo((List<string>)[machine.PublicKey]);
    }

    [Test]
    public async Task A_rows_holders_are_named_not_raw()
    {
        var store = AScratchStore(out var machine);
        ICredentialStore asked = store;

        store.Write("local:acme/widgets", "a-secret");

        var rows = CredentialRows.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            [],
            CredentialsAtRest.For(
                [ACredential("acme/widgets", "local:acme/widgets")],
                asked.RestingOf,
                asked.HoldersOf),
            keys: [],
            thisMachine: machine.PublicKey,
            pinned: []);

        var holder = rows.Single().Holders.Single();

        await Assert.That(holder.Kind).IsEqualTo(CredentialHolderKinds.ThisMachine);
        await Assert.That(holder.Fingerprint)
            .IsEqualTo(PrincipalKeyFingerprint.Of(machine.PublicKey));
    }

    [Test]
    public async Task A_row_for_a_credential_with_no_envelope_has_no_holders()
    {
        var rows = CredentialRows.For(
            [ACredential("acme/widgets", "local:acme/widgets")],
            [],
            [new CredentialAtRest("local:acme/widgets", CredentialResting.NotHere, [])],
            keys: [],
            thisMachine: null,
            pinned: []);

        await Assert.That(rows.Single().Holders).IsEmpty()
            .Because("a credential this machine has never been pushed names nobody HERE, and an "
                   + "empty list is the honest answer rather than a guess at the tenant's keys.");
    }
}
