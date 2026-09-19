using Gg.Local;

namespace Gg.Client;

/// <summary>
/// Reads a <c>keyvault://</c> reference with this machine's managed identity.
/// </summary>
/// <remarks>
/// Two REST calls and no SDK: the instance metadata service's token, then the
/// secret. A cloud SDK would be the only reflection-heavy dependency in an
/// AOT binary, for two GETs.
/// </remarks>
public sealed class KeyVaultCredentialSource(HttpClient http, TimeSpan? identityPatience = null)
{
    private readonly HttpClient _http = http;
    private readonly TimeSpan _identityPatience = identityPatience ?? TimeSpan.FromSeconds(5);

    /// <summary>The secret. Throws <see cref="CredentialUnavailableException"/> saying why not.</summary>
    public string Read(string locator) =>
        throw new NotImplementedException($"{locator}, through {_http.BaseAddress}, within {_identityPatience}");
}

/// <summary>
/// A credential this machine knows where to find and could not read, and why.
/// </summary>
public sealed class CredentialUnavailableException(string message) : InvalidOperationException(message);
