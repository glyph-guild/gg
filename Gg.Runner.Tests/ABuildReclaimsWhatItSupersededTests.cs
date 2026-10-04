using System.Net;
using System.Text.Json;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A build removes this repository's images that nothing pins and nothing
/// runs, so the bake-and-roll pipeline stops filling the pool host's disk.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured, not supposed.</b> This fleet's pool host reached 2.2 G free of
/// 61 three times, with member images at 1.4 G and 4.2 G apiece and nothing on
/// the host reclaiming any of them. It also bounds what can ever be baked: an
/// image carrying a real application would be 8-10 G a version, which is two
/// bakes before the host dies.
/// </para>
/// <para>
/// <b>Here rather than on the roll.</b> <see cref="IPoolAdapter"/> is fenced to
/// <c>/containers/</c> on purpose - its own remark says a 403 from
/// <c>/images/</c> read as drift resets every member every sweep. The builder
/// already writes images, so it is the port that may, and a build reclaims
/// what the build before it superseded: one behind, and enough, because the
/// steady state is then two images per repository rather than all of them.
/// </para>
/// <para>
/// <b>The daemon enforces the half that matters.</b> A delete without
/// <c>force</c> is refused for an image a container is using, so a member that
/// is up cannot lose what it is running even if this is asked wrongly. These
/// run against a fake daemon because the claims are about what is ASKED - that
/// <c>force</c> is never sent is not observable from a successful outcome.
/// </para>
/// </remarks>
public class ABuildReclaimsWhatItSupersededTests
{
    private const string Repository = "127.0.0.1:5000/gg-member";

    private const string Keep = "sha256:aaaa000000000000000000000000000000000000000000000000000000000000";

    private const string Superseded = "sha256:bbbb000000000000000000000000000000000000000000000000000000000000";

    private const string InUse = "sha256:cccc000000000000000000000000000000000000000000000000000000000000";

