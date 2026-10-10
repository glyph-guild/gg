using System.Text;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// Whether a machine takes ad hoc agent sessions is its own file's decision, and the
/// CLI is what gives a session its terminal (slice seventy, S70.2-01; ADR-0039
/// Decision 3).
/// </summary>
/// <remarks>
/// <b>accept-agent-login's three rules, and a fourth reason for them.</b> Off unless
/// the machine's file says so; no environment variable an image could carry
/// silently; never offerable, because a control plane that could set it could give
/// itself a standing way to type into a machine. And it gates the WIRING: a machine
/// that has not opted in is handed no host, so the runner refuses for want of one.
/// </remarks>
[NotInParallel("a-real-pty")]
public class AgentSessionsAreAMachinesDecisionTests
{
    [Test]
    public async Task Both_settings_survive_the_file()
    {
        var parsed = ConfigurationFile.Parse(ConfigurationFile.Render(new Configuration
        {
            AcceptAgentSessions = true,
            AgentSessionRoots = "/work,/srv/repos",
        }));

        await Assert.That(parsed.Configuration!.AcceptAgentSessions).IsTrue();
        await Assert.That(parsed.Configuration.AgentSessionRoots).IsEqualTo("/work,/srv/repos");
    }

    [Test]
    public async Task Nothing_but_the_file_can_turn_it_on()
    {
        foreach (var key in new[] { "accept-agent-sessions", "agent-session-roots" })
        {
            await Assert.That(Configuration.Members.Any(
                    m => string.Equals(m.Key, key, StringComparison.Ordinal) && m.Variable is { Length: > 0 }))
                .IsFalse()
                .Because($"{key} with a variable is one an image can carry silently.");
            await Assert.That(OfferableKeys.All).DoesNotContain(key);
        }
    }

    [Test]
    public async Task No_host_is_built_unless_the_file_says_so()
    {
        await Assert.That(LocalAgentSessions.For(null, "claude")).IsNull();
        await Assert.That(LocalAgentSessions.For(new Configuration { AcceptAgentSessions = false }, "claude")).IsNull();

        var wired = LocalAgentSessions.For(
            new Configuration { AcceptAgentSessions = true, AgentSessionRoots = "/work, /srv/repos" }, "claude");

        await Assert.That(wired!.Roots).IsEquivalentTo((string[])["/work", "/srv/repos"]);
        await Assert.That(wired.Host).IsTypeOf<AgentSessionHost>();
    }

    [Test]
    public async Task With_no_roots_named_the_home_directory_is_the_root()
    {
        var wired = LocalAgentSessions.For(new Configuration { AcceptAgentSessions = true }, "claude");

        await Assert.That(wired!.Roots).IsEquivalentTo(
            (string[])[Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)])
            .Because("a person who opted their own machine in expects their own projects; a "
                   + "narrower root is one line in the same file.");
    }

    [Test]
    public async Task The_agent_runs_in_a_terminal_of_the_asked_size_in_the_asked_directory()
    {
        var directory = Directory.CreateTempSubdirectory("gg-agent-session-").FullName;

        var host = new AgentSessionHost(
            "/bin/sh",
            _ => ["-c", "stty size; pwd; echo token:$GG_TEST_TOKEN; read line; echo got:$line"]);

        using var child = await host.StartAsync(
            new AgentSessionStart(
                directory, 100, 30, "a1b2", Resume: false,
                new Dictionary<string, string> { ["GG_TEST_TOKEN"] = "placed" }),
            CancellationToken.None);

        var seen = new StringBuilder();
        var buffer = new byte[4096];

        _ = Task.Run(() =>
        {
            int read;
            while ((read = child.Read(buffer)) > 0)
            {
                lock (seen)
                {
                    seen.Append(Encoding.UTF8.GetString(buffer, 0, read));
                }
            }
        });

        await Assert.That(await Until(() => Text(seen).Contains("token:placed"))).IsTrue()
            .Because($"Saw: {Text(seen)}");

        var text = Text(seen);
        await Assert.That(text).Contains("30 100")
            .Because("the agent is told the console's size, as a local mux agent is.");
        await Assert.That(text).Contains(Path.GetFileName(directory));

        child.Write("hi\r"u8);
        await Assert.That(await Until(() => Text(seen).Contains("got:hi"))).IsTrue();
        await Assert.That(await child.Exited.WaitAsync(TimeSpan.FromSeconds(10))).IsEqualTo(0);
    }

    [Test]
    public async Task The_agent_is_started_by_its_session_id_or_resumed_by_it()
    {
        await Assert.That(AgentSessionHost.ArgumentsFor(
                new AgentSessionStart("/w", 80, 24, "a1b2", Resume: false, new Dictionary<string, string>())))
            .IsEquivalentTo((string[])["--session-id", "a1b2"]);
        await Assert.That(AgentSessionHost.ArgumentsFor(
                new AgentSessionStart("/w", 80, 24, "a1b2", Resume: true, new Dictionary<string, string>())))
            .IsEquivalentTo((string[])["--resume", "a1b2"])
            .Because("the CLI as it is, plus only what a local mux agent gets: its id, so history "
                   + "can carry it on.");
    }

    private static string Text(StringBuilder seen)
    {
        lock (seen)
        {
            return seen.ToString();
        }
    }

    private static async Task<bool> Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Yield();
        }

        return condition();
    }
}
