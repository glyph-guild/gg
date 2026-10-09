using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A destroy removes the member it names and creates nothing in its place.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED ON GG-1016: a member nothing could reclaim.</b> The ui pool is
/// declared <c>size: 2</c>; <c>gg-pool-ui-3</c> exited cleanly and then failed
/// verify every five seconds for hours. Both paths that could have taken it
/// away are closed: the only destroy in the product is inside a roll and is
/// gated on the member being off the image pin, and the refresh that would
/// reuse its slot needs the live count below the ceiling - which it is not,
/// when the running members already fill the pool. The time before, the same
/// container survived thirty-three hours.
/// </para>
/// <para>
/// <b>Why an action rather than cleverness in the loop.</b> Whether a member is
/// surplus is a question about the DECLARED SIZE, and the loop does not have
/// it - <c>PoolMax</c> appears in <c>MaintainLoop</c> only inside a comment. So
/// the control plane decides and names the member, as it does for everything
/// else the loop performs.
/// </para>
/// <para>
/// <b>And it is not a reset.</b> A reset creates a running member in place of
/// the one it removed, so resetting a surplus corpse grows the pool to three -
/// which is how one got there in the first place.
/// </para>
/// </remarks>
public class ASurplusMemberIsReclaimedTests
{
    /// <summary>A pool whose members are a dictionary of name to running.</summary>
    /// <remarks>
    /// Its own rather than <c>ACorpseDoesNotKeepItsSlotTests</c>', which is
    /// private to that class - and the verify below returns the sentence the
    /// live incident carried, word for word.
    /// </remarks>
    private sealed class FakePool : IPoolAdapter
    {
        public Dictionary<string, bool> Members { get; } = new(StringComparer.Ordinal);

        public List<string> Calls { get; } = [];

        public List<string> Destroyed { get; } = [];

        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>(
                [.. Members.Select(m => new PoolMember { Name = m.Key, Running = m.Value })]);

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(Members[member.Name]
                ? new PoolObservation { Outcome = PoolOutcomes.Verified, ImageDigest = "sha256:warm" }
                : new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = $"'{member.Name}' exists and is not running (status: exited).",
                });

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"refresh:{member}");
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Calls.Add($"reset:{member}");
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<PoolObservation> DestroyAsync(
            string member, CancellationToken cancellationToken = default)
        {
            Destroyed.Add(member);
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe
            {
                Held = true,
                ProbedAt = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
            });
    }

    private const string Pinned = "acr.example/gg-member@sha256:pinned";

    /// <summary>Serves one destroy, naming its member, then nothing.</summary>
    private sealed class OneDestroy(string member) : IPoolProtocol
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
                        ActionId = Guid.CreateVersion7(),
                        Pool = pool,
                        Action = PoolActions.Destroy,
                        Member = member,
                        Image = Pinned,
                        StrategyVersion = $"{pool}@v1",
                        DecidedAt = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
                    },
                ],
            });
        }

        public Task AttestAsync(
            string pool, PoolAttestation attestation,
            CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);
            return Task.CompletedTask;
        }

        public Task<Gg.Contracts.MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<Gg.Contracts.MemberCredentialMinted?>(null);
    }

    private static async Task<(FakePool Pool, OneDestroy Protocol)>
        SweptAsync(string destroy, params (string Name, bool Running)[] members)
    {
        var adapter = new FakePool();
        foreach (var (name, running) in members)
        {
            adapter.Members[name] = running;
        }

        var protocol = new OneDestroy(destroy);
        using var stop = new CancellationTokenSource();
        var loop = new MaintainLoop(
            protocol, adapter,
            new MovableClock(DateTimeOffset.Parse("2026-09-14T10:00:00Z")),
            (_, _) =>
            {
                stop.Cancel();
                return Task.CompletedTask;
            });

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);
        return (adapter, protocol);
    }

    [Test]
    public async Task The_member_it_names_is_destroyed()
    {
        var (pool, _) = await SweptAsync(
            "gg-pool-ui-3",
            ("gg-pool-ui-1", true), ("gg-pool-ui-2", true), ("gg-pool-ui-3", false));

        await Assert.That(pool.Destroyed).Contains("gg-pool-ui-3")
            .Because("a pool declared size 2 with two running members and a third exited is the "
                   + "shape nothing could reclaim, and this is the action that reclaims it.");
    }

    [Test]
    public async Task And_nothing_is_created_in_its_place()
    {
        var (pool, _) = await SweptAsync(
            "gg-pool-ui-3",
            ("gg-pool-ui-1", true), ("gg-pool-ui-2", true), ("gg-pool-ui-3", false));

        await Assert.That(pool.Calls.Any(c => c.StartsWith("refresh:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a reset is what creates one in place of the one it removed, and resetting "
                   + "a surplus corpse is how a pool bounded at two came to hold three.");

        await Assert.That(pool.Calls.Any(c => c.StartsWith("reset:", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task The_running_members_are_left_alone()
    {
        var (pool, _) = await SweptAsync(
            "gg-pool-ui-3",
            ("gg-pool-ui-1", true), ("gg-pool-ui-2", true), ("gg-pool-ui-3", false));

        await Assert.That(pool.Destroyed).DoesNotContain("gg-pool-ui-1");
        await Assert.That(pool.Destroyed).DoesNotContain("gg-pool-ui-2");
        await Assert.That(pool.Destroyed.Count).IsEqualTo(1)
            .Because("a destroy names one member; removing a second on the strength of one "
                   + "decision is the sweep-by-pattern this estate has already paid for once.");
    }

    [Test]
    public async Task A_destroy_that_names_nobody_is_refused_rather_than_guessed()
    {
        // THE POISON TWIN. A runner that picked a container itself would be
        // deciding what only the declared size can decide - and the failure
        // would be a removal nobody asked for.
        var (pool, protocol) = await SweptAsync(
            destroy: null!,
            ("gg-pool-ui-1", true), ("gg-pool-ui-2", false));

        await Assert.That(pool.Destroyed).IsEmpty()
            .Because("there is no member to destroy, and the runner may not choose one.");

        await Assert.That(protocol.Attested.Any(a =>
                string.Equals(a.Outcome, PoolOutcomes.Failed, StringComparison.Ordinal)))
            .IsTrue()
            .Because("and it says so, because an action that silently did nothing would read as "
                   + "a reclaim that happened.");
    }
}
