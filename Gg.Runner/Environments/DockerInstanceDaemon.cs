using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Runner.Environments;

/// <summary>
/// One environment instance's own daemon, reached over the socket its user owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>A raw HttpClient over a unix socket, deliberately</b> — the reason
/// <c>DockerPoolAdapter</c> gives for the same choice: no Docker client package,
/// because a package is a dependency that can reach further than the four calls
/// this makes.
/// </para>
/// <para>
/// <b>Bound to ONE path by construction, which is the safety property.</b> The
/// connect callback dials the socket this instance was granted and nothing else,
/// so an unreachable instance is a connect error rather than a reclaim that
/// quietly emptied whatever an ambient <c>DOCKER_HOST</c> pointed at. On a pool
/// host that would be every member on the machine — slice fifty-six S56.2-03,
/// and the reason it is a construction rather than a check.
/// </para>
/// <para>
/// <b>It can remove containers, networks and volumes, and nothing else.</b> No
/// method here touches an image, because warmth is the image store (rule 4).
/// </para>
/// </remarks>
public sealed class DockerInstanceDaemon : IInstanceDaemon, IDisposable
{
    /// <summary>
    /// The host part of the URI, which a unix-socket client never resolves.
    /// </summary>
    /// <remarks>
    /// The connect callback decides where the bytes go, so this is only what
    /// <c>HttpClient</c> requires in an absolute address. `localhost` would read
    /// as a claim about the network; this reads as the placeholder it is.
    /// </remarks>
    private const string Nowhere = "http://instance";

    private readonly HttpClient _httpClient;

    private readonly bool _owned;

    /// <summary>The real one, dialling the instance's own socket.</summary>
    /// <param name="socketPath">
    /// The filesystem path of the daemon's socket — what
    /// <c>EnvironmentNaming.SocketFor</c> derives, without its <c>unix://</c>
    /// scheme.
    /// </param>
    public static DockerInstanceDaemon At(string socketPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);

        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(
                    AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                try
                {
                    await socket.ConnectAsync(
                        new UnixDomainSocketEndPoint(socketPath), cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        return new DockerInstanceDaemon(
            new HttpClient(handler) { BaseAddress = new Uri(Nowhere) }, owned: true);
    }

    /// <param name="httpClient">A client already pointed at one daemon.</param>
    /// <param name="owned">Whether disposing this disposes the client.</param>
    public DockerInstanceDaemon(HttpClient httpClient, bool owned = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _owned = owned;
    }

    public async Task<IReadOnlyList<string>> ContainersAsync(
        CancellationToken cancellationToken = default)
    {
        // all=true, because a STOPPED container still holds its name and its
        // published ports on restart, and Aspire derives names from the checkout
        // path - so listing only the running ones leaves exactly the collisions
        // a reclaim exists to prevent.
        var listed = await _httpClient.GetFromJsonAsync(
            "/containers/json?all=true",
            DaemonJson.Default.ListContainerArray,
            cancellationToken);

        return [.. (listed ?? []).Select(one => one.Id).Where(id => id is { Length: > 0 })!];
    }

    public async Task StopContainerAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // t=10, the daemon's own default, stated rather than inherited: it sends
        // the termination signal, waits that many seconds, and kills what has not
        // gone. A longer wait is a flight held open by an app that is not going to
        // exit; a shorter one is a politeness that is not one.
        //
        // 304 is "already stopped", which is not a failure here - a stack whose
        // containers exited on their own is exactly what a clean run leaves.
        using var response = await _httpClient.PostAsync(
            $"/containers/{id}/stop?t=10", content: null, cancellationToken);

        if (response.StatusCode is not System.Net.HttpStatusCode.NotModified)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task RemoveContainerAsync(
        string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // force=true takes a RUNNING container, which is the ordinary case after
        // a flight died holding its stack up. v=true takes the anonymous volumes
        // that container declared, which nothing else ever will: a named volume
        // is caught by the prune, an anonymous one is reachable only from here.
        using var response = await _httpClient.DeleteAsync(
            $"/containers/{id}?force=true&v=true", cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public Task<int> PruneNetworksAsync(CancellationToken cancellationToken = default) =>
        PrunedAsync("/networks/prune", json => json.NetworksDeleted?.Count ?? 0, cancellationToken);

    public Task<int> PruneVolumesAsync(CancellationToken cancellationToken = default) =>
        PrunedAsync("/volumes/prune", json => json.VolumesDeleted?.Count ?? 0, cancellationToken);

    private async Task<int> PrunedAsync(
        string path, Func<Pruned, int> counted, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(path, content: null, cancellationToken);

        response.EnsureSuccessStatusCode();

        var pruned = await response.Content.ReadFromJsonAsync(
            DaemonJson.Default.Pruned, cancellationToken);

        return pruned is null ? 0 : counted(pruned);
    }

    public void Dispose()
    {
        if (_owned)
        {
            _httpClient.Dispose();
        }
    }
}

/// <summary>One container, as the daemon lists it.</summary>
public sealed record ListContainer
{
    [JsonPropertyName("Id")]
    public string? Id { get; init; }
}

/// <summary>What a prune says it removed.</summary>
public sealed record Pruned
{
    [JsonPropertyName("NetworksDeleted")]
    public IReadOnlyList<string>? NetworksDeleted { get; init; }

    [JsonPropertyName("VolumesDeleted")]
    public IReadOnlyList<string>? VolumesDeleted { get; init; }
}

/// <summary>
/// Source-generated reading of the daemon's replies.
/// </summary>
/// <remarks>
/// Everything here must stay AOT-publishable, so nothing reflects — the same
/// constraint every other serializer in this binary works under.
/// </remarks>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ListContainer[]))]
[JsonSerializable(typeof(Pruned))]
internal sealed partial class DaemonJson : JsonSerializerContext
{
}
