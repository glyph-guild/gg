using Terminal.Gui.Input;

namespace Gg.Console.Views;

/// <summary>
/// The only place Terminal.Gui key events meet the pure keymap: translate
/// <see cref="Key"/> into the structural (input, KeyInfo) pair and nothing else.
/// </summary>
public static class KeyTranslator
{
    public static (char? Input, KeyInfo Key) Translate(Key key)
    {
        var info = new KeyInfo(
            Ctrl: key.IsCtrl,
            Escape: key == Key.Esc,
            Tab: key == Key.Tab);

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

        return (input, info);
    }
}
