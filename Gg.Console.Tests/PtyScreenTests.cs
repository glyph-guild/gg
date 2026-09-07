using XTerm.Options;
using XTermTerminal = XTerm.Terminal;

namespace Gg.Console.Tests;

/// <summary>
/// The frame gg paints while it hosts a child: gg's bar, then the child's screen
/// under it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A function that RETURNS the frame, rather than one that writes it.</b> The
/// spike's renderer ended in a <c>Console.Write</c>, which left every property
/// below testable only by running a terminal and looking at it — and four
/// separate terminal defects were found exactly that way, each one costing a
/// round trip through a person at a keyboard. What a frame contains is a
/// question about a string, and asking it as one is the difference between a
/// suite that catches these and a suite that cannot.
/// </para>
/// <para>
/// <b>Every escape in this file is written <c>\u001b</c>.</b> A raw escape byte
/// between quotes was silently stripped to an EMPTY string once already, and
/// every sequence then went out as visible text: no bar, nothing aligned, codes
/// strewn across the screen. A byte you cannot see in a diff is a byte that goes
/// missing, so the last test here forbids it outright.
/// </para>
/// </remarks>
public class PtyScreenTests
{
    private const string Esc = "\u001b";

    /// <summary>An emulator the size the child is told the screen is.</summary>
    private static XTermTerminal Screen(int rows, int columns, string wrote = "")
    {
        var terminal = new XTermTerminal(new TerminalOptions { Cols = columns, Rows = rows });

        if (wrote.Length > 0)
        {
            terminal.Write(wrote);
        }

        return terminal;
    }

    [Test]
    public async Task The_bar_is_the_top_row_and_the_child_starts_below_it()
    {
        // THE WHOLE POINT OF HOSTING RATHER THAN HANDING OVER. gg keeps one row.
        // The child is told the screen is a row shorter than it is, so a
        // full-screen program cannot paint over the bar - which a scroll region
        // cannot achieve, because such a program resets the region and repaints
        // everything inside it.
        var frame = PtyScreen.Paint(Screen(rows: 5, columns: 20), rows: 5, columns: 20, bar: "gg");

        await Assert.That(frame).Contains($"{Esc}[1;1H", StringComparison.Ordinal)
            .Because("the bar is written at the top left of the real terminal.");

        await Assert.That(frame)
            .Contains($"{Esc}[{PtyScreen.FirstChildRow};1H", StringComparison.Ordinal)
            .Because("and the child's first row is the second row of the terminal.");

        await Assert.That(PtyScreen.FirstChildRow).IsEqualTo(2);
    }

    [Test]
    public async Task The_bar_fills_its_row_exactly_however_long_the_text_is()
    {
        // Padded, so the row reads as a bar rather than as two characters
        // floating on whatever the child last left there. Truncated, because a
        // bar one column too long wraps onto the child's first row and shunts
        // the entire screen down by one.
        var padded = PtyScreen.Paint(Screen(rows: 3, columns: 12), rows: 3, columns: 12, bar: "gg");

        await Assert.That(padded).Contains("gg          ", StringComparison.Ordinal)
            .Because("two columns of text and ten of padding is the twelve it was given.");

        var overlong = PtyScreen.Paint(
            Screen(rows: 3, columns: 12), rows: 3, columns: 12,
            bar: "a bar far longer than this terminal is wide");

        await Assert.That(overlong).Contains("a bar far lo", StringComparison.Ordinal);
        await Assert.That(overlong).DoesNotContain("longer", StringComparison.Ordinal)
            .Because("what does not fit is cut, never wrapped onto the child's screen.");
    }

    [Test]
    public async Task The_last_row_of_the_child_is_painted_and_painting_it_does_not_scroll()
    {
        // THE DEFECT THAT ATE AN EDITOR'S STATUS LINE. Writing `columns`
        // characters into a `columns`-wide terminal leaves the cursor in the
        // last column with the wrap armed; on the LAST row the next character
        // scrolls the screen and the bottom line is gone for good. Vim's INSERT
        // indicator lives on that line, which is how this was noticed at all.
        var frame = PtyScreen.Paint(Screen(rows: 4, columns: 10), rows: 4, columns: 10, bar: "gg");

        await Assert.That(frame).Contains($"{Esc}[5;1H", StringComparison.Ordinal)
            .Because("four child rows under one bar makes the last of them terminal row "
                   + "five, and a frame that never addresses it has quietly lost a line.");

        var wrapOff = frame.IndexOf($"{Esc}[?7l", StringComparison.Ordinal);
        var wrapOn = frame.IndexOf($"{Esc}[?7h", StringComparison.Ordinal);

        await Assert.That(wrapOff).IsEqualTo(0)
            .Because("auto-wrap has to be off before anything is written, not partway.");
        await Assert.That(wrapOn).IsGreaterThan(wrapOff)
            .Because("and back on before gg stops painting - left off it outlives the "
                   + "session and changes how the shell behaves afterwards.");
    }

