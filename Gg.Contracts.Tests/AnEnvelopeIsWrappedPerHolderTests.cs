using System.Security.Cryptography;

namespace Gg.Contracts.Tests;

/// <summary>
/// The value is encrypted once and its content key is wrapped once per holder,
/// and an envelope that is not sealed to this machine says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0037 Decision 1.</b> A random content key encrypts the value once;
/// the content key is wrapped once per holder. That is what makes adding a
/// holder a rewrap rather than a re-encrypt, and it is what lets a push rewrap
/// a key without ever decrypting the credential (Decision 3).
/// </para>
/// <para>
/// <b>Not sealed to you is not corrupt, and the difference is the whole test.</b>
/// Both end with a credential that will not open, and they send a person to
/// opposite places: one to ask somebody to push it, the other to suspect a bad
/// disk. Collapsing them is how a routine "this machine was never given that
/// credential" becomes an afternoon spent on a file that was always fine.
/// </para>
/// <para>
/// <b>A holder is named by its public key, which is not a secret.</b> So the
/// refusal may say which holders an envelope IS for — that is the fact a person
/// needs to work out who can push it to them — while rule 8 still forbids
/// repeating the wrapped bytes.
/// </para>
/// </remarks>
public class AnEnvelopeIsWrappedPerHolderTests
{
    private static ECDiffieHellman AKey() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

    private static string PublicHalf(ECDiffieHellman key) =>
        Convert.ToBase64String(key.PublicKey.ExportSubjectPublicKeyInfo());

    [Test]
    public async Task One_holder_gets_one_wrapped_key()
    {
        using var holder = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(holder)]);

        await Assert.That(envelope.Wrapped.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Two_holders_get_one_wrapped_key_each()
    {
        using var first = AKey();
        using var second = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(first), PublicHalf(second)]);

        await Assert.That(envelope.Wrapped.Count).IsEqualTo(2)
            .Because("one per holder is the rule the whole rewrap depends on.");
    }

    [Test]
    public async Task The_body_is_encrypted_once_however_many_holders_there_are()
    {
        using var first = AKey();
        using var second = AKey();

        var one = CredentialSeal.Seal("a-token", [PublicHalf(first)]);
        var two = CredentialSeal.Seal("a-token", [PublicHalf(first), PublicHalf(second)]);

        // A SINGLE CIPHERTEXT, not one per holder. Its LENGTH is what says so
        // without pinning the nonce: encrypting the value twice would grow with
        // the holder list, and wrapping a key twice does not.
        await Assert.That(two.Ciphertext.Length).IsEqualTo(one.Ciphertext.Length);
    }

    [Test]
    public async Task Each_holder_opens_the_same_value()
    {
        using var first = AKey();
        using var second = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(first), PublicHalf(second)]);

        await Assert.That(CredentialSeal.Open(envelope, first)).IsEqualTo("a-token");
        await Assert.That(CredentialSeal.Open(envelope, second)).IsEqualTo("a-token");
    }

    [Test]
    public async Task A_holder_the_envelope_is_not_sealed_to_finds_no_wrapped_key()
    {
        using var sealedTo = AKey();
        using var stranger = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(sealedTo)]);

        await Assert.That(SealedCredential.WrappedFor(envelope, PublicHalf(stranger))).IsNull();
    }

    [Test]
    public async Task A_holder_it_is_sealed_to_finds_one()
    {
        using var holder = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(holder)]);

        await Assert.That(SealedCredential.WrappedFor(envelope, PublicHalf(holder))).IsNotNull();
    }

    [Test]
    public async Task Not_sealed_to_this_machine_is_refused_as_that_rather_than_as_corrupt()
    {
        using var sealedTo = AKey();
        using var stranger = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(sealedTo)]);
        var said = CredentialSeal.SaidWhenNoHolder(envelope, PublicHalf(stranger));

        // THE TWO DIAGNOSES SEND A PERSON TO DIFFERENT PLACES, which is why
        // this asserts what the sentence is NOT as well as what it is.
        await Assert.That(said).DoesNotContain("corrupt");
        await Assert.That(said).DoesNotContain("malformed");
        await Assert.That(said.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task The_no_holder_refusal_never_repeats_the_wrapped_bytes()
    {
        using var sealedTo = AKey();
        using var stranger = AKey();

        var envelope = CredentialSeal.Seal("a-token", [PublicHalf(sealedTo)]);
        var said = CredentialSeal.SaidWhenNoHolder(envelope, PublicHalf(stranger));

        foreach (var wrapped in envelope.Wrapped)
        {
            await Assert.That(said).DoesNotContain(wrapped.Wrapped)
                .Because("rule 8: a refusal names the place, never the bytes.");
        }

        await Assert.That(said).DoesNotContain(envelope.Ciphertext);
    }

    [Test]
    public async Task The_same_holder_twice_is_refused_rather_than_wrapped_twice()
    {
        using var holder = AKey();
        var twice = PublicHalf(holder);

        // ONCE PER HOLDER IS THE CRITERION, so two entries for one holder is a
        // contradiction rather than a redundancy - and the lookup would have to
        // pick between them, which is a decision nothing should be making.
        await Assert.That(() => CredentialSeal.Seal("a-token", [twice, twice]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Sealing_to_nobody_is_refused()
    {
        // An envelope nobody can open is not a credential, and producing one
        // quietly is how a push reports success at delivering nothing.
        await Assert.That(() => CredentialSeal.Seal("a-token", []))
            .Throws<ArgumentException>();
    }
}
