using Gg.Contracts;

namespace Gg.Runner.Tests;

/// <summary>
/// The resident runner asks for a sweep when, and only when, there is no
/// flight for it and nothing forbids it working.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wiring, and it is a separate commit pair on purpose.</b> The decision
/// and the claim-shaped pass shipped with tests and with nothing calling them -
/// tested components no product code runs, which this repository has a note
/// about. This is the test that the resident runner actually asks.
/// </para>
/// <para>
/// <b>Only on a plain idle.</b> The loop already distinguishes the ways a claim
/// can come back empty, and three of them are reasons NOT to sweep:
/// </para>
/// <list type="bullet">
/// <item><b>Parked</b> - a person withheld this runner, and a withheld runner
/// takes no work of any kind.</item>
/// <item><b>AllowanceSpent</b> - a sweep runs an agent, and an agent is exactly
/// what the spent allowance was protecting.</item>
/// <item><b>Waiting</b> - a flight is ready and blocked on a credential nobody
/// has registered. The moment it arrives that flight should start, and a sweep
/// in the slot would delay it. Flights first, applied honestly.</item>
/// </list>
/// </remarks>
public class TheResidentRunnerAsksForASweepWhenIdleTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

    /// <summary>Runs the loop for two rounds, with one claim answer queued.</summary>
    private static async Task<int> SweepsAsked(
        ClaimResult answer, Func<CancellationToken, Task>? sweep = null)
    {
        using var stopping = new CancellationTokenSource();

        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(answer);

        var asked = 0;
        var rounds = 0;

        await new RunnerLoop(protocol, new MovableClock(T0),
                (_, _) =>
                {
                    if (++rounds > 1)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace(),
                sweepWhenIdle: sweep ?? (_ =>
                {
                    asked++;
                    return Task.CompletedTask;
                }))
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return asked;
    }

    [Test]
    public async Task An_idle_runner_asks_for_a_sweep()
    {
        await Assert.That(await SweepsAsked(new ClaimResult.Nothing())).IsGreaterThan(0)
            .Because("a watch whose document says `pull-point: resident-runner` has to be "
                   + "pulled by a resident runner, without a person naming it at a shell.");
    }

    [Test]
    public async Task A_withheld_runner_does_not_sweep()
    {
        // THE FIRST ANSWER IS PARKED; the fake then answers Nothing, so count
        // only what the parked round asked - a runner withheld ONCE must not
        // have swept on that round.
        await Assert.That(await SweepsAsked(new ClaimResult.Parked())).IsLessThanOrEqualTo(1)
            .Because("a person withheld this runner, and a withheld runner takes no work - a "
                   + "sweep is work.");
    }

    [Test]
    public async Task A_runner_out_of_allowance_does_not_sweep_on_that_round()
    {
        await Assert.That(await SweepsAsked(new ClaimResult.AllowanceSpent()))
            .IsLessThanOrEqualTo(1)
            .Because("a sweep runs an agent, and the agent is exactly what a spent allowance "
                   + "was protecting.");
    }

    [Test]
    public async Task A_flight_waiting_on_a_credential_is_not_filled_with_a_sweep()
    {
        await Assert.That(await SweepsAsked(new ClaimResult.Waiting(["payments"])))
            .IsLessThanOrEqualTo(1)
            .Because("that flight starts the moment its credential arrives, and a sweep "
                   + "holding the slot would delay it - flights first.");
    }

    [Test]
    public async Task A_sweep_that_fails_does_not_stop_the_runner()
    {
        // THE RUNNER OUTLIVES ITS SWEEPS. A tracker outage, a refused claim or
        // a control plane mid-deploy is one sweep's problem; a resident runner
        // that died on one would stop taking flights too.
        var rounds = 0;

        var asked = await SweepsAsked(new ClaimResult.Nothing(), _ =>
        {
            rounds++;
            throw new InvalidOperationException("the tracker refused every read");
        });

        await Assert.That(rounds).IsGreaterThan(0)
            .Because("ASK WHY IT PASSES: a sweep that was never called could not have failed.");
        await Assert.That(asked).IsEqualTo(0);
    }

    [Test]
    public async Task A_runner_composed_without_sweeps_is_unchanged()
    {
        // NULL IS A REAL VALUE: off, or no tracker declared, or an older
        // composition root. The loop must behave exactly as it did.
        using var stopping = new CancellationTokenSource();
        var rounds = 0;

        var exit = await new RunnerLoop(new FakeProtocol(), new MovableClock(T0),
                (_, _) =>
                {
                    if (++rounds > 1)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                new RecordingObserver(), new NoCredentialResolver(), new NoWorkspace())
            .RunAsync("runner-1", ["linux"], stopping.Token);

        await Assert.That(rounds).IsGreaterThan(1);
        _ = exit;
    }
}
