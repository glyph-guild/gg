using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner;

/// <summary>How the runner serializes what it puts on the wire.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RunnerHeartbeat))]
[JsonSerializable(typeof(HeartbeatAccepted))]
[JsonSerializable(typeof(LeaseClaimRequest))]
[JsonSerializable(typeof(LeaseGranted))]
[JsonSerializable(typeof(LeaseRenewalRequest))]
[JsonSerializable(typeof(LeaseRenewed))]
[JsonSerializable(typeof(LeaseReleaseRequest))]
[JsonSerializable(typeof(LeaseReleased))]
public sealed partial class RunnerJsonContext : JsonSerializerContext;

/// <summary>
/// The runner protocol over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// Carries the RUNNER credential and never a session. That is not a
/// convention: this assembly cannot reference the developer client at all, so
/// there is nothing here that could hold one.
/// </para>
/// <para>
/// Paths and header names come from the declared surface rather than from
/// literals, so a control plane that moved an endpoint fails this repo's
/// conformance tests instead of failing at a customer.
/// </para>
/// </remarks>
public sealed class RunnerProtocolClient(HttpClient httpClient, string runnerToken) : IRunnerProtocol
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _runnerToken = runnerToken;

    /// <summary>This binary's own version, for the runner-version header.</summary>
    private static readonly string _binaryVersion =
        typeof(RunnerProtocolClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(ProtocolSurface.ProtocolHeader, ProtocolSurface.Revision.ToString());
        request.Headers.TryAddWithoutValidation(ProtocolSurface.RunnerVersionHeader, _binaryVersion);
        request.Headers.TryAddWithoutValidation(ProtocolSurface.FactVocabularyHeader, FactVocabulary);
        request.Headers.TryAddWithoutValidation(ProtocolSurface.RunnerHeader, _runnerToken);
        return request;
    }

    /// <summary>Pinned fact vocabulary this runner evaluates against.</summary>
    public const string FactVocabulary = "0.1.0";

    public async Task<HeartbeatAccepted> HeartbeatAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, $"/v1/runners/{runnerId}/heartbeat");
        request.Content = JsonContent.Create(
            new RunnerHeartbeat { Labels = labels }, RunnerJsonContext.Default.RunnerHeartbeat);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.HeartbeatAccepted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no heartbeat interval.");
    }

    public async Task<ClaimResult> ClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/leases:claim");
        request.Content = JsonContent.Create(
            new LeaseClaimRequest { RunnerId = runnerId, Labels = labels, MaxWaitSeconds = maxWaitSeconds },
            RunnerJsonContext.Default.LeaseClaimRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new ClaimResult.Nothing();
        }

        response.EnsureSuccessStatusCode();
        var lease = await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.LeaseGranted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane granted a lease with no body.");
        return new ClaimResult.Granted(lease);
    }

    public async Task<RenewResult> RenewAsync(
        string leaseId, int generation, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, $"/v1/leases/{leaseId}/renew");
        request.Content = JsonContent.Create(
            new LeaseRenewalRequest { Generation = generation }, RunnerJsonContext.Default.LeaseRenewalRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return new RenewResult.Fenced();
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new RenewResult.Gone();
        }

        response.EnsureSuccessStatusCode();
        var renewed = await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.LeaseRenewed, cancellationToken)
            ?? throw new InvalidOperationException("Control plane renewed a lease with no body.");
        return new RenewResult.Renewed(renewed.ExpiresAt);
    }

    public async Task<ReleaseResult> ReleaseAsync(
        string leaseId, int generation, string disposition, string? detail = null,
        CredentialResolutionFailure? credentialFailure = null,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, $"/v1/leases/{leaseId}/release");
        request.Content = JsonContent.Create(
            new LeaseReleaseRequest
            {
                Generation = generation,
                Disposition = disposition,
                Detail = detail,
                // A reference and a sentence. The type has no field for a
                // secret, so this is the diagnosis and not the value.
                CredentialFailure = credentialFailure,
            },
            RunnerJsonContext.Default.LeaseReleaseRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return new ReleaseResult.Fenced();
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ReleaseResult.Gone();
        }

        response.EnsureSuccessStatusCode();
        return new ReleaseResult.Released();
    }

    private static void ThrowIfProtocolRefused(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.UpgradeRequired)
        {
            var range = response.Headers.TryGetValues(ProtocolSurface.SupportedProtocolsHeader, out var values)
                ? string.Join(", ", values)
                : "unknown";
            throw new RunnerProtocolTooOldException(
                $"This runner speaks protocol {ProtocolSurface.Revision}; the control plane serves {range}.");
        }
    }
}

/// <summary>Raised when the control plane refuses this runner's protocol revision.</summary>
public sealed class RunnerProtocolTooOldException(string message) : Exception(message);
