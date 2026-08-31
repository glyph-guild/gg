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

    /// <summary>
    /// A SECOND pinned image, and the drift test needs one that is really
    /// different: the whole claim is that the daemon reports what a container
    /// was made FROM, and one image cannot show that.
    /// </summary>
    private static string OtherImage =>
        Environment.GetEnvironmentVariable("GG_POOL_TEST_IMAGE_B")
        ?? throw new InvalidOperationException(
            "GG_POOL_TEST_IMAGE_B is not set. Convergence needs two pinned images that are "
          + "both present locally - the pull point refuses /images/, so the daemon cannot "
          + "fetch one mid-test.");

    private static DockerPoolAdapter Adapter() =>
        new(new HttpClient { BaseAddress = new Uri(Endpoint) });

    /// <summary>A member from a previous run is a different test's state, not this one's.</summary>
    private static async Task ClearAsync(string member)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Endpoint) };
        using var _ = await http.DeleteAsync($"/containers/{member}?force=true");
    }

    [Test]
    public async Task Refresh_creates_an_absent_member_and_reports_it_fresh()
    {
        var adapter = Adapter();
        await ClearAsync("gg-e2e-pool-1");

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
        await ClearAsync("gg-e2e-pool-2");
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
    public async Task A_member_made_from_another_image_is_converged_against_a_real_daemon()
    {
        // THE ROW THAT NEEDS A DAEMON. The fake in ImageConvergenceTests holds
        // the adapter's request sequence; only a real daemon says whether
        // Config.Image is actually the reference a container was created from,
        // and that is the entire premise of the comparison. A row proven
        // against a fake proves the fake.
        var adapter = Adapter();
        await ClearAsync("gg-e2e-pool-3");

        var drifted = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-3", OtherImage);
        await Assert.That(drifted.Outcome).IsEqualTo(PoolOutcomes.Verified)
            .Because("the member has to exist before it can drift: " + drifted.Diagnosis);

        var converged = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-3", Image);

        await Assert.That(converged.Outcome).IsEqualTo(PoolOutcomes.Verified)
            .Because(converged.Diagnosis ?? "converging on the pin should not fail");
        await Assert.That(converged.Provenance).IsEqualTo(EnvironmentProvenance.Fresh)
            .Because("converge means destroy and recreate from the pin, so what comes back "
                   + "is a new container - reused would mean the drifted one was blessed.");
        await Assert.That(converged.ImageDigest).IsNotEqualTo(drifted.ImageDigest)
            .Because("the member is running a different image than it was, which is the "
                   + "difference three doc comments promised and none performed.");
    }

    [Test]
    public async Task A_member_already_on_the_pin_is_not_recreated_by_a_real_daemon()
    {
        // THE ANCHOR, AGAINST THE DAEMON. A convergence that reset every time
        // would satisfy the test above and throw away a warm pool on every
        // sweep - and only a real daemon can say the second refresh left the
        // container alone rather than replacing it with an identical one.
        var adapter = Adapter();
        await ClearAsync("gg-e2e-pool-4");
        _ = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-4", Image);

        var again = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-4", Image);

        await Assert.That(again.Provenance).IsEqualTo(EnvironmentProvenance.Reused)
            .Because("a member already running what the strategy pins IS current; "
                   + "recreating it would discard a warm environment to arrive at the "
                   + "same place, once per sweep, on somebody else's bill.");
    }

    [Test]
    public async Task A_real_member_carries_the_image_digest_variable()
    {
        // THE SEAM, AGAINST A DAEMON. The stand-in proves the create SPEC
        // carries the variable; only a real daemon proves the variable reaches
        // the container's environment, which is where the survey reads it. A
        // spec the daemon silently ignored would satisfy the fake and ship a
        // fact that still said null.
        var adapter = Adapter();
        await ClearAsync("gg-e2e-pool-6");
        var made = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-6", Image);
        await Assert.That(made.Outcome).IsEqualTo(PoolOutcomes.Verified)
            .Because(made.Diagnosis ?? "the member has to exist to be inspected");

        using var http = new HttpClient { BaseAddress = new Uri(Endpoint) };
        using var inspected = await http.GetAsync("/containers/gg-e2e-pool-6/json");
        inspected.EnsureSuccessStatusCode();
        using var body = System.Text.Json.JsonDocument.Parse(
            await inspected.Content.ReadAsStringAsync());

        var environment = body.RootElement.GetProperty("Config").GetProperty("Env")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();

        await Assert.That(environment).Contains(
            $"{Gg.Runner.Facts.EnvironmentSurvey.ImageDigestVariable}={Image}")
            .Because("the runner inside this member reads that variable on every fact ship, "
                   + "so this is the difference between a flight that can say which "
                   + "environment it ran in and one that reports null forever.");
    }

    [Test]
    public async Task The_listing_sees_only_the_pool_prefix()
    {
        var adapter = Adapter();
        // ITS OWN MEMBER, and it did not have one. This shared gg-e2e-pool-1
        // with the create test, which force-DELETES that name - so run in
        // parallel one refresh raced the other's delete and attested failed.
        // Nobody noticed because CI never runs the RealDocker category, which
        // is the same reason the arm this file now covers was never exercised.
        _ = await adapter.RefreshAsync("gg-e2e-pool", "gg-e2e-pool-5", Image);

        var members = await adapter.ListAsync("gg-e2e-pool");

        await Assert.That(members).IsNotEmpty();
        await Assert.That(members.All(m => m.Name.StartsWith("gg-e2e-pool-"))).IsTrue()
            .Because("the pool is the inventory; a member from outside the prefix in this "
                   + "list would be the adapter widening its own scope.");
    }
}
