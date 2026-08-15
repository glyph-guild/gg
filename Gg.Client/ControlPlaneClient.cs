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
[JsonSerializable(typeof(CredentialRegistrationRequest))]
[JsonSerializable(typeof(CredentialRegistered))]
[JsonSerializable(typeof(CredentialList))]
[JsonSerializable(typeof(CredentialRemoved))]
[JsonSerializable(typeof(Envelope))]
[JsonSerializable(typeof(EnvelopeState))]
[JsonSerializable(typeof(FlightAttribution))]
[JsonSerializable(typeof(GateList))]
[JsonSerializable(typeof(DecisionRequest))]
[JsonSerializable(typeof(DecisionRecorded))]
[JsonSerializable(typeof(EnvelopeApplied))]
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

    /// <summary>
    /// The tenant's envelope, or null when it has never applied one.
    /// </summary>
    /// <remarks>
    /// Null and "an envelope that governs nothing" are different answers, and
    /// the endpoint keeps them apart with a 404. A tenant that has never set
    /// one up should be told to; one whose envelope is deliberately permissive
    /// should not.
    /// </remarks>
    public async Task<EnvelopeState?> GetEnvelopeAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/envelope", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeState, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no envelope.");
    }

    /// <summary>
    /// Writes the envelope back, and answers with the version it became.
    /// </summary>
    /// <remarks>
    /// The wire is JSON even though the thing a person edited was YAML. The
    /// control plane holds no YAML parser at all - that is a property worth
    /// having deliberately rather than by accident, since it is the service that
    /// holds the platform's own signing keys - so the format is translated
    /// here, on the side that already has the grammar.
    /// </remarks>
    public async Task<EnvelopeApplied> ApplyEnvelopeAsync(
        string sessionToken, Envelope envelope, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Put, "/v1/envelope", sessionToken);
        request.Content = JsonContent.Create(envelope, ProtocolJsonContext.Default.Envelope);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The control plane's own diagnosis, carried through unchanged. It
            // validated on its own terms rather than trusting that gg did, and
            // if the two disagree the person needs to see which one refused.
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
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
    /// Registers a reference to a credential the developer stored locally.
    /// </summary>
    /// <remarks>
    /// The request type has no field capable of carrying secret material, so
    /// there is nothing this method could send even if somebody wanted it to.
    /// A 400 is the control plane refusing the reference - a kind that is not
    /// local, a scope wider than read - and the diagnosis is the actionable
    /// part, so it is carried through rather than collapsed into a status.
    /// </remarks>
    public async Task<CredentialRegistered> RegisterCredentialAsync(
        string sessionToken, CredentialRegistrationRequest registration,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/credentials", sessionToken);
        request.Content = JsonContent.Create(
            registration, ProtocolJsonContext.Default.CredentialRegistrationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new CredentialRefusedException(
                (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.CredentialRegistered, cancellationToken)
            ?? throw new InvalidOperationException("Control plane registered no credential.");
    }

    /// <summary>Every credential reference this tenant has registered.</summary>
    public async Task<CredentialList> ListCredentialsAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/credentials", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.CredentialList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no credential list.");
    }

    /// <summary>Deregisters a credential, or null if the id names none.</summary>
    public async Task<CredentialRemoved?> RemoveCredentialAsync(
        string sessionToken, string credentialId, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Delete, $"/v1/credentials/{credentialId}", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.CredentialRemoved, cancellationToken);
    }

    /// <summary>
    /// Why each obligation applied to a flight. Null when there is no such flight.
    /// </summary>
    /// <remarks>
    /// A read, and nothing more. Everything in the answer was decided by the
    /// Engine before it was serialized.
    /// </remarks>
    /// <summary>
    /// Everything waiting on a person.
    /// </summary>
    /// <remarks>
    /// A read, and there is no method beside it that answers one. The absence is the
    /// point: nothing in this client can unstick a flight.
    /// </remarks>
    /// <summary>
    /// Posts a decision, and returns what the control plane made of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Posts, never decides.</b> Nothing here marks an obligation satisfied or infers
    /// an admission: the answer comes back with whatever the Engine re-evaluated, and the
    /// client renders it. ADR-0011 - a decision is an input to evaluation, never a
    /// substitute for admission.
    /// </para>
    /// <para>
    /// A 409 means the work moved between being shown and being decided. Surfaced as a
    /// diagnosis rather than swallowed, because the caller approved something specific
    /// and the honest answer is that it is no longer what is there.
    /// </para>
    /// </remarks>
    public async Task<DecisionRecorded?> DecideAsync(
        string sessionToken,
        string reference,
        DecisionRequest decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        using var request = Request(
            HttpMethod.Post, $"/v1/flights/{reference}/decisions", sessionToken);
        // THE SOURCE-GENERATED CONTEXT, because the reflection overload is refused here:
        // this assembly ships inside an AOT binary, and JsonContent.Create<T> cannot be
        // statically analysed. The build rejects it rather than producing something that
        // works in Debug and throws in the published binary.
        request.Content = JsonContent.Create(
            decision, ProtocolJsonContext.Default.DecisionRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new DecisionRefusedException(
                "The work changed while this decision was being made, so it was not recorded. "
              + "What you were shown is not what is there now - read it again with `gg why` and "
              + "decide against the work as it stands.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // THE DIAGNOSIS, CARRIED THROUGH. This used to fall to
            // EnsureSuccessStatusCode and leave as an HttpRequestException saying
            // "400 (Bad Request)" - the control plane's sentence, which is the only
            // part anybody can act on, was thrown away one line before it was read.
            throw new DecisionRefusedException(
                (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.DecisionRecorded, cancellationToken);
    }

    public async Task<GateList> GatesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/gates", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        response.EnsureSuccessStatusCode();

        // An empty list rather than null. "Nothing is waiting" is an answer every
        // caller can render, and a null would make it a case each of them handles.
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.GateList, cancellationToken)
            ?? new GateList { Gates = [] };
    }

    public async Task<FlightAttribution?> WhyAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/why", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightAttribution, cancellationToken);
    }

    /// <summary>
    /// Records that a person took a flight over, and what came back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Article XII: actions that cannot be attributed do not happen. A person
    /// held this flight for a while and a machine did not, and the log has to be
    /// able to say so - otherwise the record reads as though the agent produced
    /// whatever is now in the tree.
    /// </para>
    /// <para>
    /// <b>Recorded even when nothing came back.</b> Somebody who took a flight
    /// and wrote nothing is a real event with a real duration, and the absence of
    /// a decision is part of what is being attributed rather than a reason to
    /// stay quiet.
    /// </para>
    /// </remarks>
    public async Task<bool> RecordTakeoverAsync(
        string sessionToken,
        string flightId,
        TakeoverRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var request = Request(HttpMethod.Post, $"/v1/flights/{flightId}/takeover", sessionToken);
        request.Content = JsonContent.Create(record, TakeoverJson.Default.TakeoverRecord);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        // A control plane too old to know the endpoint is not a reason to lose
        // the takeover locally. The console has already handed the terminal
        // over and back; refusing to continue would punish the person for the
        // control plane's version.
        return response.IsSuccessStatusCode;
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
