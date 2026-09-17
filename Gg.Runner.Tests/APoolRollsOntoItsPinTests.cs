using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A decided <c>roll</c> makes every member of a pool current with the pinned
/// image, and touches the members already made from it not at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on vmlinux001, 2026-09-17.</b> A member image was built, pushed
/// and named by an applied strategy, and three hours later the pool ran three
/// different images: <c>gg-pool-ui-1</c> from <c>…463dad1f</c>, <c>-2</c> from
/// <c>…94a81bb3</c>, <c>-3</c> from the pin. Nothing was broken and nothing
/// was going to fix it: the planner decides an act only when the pool is below
/// its warm target, and <c>verify</c> - the only action a full pool ever gets -
/// compares no images at all. A running member never converged.
/// </para>
/// <para>
/// <b>Why this is not <c>refresh</c>.</b> Refresh already promises <i>"create
/// it if absent, start it if stopped, converge it to the strategy's image if it
/// drifted"</i>, and the adapter performs all three - but the loop picks its
/// target with <c>NextSlotAsync</c>, which returns the first slot that is NOT
/// running, so the converge branch is handed a member that does not exist and
/// creates it instead. Refresh is how a pool grows, one member at a time, with
/// a hold-off tuned to that pace. Rolling is not growth: it replaces what is
/// there, it must be able to act at the warm ceiling, and it is finished when
/// no member is off the pin - so it is its own word, decided by its own rule.
/// </para>
/// <para>
/// <b>One act converges the whole pool</b>, because the decision that produced
/// it cannot see which members drifted - only the runner can, by comparing what
/// each container says it was made FROM against what the strategy pins. A roll
/// that converged one member would need the control plane to keep deciding
/// rolls until a count it cannot measure reached zero.
/// </para>
/// </remarks>
public class APoolRollsOntoItsPinTests
{
    private const string Pin = "127.0.0.1:5000/gg-member-browser@sha256:new";

    private const string Stale = "127.0.0.1:5000/gg-member-browser@sha256:old";

    private sealed class RollAdapter : IPoolAdapter
    {
        public List<string> Calls { get; } = [];

