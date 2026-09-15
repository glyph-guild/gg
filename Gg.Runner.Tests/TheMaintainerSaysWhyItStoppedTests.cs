using System.Net;
using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The pull point's own credential ending is not a crash either.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same defect the flight loop had, one process over.</b>
/// <c>ACredentialEndingIsNotACrashTests</c> records it: a pool member died
/// inside an unhandled 401 with exit 139 and a stack trace in a container log
/// nobody can open a shell on. <c>MaintainLoop</c> catches only what
/// <c>TransientFailure.IsTransient</c> admits — a 5xx, a timeout, a refused
/// connection — and a 401 is none of those, so it leaves this loop the way it
/// used to leave that one.
/// </para>
/// <para>
/// <b>And it is worse here, because of what stops.</b> A dead flight runner
/// stops taking flights. A dead pull point stops keeping every environment on
/// the host warm, and the control plane reads the silence as staleness —
/// <i>"the pull point stopped attesting"</i> — which sends somebody to look at
/// a machine rather than at a credential.
/// </para>
/// <para>
/// <b>Loudly is kept and the crash is not</b>, exactly as the flight loop's
/// rule now reads. The loop stops on the first one, because no amount of waiting
/// fixes a credential, and it says which of the two things happened: past its
/// recorded expiry the credential simply ended, and before it somebody revoked
/// or retired this runner.
/// </para>
/// <para>
/// <b>Renewal does not make this dead code.</b> A runner whose credential was
/// REVOKED meets this on its next request whatever its expiry says, and a
/// credential the control plane declines to renew reaches its end on schedule.
/// The renewal is what makes the ordinary case rare; this is what the remaining
/// cases read like.
/// </para>
/// </remarks>
public class TheMaintainerSaysWhyItStoppedTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static HttpRequestException Refused() =>
        new("Response status code does not indicate success: 401 (Unauthorized).",
            inner: null, statusCode: HttpStatusCode.Unauthorized);

    /// <summary>A control plane that refuses this runner's credential.</summary>
    private sealed class ARefusingControlPlane : IPoolProtocol
    {
        public int Pulls { get; private set; }

        public List<PoolAttestation> Attested { get; } = [];

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            Pulls++;
            throw Refused();
        }

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(null);

        /// <summary>Whether the ledger is behind the same refused door.</summary>
        public bool RefusesAttestations { get; init; }

        public Task AttestAsync(
            string pool, PoolAttestation attestation,
            CancellationToken cancellationToken = default)
        {
            if (RefusesAttestations)
            {
                throw Refused();
            }

            Attested.Add(attestation);
            return Task.CompletedTask;
        }
    }

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

    private sealed record Ran(int Exit, ARefusingControlPlane Protocol, List<string> Said);

    private static async Task<Ran> RunAsync(DateTimeOffset? expiresAt)
    {
        var protocol = new ARefusingControlPlane();
        var said = new List<string>();

        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            protocol, new AnEmptyPool(), new MovableClock(Now),
            (_, _) =>
            {
                // Four cycles' worth of patience, so a loop that kept asking
                // has room to prove it.
                if (++turns >= 4)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            narrate: said.Add,
            credentialExpiresAt: expiresAt);

        return new Ran(await loop.RunAsync("gg-pool-dev", stop.Token), protocol, said);
    }

    [Test]
    public async Task A_refused_credential_ends_the_session_rather_than_the_process()
    {
        var ran = await RunAsync(Now + TimeSpan.FromDays(10));

        await Assert.That(ran.Exit).IsNotEqualTo(0)
            .Because("a pull point that has been revoked is not maintaining anything, and "
                   + "leaving with a zero would read as a session that ended tidily.");
    }

    [Test]
    public async Task And_it_asks_exactly_once()
    {
        // No amount of waiting fixes a credential. The transient arm exists for
        // the control plane's bad minutes and this is not one.
        var ran = await RunAsync(Now + TimeSpan.FromDays(10));

        await Assert.That(ran.Protocol.Pulls).IsEqualTo(1);
    }

    [Test]
    public async Task A_revocation_is_told_apart_from_an_ending()
    {
        // The flight loop's three-way rule, and the same reason: exiting the
        // same way for both would make somebody removing a machine look like a
        // machine that reached the end of its life.
        var ran = await RunAsync(Now + TimeSpan.FromDays(10));

        await Assert.That(string.Join(" ", ran.Said)).Contains("revoked")
            .Because("its credential does not expire for ten days, so somebody took it away.");
    }

    [Test]
    public async Task A_credential_past_its_expiry_says_so_instead()
    {
        var ran = await RunAsync(Now - TimeSpan.FromMinutes(1));

        await Assert.That(string.Join(" ", ran.Said)).Contains("expired")
            .Because("a credential that ran out is the one case where nothing is wrong, and "
                   + "a person reading the log needs to be told that rather than left to "
                   + "compare two timestamps.");
    }

    [Test]
    public async Task A_runner_with_no_recorded_expiry_does_not_guess()
    {
        // UNKNOWN IS NOT FALSE, and not true either. Every pull point
        // registered before an expiry was carried is in this state.
        var ran = await RunAsync(expiresAt: null);

        var said = string.Join(" ", ran.Said);

        await Assert.That(said).Contains("not known")
            .Because("inferring 'expired' would tell somebody nothing is wrong when a "
                   + "credential may have been taken away.");
        await Assert.That(ran.Exit).IsNotEqualTo(0);
    }

    [Test]
    public async Task The_ledger_is_not_where_this_one_can_be_said()
    {
        // THE BROKEN-BOUND REFUSAL'S PRECEDENT DOES NOT REACH HERE, and saying
        // why is the point of this test. That refusal attests before exiting 69
        // because "escalation reads the ledger, and a silent exit would be
        // nothing-arrived-nothing-complained" - and it can, because the
        // credential is fine and only the scope is broken.
        //
        // A 401 is the credential. An attestation travels on the same one, so
        // it would be refused by the same door; writing to the ledger here is
        // not a thing this machine can do, and a test that made it work would
        // be proving a fake. The journal is where it is said, and a pool host
        // is a machine whose journal somebody reads.
        var ran = await RunAsync(Now + TimeSpan.FromDays(10));

        await Assert.That(ran.Said).IsNotEmpty()
            .Because("the control plane will read the silence as staleness - 'the pull point "
                   + "stopped attesting' - and send somebody to look at the machine. The one "
                   + "sentence that says otherwise is the one on this host.");
    }

    [Test]
    public async Task An_attestation_that_is_also_refused_is_not_the_crash_again()
    {
        // AND THIS IS WHY IT IS NOT ATTEMPTED BLINDLY. A loop that tried to
        // attest its own credential failure would meet the 401 a second time,
        // inside the handler for the first - which is an unhandled exception
        // from a catch block, and exactly the exit 139 this slice is about.
        var protocol = new ARefusingControlPlane { RefusesAttestations = true };
        var said = new List<string>();

        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            protocol, new AnEmptyPool(), new MovableClock(Now),
            (_, _) =>
            {
                if (++turns >= 4)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            narrate: said.Add,
            credentialExpiresAt: Now + TimeSpan.FromDays(10));

        var exit = await loop.RunAsync("gg-pool-dev", stop.Token);

        await Assert.That(exit).IsNotEqualTo(0);
        await Assert.That(string.Join(" ", said)).Contains("revoked");
    }

    [Test]
    public async Task A_bad_minute_is_still_only_a_bad_minute()
    {
        // THE ANCHOR. gg#144 and two repeats after it: this loop must not stop
        // for a 500, a timeout or a refused connection, because "a refusal is
        // not a reason to stop maintaining a pool" and systemd restarting it
        // into the same wall is how a pool grew to 196 members unattended.
        var protocol = new AFlakyControlPlane();
        var said = new List<string>();

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
            narrate: said.Add,
            credentialExpiresAt: Now + TimeSpan.FromDays(10));

        var exit = await loop.RunAsync("gg-pool-dev", stop.Token);

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(protocol.Pulls).IsGreaterThan(1)
            .Because("a control plane mid-deploy is asked again, and that has been the rule "
                   + "since the first time this loop died of one.");
    }

    /// <summary>A control plane having a bad minute, forever.</summary>
    private sealed class AFlakyControlPlane : IPoolProtocol
    {
        public int Pulls { get; private set; }

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            Pulls++;
            throw new HttpRequestException(
                "Response status code does not indicate success: 503 (Service Unavailable).",
                inner: null, statusCode: HttpStatusCode.ServiceUnavailable);
        }

        public Task<MemberCredentialMinted?> MintMemberAsync(
            string pool, string member, CancellationToken cancellationToken = default) =>
            Task.FromResult<MemberCredentialMinted?>(null);

        public Task AttestAsync(
            string pool, PoolAttestation attestation,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
