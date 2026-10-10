using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// `+` then Remote Control… lists the machines this person may reach; a machine's
/// sessions are started, attached to and resumed through the mux's remote open
/// (slice seventy, S70.4-02; since slice seventy-one the list of a machine's sessions is
/// the console's runner modal, and choosing a machine here hands off to it).
/// </summary>
/// <remarks>
/// <b>The mux names no control plane.</b> What machines there are and how one is
/// reached arrive as delegates from the composition root - history's
/// <c>Reading</c> shape - so these are proved with a machine that answers like a
/// runner and no network at all.
/// </remarks>
public class TheMuxReachesAMachineTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly RemoteMachine Vm3 = new("runner-3", "vmlinux003");
    private static readonly RemoteMachine Vm2 = new("runner-2", "vmlinux002");

    [Test]
    public async Task Choosing_a_machine_leaves_for_the_console_with_it()
    {
        // WAS: enter here listed the machine's sessions inside the mux, and enter there
        // started or attached. That list is the runner modal's sessions view now, so one
        // place lists and acts on a machine's sessions.
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Mux.Reaching(() => [Vm3, Vm2], _ => new RemoteReach(null, "the menu reaches nothing"));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("Remote Control", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the new-agent menu offers it.");

        await Assert.That(fixture.Choose("Remote Control")).IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("vmlinux002", StringComparison.Ordinal)))
            .IsTrue();

        fixture.Terminal.Type("\r");

        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.RemoteControl);
        await Assert.That(fixture.Mux.TakeRemoteControl()).IsEqualTo(Vm3.Id);
    }

    [Test]
    public async Task New_on_a_machine_starts_claude_there_as_a_row()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine();
        fixture.Mux.Reaching(() => [Vm3, Vm2], machine => new RemoteReach(machine == Vm3 ? link : null, null));

        var opened = fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false);

        await Assert.That(opened.Agent).IsEqualTo(1);
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();
        await Assert.That(fixture.Mux.Rows().Single().Label).IsEqualTo("claude @ vmlinux003");

        var started = link.Sent.OfType<StartAgentSession>().Single();
        await Assert.That(started.Columns).IsEqualTo(120 - MuxColumn.Width);
        await Assert.That(started.SessionId).IsNotNull()
            .Because("the mux gives the session its id, so history can resume it by that id.");
    }

    [Test]
    public async Task A_session_already_running_there_is_attached_to()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine(
            new AgentSessionStanding { SessionId = "a1b2", StartedAt = Noon, Alive = true, Directory = "/work" });
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var opened = fixture.Mux.OpenRemote(Vm3, "a1b2", alive: true);

        await Assert.That(opened.Agent).IsEqualTo(1);
        await Assert.That(link.Sent.OfType<AttachAgentSession>().Single().SessionId).IsEqualTo("a1b2");
        await Assert.That(link.Sent.OfType<StartAgentSession>()).IsEmpty();
    }

    [Test]
    public async Task A_session_that_ended_is_resumed_by_its_id()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine();
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var opened = fixture.Mux.OpenRemote(Vm3, "a1b2", alive: false, directory: "/work");

        await Assert.That(opened.Agent).IsEqualTo(1);
        var started = link.Sent.OfType<StartAgentSession>().Single();
        await Assert.That(started.SessionId).IsEqualTo("a1b2")
            .Because("the machine resumes a session started again by its id - `claude --resume`.");
        await Assert.That(started.Directory).IsEqualTo("/work");
    }

    [Test]
    public async Task A_machine_that_refuses_says_why_and_starts_nothing()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = new FakeLink
        {
            Answers = frame => frame switch
            {
                StartAgentSession => [new AgentSessionRefused { Because = "This machine is running a flight" }],
                _ => [],
            },
        };
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(link, null));

        var opened = fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false);

        await Assert.That(opened.Agent).IsNull();
        await Assert.That(opened.Refused).Contains("running a flight", StringComparison.Ordinal)
            .Because("the machine's own sentence, where the person is looking.");
        await Assert.That(fixture.Mux.Rows()).IsEmpty();
        await Assert.That(link.Disposed).IsTrue();
    }

    [Test]
    public async Task A_machine_that_cannot_be_reached_says_why()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(null, "vmlinux003 is offline."));

        var opened = fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false);

        await Assert.That(opened.Refused).IsEqualTo("vmlinux003 is offline.");
        await Assert.That(fixture.Mux.Rows()).IsEmpty();
    }

    [Test]
    public async Task Without_a_way_to_reach_machines_the_menu_does_not_offer_it()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("New agent", StringComparison.Ordinal))).IsTrue();

        await Assert.That(fixture.Terminal.Painted).DoesNotContain("Remote Control")
            .Because("a key advertised that does nothing is worse than no key.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await showing.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
