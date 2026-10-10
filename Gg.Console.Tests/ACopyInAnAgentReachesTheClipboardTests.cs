namespace Gg.Console.Tests;

/// <summary>
/// What an agent copies reaches the person's clipboard: its OSC 52 is passed to the
/// terminal gg runs in (slice seventy, S70.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by a person, not by a test.</b> The mux draws an agent from its emulator's
/// buffer, so no byte the agent writes reaches the terminal as written. Claude Code copies
/// by writing <c>ESC ] 52 ; c ; base64 BEL</c> - always, on a machine with no clipboard of
/// its own - and the emulator took it and nothing listened, so a copy on another machine
/// went nowhere.
/// </para>
/// <para>
/// <b>Passed on, not read back.</b> Only the write is carried. A read (<c>?</c>) would let
/// a program on another machine read this one's clipboard, and nothing asks for it.
/// </para>
/// </remarks>
public class ACopyInAnAgentReachesTheClipboardTests
{
    [Test]
    public async Task A_copy_on_another_machine_is_handed_to_the_terminal()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = new FakeLink();
        fixture.Mux.StartRemote("claude @ vmlinux002", new RemotePty(link), "a1b2");

        var showing = fixture.Showing(MuxTab.Agent(1));
        link.Hear("ready");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("ready", StringComparison.Ordinal))).IsTrue();

        link.Hear("\u001b]52;c;aGVsbG8=\u0007");

        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("\u001b]52;c;aGVsbG8=\u0007", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the terminal gg runs in owns the clipboard, so the copy is handed to it.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }

    [Test]
    public async Task A_clipboard_read_is_not_passed_on()
    {
        using var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = new FakeLink();
        fixture.Mux.StartRemote("claude @ vmlinux002", new RemotePty(link), "a1b2");

        var showing = fixture.Showing(MuxTab.Agent(1));
        link.Hear("\u001b]52;c;?\u0007");
        link.Hear("after");
        await Assert.That(MuxFixture.Until(() => fixture.Terminal.Painted.Contains("after", StringComparison.Ordinal))).IsTrue();

        await Assert.That(fixture.Terminal.Painted).DoesNotContain("]52;")
            .Because("a program on another machine does not get to read this one's clipboard.");

        fixture.Terminal.Type("\u0007");
        fixture.Terminal.Type("0");
        await Assert.That(await showing.WaitAsync(TimeSpan.FromSeconds(20))).IsEqualTo(MuxLeave.Gg);
    }
}