    /// <summary>
    /// A daemon that answers a listing and records every request, so a test can
    /// assert on what was asked rather than only on what came back.
    /// </summary>
    private sealed class Daemon : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri)> Asked { get; } = [];

        /// <summary>Id to Size, in the order the daemon lists them.</summary>
        public Dictionary<string, long> Images { get; } = new(StringComparer.Ordinal);

        /// <summary>Ids the daemon refuses to delete, and with what.</summary>
        public Dictionary<string, HttpStatusCode> Refuses { get; } = new(StringComparer.Ordinal);

        public HttpStatusCode Listing { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.PathAndQuery;
            Asked.Add((request.Method, uri));

            if (uri.StartsWith("/images/json", StringComparison.Ordinal))
            {
                if (Listing is not HttpStatusCode.OK)
                {
                    return Task.FromResult(new HttpResponseMessage(Listing));
                }

                var body = JsonSerializer.Serialize(
                    Images.Select(i => new { Id = i.Key, Size = i.Value }));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body),
                });
            }

            var id = Uri.UnescapeDataString(uri["/images/".Length..]);

            return Task.FromResult(new HttpResponseMessage(
                Refuses.TryGetValue(id, out var refusal) ? refusal : HttpStatusCode.OK));
        }
    }

    private static DockerPoolAdapter Adapter(Daemon daemon) =>
        new(new HttpClient(daemon) { BaseAddress = new Uri("http://daemon.test") });

    [Test]
    public async Task Everything_in_the_repository_but_the_kept_digests_is_removed()
    {
        var daemon = new Daemon
        {
            Images = { [Keep] = 1_400_000_000, [Superseded] = 1_500_000_000 },
        };

        var reclaimed = await Adapter(daemon).ReclaimImagesAsync(Repository, [Keep]);

        await Assert.That(reclaimed.Removed).IsEqualTo(1);
        await Assert.That(reclaimed.Freed).IsEqualTo(1_500_000_000L)
            .Because("what a reclaim freed is the only number that says whether it was worth "
                   + "doing, and a host at 2.2 G free is the reason it exists.");
        await Assert.That(reclaimed.Refused).IsNull();

        await Assert.That(daemon.Asked.Where(a => a.Method == HttpMethod.Delete).Select(a => a.Uri))
            .IsEquivalentTo([$"/images/{Uri.EscapeDataString(Superseded)}"])
            .Because("the kept digest is the one the pool is about to roll onto.");
    }

    [Test]
    public async Task The_listing_is_scoped_to_the_repository_asked_for()
    {
        var daemon = new Daemon();

        _ = await Adapter(daemon).ReclaimImagesAsync(Repository, [Keep]);

        var listing = daemon.Asked.Single(a => a.Method == HttpMethod.Get).Uri;

        await Assert.That(Uri.UnescapeDataString(listing)).Contains($$"""{"reference":[{"{{Repository}}":true}]}""")
            .Because("an unscoped reclaim on a host that also builds something else would take "
                   + "an image this pool never made. The repository is the whole bound.");
    }

    [Test]
    public async Task A_delete_is_never_forced()
    {
        var daemon = new Daemon
        {
            Images = { [Superseded] = 1, [InUse] = 2 },
        };

        _ = await Adapter(daemon).ReclaimImagesAsync(Repository, []);

        // ASSERTED AS THE WHOLE URI, not as the absence of the word. A test that
        // only looked for 'force' would pass against an implementation that
        // deleted nothing at all - it would report an absence it had caused.
        await Assert.That(daemon.Asked.Where(a => a.Method == HttpMethod.Delete).Select(a => a.Uri))
            .IsEquivalentTo([
                $"/images/{Uri.EscapeDataString(Superseded)}",
                $"/images/{Uri.EscapeDataString(InUse)}",
            ])
            .Because("without force the daemon refuses an image a container is using, and that "
                   + "refusal is the guard that keeps a running member's image. Forcing would "
                   + "throw away the only thing standing between housekeeping and an outage.");
    }

    [Test]
    public async Task An_image_a_container_is_using_is_left_and_is_not_a_refusal()
    {
        var daemon = new Daemon
        {
            Images = { [Superseded] = 1_500_000_000, [InUse] = 1_400_000_000 },
            Refuses = { [InUse] = HttpStatusCode.Conflict },
        };

        var reclaimed = await Adapter(daemon).ReclaimImagesAsync(Repository, []);

        await Assert.That(reclaimed.Removed).IsEqualTo(1);
        await Assert.That(reclaimed.Freed).IsEqualTo(1_500_000_000L)
            .Because("an image that was refused freed nothing, and counting it would report a "
                   + "disk that is not there.");
        await Assert.That(reclaimed.Refused).IsNull()
            .Because("a conflict is the daemon saying a container is using this, which is "
                   + "exactly the image this must not take - the ordinary answer, not a fault.");
    }

    [Test]
    public async Task A_daemon_that_will_not_list_images_is_reported_and_takes_nothing()
    {
        var daemon = new Daemon { Listing = HttpStatusCode.Forbidden };

        var reclaimed = await Adapter(daemon).ReclaimImagesAsync(Repository, [Keep]);

        await Assert.That(reclaimed.Removed).IsEqualTo(0);
        await Assert.That(reclaimed.Refused!).Contains("403")
            .Because("the pull point refusing /images/ is a configuration this fleet really "
                   + "has; a host that will not show its images keeps its disk, and says so.");
        await Assert.That(daemon.Asked.Where(a => a.Method == HttpMethod.Delete)).IsEmpty();
    }

    [Test]
    public async Task Anything_other_than_a_conflict_is_reported()
    {
        var daemon = new Daemon
        {
            Images = { [Superseded] = 1 },
            Refuses = { [Superseded] = HttpStatusCode.Forbidden },
        };

        var reclaimed = await Adapter(daemon).ReclaimImagesAsync(Repository, []);

        await Assert.That(reclaimed.Removed).IsEqualTo(0);
        await Assert.That(reclaimed.Refused!).Contains("403")
            .Because("a disk that silently stops being reclaimed is how it filled three times.");
    }
}
