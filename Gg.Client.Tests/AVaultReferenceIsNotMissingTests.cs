using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A credential kept in a vault reads as resting in a vault, although
/// <c>Holds</c> answers false for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.2-03, and it is a trap rather than a feature.</b>
/// <c>MachineCredentialStore.Holds</c> answers <c>false</c> for every
/// <c>keyvault://</c> reference on purpose — <i>"the only way to ask a vault
/// whether it has one is to read it"</i> — so a column built on presence reports
/// every working vault credential as one nobody ever added. That is the wrong
/// answer in the dangerous direction: it sends somebody to add a credential that
/// already resolves on every flight.
/// </para>
/// <para>
/// <b>And the local store is worse than wrong about it.</b> A <c>keyvault://</c>
/// locator never validates, so <c>PathFor</c> throws rather than answering — which
/// is why the credential verbs now compose the vault-aware store, and why a row
/// must be built from the resting word rather than from presence.
/// </para>
/// <para>
/// <b>The second assertion pins the behaviour being survived, not changed.</b>
/// If <c>Holds</c> ever starts answering true for a vault reference, it is
/// because something asked the vault — which means a read, which is the thing
/// this whole design refuses.
/// </para>
/// </remarks>
public class AVaultReferenceIsNotMissingTests
{
    private const string Vaulted = "keyvault://acme-vault/jdapp-01";

    private static CredentialSummary AVaultCredential() => new()
    {
        CredentialId = "01a0fe55-cefc-75d0-ae6a-5e65958a0a60",
        For = "jdapp-01",
        AddedAt = DateTimeOffset.UnixEpoch,
        Reference = new CredentialReference
        {
            Kind = CredentialKinds.Local,
            Locator = Vaulted,
            Identity = "jdapp-01",
            Scopes = [CredentialScopes.Read],
        },
    };

    private static FileCredentialStore AScratchStore() =>
        new(Path.Combine(Path.GetTempPath(), "gg-vault-row-" + Guid.NewGuid().ToString("N")),
            MachineKey.LoadOrCreate(
                Path.Combine(Path.GetTempPath(), "gg-vault-k-" + Guid.NewGuid().ToString("N"), "k")));

    [Test]
    public async Task Its_row_says_it_rests_in_a_vault()
    {
        var rows = CredentialRows.For(
            [AVaultCredential()],
            [],
            [new CredentialAtRest(Vaulted, CredentialResting.InAVault, [])],
            keys: [], thisMachine: null, pinned: []);

        await Assert.That(rows.Single().Resting).IsEqualTo(CredentialResting.InAVault);
    }

    [Test]
    public async Task And_it_is_not_pending_work()
    {
        // A MACHINE THAT READS IT EVERY FLIGHT HAS NOTHING LEFT TO DO. Ranking
        // this with "missing here" would float a working credential to the top
        // of a worklist for ever, because nothing a person does can change it.
        var rows = CredentialRows.For(
            [AVaultCredential()],
            [],
            [new CredentialAtRest(Vaulted, CredentialResting.InAVault, [])],
            keys: [], thisMachine: null, pinned: []);

        await Assert.That(rows.Single().Standing).IsEqualTo(CredentialStanding.Here)
            .Because("the machine reads it with its own identity when a flight needs it, which is "
                   + "the same end state as holding one - reached differently.");
    }

    [Test]
    public async Task The_store_still_answers_false_for_whether_it_holds_one()
    {
        // THE BEHAVIOUR THE ROW EXISTS TO SURVIVE, pinned so that a later change
        // to Holds is a deliberate one. Asking a vault means reading it.
        ICredentialStore store = new MachineCredentialStore(
            AScratchStore(), new KeyVaultCredentialSource(new HttpClient()));

        await Assert.That(store.Holds(Vaulted)).IsFalse();
        await Assert.That(store.RestingOf(Vaulted)).IsEqualTo(CredentialResting.InAVault);
    }

    [Test]
    public async Task The_local_store_cannot_even_be_asked_about_one()
    {
        // WHY THE DECORATOR IS NOT OPTIONAL. A bare file store throws on the
        // locator rather than answering, which is how `gg credential rm` on a
        // vault-backed credential deregistered it centrally and then crashed.
        ICredentialStore local = AScratchStore();

        await Assert.That(() => local.RestingOf(Vaulted)).Throws<ArgumentException>();
    }
}
