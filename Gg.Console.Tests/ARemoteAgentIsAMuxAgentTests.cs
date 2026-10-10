using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// An agent whose terminal is on another machine is a mux agent like any other: a
/// row, a screen that keeps filling while hidden, the width beside the column, and
/// an end that removes only its own row (slice seventy, S70.4-01; ADR-0039).
/// </summary>
/// <remarks>
/// <b>One seam, so everything above it is unchanged.</b> A local agent's pty and a
/// remote session's frames both answer <see cref="IAgentPty"/>; the column, the
/// switching, the emulator and quitting are slice sixty-nine's and do not know which
/// they hold.
/// </remarks>
public class ARemoteAgentIsAMuxAgentTests
{
    [Test]
    public async Task It_is_a_row_whose_screen_fills_while_hidden()
    {
        using var fixture = new MuxFixture();
        var link = new FakeLink();

        fixture.Mux.StartRemote("claude @ vmlinux003", new RemotePty(link), "a1b2", machine: "runner-3");

        await Assert.That(fixture.Mux.Rows().Single().Label).IsEqualTo("claude @ vmlinux003");

        link.Hear("hello from afar");

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("hello from afar", StringComparison.Ordinal)))
            .IsTrue()
            .Because("what the far terminal writes fills this emulator whether or not it is shown.");
    }

    [Test]
    public async Task It_is_told_the_width_beside_the_column()
    {
        using var fixture = new MuxFixture(columns: 70, rows: 12);
        var link = new FakeLink();

        fixture.Mux.StartRemote("claude @ vmlinux003", new RemotePty(link), "a1b2");

        await Assert.That(link.Sent.OfType<AgentResize>().Any(r => r.Columns == 70 - MuxColumn.Width)).IsTrue()
            .Because("the far agent draws for the pane it is shown in, as a local one is told.");
    }

    [Test]
    public async Task What_is_typed_crosses_as_input()
    {
        var link = new FakeLink();
        using var pty = new RemotePty(link);

        pty.Write("hi\r"u8);

        await Assert.That(link.Sent.OfType<AgentInput>().SelectMany(i => i.Bytes).ToArray())
            .IsEquivalentTo("hi\r"u8.ToArray());
    }

    [Test]
    public async Task Its_end_removes_only_its_row_and_the_ledger_knows_where_it_ran()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("local", "while :; do sleep 1; done");
        var link = new FakeLink();
        fixture.Mux.StartRemote("claude @ vmlinux003", new RemotePty(link), "a1b2", machine: "runner-3");

        await Assert.That(fixture.Ledger.Read().Single(s => s.Id == "a1b2").Machine).IsEqualTo("runner-3")
            .Because("history resumes a remote session on the machine it ran on.");

        link.Hear(new AgentSessionExited { Code = 0 });

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 1)).IsTrue();
        await Assert.That(fixture.Mux.Rows().Single().Label).IsEqualTo("local");
    }

    [Test]
    public async Task A_dropped_channel_ends_the_row_rather_than_freezing_it()
    {
        using var fixture = new MuxFixture();
        var link = new FakeLink();
        fixture.Mux.StartRemote("claude @ vmlinux003", new RemotePty(link), "a1b2");

        link.Close();

        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 0)).IsTrue()
            .Because("a row that stopped updating with nothing said is a screen that lies; the "
                   + "session itself lives on the machine and history re-attaches.");
    }

    [Test]
    public async Task Quitting_gg_ends_the_remote_agent_too()
    {
        var link = new FakeLink();

        using (var fixture = new MuxFixture())
        {
            fixture.Mux.StartRemote("claude @ vmlinux003", new RemotePty(link), "a1b2");
            link.Hear(new AgentSessionExited { Code = 0 });
        }

        var killed = new FakeLink();
        using (var fixture = new MuxFixture())
        {
            fixture.Mux.StartRemote("claude @ vmlinux003", new RemotePty(killed), "c3d4");
            _ = Task.Run(() =>
            {
                MuxFixture.Until(() => killed.Sent.OfType<KillAgentSession>().Any());
                killed.Hear(new AgentSessionExited { Code = 137 });
            });
            fixture.Mux.EndAll();
        }

        await Assert.That(killed.Sent.OfType<KillAgentSession>().Any()).IsTrue()
            .Because("quitting gg asks first and then ends every agent, wherever it runs.");
    }

    [Test]
    public async Task A_quiet_session_is_kept_alive_across_the_channel()
    {
        var link = new FakeLink();
        using var pty = new RemotePty(link, keepAlive: TimeSpan.FromMilliseconds(20));

        await Assert.That(MuxFixture.Until(() => link.Sent.OfType<ListAgentSessions>().Count() >= 2)).IsTrue()
            .Because("the runner lets a channel go after ten quiet minutes, and a person reading an "
                   + "idle Claude sends nothing for longer than that.");
    }
}
