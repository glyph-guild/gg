using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A machine that takes ad hoc sessions says so on every beat, reports what it
/// holds, and sweeps the idle ones on the same beat (slice seventy, S70.1-02 and
/// S70.2-04; ADR-0039 Decisions 3 and 5).
/// </summary>
/// <remarks>
/// <b>The beat is where a machine already tells the control plane what it will take</b>
/// (<c>AcceptsConfiguration</c>), and where it already answers introductions - so
/// the opt-in, the record of sessions and the idle bound all ride it rather than a
/// second report.
/// </remarks>
public class AMachineReportsItsSessionsOnItsBeatTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed class NoLog : IReadOnlyLog
    {
        public TailRead Tail(int lines) => new([], false);
    }

    private static async Task BeatOnceAsync(FakeProtocol protocol, IClock clock, AgentSessions? sessions)
    {
        using var stopping = new CancellationTokenSource();
        var says = new WhatThisRunnerSays(new RecordingObserver(), _ => new NoLog(), () => T0);
        var rounds = 0;

        await new RunnerLoop(protocol, clock,
                (_, _) =>
                {
                    if (++rounds > 1)
                    {
                        stopping.Cancel();
                    }

                    return Task.CompletedTask;
                },
                says, new NoCredentialResolver(), new NoWorkspace(),
                agentSessions: sessions)
            .RunAsync("runner-1", ["linux"], stopping.Token);
    }

    [Test]
    public async Task A_machine_that_takes_sessions_says_so_and_lists_them()
    {
        var protocol = new FakeProtocol();
        var sessions = new AgentSessions(
            new FakeAgentHost(), [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false, now: () => T0);
        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);

        await BeatOnceAsync(protocol, new MovableClock(T0), sessions);

        await Assert.That(protocol.LastAcceptsAgentSessions).IsTrue();
        await Assert.That(protocol.LastAgentSessions!.Select(s => s.SessionId)).Contains("a1b2")
            .Because("the control plane keeps who used a machine and for how long from this.");
    }

    [Test]
    public async Task A_machine_that_does_not_says_nothing()
    {
        var protocol = new FakeProtocol();

        await BeatOnceAsync(protocol, new MovableClock(T0), sessions: null);

        await Assert.That(protocol.LastAcceptsAgentSessions).IsNull()
            .Because("absent is not opted in, and a machine that did not opt in has nothing to say.");
        await Assert.That(protocol.LastAgentSessions).IsNull();
    }

    [Test]
    public async Task The_beat_sweeps_what_has_gone_idle()
    {
        var protocol = new FakeProtocol();
        var clock = new MovableClock(T0);
        var host = new FakeAgentHost();
        var sessions = new AgentSessions(
            host, [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false, now: () => clock.UtcNow);
        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);

        clock.Advance(AgentSessions.IdleLimit + TimeSpan.FromMinutes(1));
        await BeatOnceAsync(protocol, clock, sessions);

        await Assert.That(host.Children.Single().Killed).IsTrue();
    }

    [Test]
    public async Task The_runner_knows_when_it_is_flying()
    {
        // WHAT REFUSES A NEW SESSION WHILE A FLIGHT RUNS: the runner's own
        // account of what it is doing, the one `status` already answers from.
        var says = new WhatThisRunnerSays(new RecordingObserver(), _ => new NoLog(), () => T0);

        await Assert.That(says.Flying).IsFalse();

        says.Claimed(new LeaseGranted
        {
            LeaseId = "lease-1",
            Generation = 1,
            FlightId = "flight-1",
            FlightNumber = "GG-1",
            Repos = [],
            Credentials = [],
            ClassificationCeiling = Classifications.Internal,
            ClassificationRules = ClassificationRules.Default,
            ExpiresAt = T0.AddMinutes(30),
            RenewWithinSeconds = 30,
        });
        await Assert.That(says.Flying).IsTrue();

        says.Released("lease-1", "landed");
        await Assert.That(says.Flying).IsFalse();
    }
}
