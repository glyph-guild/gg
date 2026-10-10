using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// History resumes a remote session on the machine it ran on: it re-attaches while
/// the session lives, and asks the machine to resume it otherwise (slice seventy,
/// S70.4-03).
/// </summary>
public class HistoryResumesARemoteSessionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static async Task ResumeAsync(MuxFixture fixture)
    {
        var showing = fixture.Showing(MuxTab.History);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("Sessions", StringComparison.Ordinal))).IsTrue();

        fixture.Terminal.Type("\r");
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await showing.WaitAsync(TimeSpan.FromSeconds(20));
    }

    [Test]
    public async Task A_session_still_running_is_attached_to()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Ledger.Keep(new MuxSession("s-1", "claude @ vmlinux003", "/work", Noon, Machine: "runner-3"));
        var link = FakeLink.Machine(new AgentSessionStanding { SessionId = "s-1", StartedAt = Noon, Alive = true });
        fixture.Mux.Reading(fixture.Drafts, _ => null)
            .Reaching(() => [new RemoteMachine("runner-3", "vmlinux003")], _ => new RemoteReach(link, null));

        await ResumeAsync(fixture);

        await Assert.That(link.Sent.OfType<AttachAgentSession>().Single().SessionId).IsEqualTo("s-1");
    }

    [Test]
    public async Task A_session_that_ended_is_resumed_by_its_id()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Ledger.Keep(new MuxSession("s-1", "claude @ vmlinux003", "/work", Noon, Machine: "runner-3"));
        var link = FakeLink.Machine(new AgentSessionStanding { SessionId = "s-1", StartedAt = Noon, Alive = false });
        fixture.Mux.Reading(fixture.Drafts, _ => null)
            .Reaching(() => [new RemoteMachine("runner-3", "vmlinux003")], _ => new RemoteReach(link, null));

        await ResumeAsync(fixture);

        var started = link.Sent.OfType<StartAgentSession>().Single();
        await Assert.That(started.SessionId).IsEqualTo("s-1")
            .Because("the machine resumes a session started again by its id - `claude --resume`.");
        await Assert.That(started.Directory).IsEqualTo("/work");
    }
}
