using System.Net;
using Gg.Contracts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// The pull point rides out a control plane having a bad moment, exactly as the
/// flight runner does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The twin that was missed.</b> gg#144 was reported as <i>"the runner AND
/// maintain die on a 500"</i>, and the fix landed on
/// <see cref="RunnerLoop"/> only. <see cref="MaintainLoop"/> still caught nothing
/// but <see cref="OperationCanceledException"/>, so the resident pull point kept
/// dying unhandled on 500s and 503s - restart counter 6 on a real host, with
/// systemd restarting it into the same wall.
/// </para>
/// <para>
/// <b>Silence is half the defect.</b> This loop takes no observer and prints
/// nothing at all, so a pull point that was crash-looping for hours looked
/// identical to one quietly doing its job. Surviving without saying so would keep
/// that property; the narration is what makes the survival legible.
/// </para>
/// <para>
/// <b>The classification is shared rather than copied.</b> Writing
/// <c>Transient</c> a second time here is what produced this bug in the first
/// place - two loops, one fixed - so both read one declaration.
/// </para>
/// </remarks>
public class MaintainSurvivesRefusalTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private static HttpRequestException Answering(HttpStatusCode status) =>
        new($"Response status code does not indicate success: {(int)status} ({status}).",
            inner: null, statusCode: status);

    /// <summary>A pool adapter that holds the bound and reports one clean member.</summary>
    private sealed class SteadyAdapter : IPoolAdapter
    {
        public PoolCapabilities Capabilities { get; } = new() { Provider = "fake" };

        public Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScopeProbe { Held = true, ProbedAt = T0 });

        public Task<IReadOnlyList<PoolMember>> ListAsync(
            string pool, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PoolMember>>([new PoolMember { Name = $"{pool}-1" }]);

        public Task<PoolObservation> VerifyAsync(
            PoolMember member, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Verified });

        public Task<PoolObservation> RefreshAsync(
            string pool, string member, string image, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Refreshed });

        public Task<PoolObservation> ResetAsync(
            string member, string image, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PoolObservation { Outcome = PoolOutcomes.Reset });
    }

    /// <summary>A control plane that refuses the pull a fixed number of times.</summary>
    private sealed class RefusingProtocol(Queue<Exception> pullThrows) : IPoolProtocol
    {
        public int Pulls { get; private set; }

        public Task<PoolActionList> PullActionsAsync(
            string pool, CancellationToken cancellationToken = default)
        {
            Pulls++;

            return pullThrows.Count > 0
                ? Task.FromException<PoolActionList>(pullThrows.Dequeue())
                : Task.FromResult(new PoolActionList { Actions = [] });
        }

        public Task AttestAsync(
            string pool, PoolAttestation attestation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>Runs the loop until it has cycled enough, and collects what it said.</summary>
    private static async Task<(int Exit, int Pulls, List<string> Said)> RunAsync(
        Queue<Exception> pullThrows, int cycles = 3)
    {
        var protocol = new RefusingProtocol(pullThrows);
        var stop = new CancellationTokenSource();
        var said = new List<string>();
        var turns = 0;

        var loop = new MaintainLoop(
            protocol, new SteadyAdapter(), new MovableClock(T0),
            (_, _) =>
            {
                if (++turns >= cycles)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            narrate: said.Add);

        var exit = await loop.RunAsync("gg-pool-dev", stop.Token);
        return (exit, protocol.Pulls, said);
    }

    [Test]
    public async Task A_server_error_does_not_end_the_pull_point()
    {
        // THE DEFECT. One 500 and the process was gone - and systemd restarted it
        // into the same wall six times.
        var (exit, pulls, _) = await RunAsync(new Queue<Exception>(
            [Answering(HttpStatusCode.InternalServerError)]));

        await Assert.That(pulls).IsGreaterThan(1)
            .Because("asking again is the whole job. A pull point that died on a deploy leaves "
                   + "a pool nobody maintains, and nothing says so.");
        await Assert.That(exit).IsEqualTo(0);
    }

    [Test]
    public async Task A_service_unavailable_is_survived_too()
    {
        // The other one seen on the host, during a container-app revision roll.
        var (_, pulls, _) = await RunAsync(new Queue<Exception>(
            [Answering(HttpStatusCode.ServiceUnavailable)]));

        await Assert.That(pulls).IsGreaterThan(1);
    }

    [Test]
    public async Task The_refusal_is_said_out_loud()
    {
        // This loop narrates NOTHING today, which is why a pull point
        // crash-looping for hours looked like one quietly working. Surviving in
        // silence would preserve exactly that.
        var (_, _, said) = await RunAsync(new Queue<Exception>(
            [Answering(HttpStatusCode.InternalServerError)]));

        await Assert.That(said.Any(s => s.Contains("500", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a pull point that turns a loud crash into a quiet outage has made the "
                   + "problem harder to find, not smaller.");
    }

    [Test]
    public async Task An_unauthorized_pull_point_stops_instead_of_asking_forever()
    {
        // THE TWIN THAT KEEPS THE FIX HONEST, and the same line RunnerLoop holds.
        // A 401 is this machine's credential, and no amount of waiting fixes one.
        var stop = new CancellationTokenSource();
        var protocol = new RefusingProtocol(new Queue<Exception>(
            [Answering(HttpStatusCode.Unauthorized)]));

        var loop = new MaintainLoop(
            protocol, new SteadyAdapter(), new MovableClock(T0),
            (_, _) => Task.CompletedTask,
            narrate: _ => { });

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await loop.RunAsync("gg-pool-dev", stop.Token));

        await Assert.That(thrown!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task One_declaration_decides_transience_for_both_loops()
    {
        // The structural half. This bug existed because the rule was written in
        // one loop and not the other; a second copy here would set the same trap
        // for whoever fixes the next one.
        await Assert.That(TransientFailure.IsTransient(Answering(HttpStatusCode.InternalServerError)))
            .IsTrue();
        await Assert.That(TransientFailure.IsTransient(Answering(HttpStatusCode.Unauthorized)))
            .IsFalse();
        await Assert.That(TransientFailure.IsTransient(new HttpRequestException("refused")))
            .IsTrue()
            .Because("no status at all means the request never reached anything with an opinion.");
    }
}
