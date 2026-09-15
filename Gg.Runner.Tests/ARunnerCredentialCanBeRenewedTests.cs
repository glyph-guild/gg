using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine that nobody visits must not need visiting every thirty days.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal said it plainly.</b> <c>gg runner maintain</c> on a pool host
/// says: <i>"a pool host runs on its own runner token, which lasts thirty days
/// and cannot be renewed - so a person signs in once to mint a new one."</i>
/// That is a design stance and it was the right one while a runner token was
/// the only thing a person could mint. It also means the maintainer — the
/// process whose whole job is keeping a pool warm without anybody present —
/// stops every thirty days and waits for somebody, and everything downstream of
/// it goes cold.
/// </para>
/// <para>
/// <b>What stays a person's act is REGISTRATION, and renewal does not touch
/// it.</b> A renewal is authorized by the runner's own current credential, so
/// it grants nothing a runner did not already have: revoking or retiring the
/// runner ends it immediately, because the credential that would ask is the one
/// that was taken away; and a runner that is dark when its credential runs out
/// cannot renew afterwards, so a machine that stopped still needs a person.
/// Nothing new can be minted from nothing.
/// </para>
/// <para>
/// <b>A member may not renew, and the control plane is what says so.</b> A pool
/// member's twelve hours are deliberate — <i>"reset is only a boundary if the
/// credential dies with the container"</i> — and a member that renewed itself
/// would erase that boundary. The runner does not decide its own eligibility:
/// it asks, and a refusal is an ordinary answer it carries on from.
/// </para>
/// <para>
/// <b>Unknown does not renew.</b> A runner with no recorded expiry has nothing
/// falling due, and renewing on a guess would have every runner asking on every
/// cycle forever.
/// </para>
/// </remarks>
public class ARunnerCredentialCanBeRenewedTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A control plane that answers renewals from a script.</summary>
    private sealed class ARenewingControlPlane : IPoolProtocol, IRunnerCredential
    {
        public Queue<RunnerCredentialRenewed?> Answers { get; } = [];

        public int Asked { get; private set; }

        /// <summary>How many of the first asks answer with a bad moment.</summary>
        public int Transient { get; init; }

        public Task<RunnerCredentialRenewed?> RenewCredentialAsync(
            CancellationToken cancellationToken = default)
        {
            Asked++;
            if (Asked <= Transient)
            {
                throw new HttpRequestException(
                    "Response status code does not indicate success: 503 (Service Unavailable).",
                    inner: null, statusCode: System.Net.HttpStatusCode.ServiceUnavailable);
            }

            return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : null);
        }

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolActionList { Actions = [] });

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(null);

        public Task AttestAsync(
            string pool, PoolAttestation attestation,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>A pool with nothing in it, so the cycle is only the renewal.</summary>
    private sealed class AnEmptyPool : IPoolAdapter
    {
        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe { Held = true, ProbedAt = Now });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>([]);

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
    }

    private sealed record Swept(
        ARenewingControlPlane Protocol, List<DateTimeOffset> Remembered);

    private static async Task<Swept> SweepAsync(
        DateTimeOffset? expiresAt, int cycles = 1, params RunnerCredentialRenewed?[] answers)
    {
        var protocol = new ARenewingControlPlane();
        foreach (var answer in answers)
        {
            protocol.Answers.Enqueue(answer);
        }

        var remembered = new List<DateTimeOffset>();
        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            protocol, new AnEmptyPool(), new MovableClock(Now),
            (_, _) =>
            {
                if (++turns >= cycles)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            credential: protocol,
            credentialExpiresAt: expiresAt,
            credentialRenewed: renewed =>
            {
                remembered.Add(renewed);
                return Task.CompletedTask;
            });

        _ = await loop.RunAsync("gg-pool-dev", stop.Token);
        return new Swept(protocol, remembered);
    }

    [Test]
    public async Task A_credential_falling_due_is_renewed()
    {
        var swept = await SweepAsync(
            Now + TimeSpan.FromDays(1),
            answers: new RunnerCredentialRenewed { ExpiresAt = Now + TimeSpan.FromDays(30) });

        await Assert.That(swept.Protocol.Asked).IsEqualTo(1)
            .Because("a machine nobody visits cannot stop every thirty days waiting to be "
                   + "visited. This is the pull point that keeps a pool warm.");
    }

    [Test]
    public async Task The_new_expiry_is_remembered_where_the_next_start_will_read_it()
    {
        // A renewal only the running process knows about is a renewal that dies
        // with the process: the next start reads the stored identity, finds it
        // expired, and asks for a person who has nothing to do.
        var renewedTo = Now + TimeSpan.FromDays(30);
        var swept = await SweepAsync(
            Now + TimeSpan.FromDays(1),
            answers: new RunnerCredentialRenewed { ExpiresAt = renewedTo });

        await Assert.That(swept.Remembered).IsEquivalentTo([renewedTo]);
    }

    [Test]
    public async Task A_renewed_credential_is_not_asked_about_again()
    {
        // The window is what makes this a renewal rather than a poll. Asking
        // every cycle would be a five-second heartbeat against an endpoint that
        // writes, for twenty-nine days.
        var swept = await SweepAsync(
            Now + TimeSpan.FromDays(1),
            cycles: 4,
            answers: new RunnerCredentialRenewed { ExpiresAt = Now + TimeSpan.FromDays(30) });

        await Assert.That(swept.Protocol.Asked).IsEqualTo(1);
    }

    [Test]
    public async Task A_credential_with_time_left_is_left_alone()
    {
        var swept = await SweepAsync(Now + TimeSpan.FromDays(29), cycles: 3);

        await Assert.That(swept.Protocol.Asked).IsEqualTo(0);
    }

    [Test]
    public async Task A_runner_with_no_recorded_expiry_never_asks()
    {
        // UNKNOWN IS NOT DUE. Renewing on a guess would have every runner
        // registered before this asking on every cycle, forever.
        var swept = await SweepAsync(expiresAt: null, cycles: 3);

        await Assert.That(swept.Protocol.Asked).IsEqualTo(0);
    }

    [Test]
    public async Task A_refused_renewal_does_not_stop_the_loop_or_repeat_every_cycle()
    {
        // A MEMBER IS THE CASE. Its twelve hours are the container boundary and
        // the control plane refuses to extend them - which is an answer, not a
        // fault. The loop carries on keeping its pool warm and does not turn a
        // refusal into a write every five seconds.
        // No answer queued: the control plane has nothing to give this one.
        var swept = await SweepAsync(Now + TimeSpan.FromHours(1), cycles: 4);

        await Assert.That(swept.Protocol.Asked).IsEqualTo(1);
        await Assert.That(swept.Remembered).IsEmpty();
    }

    [Test]
    public async Task A_control_plane_having_a_moment_is_asked_again()
    {
        // THE OTHER HALF OF THE REFUSAL, and the reason a refusal is a null
        // rather than an exception. A deploy, a restart, a cold start - none of
        // them means this credential cannot be renewed, and a runner that
        // stopped asking on the first 503 would reach its expiry with nothing
        // having gone wrong at all.
        var protocol = new ARenewingControlPlane { Transient = 1 };
        protocol.Answers.Enqueue(
            new RunnerCredentialRenewed { ExpiresAt = Now + TimeSpan.FromDays(30) });

        var remembered = new List<DateTimeOffset>();
        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            protocol, new AnEmptyPool(), new MovableClock(Now),
            (_, _) =>
            {
                if (++turns >= 3)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            credential: protocol,
            credentialExpiresAt: Now + TimeSpan.FromDays(1),
            credentialRenewed: renewed =>
            {
                remembered.Add(renewed);
                return Task.CompletedTask;
            });

        _ = await loop.RunAsync("gg-pool-dev", stop.Token);

        await Assert.That(protocol.Asked).IsEqualTo(2);
        await Assert.That(remembered).IsNotEmpty()
            .Because("the second ask succeeded, and a renewal nobody remembers is one the "
                   + "next start cannot use.");
    }
}
