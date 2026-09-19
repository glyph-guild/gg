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
/// <b>Here rather than beside the reader</b> because two projects need the
/// spelling and only one may make the call: the reader lives with the credential
/// store, and the sentence that tells a person why a tool server has no
/// credential lives with the reader declarations, which cannot see the store.
/// </para>
/// </remarks>
/// <param name="Vault">The vault's host: its name, then the domain its cloud serves vaults from.</param>
/// <param name="Secret">The secret's name within it; the latest version is read.</param>
public sealed record KeyVaultReference(string Vault, string Secret)
{
    /// <summary>Every vault reference begins with this.</summary>
    public const string Scheme = "keyvault://";

    /// <summary>Whether a locator is a vault reference at all, well formed or not.</summary>
    public static bool Names(string? locator) =>
        throw new NotImplementedException();

    /// <summary>The reference, or an <see cref="ArgumentException"/> saying why it is not one.</summary>
    public static KeyVaultReference Parse(string locator) =>
        throw new NotImplementedException();

    /// <summary>Where the secret is read from.</summary>
    public Uri SecretUri => throw new NotImplementedException();

    /// <inheritdoc/>
    public override string ToString() => $"{Scheme}{Vault}/{Secret}";
}
