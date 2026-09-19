using System.Text.RegularExpressions;

namespace Gg.Local;

/// <summary>
/// A secret in a vault, named by where it is: <c>keyvault://&lt;vault-host&gt;/&lt;secret&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reference, and never a value</b> - exactly what a <c>local:</c> locator
/// is. It may be written in a machine's configuration, carried by a profile and
/// logged, because only the machine's own identity can turn it into a secret.
/// </para>
/// <para>
/// <b>The vault's whole host, as its owner copies it from the vault's own
/// page</b> - <c>keyvault://acme-fleet.vault.example.net/repo-read</c> - and not
/// a bare name this binary completes. Which cloud a vault lives in is
/// configuration, as which forge a flight clones from is: this repository names
/// no provider (<c>ProviderNeutralityTests</c>), and the adapters it has are
/// named for their shape. It also means a vault in a sovereign cloud is one
/// more reference rather than one more release.
/// </para>
/// <para>
/// <b>Here rather than beside the reader</b> because two projects need the
/// spelling and only one may make the call: the reader lives with the credential
/// store, and the sentence that tells a person why a tool server has no
/// credential lives with the reader declarations, which cannot see the store.
/// </para>
/// </remarks>
/// <param name="Vault">The vault's host: its name, then the domain its cloud serves vaults from.</param>
/// <param name="Secret">The secret's name within it; the latest version is read.</param>
public sealed partial record KeyVaultReference(string Vault, string Secret)
{
    /// <summary>Every vault reference begins with this.</summary>
    public const string Scheme = "keyvault://";

    /// <summary>The vault api version the read is made against.</summary>
    public const string ApiVersion = "7.4";

    /// <summary>Whether a locator is a vault reference at all, well formed or not.</summary>
    /// <remarks>
    /// By scheme alone, so a malformed reference is routed to the reader that
    /// can say what is wrong with it, rather than to a file store that would
    /// call it a malformed <c>local:</c> locator.
    /// </remarks>
    public static bool Names(string? locator) =>
        locator is not null && locator.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>The reference, or an <see cref="ArgumentException"/> saying why it is not one.</summary>
    /// <remarks>
    /// <b>THE HOST IS WHERE THE MACHINE'S TOKEN IS SENT.</b> So it is held to a
    /// vault's own naming rule for its first label - three to twenty-four
    /// letters, digits and hyphens, beginning with a letter - and to plain DNS
    /// labels after it: no <c>@</c>, no port, no path but the one secret, no
    /// address. And the token asked for is minted for the domain the vault sits
    /// in (<see cref="Audience"/>), so it is only ever sent to a host inside
    /// the audience it was minted for. Refused rather than tidied, as a
    /// <c>local:</c> locator is.
    /// </remarks>
    public static KeyVaultReference Parse(string locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        if (!Names(locator))
        {
            throw new ArgumentException(
                $"'{locator}' is not a vault reference: it does not begin with '{Scheme}'.",
                nameof(locator));
        }

        var body = locator[Scheme.Length..];
        var slash = body.IndexOf('/');
        var host = (slash < 0 ? body : body[..slash]).ToLowerInvariant();
        var secret = slash < 0 ? "" : body[(slash + 1)..];

        if (!VaultHost().IsMatch(host) || host[..host.IndexOf('.')].Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{locator}' does not name a vault host this machine will send its identity to. "
              + "Write the vault's host as its page gives it - its name (3 to 24 letters, digits "
              + "and single hyphens, beginning with a letter), then the domain its cloud serves "
              + $"vaults from: {Scheme}<vault>.<domain>/<secret>.",
                nameof(locator));
        }

        if (!SecretName().IsMatch(secret))
        {
            throw new ArgumentException(
                $"'{locator}' does not name one secret. A secret name is 1 to 127 letters, digits "
              + $"and hyphens, with nothing after it: {Scheme}<vault>.<domain>/<secret>, and the "
              + "latest version is the one read.",
                nameof(locator));
        }

        return new KeyVaultReference(host, secret);
    }

    /// <summary>What the machine's token is minted for: the domain the vault is served from.</summary>
    public string Audience => $"https://{Vault[(Vault.IndexOf('.') + 1)..]}";

    /// <summary>Where the secret is read from.</summary>
    public Uri SecretUri => new($"https://{Vault}/secrets/{Secret}?api-version={ApiVersion}");

    /// <inheritdoc/>
    public override string ToString() => $"{Scheme}{Vault}/{Secret}";

    // The vault's name, then at least two DNS labels: a domain, never a bare
    // top-level one, so the audience is never a whole TLD.
    [GeneratedRegex(
        "^[a-z][a-z0-9-]{1,22}[a-z0-9](?:\\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?){2,}$")]
    private static partial Regex VaultHost();

    [GeneratedRegex("^[A-Za-z0-9-]{1,127}$")]
    private static partial Regex SecretName();
}
