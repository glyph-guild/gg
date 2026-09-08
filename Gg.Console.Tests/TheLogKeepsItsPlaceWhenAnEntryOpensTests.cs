using System.Data;
using Gg.Console.Views;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// Moving the cursor through the log does not move the log under it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The entry under the cursor unwraps, so every cursor move changes the row
/// set.</b> The one being left closes and the one being arrived at opens, and
/// the rows between them shift by however many lines those two details are
/// worth. The table is refilled from scratch to draw that, and a refill says
/// nothing about where the view is scrolled to - so the offset stays where the
/// OLD row set left it and points somewhere else entirely.
/// </para>
/// <para>
/// <b>Measured, because "a bit wonky" is not something you can fix.</b> With a
/// twelve-line viewport over a log whose fifth entry unwraps into thirty-five
/// lines: read down to the end of that detail and press down once more, and the
/// cursor lands THREE LINES ABOVE THE TOP OF THE VIEWPORT. It is not that the
/// highlight jumps - it is not on the screen at all. Where the details are short
/// the same cause shows as the highlight drifting a line at a time, which is
/// what a person reports as wonky.
/// </para>
/// <para>
/// <b>The cursor keeps its line and the log moves around it.</b> The
/// alternative - anchoring whatever entry is at the top of the viewport - keeps
/// the text still and lets the highlight drift, which is the reported symptom
/// rather than a fix for it. What a person is following is the highlight.
/// </para>
/// </remarks>
public class TheLogKeepsItsPlaceWhenAnEntryOpensTests
{
    // A LOG WHOSE ENTRIES ARE NOT ALL THE SAME SIZE, because even ones hide
    // this: the rows that vanish above the cursor are exactly what moves it.
    private static IReadOnlyList<LogRow> Log() =>
        [.. Enumerable.Range(0, 20).Select(i => new LogRow(
            i, "", $"12:0{i % 10}", "1", $"event {i}",
            i == 5
                ? string.Join(' ', Enumerable.Repeat($"a-long-detail-on-{i}", 40))
                : "short"))];

    private static ITableSource Source(IReadOnlyList<LogRow> shown)
    {
        var data = new DataTable();

        foreach (var column in Rows.LogColumns)
        {
            data.Columns.Add(column);
        }

        foreach (var row in shown)
        {
            data.Rows.Add(row.Mark, row.Time, row.Attempt, row.Event);
        }

        return new DataTableSource(data);
    }

