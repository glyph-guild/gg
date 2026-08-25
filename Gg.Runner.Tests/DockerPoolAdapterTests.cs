using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The Docker adapter against a real daemon behind a real proxy: reset
/// destroys and recreates from a pinned image, refresh creates the absent,
/// and the listing sees only the pool's own prefix.
/// </summary>
/// <remarks>
/// <b>real docker, one host.</b> A pool row proven against a fake proves the
/// fake — the adapter's whole job is the daemon's actual answers. Gated on
/// <c>GG_POOL_ENDPOINT</c>, and the accessor THROWS rather than skips
/// (RealAgent's precedent): skipping would leave the slice's infrastructure
/// bet unverified. Stand the proxy up with scripts/pool-proxy first.
/// </remarks>
[Category("RealDocker")]
public class DockerPoolAdapterTests
{
    private static string Endpoint =>
        Environment.GetEnvironmentVariable("GG_POOL_ENDPOINT")
        ?? throw new InvalidOperationException(
            "GG_POOL_ENDPOINT is not set. These drive a real daemon through a real proxy; "
          + "skipping them would leave the scope bet unverified. See scripts/pool-proxy.");

    private static string Image =>
        Environment.GetEnvironmentVariable("GG_POOL_TEST_IMAGE")
        ?? throw new InvalidOperationException(
            "GG_POOL_TEST_IMAGE is not set. Pin it by digest (name@sha256:...) - what reset "
          + "resets TO must be a fixed point, in the test as in the strategy.");

    private static DockerPoolAdapter Adapter() =>
        new(new HttpClient { BaseAddress = new Uri(Endpoint) });

    [Test]
    public async Task Refresh_creates_an_absent_member_and_reports_it_fresh()
    {
        var adapter = Adapter();

        var observation = await adapter.RefreshAsync(
            "gg-e2e-pool", "gg-e2e-pool-1", Image);

        await Assert.That(observation.Outcome).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(observation.Provenance).IsEqualTo(EnvironmentProvenance.Fresh);
        await Assert.That(observation.ImageDigest!).Contains("sha256:")
            .Because("the attestation carries what the member converged to, from the "
                   + "daemon's own inspect - never from what was asked for.");
    }

    [Test]
    public async Task Reset_destroys_and_recreates_from_the_pinned_image()
    {
        var adapter = Adapter();
        _ = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-2", Image);
        var before = await adapter.VerifyAsync(new PoolMember { Name = "gg-e2e-pool-2" });

        var observation = await adapter.ResetAsync("gg-e2e-pool-2", Image);

        await Assert.That(observation.Outcome).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(observation.Provenance).IsEqualTo(EnvironmentProvenance.Fresh)
            .Because("a reset member is a new container whatever its name says - fresh is "
                   + "what makes a reused environment trustworthy again.");
        await Assert.That(observation.ImageDigest).IsEqualTo(before.ImageDigest);
    }

    [Test]
    public async Task The_listing_sees_only_the_pool_prefix()
    {
        var adapter = Adapter();
        _ = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-1", Image);

        var members = await adapter.ListAsync("gg-e2e-pool");

        await Assert.That(members).IsNotEmpty();
        await Assert.That(members.All(m => m.Name.StartsWith("gg-e2e-pool-"))).IsTrue()
            .Because("the pool is the inventory; a member from outside the prefix in this "
                   + "list would be the adapter widening its own scope.");
    }
}
