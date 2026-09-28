using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A roll reclaims a spent member that is off the pin: it destroys it and
/// recreates nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because at the ceiling nothing else can touch it.</b>
/// <c>WarmMembers</c> counts live members, so two running and one exited reads
/// as two — and <c>wantsRefresh</c> needs <c>WarmMembers &lt; PoolMax</c>, which
/// 2 &lt; 2 is not. Refresh would reuse the slot and cannot be decided; reset must
/// not take a corpse, because a reset is suppressed as soon as one attests and
/// would report a release handled while the member that ran the flight kept a
/// customer's tree. Measured: <c>gg-pool-ui-3</c> exited and sat for thirty-three
/// hours, its verify attesting failed every five seconds the whole time, which is
/// an escalation that never closes and buries the faults that matter.
/// </para>
/// <para>
/// <b>A roll is the act that already owns this.</b> It makes the whole pool
/// current with the pin — <i>"destroy and recreate every member made from
/// something the strategy no longer names"</i> — and it is the one act that may
/// run AT the ceiling, because it replaces rather than adds.
/// </para>
/// <para>
/// <b>What the old rule got right, and where it overcorrected.</b> Roll became
/// running-only because <c>ResetAsync</c> creates a RUNNING member: <i>"the first
/// roll that ran brought a spent gg-pool-ui-3 back to life and left a pool
/// bounded at two with three"</i>. Skipping stopped members fixed the growth and
/// left the corpse immortal. Destroying without recreating fixes both — the slot
/// is reclaimed and the live count does not move, because a stopped member was
/// never in it.
/// </para>
/// <para>
/// <b>Only when it is off the pin.</b> A stopped member made from the image in
/// force is a slot the next refresh reuses, exactly as <c>NextSlotAsync</c>
/// intends, and destroying it would throw away a slot the pool is about to want.
/// </para>
/// </remarks>
public class ASpentMemberDoesNotKeepItsSlotForeverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 2, 30, 0, TimeSpan.Zero);

    private const string ThePin = "127.0.0.1:5000/gg-member-browser@sha256:" + "8f449c52"
        + "2c90009a85fb3469e3c92b4d7ff853b7c670814a73c3dcce969108f8";

    private const string TheOldPin = "127.0.0.1:5000/gg-member-browser@sha256:" + "a8a0d8d5"
        + "83c4d8c0318d2d1668bd003d158dab165997896e3a5d4751e75e2ac7";

    private sealed class ARollIsDecided : IPoolProtocol
    {
        private bool _served;

        public List<PoolAttestation> Attested { get; } = [];

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            if (_served)
            {
                return Task.FromResult(new PoolActionList { Actions = [] });
            }

            _served = true;
            return Task.FromResult(new PoolActionList
            {
                Actions =
                [
                    new PoolAction
                    {
                        ActionId = Guid.NewGuid(),
                        Pool = "gg-pool-ui",
                        Action = PoolActions.Roll,
                        Image = ThePin,
                        StrategyVersion = "ui@v18",
                        DecidedAt = Now,
                    },
                ],
            });
        }

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(new MemberCredentialMinted
            {
                Nonce = "a-nonce",
                ExpiresAt = Now.AddMinutes(10),
            });

        public Task AttestAsync(
            string pool, PoolAttestation attestation,
            CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);
            return Task.CompletedTask;
        }
    }

    private sealed class APool(params PoolMember[] members) : IPoolAdapter
    {
        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public List<string> Reset { get; } = [];

        public List<string> Destroyed { get; } = [];

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe { Held = true, ProbedAt = Now });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>([.. members]);

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Reset.Add(member);
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<PoolObservation> DestroyAsync(
            string member, CancellationToken cancellationToken = default)
        {
            Destroyed.Add(member);
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }
    }

    private static PoolMember Member(string name, bool running, string madeFrom) =>
        new() { Name = name, Running = running, MadeFrom = madeFrom };

    private static async Task<APool> RunAsync(params PoolMember[] members)
    {
        var adapter = new APool(members);

        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            new ARollIsDecided(), adapter, new MovableClock(Now),
            (_, _) =>
            {
                if (++turns >= 2)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            });

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);
        return adapter;
    }

    [Test]
    public async Task A_spent_member_off_the_pin_is_destroyed_and_not_recreated()
    {
        // The live shape: two members rolled onto the pin, and one that ran out
        // of credential days ago on the image before it.
        var pool = await RunAsync(
            Member("gg-pool-ui-1", running: true, ThePin),
            Member("gg-pool-ui-2", running: true, ThePin),
            Member("gg-pool-ui-3", running: false, TheOldPin));

        await Assert.That(pool.Destroyed).Contains("gg-pool-ui-3")
            .Because("at the ceiling nothing else can reach it: refresh is bounded by "
                   + "WarmMembers < PoolMax and a corpse is not counted, so the pool reads full "
                   + "while the slot stays taken for ever.");

        await Assert.That(pool.Reset).DoesNotContain("gg-pool-ui-3")
            .Because("recreating it is what left a pool bounded at two running three. Destroying "
                   + "takes nothing away, because a stopped member was never in the live count.");
    }

    [Test]
    public async Task A_spent_member_already_on_the_pin_keeps_its_slot()
    {
        var pool = await RunAsync(
            Member("gg-pool-ui-1", running: true, ThePin),
            Member("gg-pool-ui-2", running: false, ThePin));

        await Assert.That(pool.Destroyed).IsEmpty()
            .Because("a stopped member made from the image in force is a slot the next refresh "
                   + "reuses, exactly as NextSlotAsync intends - destroying it would throw away "
                   + "a slot the pool is about to want.");
    }

    [Test]
    public async Task A_running_member_off_the_pin_is_still_rolled_rather_than_destroyed()
    {
        var pool = await RunAsync(
            Member("gg-pool-ui-1", running: true, TheOldPin));

        await Assert.That(pool.Reset).Contains("gg-pool-ui-1")
            .Because("a roll replaces what is there, and a running member off the pin is the "
                   + "case the whole act exists for.");
        await Assert.That(pool.Destroyed).IsEmpty()
            .Because("destroying a running member without recreating it would take a warm "
                   + "environment away and leave the pool short.");
    }
}
