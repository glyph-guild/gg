using System.Text.RegularExpressions;

namespace Gg.Console.Tests;

/// <summary>
/// A table that has been scrolled stays scrolled when it is filled again.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported as "clicking doesn't click the row I'm hovering over", and the
/// click was innocent.</b> Measured in a pty on the flights tab, two pages
/// down: clicking the row on screen line 20 selected exactly the right flight —
/// and then the list moved twenty-one rows. The render that follows the click
/// lost the table's scroll position, and <c>EnsureCursorIsVisible</c> scrolled
/// only far enough to bring the selected row back on screen, which parks it at
/// the bottom edge rather than leaving it under the pointer. A different flight
/// lands where the person is still pointing, so their next click goes somewhere
/// they did not choose.
/// </para>
/// <para>
/// <b>It grows with the scroll, which is why it reads as "further down".</b> At
/// the top there is no offset to lose and nothing moves; the jump is exactly
/// the distance scrolled.
/// </para>
/// <para>
/// <b>The flight log already had the fix and the tab tables never got it.</b>
/// Its own fill reads <c>RowOffset</c> before the swap and puts it back after,
/// under a comment ending <i>measured</i>. The six tab tables share one
/// <c>Fill</c>, which did not.
/// </para>
/// <para>
/// <b>This is a source scan, and the reason is worth writing down.</b> A
/// <c>TableView</c> built in a test keeps its <c>RowOffset</c> across a source
/// swap — measured twice, the second time with a real viewport set — so a
/// behavioural test of the fill passes identically with the fix and without it.
/// The reset needs the real render path, which needs a driver, which no test
/// here has. A green unit test would have been a guard that could never fail,
/// which is worse than this. The behaviour is proven in the pty and recorded on
/// the pull request; what this holds is that the restore is still here.
/// </para>
/// </remarks>
public class AScrolledTableStaysWhereItWasTests
{
    private static string Fill()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");
        var start = screen.IndexOf("private static void Fill<T>(", StringComparison.Ordinal);

        if (start < 0)
        {
            // NAMED, NOT COUNTED. A scan whose subject has been renamed should
            // say so rather than pass over an empty string.
            throw new InvalidOperationException(
                "Fill<T> is gone from ConsoleScreen, so this scan is reading nothing.");
        }

        var end = screen.IndexOf("\n    /// <summary>", start, StringComparison.Ordinal);

        return screen[start..(end < 0 ? screen.Length : end)];
    }

    [Test]
    public async Task The_shared_fill_puts_the_scroll_position_back()
    {
        var body = Fill();

        // THE SECOND FILL, NOT THE FIRST. An empty list returns early through
        // `CollectionViews.Fill(table, null)`, so matching the bare call finds
        // the early return and reports the restore as out of order - which is
        // what this test said the first time it ran.
        var reads = body.IndexOf("= table.RowOffset;", StringComparison.Ordinal);
        var swaps = body.IndexOf("CollectionViews.Fill(table, new DataTableSource", StringComparison.Ordinal);
        var puts = body.IndexOf("table.RowOffset =", StringComparison.Ordinal);

        await Assert.That(swaps).IsGreaterThan(-1)
            .Because("everything below is a position relative to the swap, so a scan that "
                   + "could not find it would pass by comparing -1 to nothing.");

        await Assert.That(reads).IsGreaterThan(-1)
            .Because("the offset has to be read BEFORE the source under it is replaced - "
                   + "afterwards there is nothing left to read.");
        await Assert.That(reads).IsLessThan(swaps)
            .Because("read after the swap, it reads back whatever the swap just left.");
        await Assert.That(puts).IsGreaterThan(swaps)
            .Because("without the restore the table is back at the top on every render, and "
                   + "a click two pages down moves the list out from under the pointer.");
    }

    [Test]
    public async Task And_the_widget_still_gets_the_last_word()
    {
        // A RESTORE, NOT A CHOICE. EnsureCursorIsVisible has to run after the
        // offset goes back, or a cursor walked off the bottom with `j' stops
        // scrolling the view - the opposite bug, and just as annoying. Verified
        // in the pty: forty-five presses of `j' still move the list.
        var body = Fill();

        await Assert.That(body.IndexOf("EnsureCursorIsVisible", StringComparison.Ordinal))
            .IsGreaterThan(body.IndexOf("table.RowOffset =", StringComparison.Ordinal))
            .Because("the restore says where the rows were; the widget still says whether "
                   + "the cursor can be seen from there, and it has to answer second.");
    }

    [Test]
    public async Task The_fill_hands_over_a_new_source_every_render_which_is_why_this_is_needed()
    {
        // THE PREMISE, ASSERTED. All of this rests on the console rebuilding the
        // source from state on every pass rather than mutating one in place. If
        // that ever changes the restore is unnecessary, and this file should be
        // deleted rather than quietly kept.
        await Assert.That(Regex.IsMatch(Fill(), @"CollectionViews\.Fill\(\s*table,\s*new DataTableSource"))
            .IsTrue()
            .Because("a fill that mutated the existing source would keep the offset by "
                   + "itself, and none of this would be needed.");
    }
}
