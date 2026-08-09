namespace Gg.Console;

/// <summary>
/// Structural subset of a key event. No Terminal.Gui types here — the keymap
/// stays a pure function testable without a terminal.
/// </summary>
public readonly record struct KeyInfo(bool Ctrl, bool Escape, bool Tab);

public readonly record struct KeymapContext(UiMode Mode);

/// <summary>
/// Pure keymap: (input, key, context) -> Command?. One module; bindings live
/// nowhere else.
/// </summary>
public static class Keymap
{
    public static Command? Resolve(char? input, KeyInfo key, KeymapContext context)
    {
        if (key.Ctrl && input is 'c')
        {
            return Command.Quit;
        }

        if (context.Mode == UiMode.Help)
        {
            return key.Escape || input is 'q' or '?' ? Command.ToggleHelp : null;
        }

        if (key.Tab)
        {
            return Command.FocusNextPane;
        }

        return input switch
        {
            'q' => Command.Quit,
            '?' => Command.ToggleHelp,
            'e' => Command.OpenEditor,
            _ => null,
        };
    }
}
