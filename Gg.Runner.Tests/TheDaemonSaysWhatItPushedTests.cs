using System.Net;
using System.Text;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The digest a push produced is read from what this daemon actually says,
/// which is not always where the API reference puts it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found on slice forty-one's walk, 2026-09-18.</b> The first build on
/// vmlinux001 fetched, built and pushed - the image was in the registry - and
/// attested failed: "the registry accepted the push without naming a digest".
/// Docker 29.7.2 on the containerd image store ends a push with a status line,
/// <c>&lt;tag&gt;: digest: sha256:&lt;hex&gt; size: &lt;n&gt;</c>, and sends no
/// <c>aux</c> object at all. The adapter read only <c>aux.Digest</c>.
/// </para>
/// <para>
/// <b>Why no test saw it.</b> Every test of the maintainer's build replaced
/// the builder with a fake that answered with a digest, so the one parse that
/// meets a real daemon was never run against a real daemon's words. These are
/// those words, recorded from the walk.
/// </para>
/// </remarks>
public class TheDaemonSaysWhatItPushedTests
{
    private const string Digest = "sha256:065b4f514b30e2da0ce31c6c096cbe114c701e827462e1325dc4118a087cff9d";

    /// <summary>A daemon that answers one push with a recorded stream.</summary>
    private sealed class Replaying(string stream) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "application/json"),
            });
    }

    private static Task<ImagePushed> PushAsync(string stream) =>
        new DockerPoolAdapter(new HttpClient(new Replaying(stream)) { BaseAddress = new Uri("http://proxy.test") })
            .PushImageAsync("127.0.0.1:5000/gg-member", "762f1aac04f9");

    [Test]
    public async Task A_containerd_store_names_the_digest_in_its_last_status_line()
    {
        // RECORDED on vmlinux001, Docker 29.7.2, containerd snapshotter.
        var pushed = await PushAsync(
            """
            {"status":"The push refers to repository [127.0.0.1:5000/gg-member]"}
            {"status":"Layer already exists","progressDetail":{},"id":"23f0cc88f6cf"}
            {"status":"Layer already exists","progressDetail":{},"id":"0926a8eb0e60"}
            {"status":"762f1aac04f9: digest: sha256:065b4f514b30e2da0ce31c6c096cbe114c701e827462e1325dc4118a087cff9d size: 1409"}

            """);

        await Assert.That(pushed).IsEqualTo(new ImagePushed.Pushed(Digest))
            .Because("the registry named the digest; it said so in the status line rather "
                   + "than in an aux object, and an image that is in the registry is not a "
                   + "failed push.");
    }

    [Test]
    public async Task A_classic_store_still_names_it_in_aux()
    {
        var pushed = await PushAsync(
            """
            {"status":"Pushed","progressDetail":{},"id":"0926a8eb0e60"}
            {"status":"762f1aac04f9: digest: sha256:065b4f514b30e2da0ce31c6c096cbe114c701e827462e1325dc4118a087cff9d size: 1409"}
            {"progressDetail":{},"aux":{"Tag":"762f1aac04f9","Digest":"sha256:065b4f514b30e2da0ce31c6c096cbe114c701e827462e1325dc4118a087cff9d","Size":1409}}

            """);

        await Assert.That(pushed).IsEqualTo(new ImagePushed.Pushed(Digest));
    }

    [Test]
    public async Task A_status_line_about_another_tag_names_nothing()
    {
        // THE TAG IS PART OF THE ANSWER. A digest line for some other tag is not
        // what this push produced, and pinning it would pin somebody else's image.
        var pushed = await PushAsync(
            """
            {"status":"latest: digest: sha256:065b4f514b30e2da0ce31c6c096cbe114c701e827462e1325dc4118a087cff9d size: 1409"}

            """);

        await Assert.That(pushed).IsTypeOf<ImagePushed.Failed>();
    }

    [Test]
    public async Task An_error_in_the_stream_is_a_failed_push_whatever_else_it_said()
    {
        var pushed = await PushAsync(
            """
            {"status":"762f1aac04f9: digest: sha256:065b4f514b30e2da0ce31c6c096cbe114c701e827462e1325dc4118a087cff9d size: 1409"}
            {"errorDetail":{"message":"unauthorized"},"error":"unauthorized"}

            """);

        await Assert.That(pushed).IsTypeOf<ImagePushed.Failed>();
    }
}