    private static int StartOf(IReadOnlyList<LogRow> rows, int entry)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Entry == entry)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// A table showing entry 5's detail, scrolled to the end of it.
    /// </summary>
    /// <remarks>
    /// What a person has done to get here: opened the log, arrowed down onto an
    /// entry whose detail is thirty-five lines, and read to the bottom of it. The
    /// next press is the one that leaves the entry.
    /// </remarks>
    private static (TableView Table, int Was, int From, int At, IReadOnlyList<LogRow> Next)
        ReadToTheEndOfADetail()
    {
        var table = CollectionViews.Table();
        table.Width = 70;
        table.Height = 12;
        table.Layout();

        var log = Log();
        var width = Rows.DetailWidth(log, table.Viewport.Width);
        var open = Rows.Unwrapped(log, 5, width);
        var last = StartOf(open, 6) - 1;

        CollectionViews.Fill(table, Source(open));
        table.SetSelection(0, last, extendExistingSelection: false, null);
        table.EnsureValidSelection();
        table.EnsureCursorIsVisible();

        var was = last;
        var from = table.RowOffset;

        // THE PRESS THAT LEAVES ENTRY 5: it closes, entry 6 opens, and the whole
        // table is refilled to draw it.
        var next = Rows.Unwrapped(log, 6, width);
        var at = StartOf(next, 6);

        CollectionViews.Fill(table, Source(next));
        table.SetSelection(0, at, extendExistingSelection: false, null);
        table.EnsureValidSelection();

        return (table, was, from, at, next);
    }

    [Test]
    public async Task A_refill_leaves_the_view_scrolled_where_the_old_rows_put_it()
    {
        // THE REPRODUCTION, against the real widget, because this is a fact
        // about Terminal.Gui rather than about this console: filling a table and
        // setting a selection does not scroll to it. Everything else here is
        // arithmetic; this is the reason the arithmetic is needed at all.
        var (table, _, from, at, _) = ReadToTheEndOfADetail();

        await Assert.That(from).IsGreaterThan(0)
            .Because("this is about a view that has been scrolled, and one that never scrolled "
                   + "would pass the assertion below for the wrong reason.");

        await Assert.That(at - table.RowOffset).IsLessThan(0)
            .Because($"the cursor is at row {at} with the view scrolled to {table.RowOffset}, so "
                   + "it is above the top of the viewport - which is the whole defect. If this "
                   + "ever stops being true the library started handling it and the arithmetic "
                   + "can go.");
    }

    [Test]
    public async Task The_same_move_with_the_fix_puts_the_cursor_back_on_the_screen()
    {
        // THE LOOP CLOSED. Every other test here is arithmetic over numbers I
        // measured once; this one takes them from the widget on the way past, so
        // a change in how Terminal.Gui scrolls cannot leave the arithmetic
        // passing against numbers that stopped being true.
        var (table, was, from, at, _) = ReadToTheEndOfADetail();

        table.RowOffset = Rows.KeepingTheCursorsLine(was, from, at);
        table.EnsureCursorIsVisible();

        var line = at - table.RowOffset;

        await Assert.That(line).IsGreaterThanOrEqualTo(0)
            .Because($"the cursor is {-line} lines above the top of the viewport.");
        await Assert.That(line).IsLessThan(table.Viewport.Height)
            .Because($"the cursor is {line} lines down a viewport {table.Viewport.Height} "
                   + "lines tall, so it is off the bottom.");
    }

    [Test]
    public async Task The_cursor_keeps_its_line_when_the_rows_above_it_vanish()
    {
        // ELEVEN LINES DOWN BEFORE, ELEVEN AFTER, whatever happened in between.
        // Deep in a long log, where there is room above to give back.
        var offset = Rows.KeepingTheCursorsLine(was: 125, offset: 114, now: 106);

        await Assert.That(106 - offset).IsEqualTo(11)
            .Because("the cursor was 11 lines down and nobody asked for it to move.");
    }

    [Test]
    public async Task The_cursor_keeps_its_line_when_rows_appear_above_it()
    {
        // THE SAME MOVE UPWARDS: the entry arrived at is above the one left, so
        // opening it pushes everything below it down.
        var offset = Rows.KeepingTheCursorsLine(was: 6, offset: 2, now: 25);

        await Assert.That(25 - offset).IsEqualTo(4);
    }

    [Test]
    public async Task Near_the_top_it_stops_at_the_top_and_the_cursor_is_still_on_screen()
    {
        // THE MEASURED CASE, and the line cannot be kept: the cursor is 11 lines
        // down and lands on row 6, and there are not 11 rows above row 6 to
        // scroll to. The honest answer is the first row - the cursor ends up
        // closer to the top of the viewport than it was, which is what actually
        // happens when what you were reading closes up above you.
        var offset = Rows.KeepingTheCursorsLine(was: 25, offset: 14, now: 6);

        await Assert.That(offset).IsEqualTo(0);
        await Assert.That(6 - offset).IsEqualTo(6)
            .Because("six lines down a twelve-line viewport is on the screen, which is the "
                   + "point. Before this it was three lines above the top of it.");
    }

    [Test]
    public async Task It_never_scrolls_the_cursor_off_the_top()
    {
        // A cursor above the viewport is the defect this exists to fix, so no
        // input may produce one - including the nonsense ones, where an offset
        // already past the cursor would otherwise come straight back out.
        foreach (var (was, offset, now) in ((int, int, int)[])
                 [(25, 14, 6), (0, 9, 0), (6, 2, 25), (3, 40, 2), (-1, 0, 0), (0, -5, 7),
                  (125, 114, 106)])
        {
            var got = Rows.KeepingTheCursorsLine(was, offset, now);

            await Assert.That(got).IsLessThanOrEqualTo(Math.Max(0, now))
                .Because($"({was}, {offset}, {now}) scrolls to {got}, which is past the cursor - "
                       + "so the highlight is above the top of the viewport, which is the thing "
                       + "being fixed.");
            await Assert.That(got).IsGreaterThanOrEqualTo(0)
                .Because($"({was}, {offset}, {now}) scrolls to {got}, and there is no row "
                       + "before the first.");
        }
    }

    [Test]
    public async Task An_empty_log_is_scrolled_to_the_top()
    {
        // The modal opens on flights that never logged anything, and this runs
        // before the absence is noticed.
        await Assert.That(Rows.KeepingTheCursorsLine(was: 0, offset: 0, now: 0)).IsEqualTo(0);
    }

    [Test]
    public async Task The_view_scrolls_the_log_after_it_refills_it()
    {
        // HELD BY READING IT, because a ConsoleScreen cannot be built without a
        // terminal - the argument FocusReachesIntoTheFlightModalTests already
        // makes about this same modal.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        var from = screen.IndexOf("private void RenderLog(", StringComparison.Ordinal);
        await Assert.That(from).IsGreaterThan(-1)
            .Because("this scans one method, and a scan that found nothing would pass "
                   + "silently.");

        var method = screen[from..screen.IndexOf("\n    }", from, StringComparison.Ordinal)];

        await Assert.That(method).Contains("KeepingTheCursorsLine", StringComparison.Ordinal)
            .Because("the refill replaces every row and says nothing about the offset, so "
                   + "without this the view stays scrolled where the previous row set left it.");
    }
}
