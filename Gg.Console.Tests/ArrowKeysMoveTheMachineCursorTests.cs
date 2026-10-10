using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The arrow keys move the cursor through the machines and a machine's sessions, in
/// whichever of the two forms the terminal sends them (slice seventy, S70.4-02).
/// </summary>
/// <remarks>
/// <b>Found by a person, not by a test.</b> The lists matched <c>ESC [ A</c> and
/// <c>ESC [ B</c> only. gg's own console turns on application cursor keys
/// (<c>ESC [ ? 1 h</c>) and nothing turns them off, so by the time the mux is on
/// screen the terminal sends <c>ESC O A</c> and <c>ESC O B</c>, and only j and k moved.
/// </remarks>
public class ArrowKeysMoveTheMachineCursorTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly RemoteMachine Vm3 = new("runner-3", "vmlinux003");
    private static readonly RemoteMachine Vm2 = new("runner-2", "vmlinux002");

    [Test]
    [Arguments("\u001b[B", "\u001b[A")]
    [Arguments("\u001bOB", "\u001bOA")]
    public async Task Down_and_up_move_through_the_machines_and_the_sessions(string down, string up)
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine(
            new AgentSessionStanding { SessionId = "a1b2", StartedAt = Noon, Alive = true, Directory = "/work" });
        fixture.Mux.Reaching(() => [Vm3, Vm2], _ => new RemoteReach(link, null));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("Remote Control", StringComparison.Ordinal))).IsTrue();
        await Assert.That(fixture.Choose("Remote Control")).IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("▸ vmlinux003", StringComparison.Ordinal))).IsTrue();

        // ONE KEY AT A TIME, waiting for each to be drawn, as a person types; and read
        // only what was painted after the key, since the screen keeps every frame.
        await Assert.That(TypedThenShows(fixture, down, "▸ vmlinux002"))
            .IsTrue()
            .Because("down moves to the next machine.");
        await Assert.That(TypedThenShows(fixture, up, "▸ vmlinux003"))
            .IsTrue()
            .Because("up moves back.");

        await Assert.That(TypedThenShows(fixture, "\r", "▸ new session")).IsTrue();
        await Assert.That(TypedThenShows(fixture, down, "▸ attach a1b2"))
            .IsTrue()
            .Because("down moves through a machine's sessions too.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    private static bool TypedThenShows(MuxFixture fixture, string key, string shown)
    {
        var from = fixture.Terminal.Painted.Length;
        fixture.Terminal.Type(key);
        return MuxFixture.Until(() => fixture.Terminal.Painted[from..].Contains(shown, StringComparison.Ordinal));
    }
}
