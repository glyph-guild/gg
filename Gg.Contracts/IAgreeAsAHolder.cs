namespace Gg.Contracts;

/// <summary>
/// A holder's key, as the sealing code needs it: a public half to be recognised
/// by, and an agreement it will perform on request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two members, and the one that is NOT here is the point.</b> There is no
/// way to get the private key out of this. That is the whole reason the interface
/// exists: .NET cannot construct an <c>ECDiffieHellman</c> over a key it does not
/// hold, so any signature demanding one can only ever be satisfied by a key in
/// this process's memory — and a PIV card, a TPM or an HSM is then permanently
/// out of reach. ADR-0037 names this cost as <i>"one type signature today and
/// painful to reopen later"</i>.
/// </para>
/// <para>
/// <b>It is declared on the contract, beside <see cref="RunnerSeal"/>, because
/// that is what takes it.</b> Both ends seal and open by one rule; the rule now
/// asks the holder to perform its own half of the agreement rather than handing
/// over the means to perform it.
/// </para>
/// <para>
/// <b>What a holder is NOT trusted with.</b> The label is the caller's, not the
/// implementer's, which is what keeps a wrapped content key from opening as an
/// introduction and the other way round. An implementation that ignored the label
/// would produce bytes nothing can open, which fails loudly at the first use
/// rather than quietly weakening a separation.
/// </para>
/// </remarks>
public interface IAgreeAsAHolder
{
    /// <summary>
    /// The public half, SubjectPublicKeyInfo in base64 — the spelling a holder is
    /// named by.
    /// </summary>
    /// <remarks>
    /// <b>The same spelling everywhere, deliberately.</b> A wrapped content key
    /// is found by matching this against <c>WrappedContentKey.Holder</c>, so a
    /// second encoding would not fail to compile; it would fail to find a key
    /// that was there, and report it as "this machine is not a holder".
    /// </remarks>
    string PublicKey { get; }

    /// <summary>
    /// Derives the labelled key this holder shares with <paramref name="theirPublicKey"/>,
    /// and returns the bytes.
    /// </summary>
    /// <remarks>
    /// <b>Bytes out, never the key.</b> Thirty-two of them, by the one derivation
    /// <see cref="RunnerSeal.AgreementOver"/> declares — so a second
    /// implementation of this interface cannot quietly choose a different hash or
    /// length and produce an envelope that opens for whoever sealed it and nobody
    /// else.
    /// </remarks>
    /// <param name="theirPublicKey">Their public half, SubjectPublicKeyInfo in base64.</param>
    /// <param name="label">
    /// What the derived key is for. Supplied by the sealing code, so that a key
    /// derived for one purpose cannot open something sealed for another.
    /// </param>
    byte[] AgreeWith(string theirPublicKey, string label);
}
