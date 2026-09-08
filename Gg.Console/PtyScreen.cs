using System.Text;
using XTerm.Buffer;

// `Terminal` is XTerm.NET's emulator type and also the root namespace of
// Terminal.Gui, which this assembly references. Named once here rather than
// qualified at every use below.
using XTermTerminal = XTerm.Terminal;

namespace Gg.Console;

/// <summary>
/// The frame gg paints while it hosts a child: gg's bar on the top row, and the
/// child's screen under it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It returns the frame rather than writing it.</b> Everything that can go
/// wrong here is a property of a string — a row never addressed, a colour never
/// emitted, a cursor placed a row off — and a renderer that ended in a
/// <c>Console.Write</c> made every one of them answerable only by running a
/// terminal and looking at it. Four such defects were found that way before
/// this was a function.
/// </para>
/// <para>
/// <b>The child is told the screen is one row shorter than it is.</b> That is
/// what keeps the bar: a full-screen program cannot paint a row it does not
/// believe exists. A scroll region cannot achieve the same thing, because a
/// program like that resets the region and repaints everything inside it.
/// </para>
/// <para>
/// <b>It repaints the whole screen every time, which a finished one must not.</b>
/// This is O(screen) per chunk the child writes, and it flickers on a slow
/// link. A diffing renderer is the obvious next step and is deliberately not in
/// this slice — correctness of the frame first, then the size of it.
/// </para>
/// </remarks>
public static class PtyScreen
{
    /// <summary>The escape byte, written as an escape.</summary>
    /// <remarks>
    /// <b>NEVER as a literal control character in the source.</b> Written as a
    /// raw 0x1b between quotes it was silently stripped to an EMPTY string, and
    /// every sequence below then went out as visible text: no bar, nothing
    /// aligned, escape codes across the screen. It happened three times, twice
    /// while writing the test that now forbids it — the byte is invisible in a
    /// terminal, in a diff and in a review, so a test is the only place it can
    /// be caught.
    /// </remarks>
    private const string Esc = "\u001b";

    /// <summary>
    /// gg's panel, then <paramref name="rows"/> rows of the child's screen
    /// under it, as one string to write in one go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The size is given rather than read off the emulator.</b> They are
    /// normally the same, and passing them says which one is authoritative when
    /// they are not: the terminal gg is painting into, not the one the child was
    /// told about. A resize is exactly the moment those disagree, and the
    /// emulator learns about it after gg does.
    /// </para>
    /// <para>
    /// <b>The panel is however many rows gg is keeping.</b> One while it is only
    /// saying what ends the session; several when it has been asked to show the
    /// envelope, because the rules in force are the wrong thing to abbreviate.
    /// The child is told the screen is shorter by exactly this many, and does
    /// not paint a row it does not believe exists — which is the same mechanism
    /// that kept a single row, told about a bigger number.
    /// </para>
    /// </remarks>
    public static string Paint(
        XTermTerminal terminal, int rows, int columns, IReadOnlyList<string> panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        // WHERE THE CHILD STARTS, DERIVED RATHER THAN DECLARED. It was a
        // constant, correct only while the panel was one row - and a cursor
        // offset by a stale constant puts the caret somewhere the typing is not.
        var firstChildRow = panel.Count + 1;

        var painted = new StringBuilder();

        // AUTO-WRAP OFF FIRST, BEFORE ANY CONTENT. Writing `columns` characters
        // into a `columns`-wide terminal leaves the cursor in the last column
        // with the wrap armed; on the LAST row the next character scrolls the
        // screen and the bottom line is gone for good. That line is where an
        // editor puts its mode indicator, which is how the defect was noticed.
        painted.Append($"{Esc}[?7l");

        // Hidden for the duration: a cursor left visible through a full repaint
        // is drawn at every row in turn.
        painted.Append($"{Esc}[?25l");

        for (var row = 0; row < panel.Count; row++)
        {
            var text = panel[row] ?? "";

            // PADDED AND CUT, EVERY ROW. Unpadded, the child's screen shows
            // through the gaps; unwrapped is the same defect the auto-wrap guard
            // exists for, arriving from gg's own text rather than the child's -
            // one long row would push everything below it down and the child's
            // last row off the bottom.
            painted.Append($"{Esc}[{row + 1};1H{Esc}[7m");
            painted.Append(text.Length > columns ? text[..columns] : text.PadRight(columns));
            painted.Append($"{Esc}[0m");
        }

        var buffer = terminal.Buffer;

        // What the terminal is currently dressed in, so the emitter can skip
        // saying it again. Starts unknown, so the first cell always states it.
        var current = string.Empty;

        for (var row = 0; row < rows; row++)
        {
            painted.Append($"{Esc}[{row + firstChildRow};1H{Esc}[K");

            var line = buffer.Lines[buffer.YDisp + row];
            if (line is null)
            {
                continue;
            }

            for (var column = 0; column < columns; column++)
            {
                var style = Sgr(line[column]);

                // ONLY WHEN IT CHANGES. A cell is usually dressed like the one
                // before it, so emitting per cell multiplies the frame by an
                // order of magnitude for no visible difference at all.
                if (!string.Equals(style, current, StringComparison.Ordinal))
                {
                    painted.Append(style);
                    current = style;
                }

                var content = line[column].Content;
                painted.Append(string.IsNullOrEmpty(content) ? " " : content);
            }
        }

        // Undressed before leaving, or the last cell's colour dresses the cursor
        // and everything the next frame writes before it says otherwise.
        painted.Append($"{Esc}[0m");

        // Offset by the bar, or the caret sits a row above where typing lands
        // and the whole thing looks haunted. Wrapping restored last, because
        // leaving it off outlives this session and changes how the shell behaves
        // afterwards.
        painted.Append($"{Esc}[{buffer.Y + firstChildRow};{buffer.X + 1}H{Esc}[?25h{Esc}[?7h");

        return painted.ToString();
    }

