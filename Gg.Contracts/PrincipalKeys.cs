using System.Security.Cryptography;

namespace Gg.Contracts;

/// <summary>
/// A person's public key, as the control plane holds it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A PUBLIC key, and that is the whole reason this may exist at all.</b>
/// Article VIII says the control plane stores references and facts, never
/// secrets — and a public key is neither a secret nor a reference to one. It is
/// the same move the introduction already makes when a console hands this side
/// an ephemeral public key so a runner can be reached.
/// </para>
/// <para>
/// <b>What it is FOR</b> is ADR-0037 Decision 2: a credential is sealed to the
/// people who may open it, and somebody sealing one has to be able to look up
/// the key of the person they are sealing it to. Nothing here opens anything.
/// </para>
/// <para>
/// <b>The private half never comes near this.</b> It is passphrase-wrapped on
/// the person's own machine and its adapter will not hand it out even locally —
/// which is what keeps a hardware-backed key possible later.
/// </para>
/// </remarks>
public static class PrincipalKeyFingerprint
{
    /// <summary>
    /// How a public key is named in a sentence a person reads.
    /// </summary>
    /// <remarks>
    /// <b>Derived once, here, for <see cref="CredentialLocator"/>'s stated
    /// reason:</b> <i>"two derivations that agree today is how a runner ends up
    /// looking for a file the CLI never wrote."</i> gg prints this and the
    /// control plane stores it, so a second spelling would make one side unable
    /// to find what the other registered.
    /// </remarks>
    public static string Of(string publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);

        return Convert.ToHexString(
            SHA256.HashData(Convert.FromBase64String(publicKey)))[..32].ToLowerInvariant();
    }
}

/// <summary>
/// Registers the public half of this person's key.
/// </summary>
/// <remarks>
/// <b>No principal id, for <see cref="CredentialRegistrationRequest"/>'s
/// reason:</b> the caller already is one, and an endpoint that accepted one
/// would be an endpoint somebody could name a different one to. A person
/// registers their own key and nobody else's.
/// </remarks>
[PinnedId("02995865-3d3f-42b6-bc69-e7095471da07")]
public sealed record PrincipalKeyRegistrationRequest
{
    /// <summary>SubjectPublicKeyInfo, base64 — the same spelling a runner registers.</summary>
    public required string PublicKey { get; init; }
}

/// <summary>What the control plane recorded. Still only a public key.</summary>
[PinnedId("e705893b-f724-4fda-8aa5-fa8adcabaab2")]
public sealed record PrincipalKeyRegistered
{
    public required string KeyId { get; init; }

    public required string PublicKey { get; init; }

    /// <summary>The short name for it, derived by <see cref="PrincipalKeyFingerprint"/>.</summary>
    public required string Fingerprint { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}

/// <summary>One registered key, as a person reads it.</summary>
/// <remarks>
/// <b>It names whose it is</b>, which <see cref="PrincipalKeyRegistrationRequest"/>
/// deliberately does not: registering is an act on your own behalf, and reading
/// is how somebody finds the key of the person they mean to seal a credential
/// to.
/// </remarks>
[PinnedId("e1786869-4ac7-42f5-842e-98ec7c886c69")]
public sealed record PrincipalKeySummary
{
    public required string KeyId { get; init; }

    /// <summary>Whose key it is, as a person would recognise them.</summary>
    public required string Principal { get; init; }

    public required string PublicKey { get; init; }

    public required string Fingerprint { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    /// <summary>
    /// When it stopped being one to seal to, or null while it still is.
    /// </summary>
    /// <remarks>
    /// <b>Retired rather than deleted.</b> A credential sealed to a key last
    /// year is still sealed to it, so a row that vanished would leave an
    /// envelope naming a holder nobody can account for — and "who could open
    /// this" is exactly the question a retired key is asked.
    /// </remarks>
    public DateTimeOffset? RetiredAt { get; init; }
}

/// <summary>Every key this tenant's people have registered.</summary>
/// <remarks>
/// A store you cannot inspect is a store people work around, which
/// <see cref="CredentialList"/> already says one layer over. Here it is also
/// the lookup: sealing a credential to somebody means finding their key.
/// </remarks>
[PinnedId("bac544a5-1a76-4256-91c3-d3943f5e2f73")]
public sealed record PrincipalKeyList
{
    public required IReadOnlyList<PrincipalKeySummary> Keys { get; init; }
}
