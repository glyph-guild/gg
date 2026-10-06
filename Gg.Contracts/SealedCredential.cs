using System.Security.Cryptography;
using System.Text;

namespace Gg.Contracts;

/// <summary>
/// Which versions of the sealed-credential format this build can open.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared before there is a second one.</b> A format that cannot say which
/// version it is, is one that can never be fixed: every later change would have
/// to be inferred from the bytes, and an inference that is wrong decrypts
/// garbage rather than refusing. One member today, and the mechanism that lets
/// there be a second.
/// </para>
/// <para>
/// <b>A list rather than a ceiling.</b> "Anything up to N" assumes every older
/// version stays openable forever; a list lets one be retired by removing it,
/// and the refusal then names what is still accepted.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class SealedCredentialVersions
{
    /// <summary>What this build seals under.</summary>
    public const int Current = 1;

    /// <summary>Every version this build can open.</summary>
    public static IReadOnlyList<int> All { get; } = [Current];
}

/// <summary>
/// One holder's copy of the content key: who can open it, and the key wrapped
/// to them. Never the key itself.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Holder"/> is a public key, so it is not a secret.</b> It is
/// SubjectPublicKeyInfo in base64 — the same spelling a runner registers and a
/// console pins — which means a refusal may say which holders an envelope is
/// for. That is the fact a person needs in order to work out who can push them
/// a credential, and saying it gives nothing away.
/// </para>
/// <para>
/// <b><see cref="Wrapped"/> is <see cref="RunnerSeal"/>'s frame over
/// thirty-two bytes.</b> The wrap is not new cryptography: sealing a content key
/// to a public key is exactly what sealing an offer to a runner already does, so
/// it reuses that framing rather than inventing a second one that agrees today.
/// </para>
/// </remarks>
[PinnedId("3d863f31-e75c-41b7-9fbd-46917be02553")]
public sealed record WrappedContentKey
{
    /// <summary>The holder's public key, SubjectPublicKeyInfo in base64.</summary>
    public required string Holder { get; init; }

    /// <summary>The content key, sealed to that holder. Base64.</summary>
    public required string Wrapped { get; init; }
}

/// <summary>
/// A credential at rest: encrypted once, and openable by each holder it was
/// wrapped for.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0037 Decision 1.</b> A random content key encrypts the value once and
/// is then wrapped once per holder. Adding a holder is a rewrap of
/// thirty-two bytes rather than a re-encryption of the credential — which is
/// what lets a push hand a credential on without ever decrypting it
/// (Decision 3), and what makes the plaintext exist in only two moments of its
/// life: when a person first seals it, and when the machine it reached uses it.
/// </para>
/// <para>
/// <b>Four members, none of which can hold a value.</b> Asserted over the
/// type's shape by <c>ASealedEnvelopeCarriesNoPlaintextTests</c>, which is
/// <c>CredentialContainmentTests</c>' discipline applied to a type its roots
/// cannot reach until step 5 connects it.
/// </para>
/// <para>
/// <b>Base64 in a <c>string</c>, not <c>byte[]</c>.</b> The credential path's
/// allowed shapes are a closed set and <c>byte[]</c> is not among them.
/// Widening that allowlist is an argued amendment, not a convenience taken
/// while adding a field, so this pays a third more bytes on a wire that carries
/// a token and keeps a guard that works.
/// </para>
/// </remarks>
[PinnedId("abce89c9-145f-419d-9f77-ecf1448b0b7e")]
public sealed record SealedCredential
{
    /// <summary>Which version of this format it was sealed under.</summary>
    public required int Version { get; init; }

    /// <summary>The value, encrypted under the content key. Base64.</summary>
    public required string Ciphertext { get; init; }

    /// <summary>The content key, wrapped once for each holder.</summary>
    public required IReadOnlyList<WrappedContentKey> Wrapped { get; init; }