    /// <summary>One cell's appearance, as the SGR sequence that produces it.</summary>
    /// <remarks>
    /// <para>
    /// <b>A renderer that wrote characters and nothing else</b> threw away
    /// everything the child said about colour, weight and inversion, and an
    /// agent that is colourful in a terminal came out monochrome inside gg.
    /// </para>
    /// <para>
    /// <b>The encoding was read off the emulator rather than assumed.</b> A
    /// foreground of 256 and a background of 257 are the DEFAULTS rather than
    /// palette entries — asking a terminal for <c>38;5;256</c> asks for a colour
    /// that does not exist. Mode 1 means the value is a packed RGB triple, and
    /// mode 0 a palette index, which <c>38;5;n</c> serves for all 256.
    /// </para>
    /// <para>
    /// <b>Reset first, every time.</b> Composing a delta from the previous cell
    /// would be smaller on the wire and needs the emitter to track which
    /// attributes are ON — which is exactly where this kind of code goes wrong,
    /// as a bold run that never turns off and bleeds through everything after it.
    /// </para>
    /// </remarks>
    private static string Sgr(BufferCell cell)
    {
        var a = cell.Attributes;
        var codes = new List<string> { "0" };

        if (a.IsBold()) { codes.Add("1"); }
        if (a.IsDim()) { codes.Add("2"); }
        if (a.IsItalic()) { codes.Add("3"); }
        if (a.IsUnderline()) { codes.Add("4"); }
        if (a.IsBlink()) { codes.Add("5"); }
        if (a.IsInverse()) { codes.Add("7"); }
        if (a.IsInvisible()) { codes.Add("8"); }
        if (a.IsStrikethrough()) { codes.Add("9"); }

        var fg = a.GetFgColor();
        if (a.GetFgColorMode() == 1)
        {
            codes.Add($"38;2;{(fg >> 16) & 0xFF};{(fg >> 8) & 0xFF};{fg & 0xFF}");
        }
        else if (fg is >= 0 and < 256)
        {
            codes.Add($"38;5;{fg}");
        }

        var bg = a.GetBgColor();
        if (a.GetBgColorMode() == 1)
        {
            codes.Add($"48;2;{(bg >> 16) & 0xFF};{(bg >> 8) & 0xFF};{bg & 0xFF}");
        }
        else if (bg is >= 0 and < 256)
        {
            codes.Add($"48;5;{bg}");
        }

        return $"{Esc}[{string.Join(';', codes)}m";
    }
}
