using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// The machine writes its sessions down, so a restart forgets none and nothing is
/// forgotten after a day, and a resume runs in the session's own directory (slice
/// seventy-one, S71.2-02; ADR-0039 Decision 8).
/// </summary>
/// <remarks>
/// <b>It replaces a defect as well as a gap.</b> The table was memory only and dropped an
/// ended session after 24 hours, and a session it no longer held was "resumed" by
/// starting <c>claude --session-id</c>: a new, empty conversation under the old id.
/// Claude files a conversation under the directory it ran in, so the directory has to
/// come back with it.
/// </remarks>
public class TheMachineKeepsALedgerOfSessionsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static string Home() =>
        Path.Combine(Directory.CreateTempSubdirectory("gg-agent-home-").FullName, "agent-sessions");

    private static async Task<AgentSessions> Ended(string home, FakeAgentHost host, Func<DateTimeOffset> now)
    {
        var sessions = new AgentSessions(host, home, flying: () => false, now: now);
        _ = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);
        host.Children.Single().End(0);
        await Eventually.TrueAsync(() => sessions.Standings().Single().Alive is false);
        return sessions;
    }

    [Test]
    public async Task A_restart_still_lists_an_ended_session()
    {
        var home = Home();
        var now = Noon;
        (await Ended(home, new FakeAgentHost(), () => now)).Dispose();

        using var restarted = new AgentSessions(new FakeAgentHost(), home, flying: () => false, now: () => now);

        var kept = restarted.Standings().Single();
        await Assert.That(kept.SessionId).IsEqualTo("a1b2");
        await Assert.That(kept.Alive).IsFalse();
        await Assert.That(kept.Directory).IsEqualTo(Path.Combine(home, "a1b2"));
        await Assert.That(kept.EndedAt).IsNotNull();
    }

    [Test]
    public async Task A_session_running_when_the_machine_stopped_reads_as_ended_after()
    {
        var home = Home();
        var first = new AgentSessions(new FakeAgentHost(), home, flying: () => false, now: () => Noon);
        _ = await first.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);
        first.Dispose();

        using var restarted = new AgentSessions(new FakeAgentHost(), home, flying: () => false, now: () => Noon);

        await Assert.That(restarted.Standings().Single().Alive).IsFalse()
            .Because("its agent died with the runner, so it can only be resumed.");
    }

    [Test]
    public async Task An_ended_session_is_kept_past_a_day()
    {
        var now = Noon;
        using var sessions = await Ended(Home(), new FakeAgentHost(), () => now);

        now = Noon.AddDays(3);
        sessions.Sweep();

        await Assert.That(sessions.Standings().Select(s => s.SessionId)).IsEquivalentTo(["a1b2"])
            .Because("a session's directory is the person's work, and only they know when it is done with.");
    }

    [Test]
    public async Task Resuming_after_a_restart_resumes_in_its_own_directory()
    {
        var home = Home();
        (await Ended(home, new FakeAgentHost(), () => Noon)).Dispose();

        var host = new FakeAgentHost();
        using var restarted = new AgentSessions(host, home, flying: () => false, now: () => Noon);
        var opened = await restarted.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);

        await Assert.That(opened.Refused).IsNull();
        var asked = host.Started.Single();
        await Assert.That(asked.Resume).IsTrue()
            .Because("it was started with --session-id before; carrying it on is --resume.");
        await Assert.That(asked.Directory).IsEqualTo(Path.Combine(home, "a1b2"));
    }
}
