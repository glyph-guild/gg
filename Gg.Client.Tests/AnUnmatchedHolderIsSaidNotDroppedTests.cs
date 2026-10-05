using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A holder whose key the lookup cannot explain is named as unknown, and never
/// dropped from the list.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.3-02, and dropping is the tempting bug.</b> A list that quietly omitted
/// the holders it could not name would answer <i>"who can open this"</i> with a
/// number that is too small — and too small in the direction that matters, because
/// every omitted holder is somebody who can read the secret.
/// </para>
/// <para>
/// <b>It is an ordinary state rather than a fault.</b> A credential sealed to a
/// colleague before this console pinned their runner, a key registered in a tenant
/// this machine has not listed, a holder from a machine that has since been
/// retired: each is a real holder gg simply cannot put a name to. The fingerprint
/// is still shown, which is what lets a person ask somebody whether it is theirs.
/// </para>
/// <para>
/// <b>And the count is the assertion.</b> Naming is checked elsewhere; what this
/// pins is that the list is as long as the envelope's holder list, whatever gg
/// managed to resolve.
/// </para>
/// </remarks>
public class AnUnmatchedHolderIsSaidNotDroppedTests
{
    private static string AKey() =>
        Disposed(System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256));

    /// <summary>
    /// The public half, with the key handle released.
    /// </summary>
    /// <remarks>
    /// <b>Disposed because these tests mint dozens.</b> An undisposed
    /// <c>ECDiffieHellman</c> holds a platform key handle until a finalizer runs,
    /// and a suite that leaks them under parallel execution is a suite whose
    /// failures depend on timing.
    /// </remarks>
    private static string Disposed(System.Security.Cryptography.ECDiffieHellman key)
    {
        using (key)
        {
            return Convert.ToBase64String(key.PublicKey.ExportSubjectPublicKeyInfo());
        }
    }

    private static PrincipalKeySummary APersonsKey(string principal, string publicKey) => new()
    {
        KeyId = Guid.NewGuid().ToString(),
        Principal = principal,
        PublicKey = publicKey,
        Fingerprint = PrincipalKeyFingerprint.Of(publicKey),
        RegisteredAt = DateTimeOffset.UnixEpoch,
    };

    [Test]
    public async Task It_is_still_in_the_list()
    {
        var ada = AKey();
        var stranger = AKey();

        var holders = CredentialHolders.Of(
            [ada, stranger], [APersonsKey("ada", ada)], thisMachine: null, pinned: []);

        await Assert.That(holders.Count).IsEqualTo(2)
            .Because("a holder gg cannot name is still somebody who can read the secret, and a "
                   + "list that hid it would answer 'who can open this' with a number that is "
                   + "wrong in the dangerous direction.");
    }

    [Test]
    public async Task It_is_said_to_be_unknown_rather_than_left_blank()
    {
        var stranger = AKey();

        var holders = CredentialHolders.Of([stranger], [], thisMachine: null, pinned: []);

        await Assert.That(holders.Single().Kind).IsEqualTo(CredentialHolderKinds.Unknown);
        await Assert.That(holders.Single().Named).IsNull()
            .Because("there is no name to give, and inventing one - 'somebody', 'a machine' - "
                   + "would read as a fact gg had established.");
    }

    [Test]
    public async Task Its_fingerprint_is_still_shown()
    {
        // THE WHOLE POINT OF NOT DROPPING IT. A fingerprint is what a person can
        // take to a colleague and ask whether it is theirs; without it, an
        // unnamed holder is a row saying only that somebody somewhere can read
        // this.
        var stranger = AKey();

        var holders = CredentialHolders.Of([stranger], [], thisMachine: null, pinned: []);

        await Assert.That(holders.Single().Fingerprint)
            .IsEqualTo(PrincipalKeyFingerprint.Of(stranger));
    }

    [Test]
    public async Task A_malformed_holder_is_not_allowed_to_take_the_list_with_it()
    {
        // THE ENVELOPE IS BYTES FROM ANOTHER MACHINE. A holder that is not base64
        // reaches PrincipalKeyFingerprint.Of and throws, and a list that let that
        // out would be one that a single damaged envelope turns into a stack
        // trace - with every other holder perfectly readable.
        var ada = AKey();

        var holders = CredentialHolders.Of(
            ["not base64 at all!!"], [APersonsKey("ada", ada)], thisMachine: null, pinned: []);

        await Assert.That(holders.Count).IsEqualTo(1);
        await Assert.That(holders.Single().Kind).IsEqualTo(CredentialHolderKinds.Unknown);
        await Assert.That(holders.Single().Fingerprint).IsNotNull();
    }

    [Test]
    public async Task The_order_is_the_envelopes_order()
    {
        // STABLE, so a refresh does not move rows under somebody. The envelope's
        // own order is the only one both ends agree on - sorting by name would
        // put every unknown holder in one clump whose membership changes as keys
        // get registered.
        var first = AKey();
        var second = AKey();
        var third = AKey();

        var holders = CredentialHolders.Of(
            [first, second, third],
            [APersonsKey("ada", second)],
            thisMachine: null,
            pinned: []);

        await Assert.That(holders[1].Named).IsEqualTo("ada");
        await Assert.That(holders[0].Fingerprint).IsEqualTo(PrincipalKeyFingerprint.Of(first));
        await Assert.That(holders[2].Fingerprint).IsEqualTo(PrincipalKeyFingerprint.Of(third));
    }
}
