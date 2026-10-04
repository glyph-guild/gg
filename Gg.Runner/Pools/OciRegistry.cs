using System.Net;

namespace Gg.Runner.Pools;

/// <summary>
/// The host's own registry, spoken to over the OCI distribution protocol.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two calls, because a tag cannot be deleted.</b> The protocol's delete
/// takes a digest, so a tag is first resolved with a <c>HEAD</c> whose
/// <c>Docker-Content-Digest</c> header names the manifest - and removing that
/// manifest removes every tag pointing at it, which is why nothing here
/// deletes by tag and hopes.
/// </para>
/// <para>
/// <b>The Accept header decides whether the HEAD answers at all.</b> The
/// containerd image store pushes an OCI <i>index</i>; a request that offers
/// only the Docker v2 manifest type gets a 200 with no digest header, which
/// reads exactly like a tag that is not there. All four media types are
/// offered for that reason.
/// </para>
/// </remarks>
public sealed class OciRegistry(HttpClient httpClient) : IImageRegistry
{
    private readonly HttpClient _httpClient = httpClient;

    /// <summary>
    /// What a manifest may be. An index first, because that is what this
    /// fleet's daemon writes.
    /// </summary>
    private static readonly string[] Manifests =
    [
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.v2+json",
    ];

    public async Task<ManifestRemoved> RemoveManifestAsync(
        string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var at = reference.LastIndexOf(':');
        var slash = reference.IndexOf('/');

        using var head = new HttpRequestMessage(
            HttpMethod.Head,
            $"{reference[..slash]}/v2/{reference[(slash + 1)..at]}/manifests/{reference[(at + 1)..]}");

        foreach (var manifest in Manifests)
        {
            head.Headers.Add("Accept", manifest);
        }

        using var found = await _httpClient.SendAsync(head, cancellationToken);

        return new ManifestRemoved { Refused = "removing is not built yet" };
    }
}
