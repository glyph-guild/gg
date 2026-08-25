using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Contracts;

namespace Gg.Runner.Pools;

/// <summary>
/// The Docker adapter: the Engine API over HTTP, against the endpoint the
/// deployment configured — the scope-enforcing proxy, never the raw socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>A raw HttpClient, deliberately.</b> No Docker client package: the
/// surface this needs is five endpoints, and a dependency that can do
/// everything the daemon can is a capability nobody granted waiting for a
/// call site. There is no socket path anywhere in this file — the endpoint
/// is a URL from <see cref="PoolConfiguration"/>, and what that URL refuses
/// is the scope (§ 12: enforced by the provider, not by us).
/// </para>
/// <para>
/// The image digest attested is the daemon's own image id from inspect —
/// what the member actually runs, never what was asked for.
/// </para>
/// </remarks>
public sealed class DockerPoolAdapter(HttpClient httpClient) : IPoolAdapter
{
    private readonly HttpClient _httpClient = httpClient;

    public PoolCapabilities Capabilities { get; } = new() { Provider = "docker" };

    public async Task<IReadOnlyList<PoolMember>> ListAsync(
        string pool, CancellationToken cancellationToken = default)
    {
        var filters = Uri.EscapeDataString($$"""{"name":["{{pool}}-"]}""");
        using var response = await _httpClient.GetAsync(
            $"/containers/json?all=true&filters={filters}", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var listed = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        var members = new List<PoolMember>();
        foreach (var container in listed.RootElement.EnumerateArray())
        {
            // The daemon's name filter is a substring match; the prefix is
            // re-checked here so a stranger whose name merely contains the
            // pool's never enters the inventory.
            var name = container.GetProperty("Names")[0].GetString()!.TrimStart('/');
            if (name.StartsWith($"{pool}-", StringComparison.Ordinal))
            {
                members.Add(new PoolMember { Name = name });
            }
        }

        return members;
    }

    public async Task<PoolObservation> VerifyAsync(
        PoolMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        var inspected = await InspectAsync(member.Name, cancellationToken);
        if (inspected is not { } state)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"'{member.Name}' is in the pool's listing and will not inspect - "
                          + "the daemon knows a name it cannot describe.",
            };
        }

        return state.Running
            ? new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = state.ImageDigest,
                Provenance = EnvironmentProvenance.Reused,
            }
            : new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                ImageDigest = state.ImageDigest,
                Diagnosis = $"'{member.Name}' exists and is not running (status: {state.Status}).",
            };
    }

    public async Task<PoolObservation> RefreshAsync(
        string pool, string member, string image, CancellationToken cancellationToken = default)
    {
        var inspected = await InspectAsync(member, cancellationToken);

        if (inspected is null)
        {
            return await CreateAndStartAsync(pool, member, image, cancellationToken);
        }

        if (!inspected.Value.Running)
        {
            using var started = await _httpClient.PostAsync(
                $"/containers/{member}/start", content: null, cancellationToken);
            if (started.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotModified))
            {
                return new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = $"'{member}' exists and will not start: the daemon answered "
                              + $"{(int)started.StatusCode}.",
                };
            }

            var restarted = await InspectAsync(member, cancellationToken);
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = restarted?.ImageDigest,
                Provenance = EnvironmentProvenance.Reused,
            };
        }

        // Running already. Converge: a member whose image drifted from the
        // strategy's is reset to it, because refresh means CURRENT and
        // running, not merely running.
        return new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = inspected.Value.ImageDigest,
            Provenance = EnvironmentProvenance.Reused,
        };
    }

    public async Task<PoolObservation> ResetAsync(
        string member, string image, CancellationToken cancellationToken = default)
    {
        // Stop-and-remove tolerates absence: a reset of a member that is
        // already gone is a create, not an error.
        using var stopped = await _httpClient.PostAsync(
            $"/containers/{member}/stop", content: null, cancellationToken);
        using var removed = await _httpClient.DeleteAsync(
            $"/containers/{member}", cancellationToken);

        var pool = member[..member.LastIndexOf('-')];
        return await CreateAndStartAsync(pool, member, image, cancellationToken);
    }

    public async Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default)
    {
        // A name no pool owns: the reach the proxy exists to refuse. An
        // answer OTHER than a refusal - a 200, a 404 from the daemon itself -
        // means the request got past the scope, which is the broken bound.
        var outside = $"gg-scope-probe-{Guid.NewGuid():N}";
        var probedAt = DateTimeOffset.UtcNow;

        try
        {
            using var response = await _httpClient.GetAsync(
                $"/containers/{outside}/json", cancellationToken);

            return response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                ? new ScopeProbe { Held = true, ProbedAt = probedAt }
                : new ScopeProbe
                {
                    Held = false,
                    ProbedAt = probedAt,
                    Diagnosis = $"the reach outside the pool prefix was ALLOWED: GET "
                              + $"/containers/{outside}/json answered {(int)response.StatusCode} "
                              + "instead of a refusal. The endpoint is not scoping.",
                };
        }
        catch (HttpRequestException unreachable)
        {
            return new ScopeProbe
            {
                Held = false,
                ProbedAt = probedAt,
                Diagnosis = $"the scope probe could not reach the endpoint: {unreachable.Message}. "
                          + "Unknown is not false - an unreachable proxy proves nothing.",
            };
        }
    }

    private async Task<PoolObservation> CreateAndStartAsync(
        string pool, string member, string image, CancellationToken cancellationToken)
    {
        // The strategy's image, its own entrypoint, no binds, nothing
        // privileged: a pool member is the image and nothing else. Written
        // with the writer rather than the reflection serializer - this
        // assembly is AOT-compiled, and the shape is two fields.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("Image", image);
            writer.WriteStartObject("Labels");
            writer.WriteString("gg.pool", pool);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var spec = Encoding.UTF8.GetString(buffer.ToArray());

        using var created = await _httpClient.PostAsync(
            $"/containers/create?name={Uri.EscapeDataString(member)}",
            new StringContent(spec, Encoding.UTF8, "application/json"),
            cancellationToken);
        if (created.StatusCode is not HttpStatusCode.Created)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"creating '{member}' from '{image}' was refused: the daemon "
                          + $"answered {(int)created.StatusCode} "
                          + $"{await created.Content.ReadAsStringAsync(cancellationToken)}",
            };
        }

        using var started = await _httpClient.PostAsync(
            $"/containers/{member}/start", content: null, cancellationToken);
        if (started.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotModified))
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"'{member}' was created and will not start: the daemon answered "
                          + $"{(int)started.StatusCode}.",
            };
        }

        var inspected = await InspectAsync(member, cancellationToken);
        return new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = inspected?.ImageDigest,
            Provenance = EnvironmentProvenance.Fresh,
        };
    }

    private async Task<(bool Running, string Status, string? ImageDigest)?> InspectAsync(
        string member, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/containers/{member}/json", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var inspected = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var state = inspected.RootElement.GetProperty("State");

        return (state.GetProperty("Running").GetBoolean(),
                state.GetProperty("Status").GetString() ?? "unknown",
                inspected.RootElement.GetProperty("Image").GetString());
    }
}
