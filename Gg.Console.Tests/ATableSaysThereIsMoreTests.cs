using Gg.Console.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Gg.Console.Tests;

/// <summary>
/// Every table in this console shows a scrollbar when there is more than fits.
/// </summary>
/// <remarks>
/// <para>
/// <b>The argument is <c>CollectionViews.Document</c>'s, and it was made about
/// a list.</b> Without a bar, a full box and a long page look exactly alike —
/// which is <i>the environment textbox is not scrollable</i> said from the
/// other side, about a page that genuinely was. Every table here has the same
/// problem and none of them had the fix: a queue, a fleet, a flight log or a
/// changeset longer than its box ended at the bottom edge with nothing saying
/// so.
/// </para>
/// <para>
/// <b>Auto rather than always</b>, so the bar is a fact about the rows rather
/// than furniture. A table that fits keeps its whole width.
/// </para>
/// <para>
/// <b>Vertical only.</b> These tables expand their last column to the width
/// they are given, so there is never anything to the right to scroll to — and a
/// horizontal bar would spend a row of height saying so. Rows are the thing
/// there is more of.
/// </para>
/// <para>
/// <b>And the bar costs a column, which is the half that bites.</b> It draws
/// INSIDE the viewport over the last column — measured in a pty for the lists,
/// where a line wrapped to the full width came back with its last character
/// under the bar. The flight log wraps its detail column to
/// <c>Viewport.Width</c>, so adding a bar without telling that arithmetic about
/// it clips the very text somebody opened the row to read.
/// </para>
/// </remarks>
public class ATableSaysThereIsMoreTests
{
    [Test]
    public async Task A_table_shows_a_bar_when_there_is_more_than_fits()
    {
        var table = CollectionViews.Table();

        await Assert.That(table.VerticalScrollBar.VisibilityMode)
            .IsEqualTo(ScrollBarVisibilityMode.Auto)
            .Because("a full box and a longer one look identical without it, which is how "
                   + "somebody comes to report that a pane does not scroll when it does.");
    }

    [Test]
    public async Task It_spends_no_height_on_a_bar_it_would_never_need()
    {
        // EXPANDLASTCOLUMN IS WHY. A table that fills the width it is given has
        // nothing off to the right, so a horizontal bar would be a row of the
        // box spent saying "there is nothing more this way".
        var table = CollectionViews.Table();

        await Assert.That(table.Style.ExpandLastColumn).IsTrue()
            .Because("the horizontal bar is refused on the strength of this, so if it ever "
                   + "stops being true the refusal has to be argued again.");

        // NOT SHOWN, rather than naming the spelling of "off". What matters is
        // that neither mode which DRAWS one is set; which constant means hidden
        // is Terminal.Gui's business and has changed name before.
        await Assert.That(table.HorizontalScrollBar.VisibilityMode
                is ScrollBarVisibilityMode.Auto or ScrollBarVisibilityMode.Always)
            .IsFalse()
            .Because("a table that fills the width it is given has nothing off to the "
                   + "right, so a horizontal bar would spend a row of the box saying so.");
    }

    [Test]
    public async Task The_width_a_table_offers_its_text_leaves_the_bar_room()
    {
        // THE ARITHMETIC THE LIST ALREADY HAS, for the widget that now needs it
        // too. The flight log wraps its detail column to the width it is told,
        // and a width that counted the bar's column would put the last
        // character of the longest line underneath it.
        var table = CollectionViews.Table();
        table.Viewport = new System.Drawing.Rectangle(0, 0, 80, 24);

        await Assert.That(CollectionViews.TextWidth(table)).IsEqualTo(79)
            .Because("the bar draws inside the viewport over the last column, so the text "
                   + "may have every column but that one.");
    }

    [Test]
    public async Task It_never_offers_a_width_too_narrow_to_be_a_line()
    {
        // THE LIST'S OWN FLOOR, for the list's own reason: a viewport is zero
        // wide before Terminal.Gui has laid anything out, and wrapping to zero
        // or to one is a column of single letters rather than a page.
        var table = CollectionViews.Table();

        await Assert.That(CollectionViews.TextWidth(table)).IsGreaterThanOrEqualTo(20)
            .Because("nothing has been laid out yet, so the viewport is zero - and a "
                   + "wrap width of minus one is not a narrower page, it is a crash or a "
                   + "column of letters.");
    }

    [Test]
    public async Task The_flight_log_asks_for_the_width_that_leaves_the_bar_room()
    {
        // A RATCHET, because this is the one that fails silently. The log's
        // detail column is wrapped to whatever the view hands DetailWidth, and
        // handing it Viewport.Width is right up until a bar appears - then the
        // longest line loses its last character and nothing says why. Source,
        // because ConsoleScreen cannot be constructed without a terminal.
        var screen = await File.ReadAllTextAsync(ScreenPath());

        await Assert.That(screen).DoesNotContain(
            "Rows.DetailWidth(log, _flightLog.Viewport.Width)", StringComparison.Ordinal)
            .Because("Viewport.Width includes the column the scrollbar draws over, so the "
                   + "detail column would wrap one character too wide and clip the text "
                   + "somebody opened the row to read.");
    }

    private static string ScreenPath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null
            && !File.Exists(Path.Combine(here.FullName, "Gg.Console", "Views", "ConsoleScreen.cs")))
        {
            here = here.Parent;
        }

        if (here is null)
        {
            throw new InvalidOperationException(
                "Gg.Console/Views/ConsoleScreen.cs is not above this test's output directory, "
              + "so this walk would assert over nothing.");
        }

        return Path.Combine(here.FullName, "Gg.Console", "Views", "ConsoleScreen.cs");
    }
}
