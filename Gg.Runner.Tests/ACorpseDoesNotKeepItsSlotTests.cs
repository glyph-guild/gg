using System.Net;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A member that has stopped is a slot to reclaim, not a slot that is taken.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on a live pool.</b> <c>gg-pool-dev-1</c> reached the end of its
/// twelve-hour member token and stopped. The container stayed — the listing
/// asks for <c>?all=true</c>, deliberately, so the pool can see members that
/// are not running — and <c>NextSlotAsync</c> reads a name that exists as a
/// name that is occupied. So the next refresh would create <c>-2</c>, the one
/// after it <c>-3</c>, and a pool of size two would carry a graveyard.
/// </para>
/// <para>
/// <b>This is the 196 by a slower route.</b> That pool reached 196 members in
/// hours because every refresh took the next index;
/// <c>WarmingThatNeverWarmsTests</c> in the control plane stopped the
/// deciding. Nothing stopped the counting: a pool whose members die of old age
/// still grows a name per member per lifetime, forever, and each corpse costs
/// an inspect and an attestation on every sweep.
/// </para>
/// <para>
/// <b>And the reclaimed slot must be RESET, not started.</b> The adapter's
/// non-running branch posts <c>/start</c>, which is right for a member whose
/// stored identity outlived the stop — a daemon restart, a reboot. It is
/// exactly wrong for the member this is about: a member token is not renewable
/// and its nonce is spent, so the restarted process reads a credential that has
/// ended and exits again. That is a restart loop wearing the costume of a
/// repair, and the pool would attest it <c>Verified</c> every time.
/// </para>
/// <para>
/// <b>So the discriminator is the exit code, which is a thing only now worth
/// reading.</b> A member's loop returns 0 when its credential ended normally
/// and non-zero when it did not — before this slice it did neither, because it
/// died inside the 401 with a signal. A container that exited 0 finished; there
/// is nothing left in it to start. One that never ran, or was killed, may still
/// be startable, and that arm is kept.
/// </para>
/// </remarks>
public class ACorpseDoesNotKeepItsSlotTests
{
    private const string Pinned =
        "ghcr.io/acme/env@sha256:" + "3333333333333333333333333333333333333333333333333333333333333333";

    // ---------------------------------------------------------------- the loop

    /// <summary>
    /// A pool whose members are named, each either running or not. Only what
    /// the slot decision reads.
    /// </summary>
    private sealed class FakePool : IPoolAdapter
    {
        public Dictionary<string, bool> Members { get; } = [];

