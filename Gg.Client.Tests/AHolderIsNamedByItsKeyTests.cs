using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Each holder of a sealed credential is named by the person or machine whose key
/// it is, joined on the key itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.3-01, and it is the column the pane is worth building for.</b> A sealed
/// envelope names its holders as SubjectPublicKeyInfo in base64 — 120 characters
/// that mean nothing to anybody. Joined against the keys this tenant's people have
/// registered, against this machine's own key, and against the runner keys this
/// console has pinned, the same list reads <i>"you and vmlinux002"</i>. That join
/// is performed nowhere today.
/// </para>
/// <para>
/// <b>Joined on the KEY, never on a recomputed fingerprint</b> (slice sixty,
/// rule 5). <see cref="PrincipalKeyFingerprint.Of"/> is derived once on the
/// contract for a stated reason, and matching on a value this side computed would
/// be a second derivation of the thing that decides who can read a secret. The
/// fingerprint is what a person reads; the key is what the match is made on.
/// </para>
/// <para>
/// <b>Nothing here opens anything.</b> A holder is a public key, so a refusal may
/// name the holders an envelope is for — the contract says so in those words — and
/// working out who can open a credential never means opening it.
/// </para>
/// </remarks>
public class AHolderIsNamedByItsKeyTests
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

    private static PrincipalKeySummary APersonsKey(
        string principal, string publicKey, DateTimeOffset? retiredAt = null) => new()
    {
        KeyId = Guid.NewGuid().ToString(),
        Principal = principal,
        PublicKey = publicKey,
        Fingerprint = PrincipalKeyFingerprint.Of(publicKey),
        RegisteredAt = DateTimeOffset.UnixEpoch,
        RetiredAt = retiredAt,
    };

    [Test]
    public async Task A_persons_key_is_named_for_the_person()
    {
        var ada = AKey();

        var holders = CredentialHolders.Of(
            [ada], [APersonsKey("ada", ada)], thisMachine: null, pinned: []);

        var holder = holders.Single();

        await Assert.That(holder.Named).IsEqualTo("ada");
        await Assert.That(holder.Kind).IsEqualTo(CredentialHolderKinds.Person);
        await Assert.That(holder.Fingerprint).IsEqualTo(PrincipalKeyFingerprint.Of(ada));
    }

    [Test]
    public async Task This_machines_own_key_is_named_as_this_machine()
    {
        // THE ROW A PERSON READS FIRST: can I open this one. Naming it by a
        // fingerprint would make them compare two strings to find out.
        var mine = AKey();

        var holders = CredentialHolders.Of([mine], [], thisMachine: mine, pinned: []);

        await Assert.That(holders.Single().Kind).IsEqualTo(CredentialHolderKinds.ThisMachine);
    }

    [Test]
    public async Task A_pinned_runners_key_is_named_as_that_runner()
    {
        // THE SECOND HOLDER OF EVERY PUSHED CREDENTIAL. A push rewraps to the
        // runner's identity key, which this console has already pinned - so
        // without this the list after a successful push reads "you and somebody".
        var runner = AKey();

        var holders = CredentialHolders.Of(
            [runner],
            [],
            thisMachine: null,
            pinned: [new PinnedKey
            {
                RunnerId = "01a0bca3-b788-72e5-b40a-be3811653226",
                PublicKey = runner,
                PinnedAt = DateTimeOffset.UnixEpoch,
            }]);

        var holder = holders.Single();

        await Assert.That(holder.Kind).IsEqualTo(CredentialHolderKinds.Runner);
        await Assert.That(holder.Named).IsEqualTo("01a0bca3-b788-72e5-b40a-be3811653226");
    }

    [Test]
    public async Task This_machine_wins_over_a_pin_of_the_same_key()
    {
        // A MACHINE THAT IS ALSO A RUNNER. This Mac runs a runner, so its own key
        // can be in its own pin file - and "this machine" is the more useful of
        // two true answers, because it tells the reader they can open it.
        var mine = AKey();

        var holders = CredentialHolders.Of(
            [mine],
            [],
            thisMachine: mine,
            pinned: [new PinnedKey
            {
                RunnerId = "a-runner-on-this-mac",
                PublicKey = mine,
                PinnedAt = DateTimeOffset.UnixEpoch,
            }]);

        await Assert.That(holders.Single().Kind).IsEqualTo(CredentialHolderKinds.ThisMachine);
    }

    [Test]
    public async Task Every_holder_of_a_real_envelope_is_accounted_for()
    {
        // END TO END OVER A REAL SEAL, because the whole column rests on the
        // envelope's holder list being the same spelling of a key as the one a
        // person registers. Both are documented as SubjectPublicKeyInfo base64,
        // and nothing has ever joined them - this is first contact.
        var ada = System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var adaKey = Convert.ToBase64String(ada.PublicKey.ExportSubjectPublicKeyInfo());
        var runner = AKey();

        var envelope = CredentialSeal.Seal("a-secret", [adaKey, runner]);

        var holders = CredentialHolders.Of(
            [.. envelope.Wrapped.Select(w => w.Holder)],
            [APersonsKey("ada", adaKey)],
            thisMachine: null,
            pinned: [new PinnedKey
            {
                RunnerId = "vmlinux002",
                PublicKey = runner,
                PinnedAt = DateTimeOffset.UnixEpoch,
            }]);

        await Assert.That(holders.Count).IsEqualTo(2);
        await Assert.That(holders.Select(h => h.Named)).Contains("ada");
        await Assert.That(holders.Select(h => h.Named)).Contains("vmlinux002");
        await Assert.That(holders.Any(h => h.Kind == CredentialHolderKinds.Unknown)).IsFalse()
            .Because("a seal's holder list and a registered key are the same spelling of the same "
                   + "key, and if they are not then 'who can open this' is unanswerable.");
    }

    [Test]
    public async Task The_match_is_on_the_key_and_not_on_a_fingerprint()
    {
        // RULE 5, ASSERTED. A key whose registered fingerprint has been tampered
        // with must still match - because the key is the truth and a fingerprint
        // is a reading of it. If this ever fails, something started matching on
        // the short string, which is a second derivation of who may read a secret.
        var ada = AKey();

        var holders = CredentialHolders.Of(
            [ada],
            [APersonsKey("ada", ada) with { Fingerprint = "0000000000000000deadbeefdeadbeef" }],
            thisMachine: null,
            pinned: []);

        await Assert.That(holders.Single().Named).IsEqualTo("ada");

        await Assert.That(holders.Single().Fingerprint).IsEqualTo(PrincipalKeyFingerprint.Of(ada))
            .Because("and what is shown is derived from the key here, not copied from whatever the "
                   + "control plane stored beside it.");
    }
}
