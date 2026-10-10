using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// `+` then `m` lists the machines this person may reach, then a machine's sessions;
/// choosing one attaches and choosing new starts one (slice seventy, S70.4-02).
/// </summary>
/// <remarks>
/// <b>The mux names no control plane.</b> What machines there are and how one is
/// reached arrive as delegates from the composition root - history's
/// <c>Reading</c> shape - so these screens are proved with a machine that answers
/// like a runner and no network at all.
/// </remarks>
public class TheMuxReachesAMachineTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly RemoteMachine Vm3 = new("runner-3", "vmlinux003");
    private static readonly RemoteMachine Vm2 = new("runner-2", "vmlinux002");

    [Test]
    public async Task New_on_a_machine_starts_claude_there_as_a_row()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine();
        fixture.Mux.Reaching(() => [Vm3, Vm2], machine => new RemoteReach(machine == Vm3 ? link : null, null));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("another machine", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the new-agent menu offers it.");

        fixture.Terminal.Type("m");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("vmlinux002", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\r");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("new session", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\r");
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();
        await Assert.That(fixture.Mux.Rows().Single().Label).IsEqualTo("claude @ vmlinux003");

        var started = link.Sent.OfType<StartAgentSession>().Single();
        await Assert.That(started.Columns).IsEqualTo(120 - MuxColumn.Width);
        await Assert.That(started.SessionId).IsNotNull()
            .Because("the mux gives the session its id, so history can resume it by that id.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task A_session_already_running_there_is_attached_to()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine(
            new AgentSessionStanding { SessionId = "a1b2", StartedAt = Noon, Alive = true, Directory = "/work" });
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("another machine", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("m");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("vmlinux003", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("\r");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("a1b2", StringComparison.Ordinal))).IsTrue();

        // ONE KEY AT A TIME, waiting for each to be drawn: typed together, two keys arrive
        // in one read, which is not how a person types.
        fixture.Terminal.Type("j");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("▸ attach a1b2", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("\r");

        await Assert.That(MuxFixture.Until(() => link.Sent.OfType<AttachAgentSession>().Any(a => a.SessionId == "a1b2"))).IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task A_machine_that_refuses_says_why_and_starts_nothing()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = new FakeLink
        {
            Answers = frame => frame switch
            {
                ListAgentSessions => [new AgentSessionList { Sessions = [] }],
                StartAgentSession => [new AgentSessionRefused { Because = "This machine is running a flight" }],
                _ => [],
            },
        };
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("another machine", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("m");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("vmlinux003", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("\r");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("new session", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("\r");

        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("running a flight", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the machine's own sentence, where the person is looking.");
        await Assert.That(fixture.Mux.Rows()).IsEmpty();

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task A_machine_that_cannot_be_reached_says_why()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(null, "vmlinux003 is offline."));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("another machine", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("m");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("vmlinux003", StringComparison.Ordinal))).IsTrue();
        fixture.Terminal.Type("\r");

        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("is offline", StringComparison.Ordinal))).IsTrue();

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task Without_a_way_to_reach_machines_the_menu_does_not_offer_it()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("New agent", StringComparison.Ordinal))).IsTrue();

        await Assert.That(fixture.Terminal.Painted).DoesNotContain("another machine")
            .Because("a key advertised that does nothing is worse than no key.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await showing.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
