using System.Security.Cryptography;

namespace Gg.Contracts.Tests;

/// <summary>
/// Handing a credential on rewraps its content key. The body is never opened
/// and never re-encrypted.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0037 Decision 3, and the difference between a design and a
/// slogan.</b> A push unwraps the CONTENT KEY — thirty-two bytes — and wraps it
/// to the recipient. It does not decrypt the credential, so the machine doing
/// the pushing never holds the value at all, and the plaintext exists in only
/// two moments of its life: when a person first seals it, and when the machine
/// it reached actually uses it.
/// </para>
/// <para>
/// <b>The body being BYTE-IDENTICAL is the assertion that proves it.</b> An
/// implementation that opened the credential and sealed it again would round
/// trip perfectly, satisfy every other test here, and quietly hold the
/// plaintext in the middle. A fresh nonce on the body is what that looks like
/// from outside, so the test pins the ciphertext.
/// </para>
/// <para>
/// <b>And rewrapping adds a holder rather than replacing one</b>, which is what
/// makes Decision 8's second holder a rewrap by somebody who already holds it
/// rather than a re-seal from plaintext nobody has.
/// </para>
/// </remarks>
public class APushRewrapsRatherThanDecryptsTests
{
    private const string Value = "ghp-a-token-nobody-should-find";

    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private static string PublicHalf(ECDiffieHellman key) =>
        Convert.ToBase64String(key.PublicKey.ExportSubjectPublicKeyInfo());

    [Test]
    public async Task The_recipient_opens_what_was_rewrapped_to_them()
    {
        using var mine = AKey();
        using var theirs = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);
        var rewrapped = CredentialSeal.Rewrap(envelope, mine, PublicHalf(theirs));

        await Assert.That(CredentialSeal.Open(rewrapped, theirs)).IsEqualTo(Value);
    }

    [Test]
    public async Task The_body_is_byte_identical()
    {
        using var mine = AKey();
        using var theirs = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);
        var rewrapped = CredentialSeal.Rewrap(envelope, mine, PublicHalf(theirs));

        // THE ASSERTION THE WHOLE DECISION RESTS ON. Re-encrypting would mint a
        // new nonce, so an identical ciphertext is proof the value was never
        // decrypted on the way through.
        await Assert.That(rewrapped.Ciphertext).IsEqualTo(envelope.Ciphertext);
    }

    [Test]
    public async Task The_sender_can_still_open_it_afterwards()
    {
        using var mine = AKey();
        using var theirs = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);
        var rewrapped = CredentialSeal.Rewrap(envelope, mine, PublicHalf(theirs));

        // ADDS A HOLDER RATHER THAN MOVING ONE. A push must not cost the pusher
        // its own access, or sending a credential to a runner would take it away
        // from the laptop that sent it.
        await Assert.That(CredentialSeal.Open(rewrapped, mine)).IsEqualTo(Value);
        await Assert.That(rewrapped.Wrapped.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_third_party_opens_neither()
    {
        using var mine = AKey();
        using var theirs = AKey();
        using var stranger = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);
        var rewrapped = CredentialSeal.Rewrap(envelope, mine, PublicHalf(theirs));

        await Assert.That(() => CredentialSeal.Open(rewrapped, stranger))
            .Throws<CryptographicException>();
    }

    [Test]
    public async Task Rewrapping_with_a_key_that_cannot_open_it_is_refused()
    {
        // YOU CANNOT PASS ON WHAT YOU CANNOT OPEN. A rewrap that silently
        // produced an envelope from a key it could not unwrap would hand the
        // recipient something that fails much later, on their machine, with a
        // diagnosis pointing at them.
        using var mine = AKey();
        using var stranger = AKey();
        using var theirs = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);

        await Assert.That(() => CredentialSeal.Rewrap(envelope, stranger, PublicHalf(theirs)))
            .Throws<CryptographicException>();
    }

    [Test]
    public async Task Rewrapping_to_a_holder_it_already_has_is_refused()
    {
        // ONE WRAPPED KEY PER HOLDER remains the rule after a rewrap, or
        // WrappedFor is choosing between two entries again.
        using var mine = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);

        await Assert.That(() => CredentialSeal.Rewrap(envelope, mine, PublicHalf(mine)))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task The_version_is_carried_rather_than_reset()
    {
        using var mine = AKey();
        using var theirs = AKey();

        var envelope = CredentialSeal.Seal(Value, [PublicHalf(mine)]);
        var rewrapped = CredentialSeal.Rewrap(envelope, mine, PublicHalf(theirs));

        await Assert.That(rewrapped.Version).IsEqualTo(envelope.Version)
            .Because("the body was sealed under the old version and is unchanged, so saying "
                   + "anything else would describe bytes that are not there.");
    }
}
