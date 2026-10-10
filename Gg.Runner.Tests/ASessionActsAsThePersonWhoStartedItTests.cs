using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A session started after the console delegated a credential gives the agent that
/// credential and gg's tools, holds it in memory only, and revokes it when the agent ends
/// (ADR-0039 Amendment 2, Decisions 13 and 14).
/// </summary>
public class ASessionActsAsThePersonWhoStartedItTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed record Machine(AgentSessions Sessions, FakeAgentHost Host, List<string> Revoked, string Home);

    private static Machine Make()
    {
        var home = Path.Combine(Directory.CreateTempSubdirectory("gg-agent-home-").FullName, "agent-sessions");
        var host = new FakeAgentHost();
        var revoked = new List<string>();
        var sessions = new AgentSessions(
            host, home, flying: () => false, now: () => Noon,
            controlPlane: "https://cp.example/",
            revoke: token =>
            {
                lock (revoked)
                {
                    revoked.Add(token);
                }

                return Task.CompletedTask;
            });
        return new Machine(sessions, host, revoked, home);
    }

    private static readonly DelegateAgentSession Delegated = new() { Token = "t0k3n", ExpiresAt = Noon.AddHours(4) };

    [Test]
    public async Task The_agent_is_given_the_credential_the_control_plane_and_gg_s_tools()
    {
        var machine = Make();

        _ = await machine.Sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None, Delegated);

        var asked = machine.Host.Started.Single();
        await Assert.That(asked.Environment[AgentSessions.TokenVariable]).IsEqualTo("t0k3n");
        await Assert.That(asked.Environment[AgentSessions.ControlPlaneVariable]).IsEqualTo("https://cp.example/");
        await Assert.That(asked.Tools).IsTrue();
    }

    [Test]
    public async Task Without_one_the_agent_gets_neither()
    {
        var machine = Make();

        _ = await machine.Sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);

        var asked = machine.Host.Started.Single();
        await Assert.That(asked.Environment.ContainsKey(AgentSessions.TokenVariable)).IsFalse();
        await Assert.That(asked.Tools).IsFalse();
    }

    [Test]
    public async Task A_lapsed_credential_is_not_handed_over()
    {
        var machine = Make();

        _ = await machine.Sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None,
            Delegated with { ExpiresAt = Noon.AddMinutes(-1) });

        await Assert.That(machine.Host.Started.Single().Environment.ContainsKey(AgentSessions.TokenVariable)).IsFalse();
    }

    [Test]
    public async Task It_is_revoked_when_the_agent_ends_and_never_written_down()
    {
        var machine = Make();
        _ = await machine.Sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None, Delegated);

        machine.Host.Children.Single().End(0);

        await Assert.That(await Eventually.TrueAsync(() =>
        {
            lock (machine.Revoked)
            {
                return machine.Revoked.Contains("t0k3n");
            }
        })).IsTrue();

        var written = string.Join("\n", Directory.EnumerateFiles(machine.Home, "*", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        await Assert.That(written).DoesNotContain("t0k3n")
            .Because("the ledger keeps the session's shape, never its credential.");
    }
}
