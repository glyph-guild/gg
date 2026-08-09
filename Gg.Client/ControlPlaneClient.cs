using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Client;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeviceAuthorizationRequest))]
[JsonSerializable(typeof(DeviceAuthorizationStarted))]
[JsonSerializable(typeof(DeviceTokenRequest))]
[JsonSerializable(typeof(SessionIssued))]
[JsonSerializable(typeof(WhoAmI))]
[JsonSerializable(typeof(RunnerRegistrationRequest))]
[JsonSerializable(typeof(RunnerRegistered))]
internal sealed partial class ProtocolJsonContext : JsonSerializerContext;

/// <summary>Outcome of one poll of a pending device authorization.</summary>
public abstract record DevicePollResult
{
    public sealed record Pending : DevicePollResult;

    public sealed record Complete(SessionIssued Session) : DevicePollResult;

    /// <summary>The authorization expired or the human refused it.</summary>
    public sealed record Declined(string Reason) : DevicePollResult;
}

/// <summary>
/// Everything gg says to the control plane. It talks to nothing else.
/// </summary>
/// <remarks>
/// No identity provider appears anywhere in this client, by design: the
/// control plane brokers that exchange. When a second provider ships, this
/// file does not change - which is the entire point of the port living on the
/// server side of the boundary.
/// </remarks>
public sealed class ControlPlaneClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    /// <summary>Applies the three version headers every request must carry.</summary>
    public static void ApplyVersionHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation(GgVersions.ProtocolHeader, GgVersions.Protocol.ToString());
        request.Headers.TryAddWithoutValidation(GgVersions.RunnerVersionHeader, GgVersions.Binary);
        request.Headers.TryAddWithoutValidation(GgVersions.FactVocabularyHeader, GgVersions.FactVocabulary);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string? sessionToken = null)
    {
        var request = new HttpRequestMessage(method, path);
        ApplyVersionHeaders(request);
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            request.Headers.TryAddWithoutValidation(GgVersions.SessionHeader, sessionToken);
        }
        return request;
    }

    /// <summary>Begins a device authorization.</summary>
    public async Task<DeviceAuthorizationStarted> StartDeviceAuthorizationAsync(
        string deviceLabel, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/auth/device");
        request.Content = JsonContent.Create(
            new DeviceAuthorizationRequest { DeviceLabel = deviceLabel },
            ProtocolJsonContext.Default.DeviceAuthorizationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.DeviceAuthorizationStarted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no authorization.");
    }

    /// <summary>
    /// Polls once. Pending is 202 - a normal wait, not an error, so it does not
    /// pollute logs and metrics with failures that aren't.
    /// </summary>
    public async Task<DevicePollResult> PollDeviceAuthorizationAsync(
        string deviceCode, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/auth/device/token");
        request.Content = JsonContent.Create(
            new DeviceTokenRequest { DeviceCode = deviceCode },
            ProtocolJsonContext.Default.DeviceTokenRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new DevicePollResult.Pending();
        }
        if (response.StatusCode == HttpStatusCode.Gone)
        {
            return new DevicePollResult.Declined("The authorization expired or was declined.");
        }

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.SessionIssued, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no session.");
        return new DevicePollResult.Complete(session);
    }

    /// <summary>Who the held session belongs to.</summary>
    public async Task<WhoAmI?> WhoAmIAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/auth/whoami", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        return response.StatusCode == HttpStatusCode.Unauthorized
            ? null
            : await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.WhoAmI, cancellationToken);
    }

    /// <summary>Revokes the session server-side. Returns false if it was already gone.</summary>
    public async Task<bool> RevokeSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/auth/logout", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Surfaces a protocol-floor refusal as something actionable rather than a
    /// bare status code.
    /// </summary>
    private static async Task ThrowIfProtocolRefusedAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.UpgradeRequired)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProtocolTooOldException(
                $"This gg is too old for the control plane. {detail}".Trim());
        }
    }
}

/// <summary>Raised when the control plane refuses this binary's protocol version.</summary>
public sealed class ProtocolTooOldException(string message) : Exception(message);