    /// <summary>
    /// The diagnosis, or null when there is nothing wrong with the envelope
    /// itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A sentence rather than a bool</b>, for <see cref="CredentialReference.Validate"/>'s
    /// reason: Article XI asks for a diagnosis, and a credential that will not
    /// open is exactly the moment somebody needs one.
    /// </para>
    /// <para>
    /// <b>It never repeats the envelope.</b> A refusal here is reached holding
    /// the whole thing, and an error that helpfully echoed what it could not
    /// open would print ciphertext into a console, a flight log and a shell
    /// history at once. The version is not secret and is named; nothing else is.
    /// </para>
    /// </remarks>
    public static string? Validate(SealedCredential envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!SealedCredentialVersions.All.Contains(envelope.Version))
        {
            // ZERO IS WHAT AN UNSET INT LOOKS LIKE, and it lands here rather
            // than in a branch of its own: a missing member and a version from
            // the future are the same problem to whoever is holding it, and
            // both are fixed by a gg that knows the format.
            return $"This credential is sealed under version {envelope.Version}, and this gg can "
                 + $"open {string.Join(", ", SealedCredentialVersions.All)}. Update gg on this "
                 + "machine, or reseal it where it was written.";
        }

        if (string.IsNullOrEmpty(envelope.Ciphertext))
        {
            return "This credential is sealed under a version this gg knows and carries nothing "
                 + "to open. It was written wrong rather than written by somebody else.";
        }

        return envelope.Wrapped.Count == 0
            ? "This credential is sealed to nobody, so nothing can open it. Seal it again naming "
            + "at least one holder."
            : null;
    }

    /// <summary>
    /// This holder's wrapped content key, or null when the envelope was not
    /// sealed to them.
    /// </summary>
    /// <remarks>
    /// <b>NULL IS AN ANSWER, not a failure.</b> "This machine was never given
    /// that credential" is ordinary and is fixed by somebody pushing it; it must
    /// not arrive looking like a damaged file. <see cref="CredentialSeal.SaidWhenNoHolder"/>
    /// is the sentence for it.
    /// </remarks>
    public static WrappedContentKey? WrappedFor(SealedCredential envelope, string holder)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Wrapped.FirstOrDefault(
            w => string.Equals(w.Holder, holder, StringComparison.Ordinal));
    }
}

/// <summary>
/// Sealing a credential to its holders, and opening one as a holder.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the contract, for <see cref="RunnerSeal"/>'s reason.</b> Whatever
/// seals and whatever opens have to agree by one rule rather than two that
/// agree today — and here the two ends are separated by more than a network:
/// they are separated by time, because an envelope written this year is opened
/// by whatever gg is installed when somebody needs it.
/// </para>
/// <para>
/// <b>It introduces no cryptography.</b> The body is AES-GCM under a random
/// content key, and each wrap is <see cref="RunnerSeal"/>'s existing frame over
/// that key — ECDH on P-256, HKDF-SHA256 with a label, AES-GCM. The only new
/// thing is the label, which keeps a wrapped content key from being openable as
/// an introduction offer and the other way round.
/// </para>
/// </remarks>
public static class CredentialSeal
{
    /// <summary>What a wrapped content key's key is derived for.</summary>
    /// <remarks>
    /// Its own label, so a wrap cannot be opened as an introduction and an
    /// introduction cannot be opened as a wrap. The same reasoning that gives
    /// an offer and an answer different labels, one mechanism over.
    /// </remarks>
    private const string WrapLabel = "gg-credential-content-key";

    /// <summary>
    /// Seals a value to every holder named, once each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A duplicate holder throws rather than being tolerated.</b> Two entries
    /// for one holder would leave <see cref="SealedCredential.WrappedFor"/>
    /// choosing between them, and nothing in this design should be making that
    /// choice.
    /// </para>
    /// <para>
    /// <b>No holders throws too.</b> An envelope nobody can open is not a
    /// credential, and producing one quietly is how a push comes to report
    /// success at delivering nothing.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>AN EMPTY VALUE SEALS, and that is deliberate.</b> It is well formed —
    /// an empty plaintext is a plaintext — and refusing it here would move a
    /// refusal that already exists and is already better placed. A store that
    /// threw on an empty secret would make `LocalCredentialResolver`'s own arm
    /// unreachable, and that arm is Article XI doing its job: *"an empty secret
    /// is a secret that fetches nothing and fails much later, in a place with no
    /// way back to here."* Sealing is about how a value rests, not about whether
    /// it is a good value.
    /// </remarks>
    public static SealedCredential Seal(string value, IReadOnlyList<string> holders)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(holders);

        if (holders.Count == 0)
        {
            throw new ArgumentException(
                "A credential is sealed to at least one holder; this named none.", nameof(holders));
        }

