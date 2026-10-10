using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Quitting gg leaves a session on another machine running, and asks only about the
/// agents it does end (slice seventy, S70.4; ADR-0039 Decision 5).
/// </summary>
/// <remarks>
/// <b>Found by the walk, not by a test.</b> On vmlinux002, quitting gg with a remote
/// row sent the machine a kill, so the session a person meant to come back to was
/// gone. A local agent is gg's child and ends with it; a remote one is the
/// machine's, and the console only lets go of it.
/// </remarks>
public class QuittingLeavesARemoteSessionRunningTests
{
    [Test]
    public async Task Ending_all_lets_go_of_a_remote_session_without_killing_it()
    {
        using var fixture = new MuxFixture();
        var link = new FakeLink();
        fixture.Mux.StartRemote("claude @ vmlinux002", new RemotePty(link), "a1b2", machine: "runner-2");

        fixture.Mux.EndAll();

        await Assert.That(link.Sent.OfType<KillAgentSession>()).IsEmpty()
            .Because("the session outlives the console, as tmux does; quitting only leaves it.");
        await Assert.That(link.Disposed).IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows().Count == 0)).IsTrue();
    }

    [Test]
    public async Task The_quit_question_names_only_what_quitting_ends()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("local", "while :; do sleep 1; done");
        fixture.Mux.StartRemote("claude @ vmlinux002", new RemotePty(new FakeLink()), "a1b2");

        await Assert.That(fixture.Mux.QuitEnds()).IsEquivalentTo(["local"]);
    }

    [Test]
    public async Task Only_remote_sessions_end_nothing_so_nothing_is_asked()
    {
        using var fixture = new MuxFixture();
        fixture.Mux.StartRemote("claude @ vmlinux002", new RemotePty(new FakeLink()), "a1b2");

        await Assert.That(fixture.Mux.QuitEnds()).IsEmpty();
    }
}
