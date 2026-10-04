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

        if (Split(reference) is not var (registry, repository, tag))
        {
            return new ManifestRemoved
            {
                Refused = $"'{reference}' is not a registry/repository:tag reference.",
            };
        }

        var manifests = $"{registry}/v2/{repository}/manifests/";

        using var head = new HttpRequestMessage(HttpMethod.Head, manifests + tag);

        // EVERY TYPE AN INDEX MAY BE. This fleet's daemon pushes an OCI index;
        // a request offering only the Docker v2 manifest type is answered 200
        // with no digest header, which is indistinguishable from a tag that is
        // not there. Measured against the real registry before this was built.
        foreach (var manifest in Manifests)
        {
            head.Headers.Add("Accept", manifest);
        }

        using var found = await _httpClient.SendAsync(head, cancellationToken);

        // A TAG THE REGISTRY DOES NOT HAVE IS NOT A PROBLEM. The two stores can
        // disagree - an image built and removed locally without ever being
        // pushed has no manifest here - and saying so would be noise on every
        // build of a recipe that failed its push once.
        if (found.StatusCode is HttpStatusCode.NotFound)
        {
            return new ManifestRemoved();
        }

        if (!found.IsSuccessStatusCode)
        {
            return new ManifestRemoved
            {
                Refused = $"the registry would not resolve {repository}:{tag} "
                        + $"(HTTP {(int)found.StatusCode}).",
            };
        }

        if (found.Headers.TryGetValues("Docker-Content-Digest", out var named)
            && named.FirstOrDefault() is { Length: > 0 } digest)
        {
            // BY DIGEST, BECAUSE A TAG CANNOT BE DELETED. The protocol has no
            // delete-a-tag; removing the manifest removes every tag pointing at
            // it, which is why this resolves rather than guessing.
            using var deleted = await _httpClient.DeleteAsync(
                manifests + digest, cancellationToken);

            return deleted.IsSuccessStatusCode
                ? new ManifestRemoved { Digest = digest }

                // 405 IS A HOST, NOT A BUILD. A registry started on the image's
                // default config carries no storage.delete.enabled and answers
                // this to every delete, which is how 12 GB accumulated with
                // nothing reporting a fault. The sentence names what to change.
                : new ManifestRemoved
                {
                    Refused = deleted.StatusCode is HttpStatusCode.MethodNotAllowed
                        ? $"the registry does not allow deletes (HTTP 405): it was started "
                        + "without storage.delete.enabled, so it keeps every image this host "
                        + "has ever built. See deploy/pool-host/README.md."
                        : $"the registry would not remove {repository}:{tag} "
                        + $"(HTTP {(int)deleted.StatusCode}).",
                };
        }

        // A 200 WITH NO DIGEST HEADER, which is the answer an unacceptable
        // media type gives. Deleting on the strength of it would build a url
        // ending in an empty digest, which answers 404 - a tidy-up that never
        // works and never says so.
        return new ManifestRemoved
        {
            Refused = $"the registry answered for {repository}:{tag} without naming a digest, "
                    + "so there is nothing to remove by.",
        };
    }

    /// <summary>
    /// A reference split into the registry to call, the repository, and the tag.
    /// </summary>
    /// <remarks>
    /// <b>The scheme is decided here and nowhere else.</b> Docker itself treats
    /// a loopback registry as insecure, which is why the pool host's one needs
    /// no certificate and no daemon setting; anything else is https, because a
    /// delete sent in clear text to somebody else's registry is a different
    /// thing entirely. The tag is taken from the LAST colon - the registry's
    /// own port carries one too.
    /// </remarks>
    private static (string Registry, string Repository, string Tag)? Split(string reference)
    {
        var slash = reference.IndexOf('/');
        var colon = reference.LastIndexOf(':');

        if (slash < 1 || colon < slash)
        {
            return null;
        }

        var host = reference[..slash];
        var loopback = host.StartsWith("127.0.0.1", StringComparison.Ordinal)
                    || host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                    || host.StartsWith("[::1]", StringComparison.Ordinal);

        return ($"{(loopback ? "http" : "https")}://{host}",
                reference[(slash + 1)..colon],
                reference[(colon + 1)..]);
    }
}
