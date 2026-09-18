using Gg.Console;
using Gg.Console.Views;
using Terminal.Gui.Input;

namespace Gg.Console.Tests;

/// <summary>
/// <c>ctrl+/</c> reaches the keymap whichever way the terminal sends it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two encodings, and only one of them arrived.</b> A terminal that speaks
/// the kitty keyboard protocol sends <c>ESC [ 47;5u</c>, which the driver hands
/// over as slash-with-ctrl and which the keymap already understands. Every
/// other terminal sends the C0 byte <c>0x1F</c> — and that reached
/// <see cref="KeyTranslator"/> as a control rune, which it drops, so the key
/// did nothing at all.
/// </para>
/// <para>
/// <b>Measured in a pty, both ways, before the key was built on.</b> With the
/// CSI-u sequence the mode opened; with <c>0x1F</c> nothing happened, while a
/// plain <c>/</c> opened the facet chooser in the same harness — so the
/// difference was the encoding rather than the binding.
/// </para>
/// <para>
/// <b>Mapped here rather than in the keymap, which stays pure.</b> This is the
/// one file that touches <c>Key</c>, and a C0 byte is exactly the kind of
/// terminal detail it exists to absorb: 0x1F is what a terminal sends FOR
/// ctrl+/, so it is translated to ctrl+/ and the rest of the console never
/// learns there were two spellings.
/// </para>
/// </remarks>
public class CtrlSlashArrivesTwoWaysTests
{
    [Test]
    public async Task The_modern_encoding_is_slash_with_ctrl()
    {
        await Assert.That(KeyTranslator.Translate(new Key('/').WithCtrl))
            .IsEqualTo(KeyStroke.Control('/'));
    }

    [Test]
    public async Task And_the_C0_byte_means_the_same_thing()
    {
        // THE ONE THAT WAS DROPPED. Rune.IsControl is true for 0x1F, so the
        // translator's two arms both fell through and the keystroke carried no
        // input at all - which matches no binding and reads as a dead key.
        await Assert.That(KeyTranslator.Translate(new Key((char)0x1F)))
            .IsEqualTo(KeyStroke.Control('/'))
            .Because("a terminal that does not speak CSI-u sends this for ctrl+/, and a key "
                   + "that works on one terminal and silently does nothing on another is "
                   + "worse than one nobody bound.");
    }

    [Test]
    public async Task A_plain_slash_is_still_a_plain_slash()
    {
        // THE KEY THIS ONE SITS BESIDE. `/` narrows the list, and a translator
        // that turned every slash into ctrl+slash would take that away.
        await Assert.That(KeyTranslator.Translate(new Key('/')))
            .IsEqualTo(KeyStroke.Char('/'));
    }

    [Test]
    public async Task And_no_other_control_byte_is_given_a_meaning()
    {
        // NARROW ON PURPOSE. The C0 range holds tab, enter and escape, and a
        // sweeping rule that mapped control bytes back to their characters
        // would rebind those three to whatever letter they share a code with.
        foreach (var byteValue in (char[])['\t', '\r', '\n', (char)0x1B, (char)0x01, (char)0x1A])
        {
            await Assert.That(KeyTranslator.Translate(new Key(byteValue)))
                .IsNotEqualTo(KeyStroke.Control('/'));
        }
    }
}