        public List<string> Calls { get; } = [];

        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe
            {
                Held = true,
                ProbedAt = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
            });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>(
                [.. Members.Select(m => new PoolMember { Name = m.Key, Running = m.Value })]);

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(Members[member.Name]
                ? new PoolObservation
                {
                    Outcome = PoolOutcomes.Verified,
                    ImageDigest = "sha256:warm",
                    Provenance = EnvironmentProvenance.Reused,
                }
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
            return Task.FromResult(new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                Provenance = EnvironmentProvenance.Fresh,
            });
        }

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default)
        {
            Calls.Add($"reset:{member}");
            return Task.FromResult(new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                Provenance = EnvironmentProvenance.Fresh,
            });
        }
    }

    private sealed class OneRefresh : IPoolProtocol
    {
        private bool _served;

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
                        Action = PoolActions.Refresh,
                        Image = Pinned,
                        StrategyVersion = "dev@v1",
                        DecidedAt = DateTimeOffset.Parse("2026-09-14T10:00:00Z"),
                    },
                ],
            });
        }

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(new()
            {
                Nonce = $"nonce-for-{member}",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            });

        public Task AttestAsync(
            string pool, PoolAttestation attestation,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static async Task<FakePool> SweptAsync(params (string Name, bool Running)[] members)
    {
        var adapter = new FakePool();
        foreach (var (name, running) in members)
        {
            adapter.Members[name] = running;
        }

        using var stop = new CancellationTokenSource();
        var loop = new MaintainLoop(
            new OneRefresh(), adapter,
            new MovableClock(DateTimeOffset.Parse("2026-09-14T10:00:00Z")),
            (_, _) =>
            {
                stop.Cancel();
                return Task.CompletedTask;
            });

        _ = await loop.RunAsync("gg-pool-dev", stop.Token);
        return adapter;
    }

    [Test]
    public async Task A_refresh_lands_on_the_stopped_member_rather_than_a_new_index()
    {
        var adapter = await SweptAsync(("gg-pool-dev-1", false));

        await Assert.That(adapter.Calls).Contains("refresh:gg-pool-dev-1")
            .Because("the pool's declared size is how many members it may hold, and a "
                   + "container that has stopped is holding nothing. Taking the next index "
                   + "instead is how one pool reached 196.");
    }

    [Test]
    public async Task A_running_member_keeps_its_slot()
    {
        // THE ANCHOR. Reclaiming a slot somebody is standing in would destroy a
        // warm member mid-flight, which is a worse fault than the one above.
        var adapter = await SweptAsync(("gg-pool-dev-1", true));

        await Assert.That(adapter.Calls).Contains("refresh:gg-pool-dev-2")
            .Because("a member that is running is a member, and warming a second one is "
                   + "what a pool below its target does.");
    }

    [Test]
    public async Task The_lowest_stopped_slot_is_taken_first()
    {
        // Cattle, and named by the lowest free number - the rule NextSlotAsync
        // already states. Stopped is free.
        var adapter = await SweptAsync(
            ("gg-pool-dev-1", true), ("gg-pool-dev-2", false), ("gg-pool-dev-3", false));

        await Assert.That(adapter.Calls).Contains("refresh:gg-pool-dev-2");
        await Assert.That(adapter.Calls).DoesNotContain("refresh:gg-pool-dev-4");
    }

    // ------------------------------------------------------------- the adapter

    /// <summary>
    /// A daemon holding one container that exited, and recording whether the
    /// adapter tried to start it or to replace it.
    /// </summary>
    private sealed class ADaemonHoldingACorpse(int exitCode) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        public bool Removed { get; private set; }

        public bool Created { get; private set; }

        public bool Started { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add($"{request.Method} {path}");

            if (!path.StartsWith("/containers", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            }

            if (path.EndsWith("/json", StringComparison.Ordinal))
            {
                // Absent once it has been removed: the recreate has to be a
                // create, not a second start of the thing that just exited.
                if (Removed)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                }

                var body = "{\"State\":{\"Running\":false,\"Status\":\"exited\","
                         + "\"ExitCode\":" + exitCode + "},"
                         + "\"Image\":\"sha256:" + new string('f', 64) + "\","
                         + "\"Config\":{\"Image\":\"" + Pinned + "\"}}";

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Delete)
            {
                Removed = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (path.EndsWith("/create", StringComparison.Ordinal))
            {
                Created = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            }

            if (path.EndsWith("/start", StringComparison.Ordinal))
            {
                Started = true;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private static MemberSpec Spec(string image) => new()
    {
        Image = image,
        ControlPlane = "https://control.example.invalid",
        Nonce = "nonce",
    };

    private static DockerPoolAdapter Adapter(ADaemonHoldingACorpse daemon) =>
        new(new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

    [Test]
    public async Task A_member_that_finished_is_replaced_rather_than_restarted()
    {
        // Exit 0 from a member's loop means its credential ended, which is the
        // one thing starting it again cannot fix: the token is not renewable
        // and the nonce it was bought with is spent. Starting it produces a
        // process that reads an ended credential and exits, and an attestation
        // that says Verified.
        var daemon = new ADaemonHoldingACorpse(exitCode: 0);

        var observed = await Adapter(daemon).RefreshAsync("gg-pool-dev", "gg-pool-dev-1", Spec(Pinned));

        await Assert.That(daemon.Removed).IsTrue()
            .Because("a member that finished has nothing left to start - its credential is "
                   + "single-use and time-boxed, and the container is cattle.");
        await Assert.That(daemon.Created).IsTrue();
        await Assert.That(observed.Outcome).IsEqualTo(PoolOutcomes.Verified);
        await Assert.That(observed.Provenance).IsEqualTo(EnvironmentProvenance.Fresh)
            .Because("what came back is a new container whatever its name says.");
    }

    [Test]
    public async Task A_member_that_was_killed_is_still_started()
    {
        // THE ARM THAT IS KEPT. A daemon restart or a reboot stops a member
        // whose stored identity is still good, and that member comes back by
        // being started - cheaper than a replacement and exactly as warm.
        // Replacing every stopped member would be right for the case above and
        // wasteful for this one, so the exit code is read rather than assumed.
        var daemon = new ADaemonHoldingACorpse(exitCode: 137);

        var observed = await Adapter(daemon).RefreshAsync("gg-pool-dev", "gg-pool-dev-1", Spec(Pinned));

        await Assert.That(daemon.Started).IsTrue();
        await Assert.That(daemon.Removed).IsFalse()
            .Because("a member that was killed still holds an identity it can come back on, "
                   + "and throwing that away arrives at the same place for more money.");
        await Assert.That(observed.Provenance).IsEqualTo(EnvironmentProvenance.Reused);
    }
}
