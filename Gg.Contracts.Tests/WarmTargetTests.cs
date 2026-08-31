using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A strategy can declare how many members should be warm BEFORE anything
/// asks — the first number in the inventory that is not a ceiling.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything in a strategy was a maximum.</b> <c>Inventory.Size</c> is how
/// many the pool may hold and <c>Bounds.PoolMax</c> is how many may be warm at
/// once; nothing said how many SHOULD be. So warming could only ever happen
/// behind a flight that was already waiting, which is the reactive half —
/// slice twelve's own pre-committed cut, deferred in writing as <i>"warming
/// ahead of demand to a time-to-warm target"</i>.
/// </para>
/// <para>
/// <b>Zero is a real answer and it is the default.</b> Every strategy written
/// before this member existed reads back and behaves exactly as it did: warm
/// only behind demand. The proactive half is opt-in, which is what makes this
/// a member rather than a value — a member may be added freely and a value may
/// not.
/// </para>
/// <para>
/// <b>One refusal, not two.</b> A <c>warm</c> above <c>inventory.size</c> looks
/// like it deserves its own arm, and it cannot be reached: <c>Validate</c>
/// already refuses <c>pool-max &gt; size</c>, so anything above the inventory
/// is above the bound first. An unreachable refusal would be the exact shape
/// the rest of this slice is about.
/// </para>
/// </remarks>
public class WarmTargetTests
{
    private static EnvironmentStrategy Strategy(int warm = 0, int poolMax = 2, int size = 3) =>
        new()
        {
            Kind = StrategyKinds.DockerHost,
            Environment = "aspire-payments",
            Inventory = new StrategyInventory { Pool = "payments-pool", Size = size, Warm = warm },
            PullPoint = PullPoints.ResidentRunner,
            Image = "ghcr.io/acme/env@sha256:" + new string('a', 64),
            Bounds = new StrategyBounds { PoolMax = poolMax },
        };

    [Test]
    public async Task A_target_inside_the_bound_is_accepted()
    {
        await Assert.That(EnvironmentStrategy.Validate(Strategy(warm: 2, poolMax: 2))).IsNull()
            .Because("a pool may be asked to keep every member it is allowed to have warm - "
                   + "the bound is a ceiling, and standing on a ceiling is legal.");
    }

    [Test]
    public async Task A_target_above_the_bound_is_refused_naming_both_numbers()
    {
        var refusal = EnvironmentStrategy.Validate(Strategy(warm: 3, poolMax: 2));

        await Assert.That(refusal).IsNotNull()
            .Because("a target the bound can never reach is a declaration error, not a "
                   + "runtime one - the decider would warm to the ceiling forever and the "
                   + "pool would read as permanently short of its own promise.");
        await Assert.That(refusal!).Contains("3");
        await Assert.That(refusal!).Contains("2")
            .Because("Article XI: the refusal names BOTH numbers, because a person reading "
                   + "'the target is too high' still has to open two files to find out how "
                   + "high is too high.");
    }

    [Test]
    public async Task A_negative_target_is_refused()
    {
        await Assert.That(EnvironmentStrategy.Validate(Strategy(warm: -1))).IsNotNull()
            .Because("below zero is not a smaller kind of zero - it is a number nothing "
                   + "can act on, and a bound nobody can evaluate binds nothing.");
    }

    [Test]
    public async Task A_strategy_that_names_no_target_is_unchanged()
    {
        // THE PRE-SLICE DOCUMENT, and it must read back meaning what it meant.
        // A default that was anything but zero would silently turn every
        // strategy already in force into a proactive one, on the deploy that
        // shipped this member.
        var before = Strategy();

        await Assert.That(before.Inventory.Warm).IsEqualTo(0)
            .Because("the member defaults to zero, so a document written before it existed "
                   + "declares 'warm only behind demand' - which is what it always said.");
        await Assert.That(EnvironmentStrategy.Validate(before)).IsNull()
            .Because("and it stays valid, so nothing already applied has to be re-authored.");
    }

    [Test]
    public async Task The_target_is_not_a_second_ceiling()
    {
        // THE NAME IS THE POINT. Two ceilings already exist and neither says
        // how many the pool SHOULD hold. A reader who sees three numbers has to
        // be able to tell which one is the want.
        await Assert.That(EnvironmentStrategy.Validate(Strategy(warm: 1, poolMax: 2))).IsNull()
            .Because("a target BELOW the bound is the ordinary case: keep one warm, allow "
                   + "two. If this were a third ceiling the two would be interchangeable.");
    }
}
