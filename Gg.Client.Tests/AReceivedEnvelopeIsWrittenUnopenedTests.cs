using System.Security.Cryptography;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A machine handed a sealed credential writes it as it arrived, and opens it
/// only when something needs the value.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writing it unopened is what makes the push cost nothing in plaintext at
/// either end.</b> The sender rewrapped without decrypting; a receiver that
/// opened the envelope in order to re-seal it under its own store would undo
/// that on arrival, and the credential would exist in the clear on a machine
/// nobody is watching, for no reason except the shape of an API.
/// </para>
/// <para>
/// <b>It is also the only way the push can reach a machine that is not the
/// holder yet.</b> A store that re-sealed on receipt would need the value;
/// taking the envelope as given needs only the bytes.
/// </para>
/// <para>
/// <b>And the written envelope must be the one that arrived, byte for
/// byte.</b> Re-serialising it with a fresh content key would be the same
/// decrypt-and-reseal wearing a different hat — so the test pins the ciphertext
/// through the whole journey.
/// </para>
/// </remarks>
public class AReceivedEnvelopeIsWrittenUnopenedTests
{
    private const string Locator = "local:acme/widgets";
    private const string Value = "ghp-a-token-nobody-should-find";

    private static string ATempDirectory() =>
        Path.Combine(Path.GetTempPath(), "gg-received-" + Guid.NewGuid().ToString("N"));

    /// <summary>A machine with a key and an empty store, as a fresh runner is.</summary>
    private static (FileCredentialStore Store, MachineKey Key) AMachine()
    {
        var key = MachineKey.LoadOrCreate(Path.Combine(ATempDirectory(), "k"));
        return (new FileCredentialStore(ATempDirectory(), key), key);
    }

    [Test]
    public async Task A_pushed_envelope_is_written_and_opens_on_the_recipient()
    {
        var (sender, senderKey) = AMachine();
        var (recipient, recipientKey) = AMachine();

        sender.Write(Locator, Value);

        var pushed = CredentialSeal.Rewrap(
            sender.SealedFor(Locator), senderKey, recipientKey.PublicKey);

        recipient.WriteSealed(Locator, pushed);

        await Assert.That(recipient.Read(Locator)).IsEqualTo(Value);
    }

    [Test]
    public async Task What_is_written_is_what_arrived()
    {
        var (sender, senderKey) = AMachine();
        var (recipient, recipientKey) = AMachine();

        sender.Write(Locator, Value);

        var pushed = CredentialSeal.Rewrap(
            sender.SealedFor(Locator), senderKey, recipientKey.PublicKey);

        recipient.WriteSealed(Locator, pushed);

        // BYTE FOR BYTE THROUGH THE WHOLE JOURNEY. A recipient that opened the
        // envelope and re-sealed it under its own key would round-trip and hold
        // the plaintext on the way, which is the defect this whole step exists
        // to make impossible.
        await Assert.That(recipient.SealedFor(Locator).Ciphertext).IsEqualTo(pushed.Ciphertext);
    }

    [Test]
    public async Task The_recipient_never_needed_the_value_to_store_it()
    {
        // THE PROPERTY THAT MAKES A PUSH POSSIBLE AT ALL. The sender's key
        // cannot open the recipient's store and vice versa; if storing required
        // the value, this sequence could not be performed without one of them
        // decrypting.
        var (sender, senderKey) = AMachine();
        var (recipient, recipientKey) = AMachine();

        sender.Write(Locator, Value);

        var pushed = CredentialSeal.Rewrap(
            sender.SealedFor(Locator), senderKey, recipientKey.PublicKey);

        recipient.WriteSealed(Locator, pushed);

        await Assert.That(recipient.Holds(Locator)).IsTrue();
        await Assert.That(recipient.ProtectionFor(Locator)).Contains("sealed");
    }

    [Test]
    public async Task An_envelope_this_machine_cannot_open_is_still_written()
    {
        // A MACHINE MAY BE HANDED SOMETHING MEANT FOR ANOTHER, and refusing to
        // write it would be the store deciding what it is allowed to hold. It
        // takes the bytes; Read is where rule 9's diagnosis belongs, and the
        // refusal there already names the locator and never the bytes.
        var (sender, senderKey) = AMachine();
        var (recipient, _) = AMachine();
        using var somebodyElse = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        sender.Write(Locator, Value);

        var elsewhere = CredentialSeal.Rewrap(
            sender.SealedFor(Locator),
            senderKey,
            Convert.ToBase64String(somebodyElse.PublicKey.ExportSubjectPublicKeyInfo()));

        recipient.WriteSealed(Locator, elsewhere);

        await Assert.That(recipient.Holds(Locator)).IsTrue();
        await Assert.That(() => recipient.Read(Locator)).Throws<CredentialUnavailableException>();
    }

    [Test]
    public async Task Writing_an_envelope_removes_a_plaintext_that_was_there()
    {
        var (recipient, recipientKey) = AMachine();
        var (sender, senderKey) = AMachine();

        var plain = recipient.PathFor(Locator);
        Directory.CreateDirectory(Path.GetDirectoryName(plain)!);
        File.WriteAllText(plain, "ghp-an-older-token");

        sender.Write(Locator, Value);
        recipient.WriteSealed(
            Locator,
            CredentialSeal.Rewrap(
                sender.SealedFor(Locator),
                senderKey,
                recipientKey.PublicKey));

        await Assert.That(File.Exists(plain)).IsFalse()
            .Because("a push that left the credential it replaced readable on disk would be the "
                   + "same leak Write already closes, arriving through a different door.");
    }

    [Test]
    public async Task Reading_a_pushed_envelope_does_not_rewrite_it()
    {
        // RESEAL-ON-READ IS FOR PLAINTEXT, and a pushed envelope is not that. A
        // store that resealed what it was handed would break the byte-identity
        // the push depends on, one read later.
        var (sender, senderKey) = AMachine();
        var (recipient, recipientKey) = AMachine();

        sender.Write(Locator, Value);

        recipient.WriteSealed(
            Locator,
            CredentialSeal.Rewrap(
                sender.SealedFor(Locator),
                senderKey,
                recipientKey.PublicKey));

        var before = File.ReadAllText(recipient.SealedPathFor(Locator));
        recipient.Read(Locator);

        await Assert.That(File.ReadAllText(recipient.SealedPathFor(Locator))).IsEqualTo(before);
    }
}
