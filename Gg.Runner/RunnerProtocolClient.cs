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
[JsonSerializable(typeof(LeaseClaimAccepted))]
[JsonSerializable(typeof(LeaseClaimStatus))]
[JsonSerializable(typeof(LeaseGranted))]
[JsonSerializable(typeof(LeaseRenewalRequest))]
[JsonSerializable(typeof(LeaseRenewed))]
[JsonSerializable(typeof(LeaseReleaseRequest))]
[JsonSerializable(typeof(LeaseReleased))]
[JsonSerializable(typeof(FactBatch))]
[JsonSerializable(typeof(FactBatchAccepted))]
[JsonSerializable(typeof(LandingDecision))]
[JsonSerializable(typeof(PoolActionList))]
[JsonSerializable(typeof(PoolAttestation))]
[JsonSerializable(typeof(MemberCredentialRequest))]
[JsonSerializable(typeof(MemberCredentialMinted))]
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
public sealed class RunnerProtocolClient(HttpClient httpClient, string runnerToken)
    : IRunnerProtocol, Pools.IPoolProtocol
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
    /// <remarks>
    /// From the contract. A runner evaluating facts against a vocabulary the
    /// control plane has moved past gives a silently wrong answer, which is
    /// the whole reason this travels on every request.
    /// </remarks>
    public const string FactVocabulary = Gg.Contracts.FactVocabulary.Version;

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

    public async Task<ClaimAcceptance> RequestClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/leases:claim");
        request.Content = JsonContent.Create(
            new LeaseClaimRequest { RunnerId = runnerId, Labels = labels, MaxWaitSeconds = maxWaitSeconds },
            RunnerJsonContext.Default.LeaseClaimRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        // BOTH OLD ANSWERS, tolerated so the two repositories can land this in
        // either order - the arrangement the decisions endpoint was given when
        // it stopped answering inline. A control plane serving this contract
        // sends neither.
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new ClaimAcceptance.Inline(new ClaimResult.Nothing());
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var inline = await response.Content.ReadFromJsonAsync(
                RunnerJsonContext.Default.LeaseGranted, cancellationToken)
                ?? throw new InvalidOperationException("Control plane granted a lease with no body.");
            return new ClaimAcceptance.Inline(new ClaimResult.Granted(inline));
        }

        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.LeaseClaimAccepted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane accepted a claim with no body.");

        return new ClaimAcceptance.Accepted(
            accepted.RequestId, TimeSpan.FromSeconds(accepted.PollAfterSeconds));
    }

    /// <summary>
    /// Asks what became of a request.
    /// </summary>
    /// <remarks>
    /// A 404 is <see cref="ClaimResult.Expired"/> rather than a throw: a request
    /// the control plane has finished with and one it never had are the same
    /// thing to a runner, and both are answered by asking again with a new one.
    /// </remarks>
    public async Task<ClaimResult> ReadClaimAsync(
        string requestId, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/leases/claims/{requestId}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ClaimResult.Expired();
        }

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.LeaseClaimStatus, cancellationToken)
            ?? throw new InvalidOperationException("Control plane answered a claim status with no body.");

        return status.State switch
        {
            // The lease is read only in the state that carries one. A granted
            // status with no lease is a control plane bug and says so here,
            // rather than becoming an idle runner nobody can explain.
            LeaseClaimStates.Granted => new ClaimResult.Granted(
                status.Lease ?? throw new InvalidOperationException(
                    "Control plane granted a claim and attached no lease.")),
            LeaseClaimStates.Waiting => new ClaimResult.Waiting(status.WaitingOn),
            LeaseClaimStates.Expired => new ClaimResult.Expired(),
            LeaseClaimStates.Pending => new ClaimResult.Nothing(),

            // WITHHELD BY A PERSON, and answered rather than halted on. This
            // branch's absence is what made parking a runner kill it: the
            // fall-through below is for a binary older than the control plane,
            // and it fired on one built from the same commit.
            LeaseClaimStates.Parked => new ClaimResult.Parked(),

            // HALT ON A STATE THIS BINARY DOES NOT KNOW. The vocabulary is
            // closed precisely so a fifth value is a version move; guessing
            // would make the closure decorative.
            _ => throw new InvalidOperationException(
                $"Control plane reported claim state '{status.State}', which this runner does not "
              + "know. Its contract version is older than the control plane's."),
        };
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

    /// <summary>
    /// Ships facts against the lease that authorises them.
    /// </summary>
    /// <remarks>
    /// Takes <see cref="Facts.FilteredFacts"/>, so nothing that has not been
    /// through the filter can reach the wire from here. A 409 is the generation
    /// fence: the flight belongs to another runner now, and its facts are not
    /// ours to assert.
    /// </remarks>
    public async Task<FactBatchAccepted> ShipFactsAsync(
        string leaseId, int generation, Facts.FilteredFacts facts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);

        using var request = Request(HttpMethod.Post, $"/v1/leases/{leaseId}/facts");
        request.Content = JsonContent.Create(
            new FactBatch { Generation = generation, Facts = facts.Items },
            RunnerJsonContext.Default.FactBatch);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Not a retry. The fence refused us, and shipping again would be
            // asserting facts about another runner's flight.
            throw new RunnerFencedException(
                $"Lease {leaseId} generation {generation} is not the live one. These facts belong to "
              + "a flight this runner no longer holds.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.FactBatchAccepted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane accepted facts with no answer.");
    }

    /// <summary>The pools half: decided actions for this pool. Serving is the claim.</summary>
    public async Task<PoolActionList> PullActionsAsync(
        string pool, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Get, $"/v1/pools/{Uri.EscapeDataString(pool)}/actions");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.PoolActionList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane answered nothing for the pull.");
    }

    /// <summary>
    /// Mints a member's nonce, or null when the control plane refuses.
    /// </summary>
    /// <remarks>
    /// <b>Null rather than a throw on a refusal.</b> A 403 here means this
    /// runner may not mint - a member asking, most likely - and that is a
    /// decision the caller has to report as a failed act rather than a crash.
    /// A transport failure still throws, and the loop's own backoff catches it.
    /// </remarks>
    public async Task<MemberCredentialMinted?> MintMemberAsync(
        string pool, string member, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post,
            $"/v1/pools/{Uri.EscapeDataString(pool)}/members/{Uri.EscapeDataString(member)}/credential");

        request.Content = JsonContent.Create(
            new MemberCredentialRequest { ProtocolVersion = Gg.Contracts.Description.ProtocolSurface.Revision },
            RunnerJsonContext.Default.MemberCredentialRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync(
                RunnerJsonContext.Default.MemberCredentialMinted, cancellationToken)
            : null;
    }

    /// <summary>Attests one action's outcome. Idempotent on the attestation id.</summary>
    public async Task AttestAsync(
        string pool, PoolAttestation attestation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        using var request = Request(
            HttpMethod.Post, $"/v1/pools/{Uri.EscapeDataString(pool)}/attestations");
        request.Content = JsonContent.Create(
            attestation, RunnerJsonContext.Default.PoolAttestation);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The contract's own Validate, applied on the other side - both
            // halves fail closed on their own format, the envelope's rule.
            throw new InvalidOperationException(
                "The attestation was refused: "
              + await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Asks whether this flight may push, and whether its work may land.
    /// </summary>
    /// <remarks>
    /// A 409 is the same generation fence shipping facts meets: the flight
    /// belongs to another runner now, and its landing is not ours to ask about.
    /// </remarks>
    public async Task<LandingDecision> ReadAdmissionAsync(
        string leaseId, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/leases/{leaseId}/admission");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        ThrowIfProtocolRefused(response);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new RunnerFencedException(
                $"Lease {leaseId} is not the live one. This flight's landing belongs to a runner "
              + "this one is not.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            RunnerJsonContext.Default.LandingDecision, cancellationToken)
            ?? throw new InvalidOperationException(
                "Control plane answered the admission route with no decision.");
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

/// <summary>
/// The generation fence refused this runner.
/// </summary>
/// <remarks>
/// Its own exception rather than a status code, because the correct response
/// is emphatically not to retry: the flight belongs to another runner now, and
/// asserting facts about it would write into somebody else's evidence.
/// </remarks>
public sealed class RunnerFencedException(string message) : Exception(message);