    [Test]
    public async Task What_the_child_said_about_colour_survives_the_repaint()
    {
        // The spike's first renderer wrote characters and nothing else, so
        // everything the child said about colour, weight and inversion went on
        // the floor and Claude Code came out monochrome.
        var frame = PtyScreen.Paint(
            Screen(rows: 3, columns: 10, wrote: $"{Esc}[31mred"),
            rows: 3, columns: 10, bar: "gg");

        await Assert.That(frame).Contains("38;5;1", StringComparison.Ordinal)
            .Because("red is palette entry one, and 38;5;n serves all 256 of them.");

        var bold = PtyScreen.Paint(
            Screen(rows: 3, columns: 10, wrote: $"{Esc}[1mloud"),
            rows: 3, columns: 10, bar: "gg");

        await Assert.That(bold).Contains($"{Esc}[0;1m", StringComparison.Ordinal)
            .Because("reset then bold, rather than a delta from the previous cell: a delta "
                   + "needs the emitter to track what is ON, which is where a bold run that "
                   + "never turns off and bleeds through the rest of the screen comes from.");
    }

    [Test]
    public async Task A_screen_the_child_left_plain_says_nothing_about_colour()
    {
        // The emulator's defaults are 256 for foreground and 257 for background,
        // and neither is a palette entry - emitting one as `38;5;256` asks the
        // terminal for a colour that does not exist. Read off the emulator
        // rather than assumed, because assuming it is how a first version got
        // every cell on the screen wrong at once.
        var frame = PtyScreen.Paint(
            Screen(rows: 3, columns: 10, wrote: "plain"), rows: 3, columns: 10, bar: "gg");

        foreach (var absurd in (string[])["38;5;256", "48;5;257", "38;5;-1", "48;5;-1"])
        {
            await Assert.That(frame).DoesNotContain(absurd, StringComparison.Ordinal)
                .Because($"'{absurd}' is a default being mistaken for a palette index.");
        }
    }

    [Test]
    public async Task The_cursor_lands_where_the_child_thinks_it_is_plus_the_bar()
    {
        // Off by the bar and typing looks haunted: the character lands one row
        // above the caret that is drawn.
        var terminal = Screen(rows: 5, columns: 20, wrote: $"{Esc}[3;7Hhere");
        var frame = PtyScreen.Paint(terminal, rows: 5, columns: 20, bar: "gg");

        var row = terminal.Buffer.Y + PtyScreen.FirstChildRow;
        var column = terminal.Buffer.X + 1;

        await Assert.That(frame).EndsWith(
            $"{Esc}[{row};{column}H{Esc}[?25h{Esc}[?7h", StringComparison.Ordinal)
            .Because("placed last, then shown, then wrapping restored - a cursor shown "
                   + "before the paint has finished is drawn at every row in turn.");
    }

    [Test]
    public async Task The_frame_hides_the_cursor_while_it_paints_and_ends_undressed()
    {
        var frame = PtyScreen.Paint(
            Screen(rows: 3, columns: 10, wrote: $"{Esc}[41mon red"),
            rows: 3, columns: 10, bar: "gg");

        await Assert.That(frame).Contains($"{Esc}[?25l", StringComparison.Ordinal)
            .Because("a cursor visible during a full repaint is drawn at every row in turn.");

        var reset = frame.LastIndexOf($"{Esc}[0m", StringComparison.Ordinal);
        var shown = frame.LastIndexOf($"H{Esc}[?25h", StringComparison.Ordinal);

        await Assert.That(reset).IsGreaterThan(0);
        await Assert.That(reset).IsLessThan(shown)
            .Because("without a reset at the end the last cell's colour dresses the cursor, "
                   + "and everything the next frame writes before it says otherwise.");
    }

    [Test]
    public async Task No_escape_is_written_as_a_byte_you_cannot_see()
    {
        // THE RATCHET FOR THE BUG THAT CAUSED ALL FOUR REPORTED SYMPTOMS AT
        // ONCE. A raw 0x1b typed between quotes did not survive being written to
        // disk; the constant became an empty string and every sequence went out
        // as text. It is invisible in a diff and invisible in a review, which is
        // why it is a test instead.
        foreach (var file in ConsoleSource.In("Gg.Console", "Gg.Console.Tests"))
        {
            var text = await File.ReadAllTextAsync(file);

            await Assert.That(text).DoesNotContain(Esc, StringComparison.Ordinal)
                .Because($"{Path.GetFileName(file)} holds a literal escape byte. Write it as "
                       + "an escape instead, which survives every editor, encoding and diff "
                       + "- this file's own source is nothing but ASCII and it composes them "
                       + "all the same.");
        }
    }
}
