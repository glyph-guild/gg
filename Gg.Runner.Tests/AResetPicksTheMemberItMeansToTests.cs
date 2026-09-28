using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A reset scrubs a named member, and which one has to be this loop's decision
/// rather than the daemon's listing order.
/// </summary>
/// <remarks>
/// <para>
/// <b>It was <c>members is [var first, ..]</c>.</b> That is the first entry of
/// <c>/containers/json</c>, which the daemon returns newest-first — a fact
/// written down nowhere, promised by nobody, and free to change under us. With
/// one member it was accidentally right; the <c>ui</c> pool has run three.
/// </para>
/// <para>
/// <b>What a reset is for decides which member.</b> It is <i>"what makes a
/// reused environment trustworthy again after a flight"</i>, and the decider
/// only asks for one when no lease stands on the label — so nothing is
/// mid-flight when this runs, and every running member is a post-flight member
/// that will be handed to somebody next. The lowest running slot is the one that
/// gets used soonest, and picking by slot makes the choice legible in the
/// attestation rather than dependent on how long ago a container was made.
/// </para>
/// <para>
/// <b>A STOPPED MEMBER IS NEVER THE TARGET, and that is the sharper half.</b>
/// <c>wantsReset</c> fires once per release — it is suppressed again as soon as a
/// reset attests — so a reset spent on a corpse reports the release handled while
/// the member that actually ran the flight keeps a customer's tree. Scrubbing
/// something nobody will be handed is worse than refusing, because refusing says
/// so.
/// </para>
/// <para>
/// <b>What this does NOT fix, recorded because it is the reason a corpse was
/// found at all.</b> A stopped member in a pool at its ceiling is reachable by
/// nothing: <c>WarmMembers</c> counts live members only, so two live and one
/// exited reads as two, and <c>wantsRefresh</c> needs
/// <c>WarmMembers &lt; PoolMax</c> — 2 &lt; 2 is false. Refresh would recreate it
/// and cannot be decided; reset must not. <c>gg-pool-ui-3</c> sat exited for
/// thirty-three hours on exactly that arithmetic, and it needs its own decision
/// rather than a reset quietly widened to cover it.
/// </para>
/// </remarks>
public class AResetPicksTheMemberItMeansToTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 2, 0, 0, TimeSpan.Zero);

    /// <summary>A control plane that serves one reset and then nothing.</summary>
    private sealed class AResetIsDecided : IPoolProtocol
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
                        Action = PoolActions.Reset,
                        Image = "a-registry/gg-member-browser@sha256:" + new string('b', 64),
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

    /// <summary>
    /// A pool whose listing comes back in the daemon's own order — newest first,
    /// which is the order the defect depended on.
    /// </summary>
    private sealed class APoolListedNewestFirst(params (string Name, bool Running)[] members)
        : IPoolAdapter
    {
        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

    /// <summary>Reclaiming: this fake records nothing about it.</summary>
    public Task<PoolObservation> DestroyAsync(
        string member, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public string? Reset { get; private set; }

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe { Held = true, ProbedAt = Now });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>(
                [.. members.Select(m => new PoolMember { Name = m.Name, Running = m.Running })]);

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
            Reset = member;
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }
    }

    private static async Task<(string? Reset, IReadOnlyList<PoolAttestation> Attested)> RunAsync(
        params (string Name, bool Running)[] members)
    {
        var protocol = new AResetIsDecided();
        var adapter = new APoolListedNewestFirst(members);

        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            protocol, adapter, new MovableClock(Now),
            (_, _) =>
            {
                if (++turns >= 2)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            });

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);
        return (adapter.Reset, protocol.Attested);
    }

    [Test]
    public async Task It_takes_the_lowest_running_slot_rather_than_whatever_was_listed_first()
    {
        // The daemon's order: newest first. Under the old rule this reset
        // gg-pool-ui-3 because it happened to be made most recently.
        var ran = await RunAsync(
            ("gg-pool-ui-3", true), ("gg-pool-ui-2", true), ("gg-pool-ui-1", true));

        await Assert.That(ran.Reset).IsEqualTo("gg-pool-ui-1")
            .Because("the lowest running slot is the one handed out soonest, and picking by slot "
                   + "makes the choice legible in the attestation instead of depending on how "
                   + "long ago a container happened to be made.");
    }

    [Test]
    public async Task A_stopped_member_is_never_the_one_scrubbed()
    {
        // The live shape on the ui pool: a corpse listed first, two members
        // running behind it.
        var ran = await RunAsync(
            ("gg-pool-ui-3", false), ("gg-pool-ui-2", true), ("gg-pool-ui-1", true));

        await Assert.That(ran.Reset).IsEqualTo("gg-pool-ui-1")
            .Because("a reset is suppressed again as soon as one attests, so spending it on a "
                   + "corpse reports the release handled while the member that actually ran the "
                   + "flight keeps a customer's tree.");
    }

    [Test]
    public async Task A_pool_with_nothing_running_refuses_rather_than_scrubbing_a_corpse()
    {
        var ran = await RunAsync(("gg-pool-ui-1", false), ("gg-pool-ui-2", false));

        await Assert.That(ran.Reset).IsNull()
            .Because("there is nothing here that anybody will be handed, so there is nothing to "
                   + "make trustworthy - and recreating one is a refresh's decision, which is "
                   + "the one bounded by the pool's ceiling.");

        var reset = ran.Attested
            .Where(a => string.Equals(a.Action, PoolActions.Reset, StringComparison.Ordinal))
            .ToList();

        await Assert.That(reset).IsNotEmpty()
            .Because("Article XI: a decided action that did nothing has to say so, or the ledger "
                   + "shows a reset the pool never had.");
        await Assert.That(reset[0].Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(reset[0].Diagnosis!).Contains("running")
            .Because("the diagnosis has to name what was missing, because the remedy - warm one "
                   + "- is a different decision from the one that failed.");
    }
}
