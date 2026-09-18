using Terminal.Gui.Input;

namespace Gg.Console.Views;

/// <summary>
/// The only place Terminal.Gui key events meet the pure keymap.
/// </summary>
/// <remarks>
/// Translates <see cref="Key"/> into a <see cref="KeyStroke"/> and nothing
/// else. Every decision about what a key MEANS is on the other side of this
/// function, which is what keeps the keymap testable without a terminal.
/// </remarks>
public static class KeyTranslator
{
    /// <summary>What a terminal without CSI-u sends for <c>ctrl+/</c>.</summary>
    private const int UnitSeparator = 0x1F;

    public static KeyStroke Translate(Key key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key == Key.Esc)
        {
            return KeyStroke.Esc;
        }
        if (key == Key.Tab)
        {
            return KeyStroke.TabKey;
        }
        // NAMED, BECAUSE ITS RUNE IS A CONTROL CHARACTER. Enter arrives as
        // KeyCode 13, which Rune.IsControl rejects below - so without this arm
        // it became a KeyStroke with nothing set at all, matching no binding.
        // The keymap knew about `enter`, the thing that produces keystrokes did
        // not, and the seam between them had no test: that is what
        // KeyTranslatorTests is for.
        if (key == Key.Enter)
        {
            return KeyStroke.EnterKey;
        }

        // THE ARROWS THAT MEAN LESS AND MORE. Named for the reason enter is:
        // they arrive as KeyCodes rather than runes, so without an arm here
        // they became a KeyStroke with nothing set and matched no binding -
        // which is the seam KeyTranslatorTests exists for.
        if (key == Key.CursorLeft)
        {
            return KeyStroke.LeftKey;
        }
        if (key == Key.CursorRight)
        {
            return KeyStroke.RightKey;
        }

        // THE ONE C0 BYTE THIS CONSOLE GIVES A MEANING, and it is the byte a
        // terminal sends FOR ctrl+/. A terminal that speaks the kitty keyboard
        // protocol sends ESC [ 47;5u instead, which arrives below as slash with
        // ctrl and needs nothing; everywhere else this arrives as 0x1F, which
        // Rune.IsControl drops - so the key worked on one terminal and did
        // nothing on another, measured in a pty both ways.
        //
        // NARROW ON PURPOSE. Tab, enter and escape are C0 bytes too, and a rule
        // that mapped the range back to its characters would rebind all three.
        if (key.AsRune.Value == UnitSeparator)
        {
            return KeyStroke.Control('/');
        }

        var bare = key.NoCtrl.NoAlt.NoShift;
        char? input = null;
        if (bare.AsRune.IsAscii && !System.Text.Rune.IsControl(bare.AsRune))
        {
            input = char.ToLowerInvariant((char)bare.AsRune.Value);
        }
        else if (key.AsRune.IsAscii && !System.Text.Rune.IsControl(key.AsRune))
        {
            input = char.ToLowerInvariant((char)key.AsRune.Value);
        }

        return new KeyStroke(input, Ctrl: key.IsCtrl);
    }
}
