using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A session on another machine is on its row the moment it is asked for, saying it is reaching
/// the machine, and the reaching happens behind it (owner, 2026-10-10: "we should give indication
/// it is working").
/// </summary>
/// <remarks>
/// <para>
/// <b>Reaching a machine takes seconds.</b> It is a control-plane introduction the machine answers
/// on its next heartbeat, then a channel, then a credential minted and the session started - and
/// the console sat on the runner modal through all of it, with nothing to say it had heard the key.
/// </para>
/// <para>
/// <b>A refusal is said on the row</b>, where the person now is, and stays until a key closes it:
/// a row that vanished would take the sentence with it.
/// </para>
/// </remarks>
public class ARemoteSessionOpensAtOnceTests
{
    private static readonly RemoteMachine Vm3 = new("runner-3", "vmlinux003");

    [Test]
    public async Task The_row_is_placed_before_the_machine_answers()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        using var answers = new ManualResetEventSlim();
        var link = FakeLink.Machine();
        fixture.Mux.Reaching(() => [Vm3], _ =>
        {
            answers.Wait(TimeSpan.FromSeconds(30));
            return new RemoteReach(link, null);
        });

        try
        {
            var opening = Task.Run(() => fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false));

            await Assert.That(opening.Wait(TimeSpan.FromSeconds(5))).IsTrue()
                .Because("the key is answered at once; the machine answers when it answers.");
            await Assert.That(opening.Result.Agent).IsEqualTo(1);
            await Assert.That(fixture.Mux.Rows().Single().Label).IsEqualTo("claude @ vmlinux003");
            await Assert.That(link.Sent).IsEmpty();
        }
        finally
        {
            answers.Set();
        }

        await Assert.That(MuxFixture.Until(() => link.Sent.OfType<StartAgentSession>().Any())).IsTrue()
            .Because("once reached, the session is started behind the row.");
    }

    [Test]
    public async Task The_row_says_it_is_reaching_the_machine_then_shows_the_session()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        using var answers = new ManualResetEventSlim();
        var link = FakeLink.Machine();
        fixture.Mux.Reaching(() => [Vm3], _ =>
        {
            answers.Wait(TimeSpan.FromSeconds(30));
            return new RemoteReach(link, null);
        });

        try
        {
            _ = fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false);
            _ = fixture.Showing(MuxTab.Agent(1));

            await Assert.That(MuxFixture.Until(() =>
                    fixture.Terminal.Painted.Contains("Reaching vmlinux003", StringComparison.Ordinal)))
                .IsTrue();
        }
        finally
        {
            answers.Set();
        }

        await Assert.That(MuxFixture.Until(() => link.Sent.OfType<StartAgentSession>().Any())).IsTrue();
        link.Hear("hello from claude");

        await Assert.That(MuxFixture.Until(() =>
                fixture.Terminal.Painted.Contains("hello from claude", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task A_refusal_is_said_on_the_row_and_a_key_closes_it()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Mux.Reaching(() => [Vm3], _ => new RemoteReach(null, "vmlinux003 is offline."));

        var opened = fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false);
        await Assert.That(opened.Agent).IsEqualTo(1);

        _ = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() =>
                fixture.Terminal.Painted.Contains("vmlinux003 is offline.", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(fixture.Mux.Rows()).Count().IsEqualTo(1)
            .Because("the sentence stays until the person has read it.");

        fixture.Terminal.Type("x");

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 0)).IsTrue();
    }

    [Test]
    public async Task A_machine_that_refuses_the_start_is_said_on_the_row_too()
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

        _ = fixture.Mux.OpenRemote(Vm3, sessionId: null, alive: false);
        _ = fixture.Showing(MuxTab.Agent(1));

        await Assert.That(MuxFixture.Until(() =>
                fixture.Terminal.Painted.Contains("running a flight", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(MuxFixture.Until(() => link.Disposed)).IsTrue()
            .Because("a refused start holds nothing open.");
    }
}
