using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A new session runs in an empty directory the machine made for it, named after it and
/// readable only by the runner's user (slice seventy-one, S71.2-01; ADR-0039 Decision 7).
/// </summary>
/// <remarks>
/// <b>Isolated, as the owner asked</b>: two sessions never share a working tree, and none
/// starts in the runner's home among its configuration. The console no longer chooses a
/// directory, so one it sends is ignored rather than checked.
/// </remarks>
public class ASessionHasADirectoryOfItsOwnTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static (AgentSessions Sessions, FakeAgentHost Host, string Home) Machine()
    {
        var home = Path.Combine(Directory.CreateTempSubdirectory("gg-agent-home-").FullName, "agent-sessions");
        var host = new FakeAgentHost();
        return (new AgentSessions(host, home, flying: () => false, now: () => Noon), host, home);
    }

    [Test]
    public async Task A_new_session_runs_in_an_empty_directory_named_after_it()
    {
        var (sessions, host, home) = Machine();

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2" }, CancellationToken.None);

        var directory = Path.Combine(home, "a1b2");
        await Assert.That(opened.Refused).IsNull();
        await Assert.That(host.Started.Single().Directory).IsEqualTo(directory);
        await Assert.That(Directory.Exists(directory)).IsTrue();
        await Assert.That(Directory.EnumerateFileSystemEntries(directory)).IsEmpty();

        if (!OperatingSystem.IsWindows())
        {
            await Assert.That(File.GetUnixFileMode(directory))
                .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)
                .Because("a session's work is its user's, as a flight's working tree is.");
        }
    }

    [Test]
    public async Task Two_sessions_get_two_directories()
    {
        var (sessions, host, _) = Machine();

        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);
        _ = await sessions.StartAsync(new StartAgentSession { Columns = 80, Rows = 24 }, CancellationToken.None);

        await Assert.That(host.Started.Select(s => s.Directory).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task A_directory_the_console_sends_is_ignored()
    {
        var (sessions, host, home) = Machine();

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = "a1b2", Directory = "/etc" },
            CancellationToken.None);

        await Assert.That(opened.Refused).IsNull();
        await Assert.That(host.Started.Single().Directory).IsEqualTo(Path.Combine(home, "a1b2"));
    }

    [Test]
    [Arguments("../escape")]
    [Arguments("a/b")]
    [Arguments(".")]
    public async Task An_id_that_is_not_a_plain_name_is_refused(string id)
    {
        var (sessions, host, _) = Machine();

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = id }, CancellationToken.None);

        await Assert.That(opened.Refused).Contains("session id")
            .Because("the id names a directory, so it may not name a path.");
        await Assert.That(host.Started).IsEmpty();
    }
}
