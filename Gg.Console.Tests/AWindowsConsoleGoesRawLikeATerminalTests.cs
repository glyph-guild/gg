using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The console modes gg sets on Windows to host a child, worked out apart from the console.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same three promises as termios raw mode.</b> No line editing, no echo, and a read
/// that never waits for Enter. On Windows those are console mode flags on the input handle,
/// and the output handle has to interpret the escape sequences gg paints.
/// </para>
/// <para>
/// <b>Pure, so it runs on every build.</b> CI's test job runs on Linux, where no Windows
/// console exists; the arithmetic on the flags is the part that can be wrong without one.
/// </para>
/// </remarks>
public class AWindowsConsoleGoesRawLikeATerminalTests
{
    // What a fresh Windows console starts with: processed, line, echo, insert, quick edit,
    // extended flags and auto position.
    private const uint FreshInput = 0x01 | 0x02 | 0x04 | 0x20 | 0x40 | 0x80 | 0x100;

    [Test]
    public async Task Raw_input_has_no_line_editing_echo_or_processing()
    {
        var raw = ConsoleModes.RawInput(FreshInput);

        await Assert.That(raw & ConsoleModes.LineInput).IsEqualTo(0u);
        await Assert.That(raw & ConsoleModes.EchoInput).IsEqualTo(0u);
        await Assert.That(raw & ConsoleModes.ProcessedInput).IsEqualTo(0u)
            .Because("ctrl+c belongs to the child while it is hosted, as it does under termios raw mode.");
    }

    [Test]
    public async Task Raw_input_arrives_as_escape_sequences()
    {
        var raw = ConsoleModes.RawInput(FreshInput);

        await Assert.That(raw & ConsoleModes.VirtualTerminalInput).IsEqualTo(ConsoleModes.VirtualTerminalInput)
            .Because("the host forwards bytes, and an arrow key must reach the child as ESC [ A.");
        await Assert.That(raw & ConsoleModes.WindowInput).IsEqualTo(ConsoleModes.WindowInput)
            .Because("a resize is reported as an input event, which is how the host hears it.");
    }

    [Test]
    public async Task Quick_edit_is_off_so_a_click_reaches_the_child()
    {
        var raw = ConsoleModes.RawInput(FreshInput);

        await Assert.That(raw & ConsoleModes.QuickEdit).IsEqualTo(0u);
        await Assert.That(raw & ConsoleModes.ExtendedFlags).IsEqualTo(ConsoleModes.ExtendedFlags)
            .Because("Windows ignores a change to quick edit unless extended flags is set beside it.");
    }

    [Test]
    public async Task Output_interprets_what_gg_paints()
    {
        var output = ConsoleModes.PaintingOutput(0x01 | 0x02);

        await Assert.That(output & ConsoleModes.VirtualTerminalProcessing)
            .IsEqualTo(ConsoleModes.VirtualTerminalProcessing);
        await Assert.That(output & ConsoleModes.ProcessedOutput).IsEqualTo(ConsoleModes.ProcessedOutput);
        await Assert.That(output & ConsoleModes.DisableNewlineAutoReturn)
            .IsEqualTo(ConsoleModes.DisableNewlineAutoReturn);
    }

    [Test]
    public async Task A_raw_console_is_described_as_raw()
    {
        var state = ConsoleModes.Describe(ConsoleModes.RawInput(FreshInput));

        await Assert.That(state).IsEqualTo(
            new TerminalState(IsTerminal: true, Canonical: false, Echo: false, MinimumBytes: 0));
    }

    [Test]
    public async Task A_fresh_console_is_described_as_cooked()
    {
        var state = ConsoleModes.Describe(FreshInput);

        await Assert.That(state.Canonical).IsTrue();
        await Assert.That(state.Echo).IsTrue();
    }
}
