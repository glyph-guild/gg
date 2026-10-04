using System.Net;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The host's registry is the second store a build writes, and the larger one.
/// Removing a superseded image from the daemon leaves the registry holding it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on this fleet's pool host:</b> 12.05 GB over 25 tags, two of
/// them pinned, against 6 GB of daemon images. A push writes both stores and
/// only one of them was ever read back.
/// </para>
/// <para>
/// <b>Two calls, because a tag cannot be deleted.</b> The protocol's delete
/// takes a digest, so a tag is resolved with a <c>HEAD</c> first. The Accept
/// header decides whether that answers at all: the containerd image store
/// pushes an OCI <i>index</i>, and a request offering only the Docker v2
/// manifest type gets a 200 with no digest header - which reads exactly like
/// a tag that is not there.
/// </para>
/// </remarks>
public class ASupersededManifestLeavesTheRegistryTests
{
    private const string Reference = "127.0.0.1:5000/gg-member:1c26776e2b1f";

    private const string Digest =
        "sha256:71e1ec089205aa71cf6b5a70c94e4518f241e1f8fb9cd4272e8c7075abf3d496";

    /// <summary>A registry that records what was asked and answers as one does.</summary>
    private sealed class Registry : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string[] Accept)> Asked { get; } = [];

        /// <summary>What the HEAD answers with. Null means no digest header at all.</summary>
        public string? Resolves { get; set; } = Digest;

        public HttpStatusCode Heading { get; set; } = HttpStatusCode.OK;

        /// <summary>405 is a registry started on the image's default config.</summary>
        public HttpStatusCode Deleting { get; set; } = HttpStatusCode.Accepted;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add((
                request.Method,
                request.RequestUri!.ToString(),
                [.. request.Headers.Accept.Select(a => a.MediaType!)]));

            if (request.Method == HttpMethod.Head)
            {
                var answer = new HttpResponseMessage(Heading);

                if (Resolves is { } digest)
                {
                    answer.Headers.Add("Docker-Content-Digest", digest);
                }

                return Task.FromResult(answer);
            }

            return Task.FromResult(new HttpResponseMessage(Deleting));
        }
    }

    private static OciRegistry Under(Registry registry) =>
        new(new HttpClient(registry));

    [Test]
    public async Task A_tag_is_resolved_to_a_digest_and_that_digest_is_removed()
    {
        var registry = new Registry();

        var removed = await Under(registry).RemoveManifestAsync(Reference);

        await Assert.That(removed.Digest).IsEqualTo(Digest);
        await Assert.That(removed.Refused).IsNull();

        await Assert.That(registry.Asked.Select(a => (a.Method.Method, a.Uri))).IsEquivalentTo([
            ("HEAD", "http://127.0.0.1:5000/v2/gg-member/manifests/1c26776e2b1f"),
            ("DELETE", $"http://127.0.0.1:5000/v2/gg-member/manifests/{Digest}"),
        ]).Because("the protocol's delete takes a digest; removing the manifest removes every "
                 + "tag pointing at it, which is why nothing deletes by tag and hopes.");
    }

    [Test]
    public async Task The_head_offers_every_manifest_type_an_index_may_be()
    {
        var registry = new Registry();

        _ = await Under(registry).RemoveManifestAsync(Reference);

        var accept = registry.Asked.First(a => a.Method == HttpMethod.Head).Accept;

        await Assert.That(accept).Contains("application/vnd.oci.image.index.v1+json")
            .Because("this fleet's daemon pushes an OCI index. A request offering only the "
                   + "Docker v2 manifest type gets a 200 with no digest header, which reads "
                   + "exactly like a tag that is not there - measured against the real "
                   + "registry on vmlinux001 before this was written.");
        await Assert.That(accept).Contains("application/vnd.docker.distribution.manifest.v2+json");
    }

    [Test]
    public async Task A_loopback_registry_is_reached_without_tls()
    {
        // Docker itself treats a loopback registry as insecure, which is why
        // the pool host's one needs no certificate and no daemon setting. An
        // https request to it would simply not connect.
        var registry = new Registry();

        _ = await Under(registry).RemoveManifestAsync(Reference);

        await Assert.That(registry.Asked.Select(a => a.Uri).Distinct().All(u => u.StartsWith("http://")))
            .IsTrue();
    }

    [Test]
    public async Task Any_other_registry_is_reached_over_tls()
    {
        // THE POISON TWIN. A rule that answered http for everything would pass
        // every test above and send a delete for somebody else's registry in
        // clear text the first time a pool pinned from one.
        var registry = new Registry();

        _ = await Under(registry).RemoveManifestAsync("ghcr.io/glyph-guild/gg-member:abc");

        await Assert.That(registry.Asked[0].Uri)
            .StartsWith("https://ghcr.io/v2/glyph-guild/gg-member/manifests/abc");
    }

    [Test]
    public async Task A_registry_that_refuses_deletes_says_so_and_removes_nothing()
    {
        // 405 UNSUPPORTED is a registry started on the image's default config -
        // a host that has not been reconfigured, not a build that went wrong.
        var registry = new Registry { Deleting = HttpStatusCode.MethodNotAllowed };

        var removed = await Under(registry).RemoveManifestAsync(Reference);

        await Assert.That(removed.Digest).IsNull();
        await Assert.That(removed.Refused!).Contains("405");
        await Assert.That(removed.Refused!).Contains("delete")
            .Because("the sentence has to name what to change. A host whose registry silently "
                   + "kept every image is how 12 GB accumulated unnoticed.");
    }

    [Test]
    public async Task A_tag_the_registry_does_not_have_is_not_a_refusal()
    {
        // The daemon and the registry can disagree: an image built and removed
        // locally without ever being pushed has no manifest to remove. Nothing
        // is wrong and nothing should be said.
        var registry = new Registry { Heading = HttpStatusCode.NotFound, Resolves = null };

        var removed = await Under(registry).RemoveManifestAsync(Reference);

        await Assert.That(removed.Digest).IsNull();
        await Assert.That(removed.Refused).IsNull();
        await Assert.That(registry.Asked.Where(a => a.Method == HttpMethod.Delete)).IsEmpty();
    }

    [Test]
    public async Task A_head_that_answers_without_a_digest_removes_nothing()
    {
        // THE SHAPE THAT COST THE MOST TIME against the real registry: a 200
        // with no Docker-Content-Digest. Deleting on the strength of it would
        // mean building a DELETE url with an empty digest, which a registry
        // answers 404 to - a tidy-up that silently never works.
        var registry = new Registry { Resolves = null };

        var removed = await Under(registry).RemoveManifestAsync(Reference);

        await Assert.That(removed.Digest).IsNull();
        await Assert.That(removed.Refused!).Contains("digest");
        await Assert.That(registry.Asked.Where(a => a.Method == HttpMethod.Delete)).IsEmpty();
    }
}
