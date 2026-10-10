using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A person deletes an ended session - its ledger entry, its directory and Claude's
/// transcript of it - or every ended one; a live one is refused (slice seventy-one,
/// S71.2-03; ADR-0039 Decision 9).
/// </summary>
public class AnEndedSessionCanBeForgottenTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private sealed record Machine(AgentSessions Sessions, FakeAgentHost Host, string Home, string Transcripts);

    private static Machine Make()
    {
        var root = Directory.CreateTempSubdirectory("gg-agent-home-").FullName;
        var home = Path.Combine(root, "agent-sessions");
        var transcripts = Directory.CreateDirectory(Path.Combine(root, "claude-projects")).FullName;
        var host = new FakeAgentHost();
        return new Machine(
            new AgentSessions(host, home, flying: () => false, now: () => Noon, transcripts: transcripts),
            host, home, transcripts);
    }

    private static async Task Start(Machine machine, string id, bool end)
    {
        _ = await machine.Sessions.StartAsync(
            new StartAgentSession { Columns = 80, Rows = 24, SessionId = id }, CancellationToken.None);

        if (end)
        {
            machine.Host.Children.Last().End(0);
            await Eventually.TrueAsync(() => machine.Sessions.Standings().Single(s => s.SessionId == id).Alive is false);
        }
    }

    /// <summary>Where Claude keeps a directory's conversations: the path, every other character a dash.</summary>
    private static string TranscriptOf(Machine machine, string directory) =>
        Path.Combine(machine.Transcripts,
            new string([.. directory.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]));

    [Test]
    public async Task Forgetting_an_ended_session_deletes_its_entry_directory_and_transcript()
    {
        var machine = Make();
        await Start(machine, "a1b2", end: true);
        var directory = Path.Combine(machine.Home, "a1b2");
        var transcript = Directory.CreateDirectory(TranscriptOf(machine, directory)).FullName;
        File.WriteAllText(Path.Combine(transcript, "a1b2.jsonl"), "{}");

        var refused = machine.Sessions.Forget("a1b2");

        await Assert.That(refused).IsNull();
        await Assert.That(machine.Sessions.Standings()).IsEmpty();
        await Assert.That(Directory.Exists(directory)).IsFalse();
        await Assert.That(Directory.Exists(transcript)).IsFalse();

        using var restarted = new AgentSessions(new FakeAgentHost(), machine.Home, flying: () => false, now: () => Noon);
        await Assert.That(restarted.Standings()).IsEmpty()
            .Because("forgetting is written down too, or a restart would bring it back.");
    }

    [Test]
    public async Task A_live_session_is_refused()
    {
        var machine = Make();
        await Start(machine, "a1b2", end: false);

        await Assert.That(machine.Sessions.Forget("a1b2")).Contains("running");
        await Assert.That(Directory.Exists(Path.Combine(machine.Home, "a1b2"))).IsTrue();
    }

    [Test]
    public async Task An_unknown_session_is_refused()
    {
        var machine = Make();

        await Assert.That(machine.Sessions.Forget("nope")).Contains("no session");
    }

    [Test]
    public async Task Forgetting_every_ended_session_leaves_the_live_one()
    {
        var machine = Make();
        await Start(machine, "one", end: true);
        await Start(machine, "two", end: true);
        await Start(machine, "live", end: false);

        var refused = machine.Sessions.Forget(null);

        await Assert.That(refused).IsNull();
        await Assert.That(machine.Sessions.Standings().Select(s => s.SessionId)).IsEquivalentTo(["live"]);
        await Assert.That(Directory.Exists(Path.Combine(machine.Home, "one"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(machine.Home, "live"))).IsTrue();
    }
}
