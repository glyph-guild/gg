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
