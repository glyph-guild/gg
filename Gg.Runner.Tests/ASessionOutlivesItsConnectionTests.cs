using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>
/// A session outlives the console attached to it: attaching again shows the recent
/// screen and makes the agent redraw, and the latest attach drives
/// (slice seventy, S70.2-03; ADR-0039 Decision 5).
/// </summary>
/// <remarks>
/// <b>tmux's bargain, kept small.</b> The agent keeps running and writing while
/// nobody watches; what a returning console sees is a bounded ring of what it
/// wrote, then whatever Claude repaints when its size is nudged - which is the
/// screen as it actually is, not a reconstruction.
/// </remarks>
public class ASessionOutlivesItsConnectionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static async Task<(AgentSession Session, FakeAgentChild Child)> RunningAsync()
    {
        var host = new FakeAgentHost();
        var sessions = new AgentSessions(
            host,
            [Directory.CreateTempSubdirectory("gg-agent-root-").FullName],
            flying: () => false,
            now: () => Noon);

        var opened = await sessions.StartAsync(
            new StartAgentSession { Columns = 100, Rows = 30, SessionId = "a1b2" }, CancellationToken.None);

        return (opened.Session!, host.Children.Single());
    }

    [Test]
    public async Task What_it_wrote_while_nobody_watched_is_shown_to_the_next_console()
    {
        var (session, child) = await RunningAsync();

        var first = new FakeViewer();
        var attached = session.Attach(first, 100, 30);
        child.Say("hello ");
        await Assert.That(await Eventually.TrueAsync(() => first.Seen == "hello ")).IsTrue();

        // THE CONSOLE GOES AWAY, and the agent does not notice.
        attached.Dispose();
        child.Say("while you were gone");
        await Assert.That(await Eventually.TrueAsync(() => session.Buffered >= 25)).IsTrue();

        var second = new FakeViewer();
        using var back = session.Attach(second, 100, 30);

        await Assert.That(await Eventually.TrueAsync(() => second.Seen.Contains("while you were gone")))
            .IsTrue()
            .Because("the ring is replayed to whoever attaches next.");
        await Assert.That(session.Alive).IsTrue();
    }

    [Test]
    public async Task Attaching_nudges_the_size_so_the_agent_redraws()
    {
        var (session, child) = await RunningAsync();
        var before = child.Resized.Count;

        using var attached = session.Attach(new FakeViewer(), 120, 40);

        await Assert.That(child.Resized.Count).IsGreaterThan(before);
        await Assert.That(child.Resized[^1]).IsEqualTo((120, 40))
            .Because("it ends at the attaching console's size, having moved off it first so "
                   + "the agent sees a change and repaints the screen.");
    }

    [Test]
    public async Task The_latest_attach_drives_and_the_earlier_one_watches()
    {
        var (session, child) = await RunningAsync();

        var first = new FakeViewer();
        using var a = session.Attach(first, 100, 30);
        var second = new FakeViewer();
        using var b = session.Attach(second, 100, 30);

        await Assert.That(first.WasMadeReadOnly).IsTrue();
        await Assert.That(second.WasMadeReadOnly).IsFalse();

        session.Input(first, "ignored"u8);
        session.Input(second, "typed"u8);
        session.Resize(first, 10, 10);

        await Assert.That(System.Text.Encoding.UTF8.GetString([.. child.Typed])).IsEqualTo("typed")
            .Because("two keyboards on one TUI is not a feature anybody asked for.");
        await Assert.That(child.Resized.Contains((10, 10))).IsFalse();

        child.Say("both see this");
        await Assert.That(await Eventually.TrueAsync(
            () => first.Seen.Contains("both see this") && second.Seen.Contains("both see this"))).IsTrue()
            .Because("a watcher still watches.");
    }

    [Test]
    public async Task The_ring_is_bounded_and_keeps_the_newest()
    {
        var (session, child) = await RunningAsync();

        var burst = new byte[1024 * 1024];
        Array.Fill(burst, (byte)'a');
        burst[^1] = (byte)'z';
        child.Say(burst);

        // ALL OF IT PUMPED, not merely enough to fill the ring: a console that
        // attached while the rest was still arriving would be sent it live too.
        await Assert.That(await Eventually.TrueAsync(() => session.Written == burst.Length)).IsTrue();
        await Assert.That(session.Buffered).IsEqualTo(AgentSessions.RingBytes);

        var late = new FakeViewer();
        using var attached = session.Attach(late, 100, 30);

        await Assert.That(late.SeenBytes).IsEqualTo(AgentSessions.RingBytes);
        await Assert.That(late.Seen[^1]).IsEqualTo('z')
            .Because("the newest output is what the screen is made of; the oldest goes first.");
    }

    [Test]
    public async Task Its_end_is_told_to_every_console_attached()
    {
        var (session, child) = await RunningAsync();
        var viewer = new FakeViewer();
        using var attached = session.Attach(viewer, 100, 30);

        child.End(0);

        await Assert.That(await Eventually.TrueAsync(() => viewer.ExitedWith == 0)).IsTrue();
        await Assert.That(session.Alive).IsFalse();
    }
}
