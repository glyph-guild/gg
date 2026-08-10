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
[JsonSerializable(typeof(FlightLaunchRequest))]
[JsonSerializable(typeof(FlightLaunched))]
[JsonSerializable(typeof(FlightSummary))]
[JsonSerializable(typeof(FlightList))]
[JsonSerializable(typeof(FlightLog))]
[JsonSerializable(typeof(RunnerList))]
[JsonSerializable(typeof(TelemetryDisclosure))]
/// <summary>
/// How this client serializes wire types.
/// </summary>
/// <remarks>
/// Public so conformance tests can read the metadata the SERIALIZER will use,
/// rather than the C# property names. A naming policy or a [JsonPropertyName]
/// changes the wire without changing a property name, and it is the wire that
/// has to match the control plane.
/// </remarks>
public sealed partial class ProtocolJsonContext : JsonSerializerContext;

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

    /// <summary>
    /// Registers a runner and returns its credential, shown once.
    /// </summary>
    /// <remarks>
    /// A person does this, with their session. The credential that comes back
    /// is handed to the runner process and is the only thing it ever holds -
    /// attribution stays with the developer, authority is the runner protocol
    /// alone.
    /// </remarks>
    public async Task<RunnerRegistered> RegisterRunnerAsync(
        string sessionToken, string label, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/runners", sessionToken);
        request.Content = JsonContent.Create(
            new RunnerRegistrationRequest { Label = label, ProtocolVersion = GgVersions.Protocol },
            ProtocolJsonContext.Default.RunnerRegistrationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RunnerRegistered, cancellationToken)
            ?? throw new InvalidOperationException("Control plane registered no runner.");
    }

    /// <summary>
    /// The cheapest call that reaches the protocol floor.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose. The floor is checked BEFORE authentication
    /// server-side, so an unauthenticated request still gets a 426 - which
    /// means one call answers both "is it up" and "will it talk to this
    /// binary", and answers the second even for somebody who is not signed in.
    /// </remarks>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/auth/whoami");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        // Any other status means the server answered, which is the question.
    }

    /// <summary>The tenant's flights.</summary>
    public async Task<FlightList> ListFlightsAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/flights", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.FlightList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no flight list.");
    }

    /// <summary>One flight, or null if the reference names none.</summary>
    public async Task<FlightSummary?> GetFlightAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightSummary, cancellationToken);
    }

    /// <summary>A flight's log, or null if the reference names no flight.</summary>
    public async Task<FlightLog?> GetFlightLogAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/log", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.FlightLog, cancellationToken);
    }

    /// <summary>The tenant's runners, with the state the control plane derived.</summary>
    public async Task<RunnerList> ListRunnersAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/runners", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.RunnerList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no runner list.");
    }

    /// <summary>Opens a flight. Answers 202: the number is minted afterwards.</summary>
    public async Task<FlightLaunched> LaunchFlightAsync(
        string sessionToken, FlightLaunchRequest launch, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/flights", sessionToken);
        request.Content = JsonContent.Create(launch, ProtocolJsonContext.Default.FlightLaunchRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Article XI reaching the person: the control plane refused with a
            // diagnosis, and swallowing it into "bad request" would lose the
            // only part they can act on.
            throw new FlightIntentException(
                (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightLaunched, cancellationToken)
            ?? throw new InvalidOperationException("Control plane opened no flight.");
    }

    /// <summary>
    /// What the control plane says it transmits. Null if it is too old to say.
    /// </summary>
    /// <remarks>
    /// A 404 means an older control plane that predates the disclosure, which
    /// is a different fact from "exports nothing" and must not be reported as
    /// it - the whole point is to stop a silent transmission looking like
    /// silence.
    /// </remarks>
    public async Task<TelemetryDisclosure?> TelemetryAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/telemetry", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.TelemetryDisclosure, cancellationToken);
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
