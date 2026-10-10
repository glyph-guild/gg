namespace Gg.Console.Tests;

/// <summary>
/// The new-agent menu is a list a person moves through with the arrows and picks from with
/// enter, not a row of letters (owner, 2026-10-10).
/// </summary>
/// <remarks>
/// <b>Claude Code here comes first</b>, because it is the one most often wanted, and
/// <b>Remote Control… last</b>, because it opens a choice of its own rather than an agent.
/// </remarks>
public class TheNewAgentMenuIsAListTests
{
    [Test]
    public async Task It_opens_on_claude_code_here_and_lists_remote_control_last()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        fixture.Mux.Reaching(() => [], _ => new RemoteReach(null, null));

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Cursor().StartsWith("Claude Code here", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the cursor starts on the agent most often wanted.");

        var menu = fixture.Terminal.Painted;
        string[] order =
        [
            "Claude Code here",
            "manage gg with an agent",
            "plan several flights with an agent",
            "manage the airspace with an agent",
            "a new flight",
            "Remote Control…",
        ];
        var at = order.Select(item => menu.LastIndexOf(item, StringComparison.Ordinal)).ToArray();
        await Assert.That(at.All(index => index >= 0)).IsTrue()
            .Because("every item is listed: " + string.Join(", ", order.Zip(at)));
        await Assert.That(at).IsInOrder();

        fixture.Terminal.Type("\u001b");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task A_letter_chooses_nothing()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Cursor().Length > 0)).IsTrue();

        // WAITED FOR between the two, or they arrive in one read and neither is a key.
        var from = fixture.Terminal.Painted.Length;
        fixture.Terminal.Type("l");
        MuxFixture.Until(() => fixture.Terminal.Painted.Length > from);
        fixture.Terminal.Type("\u001b");

        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg)
            .Because("the letters are gone: l no longer plans, so esc is the first thing that leaves.");
    }

    [Test]
    public async Task Up_from_the_top_stays_at_the_top()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);

        var showing = fixture.Showing(MuxTab.New);
        await Assert.That(MuxFixture.Until(() => fixture.Cursor().StartsWith("Claude Code here", StringComparison.Ordinal))).IsTrue();

        var from = fixture.Terminal.Painted.Length;
        fixture.Terminal.Type("\u001bOA");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Length > from)).IsTrue();
        await Assert.That(fixture.Cursor()).StartsWith("Claude Code here");

        fixture.Terminal.Type("\u001b");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }
}
