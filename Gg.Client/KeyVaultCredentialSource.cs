using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Local;

namespace Gg.Client;

/// <summary>
/// Reads a <c>keyvault://</c> reference with this machine's managed identity.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two REST calls and no SDK</b>: the instance metadata service's token,
/// then the secret. A cloud SDK would be the only reflection-heavy dependency
/// in an AOT binary, for two GETs.
/// </para>
/// <para>
/// <b>Nothing is kept.</b> Not the secret, which is returned to the flight that
/// asked and held nowhere else, and not the token either: a token cached on
/// disk would be a bearer credential for every vault this machine may read, and
/// one cached in memory buys one call per flight. Each read asks both again.
/// </para>
/// <para>
/// <b>The metadata service at its link-local address, and nothing else.</b>
/// Hosted app platforms publish a different endpoint (<c>IDENTITY_ENDPOINT</c>)
/// and Kubernetes a federated token file; a runner runs on neither of the
/// first, and the second is slice forty-four's, where the cluster is. A machine
/// with several user-assigned identities needs a client id this reader does not
/// send - the metadata service answers for the one identity a machine has,
/// which is the enrolled machine's case.
/// </para>
/// <para>
/// <b>What leaves in a refusal is the reference and a status</b> - never a
/// response body. A vault's error can echo the caller, and the caller is this
/// machine's bearer token; the sentence becomes a flight-log event, which is
/// the one part of a vault read that ever reaches the control plane.
/// </para>
/// </remarks>
public sealed class KeyVaultCredentialSource(HttpClient http, TimeSpan? identityPatience = null)
{
    /// <summary>Where this machine's managed identity is asked for a token, for one audience.</summary>
    /// <remarks>
    /// Link-local, and reachable only from the machine itself - which is the
    /// whole of what "the machine's own identity" means.
    /// </remarks>
    public static Uri IdentityEndpointFor(KeyVaultReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new Uri(
            "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource="
          + Uri.EscapeDataString(reference.Audience));
    }

    private readonly HttpClient _http = http;

    // SHORT, because off a cloud machine the link-local address is not refused but
    // silent, and a read that waited the client's whole timeout would hold a
    // flight for an identity this machine was never going to have.
    private readonly TimeSpan _identityPatience = identityPatience ?? TimeSpan.FromSeconds(5);

    /// <summary>The secret. Throws <see cref="CredentialUnavailableException"/> saying why not.</summary>
    /// <remarks>
    /// Synchronous, because the port it sits behind is: <c>ICredentialStore.Read</c>
    /// and the runner's <c>secretFor</c> both are, and a flight resolves its
    /// credential once, before anything starts.
    /// </remarks>
    public string Read(string locator)
    {
        // Parsed BEFORE any call, so a reference that could steer the token is
        // refused while there is still no token to steer.
        var reference = KeyVaultReference.Parse(locator);

        var token = IdentityToken(reference);

        using var request = new HttpRequestMessage(HttpMethod.Get, reference.SecretUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = _http.Send(request);
        }
        catch (Exception unreachable) when (unreachable is HttpRequestException or OperationCanceledException)
        {
            throw new CredentialUnavailableException(
                $"Could not read {reference}: vault '{reference.Vault}' did not answer "
              + $"({reference.SecretUri.Host}: {unreachable.Message}).");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new CredentialUnavailableException(
                    $"Could not read {reference}: vault '{reference.Vault}' refused this machine's "
                  + $"managed identity ({(int)response.StatusCode}). Grant that identity read on the "
                  + "vault's secrets - the 'get' secret permission, or the Key Vault Secrets User role.");
            }

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                throw new CredentialUnavailableException(
                    $"Could not read {reference}: vault '{reference.Vault}' has no secret named "
                  + $"'{reference.Secret}', or it is disabled.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new CredentialUnavailableException(
                    $"Could not read {reference}: vault '{reference.Vault}' answered "
                  + $"{(int)response.StatusCode}.");
            }

            return Parsed(response, KeyVaultJson.Default.VaultSecret, reference)?.Value
                ?? throw new CredentialUnavailableException(
                    $"Could not read {reference}: vault '{reference.Vault}' answered without a value.");
        }
    }

    /// <summary>A token for vaults, from this machine's own identity.</summary>
    private string IdentityToken(KeyVaultReference reference)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, IdentityEndpointFor(reference));

        // The metadata service's own guard against a request forged through
        // something else on this machine: it answers nothing without it.
        request.Headers.Add("Metadata", "true");

        using var patience = new CancellationTokenSource(_identityPatience);

        HttpResponseMessage response;
        try
        {
            response = _http.Send(request, patience.Token);
        }
        catch (Exception unreachable) when (unreachable is HttpRequestException or OperationCanceledException)
        {
            throw NoIdentity(reference, unreachable is OperationCanceledException
                ? $"the instance metadata service did not answer within {_identityPatience.TotalSeconds:0.#}s"
                : $"the instance metadata service could not be reached: {unreachable.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw NoIdentity(reference,
                    $"the instance metadata service answered {(int)response.StatusCode}");
            }

            return Parsed(response, KeyVaultJson.Default.ManagedIdentityToken, reference)?.AccessToken
                is { Length: > 0 } token
                ? token
                : throw NoIdentity(reference, "the instance metadata service answered without a token");
        }
    }

    private static CredentialUnavailableException NoIdentity(KeyVaultReference reference, string why) =>
        new($"Could not read {reference}: this machine has no managed identity to read a vault with "
          + $"({why}). A vault reference is read by the machine's own identity - give this machine "
          + $"one, and grant it read on vault '{reference.Vault}'.");

    private static T? Parsed<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,
        KeyVaultReference reference)
    {
        try
        {
            using var body = response.Content.ReadAsStream();
            return JsonSerializer.Deserialize(body, type);
        }
        catch (JsonException)
        {
            // THE BODY IS NOT QUOTED. It may hold the token or the secret, and
            // this sentence is the part of a read that leaves the machine.
            throw new CredentialUnavailableException(
                $"Could not read {reference}: an answer on the way was not the JSON it should be.");
        }
    }
}

/// <summary>
/// A credential this machine knows where to find and could not read, and why.
/// </summary>
/// <remarks>
/// A sentence somebody can act on, which is what ADR-0004 asked of exactly this
/// failure: <i>a runner that cannot read a vault produces a stalled flight that
/// looks like a broken product.</i> It names the reference and never the value.
/// </remarks>
public sealed class CredentialUnavailableException(string message) : InvalidOperationException(message);

/// <summary>The metadata service's token answer; only the token is read.</summary>
internal sealed record ManagedIdentityToken
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }
}

/// <summary>The vault's secret answer; only the value is read.</summary>
internal sealed record VaultSecret
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

[JsonSerializable(typeof(ManagedIdentityToken))]
[JsonSerializable(typeof(VaultSecret))]
internal sealed partial class KeyVaultJson : JsonSerializerContext;
