using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// An idle session is ended on the heartbeat after the limit, and the runner still
/// touches no pty itself (slice seventy, S70.2-04).
/// </summary>
/// <remarks>
/// <b>What bounds a standing way into a machine, in place of a lease.</b> ADR-0039
/// says so in its costs: the opt-in, the roots and this limit are the bound, and
/// they are weaker than a flight's. This one is measured from the last byte either
/// way, so a session somebody is reading is never idle while it is still writing.
/// </remarks>
public class AnIdleAgentSessionEndsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task A_session_quiet_past_the_limit_is_ended()
    {
        var now = Noon;
        var host = new FakeAgentHost();
        var sessions = new AgentSessions(
            host, [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false, now: () => now);

        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);

        now = Noon + AgentSessions.IdleLimit - TimeSpan.FromMinutes(1);
        sessions.Sweep();
        await Assert.That(host.Children.Single().Killed).IsFalse();

        now = Noon + AgentSessions.IdleLimit + TimeSpan.FromMinutes(1);
        sessions.Sweep();
        await Assert.That(host.Children.Single().Killed).IsTrue();
    }

    [Test]
    public async Task Typing_or_output_keeps_it_alive()
    {
        var now = Noon;
        var host = new FakeAgentHost();
        var sessions = new AgentSessions(
            host, [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false, now: () => now);

        var opened = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);
        var viewer = new FakeViewer();
        using var attached = opened.Session!.Attach(viewer, 80, 24);

        now = Noon + AgentSessions.IdleLimit - TimeSpan.FromMinutes(1);
        opened.Session.Input(viewer, "x"u8);

        now = Noon + AgentSessions.IdleLimit + TimeSpan.FromMinutes(1);
        sessions.Sweep();

        await Assert.That(host.Children.Single().Killed).IsFalse()
            .Because("the clock runs from the last byte either way, not from the start.");
    }

    [Test]
    public async Task The_runner_reaches_no_terminal_library()
    {
        // THE PORT IS THE WHOLE POINT. The runner's package list is SIPSorcery
        // alone (ProjectReferenceTests); a pty arriving here by convenience would
        // be the change NoTerminalTests exists to catch, and this names the new
        // file so nobody reads it as covered by accident.
        var runner = typeof(AgentSessions).Assembly;

        await Assert.That(runner.GetReferencedAssemblies().Select(a => a.Name))
            .DoesNotContain("Porta.Pty");
    }
}
