using Gg.Console;
using Gg.Console.Views;
using Terminal.Gui.Input;

namespace Gg.Console.Tests;

/// <summary>
/// <c>ctrl+g</c> reaches the keymap whichever way the terminal sends it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two encodings, the same as ctrl+/.</b> On macOS and Linux the driver
/// hands ctrl+g over as G with ctrl. On Windows a terminal that speaks
/// win32-input-mode does the same, but one that sends the C0 byte <c>0x07</c>
/// through ConPTY arrives as a bare control rune, which the translator drops -
/// so ctrl+g, the only way to the agent column, did nothing.
/// </para>
/// <para>
/// <b>Measured before it was built.</b> The Windows console probe pressed
/// ctrl+g as the byte and nothing switched, while every printable key in the
/// same run worked.
/// </para>
/// </remarks>
public class CtrlGArrivesTwoWaysTests
{
    [Test]
    public async Task The_named_encoding_is_g_with_ctrl()
    {
        await Assert.That(KeyTranslator.Translate(Key.G.WithCtrl))
            .IsEqualTo(KeyStroke.Control('g'));
    }

    [Test]
    public async Task And_the_C0_byte_means_the_same_thing()
    {
        // WHAT A WINDOWS CONSOLE HANDS OVER for 0x07 with no key named: the
        // character alone, with no modifier, which Rune.IsControl rejects.
        await Assert.That(KeyTranslator.Translate(new Key((char)0x07)))
            .IsEqualTo(KeyStroke.Control('g'))
            .Because("a terminal that sends the byte for ctrl+g must reach the agent column too, "
                   + "or the key works on one terminal and silently does nothing on another.");
    }

    [Test]
    public async Task A_plain_g_is_still_a_plain_g()
    {
        await Assert.That(KeyTranslator.Translate(new Key('g')))
            .IsEqualTo(KeyStroke.Char('g'));
    }

    [Test]
    public async Task And_no_other_control_byte_becomes_ctrl_g()
    {
        // NARROW ON PURPOSE, as for ctrl+/: tab, enter and escape are C0
        // bytes too, and must keep the meanings they have.
        foreach (var byteValue in (char[])['\t', '\r', '\n', (char)0x1B, (char)0x01, (char)0x1A, (char)0x1F])
        {
            await Assert.That(KeyTranslator.Translate(new Key(byteValue)))
                .IsNotEqualTo(KeyStroke.Control('g'));
        }
    }
}
