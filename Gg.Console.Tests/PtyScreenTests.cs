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

        await Assert.That(frame).Contains($"{Esc}[2;1H", StringComparison.Ordinal)
            .Because("and the child's first row is the SECOND row of the terminal. Written "
                   + "out rather than through PtyScreen.FirstChildRow, because an assertion "
                   + "phrased in the constant moves when the constant does and goes on "
                   + "passing while the bar is painted over.");
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
    [Arguments("16-colour foreground", "[31m", "38;5;1")]
    [Arguments("16-colour background", "[41m", "48;5;1")]
    [Arguments("bright 16-colour foreground", "[91m", "38;5;9")]
    [Arguments("256-colour foreground", "[38;5;208m", "38;5;208")]
    [Arguments("256-colour background", "[48;5;54m", "48;5;54")]
    [Arguments("truecolour foreground", "[38;2;17;34;51m", "38;2;17;34;51")]
    [Arguments("truecolour background", "[48;2;68;85;102m", "48;2;68;85;102")]
    [Arguments("bold", "[1m", "0;1")]
    [Arguments("dim", "[2m", "0;2")]
    [Arguments("italic", "[3m", "0;3")]
    [Arguments("underline", "[4m", "0;4")]
    [Arguments("inverse", "[7m", "0;7")]
    [Arguments("strikethrough", "[9m", "0;9")]
    public async Task Every_way_the_child_can_dress_a_cell_survives(
        string kind, string wrote, string expected)
    {
        // NOT ONE COLOUR AND A SHRUG. The renderer that dropped everything was
        // not obviously wrong for any single case - it wrote characters, and
        // characters are most of a screen. Each row here is a way an agent
        // actually dresses its output, and the truecolour ones matter most
        // because they take a different branch: the value is a packed RGB
        // triple rather than an index, and a renderer that read it as an index
        // asks for palette entry 1122867.
        var frame = PtyScreen.Paint(
            Screen(rows: 3, columns: 20, wrote: $"{Esc}{wrote}dressed"),
            rows: 3, columns: 20, bar: "gg");

        await Assert.That(frame).Contains(expected, StringComparison.Ordinal)
            .Because($"{kind} is something the child said and gg is repainting it.");
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
    public async Task Replayed_into_a_terminal_the_frame_IS_the_child_s_screen()
    {
        // THE STRONGEST ASSERTION AVAILABLE HERE, and it costs one more emulator.
        // Every other test in this file checks that the frame CONTAINS something
        // - a row address, a colour, a cursor - which is a check on a symptom of
        // being right. This one interprets gg's output the way a terminal will
        // and asks whether the screen that comes out is the screen that went in.
        //
        // It is what makes the renderer safe to change. A diffing version, when
        // there is a reason to write one, is correct exactly when this still
        // passes, and nothing else in this file would notice a stale cell left
        // behind by one.
        var child = Screen(rows: 8, columns: 30);

        // Shaped like something worth getting wrong: colour, attributes, a
        // cursor moved about, and content on the first and last rows, which are
        // the two the bar and the wrap defects each ate.
        child.Write($"{Esc}[1;1Hfirst row, plain");
        child.Write($"{Esc}[3;5H{Esc}[31mred{Esc}[0m and {Esc}[1mbold{Esc}[0m");
        child.Write($"{Esc}[5;1H{Esc}[48;5;54mon a background{Esc}[0m");
        child.Write($"{Esc}[8;1Hlast row, which is the one that scrolled away");

        // AND IT HAS SCROLLED, which is the case that matters. That last line is
        // forty-five characters on a thirty-column screen, so it wraps and takes
        // the top row into scrollback - and an agent does that continuously.
        await Assert.That(child.Buffer.YDisp).IsGreaterThan(0)
            .Because("a round-trip that never scrolled would be checking the easy half.");

        var frame = PtyScreen.Paint(child, rows: 8, columns: 30, bar: "gg | flight 41");

        // A terminal the size of the REAL one - the child's rows plus gg's row -
        // told exactly what gg would have written to it.
        var screen = new XTermTerminal(new TerminalOptions { Cols = 30, Rows = 9 });
        screen.Write(frame);

        await Assert.That(screen.GetLine(0)).StartsWith("gg | flight 41", StringComparison.Ordinal)
            .Because("row one of the real terminal is gg's, and this is what a terminal makes "
                   + "of what gg wrote there.");

        // THE VIEWPORT, NOT THE BUFFER, and the distinction is the whole reason
        // this test earned its keep. GetLine is indexed from the scrollback
        // origin, so once anything has scrolled it answers about a row that is
        // no longer on screen; GetVisibleLines is what a terminal is showing.
        // A first version of this compared against GetLine and failed against a
        // renderer that was right - which is the good direction for a test to be
        // wrong in, but only if somebody goes and looks.
        var shown = screen.GetVisibleLines();
        var drew = child.GetVisibleLines();

        for (var row = 0; row < drew.Length; row++)
        {
            await Assert.That(shown[row + 1]).IsEqualTo(drew[row])
                .Because($"child row {row} is terminal row {row + 1}, and a repaint that does "
                       + "not reproduce it is one a person is reading instead of the screen "
                       + "the child drew.");
        }
    }

    [Test]
    public async Task The_panel_can_be_more_than_one_row_and_the_child_starts_under_it()
    {
        // THE BAR OPENS. gg keeps one row while it is only saying what ends the
        // session; asked to show the envelope it keeps several, because the
        // envelope does not fit on one and abbreviating the rules in force is
        // the wrong thing to abbreviate.
        //
        // The child does not paint a row it does not believe exists, which is
        // what kept ONE row gg's - so the same lie, told about a bigger number,
        // is the whole mechanism.
        var frame = PtyScreen.Paint(
            Screen(rows: 6, columns: 30), rows: 6, columns: 30,
            panel: ["gg · composing", "instructions in force:", "  keep the diff small"]);

        for (var row = 1; row <= 3; row++)
        {
            await Assert.That(frame).Contains($"{Esc}[{row};1H", StringComparison.Ordinal)
                .Because($"panel row {row} is terminal row {row}.");
        }

        await Assert.That(frame).Contains($"{Esc}[4;1H", StringComparison.Ordinal)
            .Because("three rows of panel means the child's first row is the fourth.");

        await Assert.That(frame).Contains("instructions in force:", StringComparison.Ordinal);
        await Assert.That(frame).Contains("keep the diff small", StringComparison.Ordinal);
    }

    [Test]
    public async Task Every_panel_row_fills_its_width()
    {
        // The one-row bar padded so it read as a bar rather than as words
        // floating on whatever the child left there. Every row of a taller panel
        // is the same: an unpadded one shows the child's screen through the gaps.
        var frame = PtyScreen.Paint(
            Screen(rows: 5, columns: 12), rows: 5, columns: 12,
            panel: ["gg", "envelope v6"]);

        await Assert.That(frame).Contains("gg          ", StringComparison.Ordinal);
        await Assert.That(frame).Contains("envelope v6 ", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_panel_row_too_long_is_cut_rather_than_wrapped()
    {
        // Wrapping would push every row below it down by one and the last row of
        // the child off the bottom - the same defect the auto-wrap guard exists
        // for, arriving from gg's own text instead of the child's.
        var frame = PtyScreen.Paint(
            Screen(rows: 5, columns: 12), rows: 5, columns: 12,
            panel: ["gg", "an instruction far longer than this terminal is wide"]);

        await Assert.That(frame).Contains("an instructi", StringComparison.Ordinal);
        await Assert.That(frame).DoesNotContain("longer", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_cursor_is_offset_by_however_many_rows_gg_kept()
    {
        // Off by the bar and typing looks haunted; off by a PANEL and it looks
        // haunted by more. The offset is whatever the panel actually is, not a
        // constant that was right when the panel was one row.
        var terminal = Screen(rows: 5, columns: 20, wrote: $"{Esc}[2;4Hhere");

        var frame = PtyScreen.Paint(
            terminal, rows: 5, columns: 20, panel: ["gg", "one", "two", "three"]);

        await Assert.That(frame).EndsWith(
            $"{Esc}[{terminal.Buffer.Y + 5};{terminal.Buffer.X + 1}H{Esc}[?25h{Esc}[?7h",
            StringComparison.Ordinal)
            .Because("four panel rows put the child's own row zero on terminal row five.");
    }

    [Test]
    public async Task Replayed_into_a_terminal_a_taller_panel_still_IS_the_child_s_screen()
    {
        // THE ROUND TRIP, EXTENDED. The property that made the one-row renderer
        // safe to change is the one that has to survive the panel growing: what
        // a terminal makes of gg's frame is the child's screen, whatever gg kept
        // above it.
        var child = Screen(rows: 6, columns: 30);
        child.Write($"{Esc}[1;1Hfirst{Esc}[3;5H{Esc}[31mred{Esc}[0m{Esc}[6;1Hlast");

        string[] panel = ["gg · composing", "envelope v6", "  keep the diff small"];

        var frame = PtyScreen.Paint(child, rows: 6, columns: 30, panel: panel);

        var screen = new XTermTerminal(new TerminalOptions { Cols = 30, Rows = 9 });
        screen.Write(frame);

        var shown = screen.GetVisibleLines();
        var drew = child.GetVisibleLines();

        for (var row = 0; row < panel.Length; row++)
        {
            await Assert.That(shown[row]).StartsWith(panel[row], StringComparison.Ordinal);
        }

        for (var row = 0; row < drew.Length; row++)
        {
            await Assert.That(shown[row + panel.Length]).IsEqualTo(drew[row])
                .Because($"child row {row} is terminal row {row + panel.Length}.");
        }
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
