using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A retired key is still named as a holder, because a credential sealed to it is
/// still sealed to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.3-03.</b> The contract already argues this from the other end:
/// <i>"Retired rather than deleted. A credential sealed to a key last year is
/// still sealed to it, so a row that vanished would leave an envelope naming a
/// holder nobody can account for — and 'who could open this' is exactly the
/// question a retired key is asked."</i> Registering a new key retires the old
/// one and changes nothing about any envelope already written.
/// </para>
/// <para>
/// <b>Named AND marked, because either alone misleads.</b> Dropping it makes the
/// holder unexplainable; showing it as live says somebody can open the credential
/// with a key they have replaced. The row is the honest middle: this person could
/// open it, with a key that is no longer one to seal to.
/// </para>
/// <para>
/// <b>Which is also the argument for rotation being incomplete.</b> gg does not
/// rewrap on retirement — nothing does — so a retired holder stays a holder until
/// somebody reseals. That is a real property of the design and this column is the
/// first place it is visible.
/// </para>
/// </remarks>
public class ARetiredKeyIsStillAHolderTests
{
    private static readonly DateTimeOffset Retired = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);

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
    public async Task It_is_named_for_its_person()
    {
        var old = AKey();

        var holders = CredentialHolders.Of(
            [old], [APersonsKey("ada", old, retiredAt: Retired)], thisMachine: null, pinned: []);

        await Assert.That(holders.Single().Named).IsEqualTo("ada");
        await Assert.That(holders.Single().Kind).IsEqualTo(CredentialHolderKinds.Person);
    }

    [Test]
    public async Task And_it_says_when_it_was_retired()
    {
        var old = AKey();

        var holders = CredentialHolders.Of(
            [old], [APersonsKey("ada", old, retiredAt: Retired)], thisMachine: null, pinned: []);

        await Assert.That(holders.Single().RetiredAt).IsEqualTo(Retired)
            .Because("showing a retired holder as live says somebody can open this with a key "
                   + "they have replaced, which is the opposite error from dropping it.");
    }

    [Test]
    public async Task A_live_key_carries_no_retirement()
    {
        var live = AKey();

        var holders = CredentialHolders.Of(
            [live], [APersonsKey("ada", live)], thisMachine: null, pinned: []);

        await Assert.That(holders.Single().RetiredAt).IsNull();
    }

    [Test]
    public async Task A_person_who_rotated_appears_twice_if_the_envelope_names_both()
    {
        // ROTATION DOES NOT REWRAP, and this is where that becomes visible. The
        // old key stays a holder of every envelope written before the new one; a
        // list showing only the live key would say the rotation was complete.
        var old = AKey();
        var current = AKey();

        var holders = CredentialHolders.Of(
            [old, current],
            [APersonsKey("ada", old, retiredAt: Retired), APersonsKey("ada", current)],
            thisMachine: null,
            pinned: []);

        await Assert.That(holders.Count).IsEqualTo(2);
        await Assert.That(holders.Count(h => h.Named == "ada")).IsEqualTo(2);
        await Assert.That(holders.Count(h => h.RetiredAt is not null)).IsEqualTo(1)
            .Because("one of ada's two keys has been replaced and the credential is sealed to "
                   + "both, which is exactly the state a rotation leaves behind until somebody "
                   + "reseals.");
    }

    [Test]
    public async Task A_retired_key_this_machine_still_holds_is_said_to_be_this_machine()
    {
        // BECAUSE IT CAN STILL OPEN IT. Which key the control plane considers
        // current has nothing to do with what is on this disk.
        var mine = AKey();

        var holders = CredentialHolders.Of(
            [mine],
            [APersonsKey("ada", mine, retiredAt: Retired)],
            thisMachine: mine,
            pinned: []);

        await Assert.That(holders.Single().Kind).IsEqualTo(CredentialHolderKinds.ThisMachine);
        await Assert.That(holders.Single().RetiredAt).IsEqualTo(Retired)
            .Because("both facts are true and the reader needs both: this machine can open it, "
                   + "and the key it opens with is no longer one to seal to.");
    }
}
