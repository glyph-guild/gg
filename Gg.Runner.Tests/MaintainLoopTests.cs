using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The maintain loop: probe the scope once, verify and attest each member,
/// pull decided actions, execute them through the adapter, attest each with
/// its action id — and stop the session when the scope bound cannot be
/// proved.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bound is the precondition, slice eleven's shape on
/// infrastructure.</b> A session whose out-of-inventory reach was NOT
/// refused attests a failed verify naming the diagnosis and stops — it never
/// verifies, never refreshes, never resets, because an unproved scope plus
/// an outward act is exactly what § 12 forbids.
/// </para>
/// <para>
/// Every attestation carries the session's <c>ScopeProbedAt</c>, because the
/// decider's outward-act rule reads it: unknown is not false.
/// </para>
/// </remarks>
public class MaintainLoopTests
{
    private sealed class FakeAdapter : IPoolAdapter
    {
        public List<string> Calls { get; } = [];

        public ScopeProbe Probe { get; set; } = new()
        {
            Held = true,
            ProbedAt = DateTimeOffset.Parse("2026-08-25T10:00:00Z"),
        };

        public Dictionary<string, PoolObservation> Verifications { get; } = [];

        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        /// <summary>Answers each probe as it is asked, when a test needs them to differ.</summary>
        public Func<ScopeProbe>? Probing { get; set; }

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("probe");
            return Task.FromResult(Probing?.Invoke() ?? Probe);
        }

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            Calls.Add($"list:{pool}");
            return Task.FromResult<IReadOnlyList<PoolMember>>(
                [.. Verifications.Select(v => new PoolMember
                {
                    Name = v.Key,

                    // A MEMBER IS RUNNING WHEN ITS VERIFY SAYS SO, which is the
                    // adapter's own rule: VerifyAsync reports Verified only for
                    // a container that runs. Saying it twice in a fake is how a
                    // fixture ends up describing a pool the daemon could not.
                    Running = string.Equals(
                        v.Value.Outcome, PoolOutcomes.Verified, StringComparison.Ordinal),
                })]);
        }

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default)
        {
            Calls.Add($"verify:{member.Name}");
            return Task.FromResult(Verifications[member.Name]);
        }

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Calls.Add($"refresh:{member}:{spec.Image}");
            return Task.FromResult(new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = "sha256:refreshed",
                Provenance = EnvironmentProvenance.Fresh,
            });
        }

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Calls.Add($"reset:{member}:{spec.Image}");
            return Task.FromResult(new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = "sha256:reset",
                Provenance = EnvironmentProvenance.Fresh,
            });
        }
    }

    private sealed class FakePoolProtocol : IPoolProtocol
    {
        public Queue<IReadOnlyList<PoolAction>> Served { get; } = [];

        public List<PoolAttestation> Attested { get; } = [];

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolActionList
            {
                Actions = Served.TryDequeue(out var actions) ? actions : [],
            });

        /// <summary>Always mints. A refusal has its own test.</summary>
        public Task<Gg.Contracts.MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<Gg.Contracts.MemberCredentialMinted?>(new()
            {
                Nonce = $"nonce-for-{member}",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            });

        public Task AttestAsync(
            string pool, PoolAttestation attestation, CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);
            return Task.CompletedTask;
        }
    }

    private static (MaintainLoop Loop, FakeAdapter Adapter, FakePoolProtocol Protocol, CancellationTokenSource Stop)
        Rig(int cyclesBeforeStop = 1)
    {
        var adapter = new FakeAdapter();
        var protocol = new FakePoolProtocol();
        var stop = new CancellationTokenSource();
        var cycles = 0;
        var loop = new MaintainLoop(
            protocol, adapter, new MovableClock(DateTimeOffset.Parse("2026-08-25T10:00:00Z")),
            (_, _) =>
            {
                if (++cycles >= cyclesBeforeStop)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            });
        return (loop, adapter, protocol, stop);
    }

    /// <summary>A loop whose clock moves by <paramref name="step"/> on every wait.</summary>
    private static (MaintainLoop Loop, FakeAdapter Adapter, FakePoolProtocol Protocol,
        CancellationTokenSource Stop, MovableClock Clock) Aging(TimeSpan step, int cycles)
    {
        var clock = new MovableClock(DateTimeOffset.Parse("2026-09-17T07:25:00Z"));
        var adapter = new FakeAdapter();
        adapter.Probing = () => new ScopeProbe { Held = true, ProbedAt = clock.UtcNow };
        var protocol = new FakePoolProtocol();
        var stop = new CancellationTokenSource();
        var waited = 0;
        var loop = new MaintainLoop(
            protocol, adapter, clock,
            (_, _) =>
            {
                clock.Advance(step);
                if (++waited >= cycles)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            });
        return (loop, adapter, protocol, stop, clock);
    }

    [Test]
    public async Task A_session_as_old_as_its_bound_probes_again()
    {
        // FOUND ON vmlinux001. gg-pool-ui-1 ended on its credential at 11:42 and
        // nothing replaced it for three hours; a newly applied image did not roll
        // out either, and `gg pools` said "exists and is not running" every five
        // seconds the whole time. The control plane refuses an outward act whose
        // stamp is older than an hour - "a scope probe older than this governs
        // some other session" - and this session had probed once, at 07:25, and
        // stamped that instant on everything it said for seven hours. Restarting
        // the maintainer replaced the member six seconds later.
        //
        // STILL ONE PROBE PER SESSION, which the test above holds. What changes is
        // that a session ends: at its bound, not per cycle.
        var (loop, adapter, protocol, stop, clock) =
            Aging(MaintainLoop.SessionLength + TimeSpan.FromMinutes(1), cycles: 2);

        var exit = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(adapter.Calls.Count(c => c == "probe")).IsEqualTo(2)
            .Because("the second cycle began past the session's bound, so it opened a new one.");

        var last = protocol.Attested.Last();
        await Assert.That(last.ScopeProbedAt)
            .IsEqualTo(DateTimeOffset.Parse("2026-09-17T07:25:00Z")
                     + MaintainLoop.SessionLength + TimeSpan.FromMinutes(1))
            .Because("what the decider reads is the stamp, so the new probe has to be the one "
                   + "the next attestation carries.");
    }

    [Test]
    public async Task A_session_younger_than_its_bound_does_not_probe_again()
    {
        var (loop, adapter, _, stop, _) =
            Aging(MaintainLoop.SessionLength - TimeSpan.FromMinutes(1), cycles: 2);

        _ = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(adapter.Calls.Count(c => c == "probe")).IsEqualTo(1);
    }

    [Test]
    public async Task A_probe_that_no_longer_holds_stops_the_session_as_the_first_would()
    {
        // FAIL CLOSED, THE STARTUP RULE ONE SESSION LATER. A bound that held at
        // startup and has since stopped holding is the same fact as one that
        // never held, and the same answer: say so on the ledger, unstamped, and
        // stop rather than act.
        var (loop, adapter, protocol, stop, clock) =
            Aging(MaintainLoop.SessionLength + TimeSpan.FromMinutes(1), cycles: 3);
        var asked = 0;
        adapter.Probing = () => ++asked == 1
            ? new ScopeProbe { Held = true, ProbedAt = clock.UtcNow }
            : new ScopeProbe
            {
                Held = false,
                ProbedAt = clock.UtcNow,
                Diagnosis = "an out-of-inventory reach was not refused",
            };

        var exit = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(exit).IsEqualTo(69);

        var last = protocol.Attested.Last();
        await Assert.That(last.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(last.ScopeProbedAt).IsNull()
            .Because("a broken probe proves nothing, and stamping it would let a refusal read "
                   + "as a proof.");
        await Assert.That(last.Diagnosis).Contains("not refused");
    }

    [Test]
    public async Task A_session_ends_well_inside_the_control_planes_bound()
    {
        // THE CONTROL PLANE'S ProbeFreshness IS ONE HOUR, in another repository.
        // Half of it leaves room for the decider's own tick, a cycle's backoff and
        // clock skew between the two machines before a stamp reads as stale.
        await Assert.That(MaintainLoop.SessionLength).IsLessThanOrEqualTo(TimeSpan.FromMinutes(30));
        await Assert.That(MaintainLoop.SessionLength).IsGreaterThan(MaintainLoop.PollEvery)
            .Because("a session shorter than a cycle would be a probe per cycle, which is a "
                   + "different design.");
    }

    [Test]
    public async Task An_empty_pool_announces_itself()
    {
        // THE FIRST ATTESTATION IS THE PULL POINT COMING UP. A pool with no
        // members yet has nothing to verify member-by-member - but a loop
        // that stays silent until a member exists can never recover the
        // bring-up ask, and the decider will not decide an outward act toward
        // a pool with no scope-probed attestation: the empty pool must say
        // "I am here, probed, holding nothing" or the whole management story
        // deadlocks at birth. Found live, by the walk.
        var (loop, _, protocol, stop) = Rig(cyclesBeforeStop: 1);

        _ = await loop.RunAsync("gg-pool-payments", stop.Token);

        var announced = protocol.Attested.Single(a =>
            a.Action == PoolActions.Verify && a.Outcome == PoolOutcomes.Verified);
        await Assert.That(announced.ScopeProbedAt).IsNotNull()
            .Because("the announcement carries the session's scope probe - it is the "
                   + "attestation everything downstream waits for.");
        await Assert.That(announced.Diagnosis).IsNull();
    }

    [Test]
    public async Task The_session_probes_once_and_stamps_every_attestation()
    {
        var (loop, adapter, protocol, stop) = Rig(cyclesBeforeStop: 2);
        adapter.Verifications["gg-pool-payments-1"] = new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = "sha256:abc",
            Provenance = EnvironmentProvenance.Reused,
        };

        var exit = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(adapter.Calls.Count(c => c == "probe")).IsEqualTo(1)
            .Because("per session means per session: one probe governs the invocations "
                   + "that follow it, and a probe per cycle would be a different design "
                   + "asserted nowhere.");
        await Assert.That(protocol.Attested).IsNotEmpty();
        await Assert.That(protocol.Attested.All(
                a => a.ScopeProbedAt == adapter.Probe.ProbedAt)).IsTrue()
            .Because("the decider's outward-act rule reads the stamp; an attestation "
                   + "without one is a pool nothing outward is decided toward.");
    }

    [Test]
    public async Task Verified_and_failed_both_cross_with_their_shapes()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Verifications["gg-pool-payments-1"] = new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = "sha256:abc",
            Provenance = EnvironmentProvenance.Reused,
        };
        adapter.Verifications["gg-pool-payments-2"] = new PoolObservation
        {
            Outcome = PoolOutcomes.Failed,
            Diagnosis = "container exists and will not start: exit 127 at boot",
        };

        _ = await loop.RunAsync("gg-pool-payments", stop.Token);

        var verified = protocol.Attested.Single(a => a.Outcome == PoolOutcomes.Verified);
        await Assert.That(verified.ImageDigest).IsEqualTo("sha256:abc");
        await Assert.That(verified.Action).IsEqualTo(PoolActions.Verify);
        var failed = protocol.Attested.Single(a => a.Outcome == PoolOutcomes.Failed);
        await Assert.That(failed.Diagnosis!).Contains("will not start")
            .Because("the failed attestation is what escalation reads, and a failure "
                   + "that cannot say why escalates nothing.");
    }

    [Test]
    public async Task A_decided_refresh_executes_and_attests_with_its_action_id()
    {
        var (loop, adapter, protocol, stop) = Rig();
        var actionId = Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057");
        protocol.Served.Enqueue(
        [
            new PoolAction
            {
                ActionId = actionId,
                Pool = "gg-pool-payments",
                Action = PoolActions.Refresh,
                Image = "busybox@sha256:abc",
                StrategyVersion = "payments-pool@v1",
                DecidedAt = DateTimeOffset.Parse("2026-08-25T09:59:00Z"),
            },
        ]);

        _ = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(adapter.Calls).Contains("refresh:gg-pool-payments-1:busybox@sha256:abc")
            .Because("the member name derives from the pool and the image from the action "
                   + "row - current policy, stamped at serve time.");
        var answered = protocol.Attested.Single(a => a.ActionId == actionId);
        await Assert.That(answered.Action).IsEqualTo(PoolActions.Refresh);
        await Assert.That(answered.Outcome).IsEqualTo(PoolOutcomes.Verified);
    }

    [Test]
    public async Task A_decided_action_without_an_image_attests_failed_naming_the_gap()
    {
        var (loop, adapter, protocol, stop) = Rig();
        var actionId = Guid.Parse("01890a5d-ac96-774b-bcce-b30209918057");
        protocol.Served.Enqueue(
        [
            new PoolAction
            {
                ActionId = actionId,
                Pool = "gg-pool-payments",
                Action = PoolActions.Reset,
                Image = null,
                StrategyVersion = "payments-pool@v1",
                DecidedAt = DateTimeOffset.Parse("2026-08-25T09:59:00Z"),
            },
        ]);

        _ = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(adapter.Calls.Any(c => c.StartsWith("reset:"))).IsFalse()
            .Because("a reset converges on an image; without one there is nothing to "
                   + "converge on and acting anyway would converge on whatever is lying "
                   + "around.");
        var answered = protocol.Attested.Single(a => a.ActionId == actionId);
        await Assert.That(answered.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(answered.Diagnosis!).Contains("image");
    }

    [Test]
    public async Task A_broken_scope_bound_attests_the_failure_and_stops_the_session()
    {
        var (loop, adapter, protocol, stop) = Rig();
        adapter.Probe = new ScopeProbe
        {
            Held = false,
            ProbedAt = DateTimeOffset.Parse("2026-08-25T10:00:00Z"),
            Diagnosis = "the reach outside the pool prefix was ALLOWED: GET answered 200",
        };
        adapter.Verifications["gg-pool-payments-1"] = new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
        };

        var exit = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(exit).IsEqualTo(69)
            .Because("the bound is the precondition: a session that cannot prove its "
                   + "scope does not act on a customer's host.");
        await Assert.That(adapter.Calls.Any(c => c.StartsWith("verify:"))).IsFalse();
        var attested = protocol.Attested.Single();
        await Assert.That(attested.Outcome).IsEqualTo(PoolOutcomes.Failed);
        await Assert.That(attested.Diagnosis!).Contains("ALLOWED")
            .Because("the failure crosses so escalation can mint the incident - a silent "
                   + "stop would be nothing-arrived-nothing-complained.");
        await Assert.That(attested.ScopeProbedAt).IsNull()
            .Because("a broken probe proves nothing; stamping it would let the decider "
                   + "read a refusal as a proof.");
    }

    [Test]
    public async Task Attestation_ids_are_unique_and_version_seven()
    {
        var (loop, adapter, protocol, stop) = Rig(cyclesBeforeStop: 3);
        adapter.Verifications["gg-pool-payments-1"] = new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
        };

        _ = await loop.RunAsync("gg-pool-payments", stop.Token);

        await Assert.That(protocol.Attested.Select(a => a.AttestationId).Distinct().Count())
            .IsEqualTo(protocol.Attested.Count)
            .Because("the id is the idempotency key: a reused one would make two "
                   + "measurements one row.");
        await Assert.That(protocol.Attested.All(
            a => a.AttestationId.ToString("N")[12] == '7')).IsTrue();
    }
}
