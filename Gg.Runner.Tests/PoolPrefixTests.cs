using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The name a pool may carry is a contract with the proxy, held on both sides.
/// </summary>
/// <remarks>
/// <para>
/// <b>The proxy's allowlist named the WALK.</b> Its create rule refused any
/// name not starting <c>gg-e2e-pool-</c>, and <c>MaintainLoop</c> mints members
/// as <c>{pool}-{slot}</c> — so only a pool literally called
/// <c>gg-e2e-pool</c> could ever create one. Every other pool was refused at
/// create, and nothing had noticed because the walk is the only thing that has
/// ever used it.
/// </para>
/// <para>
/// <b>And the failure was indistinguishable from the control working.</b> A 403
/// from that proxy is exactly what a correct refusal looks like — it is what
/// the scope probe asks for and treats as success. A misnamed pool and an
/// out-of-scope reach produced the same answer, so the bound could not be told
/// from the bug.
/// </para>
/// <para>
/// <b>So the prefix is reserved and enforced on BOTH sides.</b> The runner
/// refuses a pool that cannot pass the proxy, before it asks the proxy
/// anything; the proxy allows exactly what the runner may mint. After this, a
/// 403 means one thing only: something reached outside the scope.
/// </para>
/// </remarks>
public class PoolPrefixTests
{
    [Test]
    public async Task A_pool_that_could_never_pass_the_proxy_is_refused_before_asking_it()
    {
        // BEFORE, not after. Asking and being refused is a 403 that reads like
        // the scope bound working, which is the confusion this exists to end.
        var thrown = Assert.Throws<ArgumentException>(
            () => PoolNaming.Require("someones-pool"));

        await Assert.That(thrown!.Message).Contains("someones-pool");
        await Assert.That(thrown.Message).Contains(PoolNaming.ReservedPrefix)
            .Because("a refusal that does not name the prefix leaves somebody guessing at a "
                   + "convention nothing in their pool's name hints at.");
    }

    [Test]
    public async Task A_pool_carrying_the_reserved_prefix_is_accepted()
    {
        await Assert.That(PoolNaming.Require($"{PoolNaming.ReservedPrefix}staging"))
            .IsEqualTo($"{PoolNaming.ReservedPrefix}staging");
    }

    [Test]
    public async Task Every_member_of_an_accepted_pool_still_carries_the_prefix()
    {
        // Members are {pool}-{slot}. The property the proxy's create rule
        // depends on is that a member of an accepted pool is itself accepted -
        // if that ever stopped holding, warming would 403 while listing worked.
        var pool = PoolNaming.Require($"{PoolNaming.ReservedPrefix}staging");

        await Assert.That($"{pool}-1".StartsWith(PoolNaming.ReservedPrefix, StringComparison.Ordinal))
            .IsTrue();
    }

    [Test]
    public async Task The_proxy_allows_exactly_what_the_runner_may_mint()
    {
        // THE TWO SIDES, TIED. This is the assertion that would have caught the
        // original: the config named a prefix the runner does not use, and no
        // test compared them.
        var config = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "pool-proxy", "nginx.conf"));

        await Assert.That(config).Contains(PoolNaming.ReservedPrefix)
            .Because("the proxy must allow what the runner mints, or every pool is refused at "
                   + "create by a bound that looks like it is working.");
        await Assert.That(config).DoesNotContain("gg-e2e-pool")
            .Because("naming the walk's fixture in a deployment artefact is what tied every "
                   + "deployment to the one pool the walk happens to use.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("repository root not found")).FullName;
    }
}
