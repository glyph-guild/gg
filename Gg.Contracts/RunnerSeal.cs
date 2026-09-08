using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Gg.Contracts;

/// <summary>
/// Sealing what passes between a console and a runner, so nothing carrying it can read it.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the CONTRACT, for <see cref="ControlText"/>'s reason.</b> Both ends
/// have to seal and open by one rule rather than two that agree today — a
/// console that framed its offer differently from the runner that opens it
/// fails at the only moment anybody would find out, which is in front of a
/// person waiting to see a log.
/// </para>
/// <para>
/// <b>One ECDH, two directions, two keys.</b> The console's ephemeral private
/// half and the runner's registered public half agree a secret; so do the
/// runner's private half and the console's ephemeral public half. It is the same
/// secret, so it is derived twice with different labels — an offer opened with
/// the answer's key would be a bug that only showed up under a replay, and
/// separate labels make it impossible instead of unlikely.
/// </para>
/// <para>
/// <b>The ephemeral public key travels in the clear, framed ahead of the
/// ciphertext.</b> The runner cannot do the exchange without it, and the
/// capability binds only its HASH — so it has to arrive beside the sealed body
/// rather than inside it. Nothing is given away: it is single-use, it is public,
/// and the control plane already holds its hash from the introduction it minted.
/// </para>
/// <para>
/// <b>AES-GCM, so a relay that flips a byte is caught rather than obeyed.</b>
/// What is inside is SDP and ICE candidates, which both ends act on; an
/// unauthenticated cipher would let whatever carries this redirect a connection
/// without being able to read it.
/// </para>
/// </remarks>
public static class RunnerSeal
{
    /// <summary>What the offer's key is derived for.</summary>
    private const string OfferLabel = "gg-runner-introduction-offer";

    /// <summary>What the answer's key is derived for.</summary>
    private const string AnswerLabel = "gg-runner-introduction-answer";

    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    /// <summary>
    /// Seals an offer to a runner's registered key.
    /// </summary>
    /// <param name="runnerPublicKey">
    /// SubjectPublicKeyInfo, base64 — what the runner registered and the console
    /// pinned.
    /// </param>
    /// <param name="ephemeral">The console's key for this introduction and no other.</param>
    /// <param name="offer">What is being said. Never leaves this method readable.</param>
    public static byte[] SealOffer(string runnerPublicKey, ECDiffieHellman ephemeral, byte[] offer)
    {
        ArgumentNullException.ThrowIfNull(ephemeral);

        return Seal(
            Agree(ephemeral, runnerPublicKey, OfferLabel),
            ephemeral.PublicKey.ExportSubjectPublicKeyInfo(),
            offer);
    }

    /// <summary>Opens an offer sealed to this runner's key.</summary>
    public static byte[] OpenOffer(ECDiffieHellman runnerKey, byte[] sealedOffer)
    {
        ArgumentNullException.ThrowIfNull(runnerKey);

        return Open(sealedOffer, ephemeral => Agree(runnerKey, ephemeral, OfferLabel));
    }

    /// <summary>
    /// Seals an answer to the console's ephemeral key.
    /// </summary>
    /// <remarks>
    /// <b>The runner's own public key is framed here too</b>, so the console
    /// opens the answer the same way the runner opened the offer. It already has
    /// that key — it pinned it — and re-deriving from what arrived would mean
    /// trusting the frame over the pin, which is the substitution the pin exists
    /// to catch. So the console passes what it pinned and this is symmetry
    /// rather than a second source of truth.
    /// </remarks>
    public static byte[] SealAnswer(
        string ephemeralPublicKey, ECDiffieHellman runnerKey, byte[] answer)
    {
        ArgumentNullException.ThrowIfNull(runnerKey);

        return Seal(
            Agree(runnerKey, ephemeralPublicKey, AnswerLabel),
            runnerKey.PublicKey.ExportSubjectPublicKeyInfo(),
            answer);
    }

    /// <summary>
    /// Opens an answer, using the runner key this console PINNED.
    /// </summary>
    /// <remarks>
    /// <b>The pinned key, not the one in the frame.</b> Opening with whatever
    /// arrived would authenticate the message against itself. Passing the pin in
    /// is what makes a substituted answer fail to open at all.
    /// </remarks>
    public static byte[] OpenAnswer(
        ECDiffieHellman ephemeral, string pinnedRunnerPublicKey, byte[] sealedAnswer)
    {
        ArgumentNullException.ThrowIfNull(ephemeral);

        return Open(sealedAnswer, _ => Agree(ephemeral, pinnedRunnerPublicKey, AnswerLabel));
    }

    private static byte[] Agree(ECDiffieHellman ours, string theirs, string label) =>
        Agree(ours, Convert.FromBase64String(theirs), label);

    private static byte[] Agree(ECDiffieHellman ours, byte[] theirs, string label)
    {
        using var peer = ECDiffieHellman.Create();
        peer.ImportSubjectPublicKeyInfo(theirs, out _);

        // HKDF WITH A LABEL RATHER THAN THE RAW SECRET. Two directions share one
        // agreement, and using it directly would make the offer's key and the
        // answer's key the same - so a replayed offer would open as an answer.
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ours.DeriveRawSecretAgreement(peer.PublicKey),
            KeyBytes,
            info: System.Text.Encoding.UTF8.GetBytes(label));
    }

    private static byte[] Seal(byte[] key, byte[] framedPublicKey, byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];

        using (var gcm = new AesGcm(key, TagBytes))
        {
            gcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // LENGTH-PREFIXED, so opening is not a guess about where a key ends. A
        // fixed offset would work until a curve changed and would then fail as a
        // decryption error rather than as a framing one.
        var sealedBytes = new byte[4 + framedPublicKey.Length + NonceBytes + TagBytes + ciphertext.Length];
        var at = sealedBytes.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(at, framedPublicKey.Length);
        at = at[4..];
        framedPublicKey.CopyTo(at);
        at = at[framedPublicKey.Length..];
        nonce.CopyTo(at);
        at = at[NonceBytes..];
        tag.CopyTo(at);
        at = at[TagBytes..];
        ciphertext.CopyTo(at);

        return sealedBytes;
    }

    private static byte[] Open(byte[] sealedBytes, Func<byte[], byte[]> keyFor)
    {
        ArgumentNullException.ThrowIfNull(sealedBytes);
        ArgumentNullException.ThrowIfNull(keyFor);

        if (sealedBytes.Length < 4)
        {
            throw new CryptographicException(
                "A sealed message shorter than its own length prefix cannot be opened. "
              + "Something truncated it.");
        }

        var keyLength = BinaryPrimitives.ReadInt32BigEndian(sealedBytes);

        if (keyLength <= 0 || sealedBytes.Length < 4 + keyLength + NonceBytes + TagBytes)
        {
            // LOUD AND SPECIFIC, because this is what a relay that rewrote the
            // frame looks like - and a person needs to be able to tell it from a
            // key that simply does not match.
            throw new CryptographicException(
                "A sealed message's frame does not describe its own contents. It was rewritten "
              + "or truncated in transit.");
        }

        var at = sealedBytes.AsSpan(4);
        var framedPublicKey = at[..keyLength].ToArray();
        at = at[keyLength..];
        var nonce = at[..NonceBytes];
        at = at[NonceBytes..];
        var tag = at[..TagBytes];
        var ciphertext = at[TagBytes..];

        var plaintext = new byte[ciphertext.Length];

        using var gcm = new AesGcm(keyFor(framedPublicKey), TagBytes);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
