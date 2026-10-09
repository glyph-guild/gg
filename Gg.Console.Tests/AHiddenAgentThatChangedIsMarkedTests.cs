namespace Gg.Console.Tests;

/// <summary>
/// <b>S69.2-02</b> - an agent whose screen changed while it was not shown is marked until it is
/// shown.
/// </summary>
public class AHiddenAgentThatChangedIsMarkedTests
{
    [Test]
    public async Task A_hidden_agent_that_wrote_is_marked_until_it_is_shown()
    {
        using var fixture = new MuxFixture();
        fixture.Agent("A", fixture.Speaks("a-first", "a", "a-later", "end"));

        fixture.Raise("a");

        // WAITED FOR ON ITS SCREEN, NOT ON THE MARK. "a-first" alone sets the mark, so waiting on
        // the mark let the test show and leave the agent inside the 20ms it takes to notice its
        // flag - and "a-later" then landed after leaving, a genuine hidden write the last
        // assertion read as a stray mark. Failed 3 times in CI and 14 runs in 120 locally.
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Screen(1).Contains("a-later", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows() is [{ Changed: true }])).IsTrue()
            .Because("nobody has looked at A since it wrote, which is what the mark says.");

        await Assert.That(MuxColumn.Lines(fixture.Mux.Rows(), MuxTab.Gg, 12)[2].Text).Contains("•");

        var showing = fixture.Showing(MuxTab.Agent(1));
        await Assert.That(MuxFixture.Until(() => fixture.Mux.Rows() is [{ Changed: false }])).IsTrue()
            .Because("showing it is looking at it.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);

        await Assert.That(fixture.Mux.Rows() is [{ Changed: false }]).IsTrue()
            .Because("it has written nothing since it was last shown.");
    }
}