        /// <summary>Each member, and the reference it says it was made from.</summary>
        public Dictionary<string, string?> Members { get; } = [];

        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public PoolObservation Resetting { get; set; } = new()
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = "sha256:reset",
            Provenance = EnvironmentProvenance.Fresh,
        };

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe
            {
                Held = true,
                ProbedAt = DateTimeOffset.Parse("2026-09-17T23:00:00Z"),
            });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            Calls.Add($"list:{pool}");
            return Task.FromResult<IReadOnlyList<PoolMember>>(
                [.. Members.Select(m => new PoolMember
                {
                    Name = m.Key,
                    Running = true,
                    MadeFrom = m.Value,
                })]);
        }

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default)
        {
            Calls.Add($"verify:{member.Name}");
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Calls.Add($"refresh:{member}");
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Calls.Add($"reset:{member}:{spec.Image}");
            return Task.FromResult(Resetting);
        }
    }

    private sealed class RollProtocol : IPoolProtocol
    {
        public Queue<IReadOnlyList<PoolAction>> Served { get; } = [];

        public List<PoolAttestation> Attested { get; } = [];

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolActionList
            {
                Actions = Served.TryDequeue(out var actions) ? actions : [],
            });

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(new()
            {
                Nonce = $"nonce-for-{member}",
                ExpiresAt = DateTimeOffset.Parse("2026-09-18T11:00:00Z"),
            });

        public Task AttestAsync(
            string pool, PoolAttestation attestation, CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);
            return Task.CompletedTask;
        }
    }

    private static (MaintainLoop Loop, RollAdapter Adapter, RollProtocol Protocol,
        CancellationTokenSource Stop) Rig()
    {
        var adapter = new RollAdapter();
        var protocol = new RollProtocol();
        var stop = new CancellationTokenSource();
        var loop = new MaintainLoop(
            protocol, adapter, new MovableClock(DateTimeOffset.Parse("2026-09-17T23:00:00Z")),
            (_, _) =>
            {
                stop.Cancel();
                return Task.CompletedTask;
            });
        return (loop, adapter, protocol, stop);
    }

    private static PoolAction Roll() => new()
    {
        ActionId = Guid.Parse("01a0b200-0000-7000-8000-000000000001"),
        Pool = "gg-pool-ui",
        Action = PoolActions.Roll,
        Image = Pin,
        StrategyVersion = "ui@v9",
        DecidedAt = DateTimeOffset.Parse("2026-09-17T22:59:00Z"),
    };

    [Test]
    public async Task Every_member_off_the_pin_is_recreated_from_it()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Members["gg-pool-ui-1"] = Stale;
        adapter.Members["gg-pool-ui-2"] = Stale;
        adapter.Members["gg-pool-ui-3"] = Pin;
        protocol.Served.Enqueue([Roll()]);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);

        await Assert.That(adapter.Calls).Contains($"reset:gg-pool-ui-1:{Pin}")
            .Because("a member made from something the strategy does not name is the "
                   + "whole subject of the act.");
        await Assert.That(adapter.Calls).Contains($"reset:gg-pool-ui-2:{Pin}")
            .Because("one act converges the pool: the decision cannot see which members "
                   + "drifted, so a roll that stopped after the first would leave the rest "
                   + "for a decision nobody will make.");
    }

    [Test]
    public async Task A_member_already_made_from_the_pin_is_left_alone()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Members["gg-pool-ui-1"] = Stale;
        adapter.Members["gg-pool-ui-3"] = Pin;
        protocol.Served.Enqueue([Roll()]);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);

        await Assert.That(adapter.Calls).DoesNotContain($"reset:gg-pool-ui-3:{Pin}")
            .Because("destroying a current member is a warm member taken away for nothing, "
                   + "and a pool that rolls what is already rolled never settles.");
    }

    [Test]
    public async Task A_pool_already_on_its_pin_destroys_nothing_and_says_so()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Members["gg-pool-ui-1"] = Pin;
        adapter.Members["gg-pool-ui-2"] = Pin;
        protocol.Served.Enqueue([Roll()]);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);

        await Assert.That(adapter.Calls.Any(c => c.StartsWith("reset:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a strategy applied without moving the image decides a roll too, and "
                   + "that roll has nothing to do.");

        var rolled = protocol.Attested.Single(a => string.Equals(
            a.Action, PoolActions.Roll, StringComparison.Ordinal));
        await Assert.That(rolled.Outcome).IsEqualTo(PoolOutcomes.Verified)
            .Because("nothing to do is done, not failed.");
    }

    [Test]
    public async Task A_member_that_will_not_say_what_it_was_made_from_is_not_destroyed()
    {
        // UNKNOWN IS NOT "WRONG". The daemon reports Config.Image for every
        // container it made, but a listing that omits it says nothing about the
        // member, and reading silence as drift destroys a warm member on the
        // strength of a missing field.
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Members["gg-pool-ui-1"] = null;
        protocol.Served.Enqueue([Roll()]);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);

        await Assert.That(adapter.Calls.Any(c => c.StartsWith("reset:", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task A_reset_that_fails_is_what_the_roll_attests()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Members["gg-pool-ui-1"] = Stale;
        adapter.Resetting = new PoolObservation
        {
            Outcome = PoolOutcomes.Failed,
            Diagnosis = "the daemon answered 409.",
        };
        protocol.Served.Enqueue([Roll()]);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);

        var rolled = protocol.Attested.Single(a => string.Equals(
            a.Action, PoolActions.Roll, StringComparison.Ordinal));
        await Assert.That(rolled.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(rolled.Diagnosis).Contains("gg-pool-ui-1")
            .Because("which member could not be rolled is the first thing a reader needs, "
                   + "and the adapter's sentence does not carry a name.");
    }

    [Test]
    public async Task A_roll_decided_without_an_image_converges_on_nothing()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Members["gg-pool-ui-1"] = Stale;
        protocol.Served.Enqueue([Roll() with { Image = null }]);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);

        await Assert.That(adapter.Calls.Any(c => c.StartsWith("reset:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("with no pin to compare against, every member is off it - and rolling "
                   + "the whole pool onto nothing is the destructive reading of a bug.");

        var rolled = protocol.Attested.Single(a => string.Equals(
            a.Action, PoolActions.Roll, StringComparison.Ordinal));
        await Assert.That(rolled.Outcome).IsEqualTo(PoolOutcomes.Failed);
    }
}