        if (holders.Distinct(StringComparer.Ordinal).Count() != holders.Count)
        {
            // THE HOLDER IS A PUBLIC KEY, so naming it here is safe - and it is
            // the only thing that tells whoever hit this which one they repeated.
            throw new ArgumentException(
                "A credential is wrapped once per holder, and this named one twice.",
                nameof(holders));
        }

        // ONE CONTENT KEY, ONE ENCRYPTION, however many holders there are.
        var contentKey = RandomNumberGenerator.GetBytes(RunnerSeal.KeyBytes);

        try
        {
            return new SealedCredential
            {
                Version = SealedCredentialVersions.Current,
                Ciphertext = Convert.ToBase64String(
                    RunnerSeal.SealUnder(contentKey, Encoding.UTF8.GetBytes(value))),
                Wrapped =
                [
                    .. holders.Select(holder => new WrappedContentKey
                    {
                        Holder = holder,
                        Wrapped = Convert.ToBase64String(
                            RunnerSeal.SealTo(holder, contentKey, WrapLabel)),
                    }),
                ],
            };
        }
        finally
        {
            // The content key is the only thing here that opens the body, and
            // this method is the one place it exists in the clear.
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    /// <summary>
    /// Adds a holder by rewrapping the content key. The body is not touched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ADR-0037 Decision 3, and the difference between a design and a
    /// slogan.</b> This unwraps thirty-two bytes and wraps them again. It never
    /// decrypts the credential, so the machine performing it never holds the
    /// value — and the plaintext exists in only two moments of a credential's
    /// life: when a person first seals it, and when the machine it reached uses
    /// it.
    /// </para>
    /// <para>
    /// <b>The ciphertext is carried across unchanged</b>, which is what makes
    /// that claim checkable from outside: sealing again would mint a fresh
    /// nonce, so a byte-identical body is proof nothing was opened. The version
    /// is carried for the same reason — the body was sealed under it and is
    /// still those bytes.
    /// </para>
    /// <para>
    /// <b>It ADDS rather than moves.</b> A push must not cost the pusher its own
    /// access, or handing a credential to a runner would take it away from the
    /// laptop that sent it. Decision 8's second holder is this same verb pointed
    /// at a person.
    /// </para>
    /// <para>
    /// <b>It takes an AGREEMENT, not a key, and that is Decision 2 becoming
    /// reachable.</b> While this demanded an <see cref="ECDiffieHellman"/> it
    /// could only be performed by a key in this process's memory, so a person's
    /// key — which never leaves its adapter, because a card-backed one cannot —
    /// was shut out of the one act the ADR says belongs to it, and slice
    /// fifty-nine reached for the machine key instead. The machine still performs
    /// a rewrap: its key wears <see cref="IAgreeAsAHolder"/> too, which is what
    /// makes Decision 5's host-to-member recursion the same verb rather than a
    /// second path.
    /// </para>
    /// </remarks>
    public static SealedCredential Rewrap(
        SealedCredential envelope, IAgreeAsAHolder ours, string holder)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentException.ThrowIfNullOrEmpty(holder);

        if (SealedCredential.WrappedFor(envelope, holder) is not null)
        {
            // ONE WRAPPED KEY PER HOLDER survives a rewrap, or WrappedFor is
            // choosing between two entries again - which is the decision nothing
            // in this design should be making.
            throw new ArgumentException(
                "This credential is already sealed to that holder.", nameof(holder));
        }

        // UNWRAPPED FIRST, so a key that cannot open this refuses HERE rather
        // than producing an envelope that fails later, on the recipient's
        // machine, with a diagnosis pointing at them.
        var contentKey = Opened(envelope, ours);

        try
        {
            return envelope with
            {
                Wrapped =
                [
                    .. envelope.Wrapped,
                    new WrappedContentKey
                    {
                        Holder = holder,
                        Wrapped = Convert.ToBase64String(
                            RunnerSeal.SealTo(holder, contentKey, WrapLabel)),
                    },
                ],
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    /// <summary>
    /// Opens a credential as one of its holders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The holder performs its own agreement</b>, so this is the overload a
    /// person's key reaches — and a card-backed one later. It arrived in step 2,
    /// when a credential started being sealed to a person: before that nothing
    /// but a machine ever opened one, so the machine overload below was the only
    /// one there was.
    /// </para>
    /// <para>
    /// <b>Opening is not the same act as moving</b>, and both exist on purpose. A
    /// person opens a credential to read it; a person REWRAPS one to move it, and
    /// <see cref="Rewrap"/> never decrypts the body. A caller reaching for this
    /// when it meant to push is asking for the plaintext, which is the thing
    /// Decision 3 keeps out of a push path.
    /// </para>
    /// </remarks>
    public static string Open(SealedCredential envelope, IAgreeAsAHolder ours)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(ours);

        if (Validate(envelope) is { } refused)
        {
            throw new CryptographicException(refused);
        }

        var contentKey = Opened(envelope, ours);

        try
        {
            return Encoding.UTF8.GetString(
                RunnerSeal.OpenUnder(contentKey, Convert.FromBase64String(envelope.Ciphertext)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    /// <summary>
    /// Opens a credential with a key this process holds.
    /// </summary>
    /// <remarks>
    /// <b>The MACHINE path, whose key is a file on the machine by necessity.</b> A
    /// person's key is never typed as an <see cref="ECDiffieHellman"/> outside its
    /// own adapter (ADR-0037), because a token-backed key is not one and that is
    /// the line keeping hardware possible. Kept beside the holder overload rather
    /// than folded into it so that a runner opening its own store reads as what it
    /// is.
    /// </remarks>
    public static string Open(SealedCredential envelope, ECDiffieHellman ours)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(ours);

        if (Validate(envelope) is { } refused)
        {
            throw new CryptographicException(refused);
        }

        var contentKey = Opened(envelope, RunnerSeal.AsAHolder(ours));

        try
        {
            return Encoding.UTF8.GetString(
                RunnerSeal.OpenUnder(contentKey, Convert.FromBase64String(envelope.Ciphertext)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentKey);
        }
    }

    /// <summary>
    /// What to say when an envelope is not sealed to this holder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NOT SEALED TO YOU IS NOT CORRUPT, and the difference is the whole
    /// reason this sentence exists.</b> Both end with a credential that will not
    /// open, and they send a person to opposite places: one to ask somebody to
    /// push it, the other to suspect a bad disk. Collapsing them turns a routine
    /// "this machine was never given that" into an afternoon spent on a file
    /// that was always fine.
    /// </para>
    /// <para>
    /// <b>It names the holders and never the bytes.</b> A holder is a public
    /// key, so saying which ones an envelope is for gives nothing away and is
    /// exactly what somebody needs to work out who can push it to them.
    /// </para>
    /// </remarks>
    public static string SaidWhenNoHolder(SealedCredential envelope, string holder)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return $"This credential is sealed to {envelope.Wrapped.Count} holder(s), and this machine "
             + $"is not one of them. Nothing is wrong with it - it was never pushed here. Ask "
             + $"somebody who holds it to push it to this machine's key, {Short(holder)}.";
    }

    /// <summary>Enough of a public key to recognise, for a sentence a person reads.</summary>
    /// <remarks>
    /// A public key is not a secret, so this is a courtesy rather than a
    /// control: a full SubjectPublicKeyInfo is unreadable in a terminal and
    /// whoever needs the whole thing has `gg` to print it.
    /// </remarks>
    private static string Short(string key) =>
        key.Length <= 16 ? key : key[..16] + "…";

    private static string? Validate(SealedCredential envelope) =>
        SealedCredential.Validate(envelope);

    /// <summary>
    /// The content key, for a holder that can get at it.
    /// </summary>
    /// <remarks>
    /// <b>Shared by <see cref="Open"/> and <see cref="Rewrap"/>, because they
    /// ask the same question.</b> Two copies would be two places for "which
    /// wrapped key is mine" to drift, and the one that drifted would produce an
    /// envelope that opens for the sender and not the recipient.
    /// </remarks>
    private static byte[] Opened(SealedCredential envelope, IAgreeAsAHolder ours)
    {
        var holder = ours.PublicKey;

        return SealedCredential.WrappedFor(envelope, holder) is { } mine
            ? RunnerSeal.OpenWith(ours, Convert.FromBase64String(mine.Wrapped), WrapLabel)
            : throw new CryptographicException(SaidWhenNoHolder(envelope, holder));
    }
}
