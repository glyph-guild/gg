using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A pull point that keeps a host's environments warm has to say what it is
/// doing, because nobody is watching it and nobody can ask.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured 2026-09-27, and this is what it cost.</b> Two pools on one host
/// sat at zero warm members — one for 27 hours, one for eight days — while
/// <c>gg-runner-maintain-ui</c> verified every five seconds and attested
/// <i>'gg-pool-ui-1' exists and is not running (status: exited)</i> every single
/// time. <c>journalctl -u gg-runner-maintain-ui</c> held nothing but systemd's
/// own start and stop lines: seven restarts over three days and not one line of
/// application output. The host could not say why either pool was empty, so the
/// diagnosis had to be reconstructed from the control plane's ledger and the
/// planner's source.
/// </para>
/// <para>
/// <b><c>narrate</c> already existed for this reason and did not reach far
/// enough.</b> Its own parameter doc says the loop <i>"reported NOTHING for its
/// whole life, so hours of crash-looping looked identical to hours of quietly
/// working"</i> — but every call site is a credential ending, a transient
/// refusal or a build failure. The ordinary case, an action that ran and came
/// back failed, was silent.
/// </para>
/// <para>
/// <b>A failure is state; an action is an event.</b> The loop turns every five
/// seconds, so narrating a standing failure each time would be seventeen
/// thousand identical lines a day — which is the same as silence, only more
/// expensive to read. So a failing verify is said when it starts and when it
/// clears, and a decided action is said every time because deciding one is rare
/// and somebody is waiting to see it land.
/// </para>
/// </remarks>
public class TheMaintainerSaysWhatItDidTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 20, 0, 0, TimeSpan.Zero);

    private const string Exited = "'gg-pool-ui-1' exists and is not running (status: exited).";

    /// <summary>A control plane that serves what it is given and records nothing else.</summary>
    private sealed class AServingControlPlane(params PoolAction[] once) : IPoolProtocol
    {
        private bool _served;

        public List<PoolAttestation> Attested { get; } = [];

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            // SERVED ONCE, the way the real door serves: the UPDATE that hands an
            // action over is the claim, so a second pull sees nothing.
            if (_served)
            {
                return Task.FromResult(new PoolActionList { Actions = [] });
            }

            _served = true;
            return Task.FromResult(new PoolActionList { Actions = once });
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
    /// One member, whose verify outcome is whatever the test says on that turn.
    /// </summary>
    private sealed class APoolOfOne(Func<int, PoolObservation> verifies) : IPoolAdapter
    {
        private int _turn;

        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public string? Acted { get; private set; }

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe { Held = true, ProbedAt = Now });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>(
                [new PoolMember { Name = $"{pool}-1", Running = false }]);

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(verifies(_turn++));

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, MemberSpec spec,
            CancellationToken cancellationToken = default)
        {
            Acted = PoolActions.Refresh;
            return Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
        }

        public Task<PoolObservation> ResetAsync(
            string member, MemberSpec spec, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });
    }

    private static PoolObservation Failed(string diagnosis) =>
        new() { Outcome = PoolOutcomes.Failed, Diagnosis = diagnosis };

    private static PoolObservation Verified() =>
        new() { Outcome = PoolOutcomes.Verified };

    private static PoolAction Refresh() =>
        new()
        {
            ActionId = Guid.NewGuid(),
            Pool = "gg-pool-ui",
            Action = PoolActions.Refresh,
            Image = "a-registry/gg-member-browser@sha256:" + new string('a', 64),
            StrategyVersion = "ui@v17",
            DecidedAt = Now,
        };

    private static async Task<List<string>> RunAsync(
        Func<int, PoolObservation> verifies, int cycles = 4, params PoolAction[] serve)
    {
        var said = new List<string>();
        using var stop = new CancellationTokenSource();
        var turns = 0;

        var loop = new MaintainLoop(
            new AServingControlPlane(serve),
            new APoolOfOne(verifies),
            new MovableClock(Now),
            (_, _) =>
            {
                if (++turns >= cycles)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            narrate: said.Add);

        _ = await loop.RunAsync("gg-pool-ui", stop.Token);
        return said;
    }

    [Test]
    public async Task A_failing_verify_is_said_out_loud()
    {
        var said = await RunAsync(_ => Failed(Exited));

        await Assert.That(string.Join("\n", said)).Contains(Exited)
            .Because("a pool empty because its member exited is the whole diagnosis, and it "
                   + "was already being attested every five seconds - saying it here is what "
                   + "lets somebody on the host read it without the control plane.");
    }

    [Test]
    public async Task And_it_is_said_once_rather_than_on_every_turn()
    {
        var said = await RunAsync(_ => Failed(Exited), cycles: 4);

        await Assert.That(said.Count(line => line.Contains(Exited, StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("the loop turns every five seconds and a standing failure does not change, "
                   + "so repeating it is seventeen thousand identical lines a day - which reads "
                   + "the same as silence and costs more to page through.");
    }

    [Test]
    public async Task A_pool_that_verifies_again_says_that_too()
    {
        // Fails on the first turn, verifies afterwards: the recovery is the
        // second thing a person watching needs, and a loop that only ever
        // announced failures would leave them believing the first one stands.
        var said = await RunAsync(turn => turn == 0 ? Failed(Exited) : Verified());

        await Assert.That(said.Count(line => line.Contains(Exited, StringComparison.Ordinal)))
            .IsEqualTo(1);
        await Assert.That(string.Join("\n", said)).Contains("verifies")
            .Because("a failure that cleared has to be said, or the last word on this pool is "
                   + "a fault that no longer exists.");
    }

    [Test]
    public async Task An_executed_action_says_what_it_was_and_how_it_came_out()
    {
        var said = await RunAsync(_ => Verified(), cycles: 4, serve: Refresh());

        var about = said.Where(line =>
            line.Contains(PoolActions.Refresh, StringComparison.Ordinal)).ToList();

        await Assert.That(about).IsNotEmpty()
            .Because("a decided action is an event rather than a state: it is rare, somebody "
                   + "is waiting to see it land, and the host is the only place that knows "
                   + "whether it ran.");
        await Assert.That(string.Join("\n", about)).Contains(PoolOutcomes.Verified)
            .Because("the outcome is the half that matters - 'a refresh ran' and 'a refresh "
                   + "worked' are different sentences and only one of them is worth reading.");
    }
}
