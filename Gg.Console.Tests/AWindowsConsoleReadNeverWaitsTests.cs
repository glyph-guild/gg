using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Which pending Windows console events a read can take without waiting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every caller polls.</b> The host and the mux read keystrokes in a loop and treat
/// nought as "nothing yet", because termios raw mode is set with no minimum and no wait.
/// A Windows console read blocks until it has text, and some events never become text: a
/// key released, a modifier pressed on its own, focus, a menu. A read with only those
/// pending would hang the loop, so they are taken off the queue first.
/// </para>
/// <para>
/// <b>And one of them is the resize.</b> Windows has no SIGWINCH; it puts a window-size
/// event in the input queue. Skipping it is where the host hears the screen changed.
/// </para>
/// </remarks>
public class AWindowsConsoleReadNeverWaitsTests
{
    private static ConsoleEvent Down(ushort virtualKey, char character) =>
        new(ConsoleEventKind.Key, KeyDown: true, virtualKey, character);

    private static ConsoleEvent Up(ushort virtualKey, char character) =>
        new(ConsoleEventKind.Key, KeyDown: false, virtualKey, character);

    private static readonly ConsoleEvent Resize = new(ConsoleEventKind.WindowSize, false, 0, '\0');
    private static readonly ConsoleEvent Focus = new(ConsoleEventKind.Focus, false, 0, '\0');

    [Test]
    public async Task Nothing_pending_is_nothing_to_read()
    {
        await Assert.That(ConsoleInput.Classify([])).IsEqualTo(new ConsolePending(0, Resized: false, Ready: false));
    }

    [Test]
    public async Task A_typed_letter_is_ready()
    {
        await Assert.That(ConsoleInput.Classify([Down(0x41, 'a')]))
            .IsEqualTo(new ConsolePending(0, Resized: false, Ready: true));
    }

    [Test]
    public async Task An_arrow_has_no_character_and_is_still_ready()
    {
        // VIRTUAL TERMINAL INPUT turns it into ESC [ A on the read, so it is text.
        await Assert.That(ConsoleInput.Classify([Down(0x26, '\0')]).Ready).IsTrue();
    }

    [Test]
    public async Task Releases_modifiers_and_focus_before_a_key_are_skipped()
    {
        ConsoleEvent[] pending = [Up(0x41, 'a'), Down(0x10, '\0'), Focus, Down(0x11, '\0'), Down(0x47, '\a')];

        await Assert.That(ConsoleInput.Classify(pending))
            .IsEqualTo(new ConsolePending(4, Resized: false, Ready: true));
    }

    [Test]
    public async Task Only_events_that_never_become_text_are_all_skipped_and_nothing_is_read()
    {
        ConsoleEvent[] pending = [Up(0x41, 'a'), Down(0x10, '\0'), Focus];

        await Assert.That(ConsoleInput.Classify(pending))
            .IsEqualTo(new ConsolePending(3, Resized: false, Ready: false))
            .Because("a read with only these pending would wait for the next key, and the caller is polling.");
    }

    [Test]
    public async Task A_resize_is_skipped_and_reported()
    {
        await Assert.That(ConsoleInput.Classify([Resize]))
            .IsEqualTo(new ConsolePending(1, Resized: true, Ready: false));
    }

    [Test]
    public async Task A_resize_after_the_first_key_is_left_for_the_next_read()
    {
        // ORDER IS KEPT. Skipping past a key to reach the resize would lose the key's place.
        await Assert.That(ConsoleInput.Classify([Down(0x41, 'a'), Resize]))
            .IsEqualTo(new ConsolePending(0, Resized: false, Ready: true));
    }
}
