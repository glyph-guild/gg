using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// An opted-in machine starts its agent through the CLI's port, under a root it
/// allows, at the size asked for - and refuses, in a sentence, what it will not do
/// (slice seventy, S70.2-01; ADR-0039 Decisions 3 and 4).
/// </summary>
/// <remarks>
/// <b>The runner holds the table, never the terminal.</b> <c>NoTerminalTests</c>
/// forbids a pty anywhere in this project, so the child arrives through
/// <see cref="IHostAgentSessions"/> - agent login's shape - and everything here is
/// bookkeeping over bytes.
/// </remarks>
public class AMachineHostsAgentSessionsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static (AgentSessions Sessions, FakeAgentHost Host, string Root) Machine(
        bool flying = false, string? token = "the-agents-token")
    {
        var root = Directory.CreateTempSubdirectory("gg-agent-root-").FullName;
        var host = new FakeAgentHost();

        var sessions = new AgentSessions(
            host,
            [root],
            flying: () => flying,
            now: () => Noon,
            environment: () => token is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["CLAUDE_CODE_OAUTH_TOKEN"] = token });

        return (sessions, host, root);
    }

    [Test]
    public async Task It_starts_the_agent_under_its_first_root_at_the_asked_size()
    {
        var (sessions, host, root) = Machine();

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 120, Rows = 40, SessionId = "a1b2" }, CancellationToken.None);

        await Assert.That(opened.Refused).IsNull();
        await Assert.That(opened.Session!.Id).IsEqualTo("a1b2");

        var asked = host.Started.Single();

        await Assert.That(asked.Directory).IsEqualTo(root);
        await Assert.That((asked.Columns, asked.Rows)).IsEqualTo((120, 40));
        await Assert.That(asked.Resume).IsFalse();
        await Assert.That(asked.Environment["CLAUDE_CODE_OAUTH_TOKEN"]).IsEqualTo("the-agents-token")
            .Because("the session's agent authenticates as a flight's does - the machine's own "
                   + "token, placed in its environment, never in an argument.");
    }

    [Test]
    public async Task A_session_may_start_in_a_directory_under_a_root()
    {
        var (sessions, host, root) = Machine();
        var below = Directory.CreateDirectory(Path.Combine(root, "jdnext")).FullName;

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, Directory = below }, CancellationToken.None);

        await Assert.That(opened.Refused).IsNull();
        await Assert.That(host.Started.Single().Directory).IsEqualTo(below);
        await Assert.That(opened.Session!.Id).IsNotEmpty()
            .Because("no id asked for is one the machine chooses.");
    }

    [Test]
    public async Task Outside_its_roots_is_refused()
    {
        var (sessions, host, root) = Machine();

        foreach (var outside in new[] { "/etc", Path.Combine(root, "..", "elsewhere") })
        {
            var opened = await sessions.StartAsync(
                new StartAgentSession { Columns = 80, Rows = 24, Directory = outside }, CancellationToken.None);

            await Assert.That(opened.Session).IsNull();
            await Assert.That(opened.Refused!).Contains("root")
                .Because($"{outside} is not under a root this machine's owner allowed.");
        }

        await Assert.That(host.Started).IsEmpty();
    }

    [Test]
    public async Task While_flying_a_new_session_is_refused()
    {
        var (sessions, host, _) = Machine(flying: true);

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);

        await Assert.That(opened.Refused!).Contains("flight")
            .Because("one subscription and one working tree; a flight's governance assumes the "
                   + "agent on this machine is the flight's (ADR-0039 Decision 4).");
        await Assert.That(host.Started).IsEmpty();
    }

    [Test]
    public async Task Past_the_cap_a_session_is_refused()
    {
        var (sessions, host, _) = Machine();

        for (var i = 0; i < AgentSessions.MostSessions; i++)
        {
            var started = await sessions.StartAsync(
                new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);
            await Assert.That(started.Refused).IsNull();
        }

        var one = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);

        await Assert.That(one.Refused!).Contains(AgentSessions.MostSessions.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(host.Started.Count).IsEqualTo(AgentSessions.MostSessions);
    }

    [Test]
    public async Task Attaching_to_a_session_it_does_not_hold_is_refused()
    {
        var (sessions, _, _) = Machine();

        var found = sessions.Find("never-started");

        await Assert.That(found.Session).IsNull();
        await Assert.That(found.Refused!).Contains("never-started");
    }

    [Test]
    public async Task Starting_an_ended_session_by_its_id_resumes_it()
    {
        var (sessions, host, _) = Machine();

        var first = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);
        host.Children.Single().End(0);
        await Assert.That(await Eventually.TrueAsync(() => !first.Session!.Alive)).IsTrue();

        var again = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);

        await Assert.That(again.Refused).IsNull();
        await Assert.That(host.Started.Last().Resume).IsTrue()
            .Because("an ended session asked for by its id is `claude --resume`, so the "
                   + "conversation carries on rather than starting over.");
    }

    [Test]
    public async Task Its_standings_say_what_it_holds()
    {
        var (sessions, host, root) = Machine();

        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24, SessionId = "live" }, CancellationToken.None);
        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24, SessionId = "done" }, CancellationToken.None);
        host.Children[1].End(0);

        await Assert.That(await Eventually.TrueAsync(
            () => sessions.Standings().Any(s => s.SessionId == "done" && !s.Alive))).IsTrue();

        var standings = sessions.Standings();

        await Assert.That(standings.Single(s => s.SessionId == "live").Alive).IsTrue();
        await Assert.That(standings.Single(s => s.SessionId == "live").Directory).IsEqualTo(root);
        await Assert.That(standings.Single(s => s.SessionId == "live").StartedAt).IsEqualTo(Noon);
    }
}
