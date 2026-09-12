using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The three views of a document are tabs along the bottom of the airspace
/// tab's right-hand pane, not a modal.
/// </summary>
/// <remarks>
/// <para>
/// <b>A MODAL WAS THE WRONG SHAPE FOR THIS.</b> Reading what governs a work
/// kind meant taking the tree away and putting a document in its place, so
/// comparing a row against its neighbour was two keypresses and a memory. The
/// tree and the document belong on screen together: the cursor picks the
/// subject, the pane answers about it.
/// </para>
/// <para>
/// <b>ALONG THE BOTTOM, because the top already has a bar.</b> The console's
/// tabs run across the top of the window, and a second row of tabs up there
/// would read as more of the same bar rather than as a question about the
/// selected row.
/// </para>
/// <para>
/// <b>AND THE SAME RECONCILE AS THE TOP BAR.</b> Which views a row has moves
/// with the row - on-disk appears only when the file differs, effective only
/// for a work kind - so this bar's membership changes on an arrow key, where
/// the top bar's changed once in the life of the console and crashed it. The
/// bar that moves constantly may not be the copy that gets it wrong.
/// </para>
/// </remarks>
public class TheAirspaceViewsAreTabsOnThePaneTests
{
    [Test]
    public async Task Each_view_has_a_name_to_put_on_a_tab()
    {
        // WHAT THE TAB SAYS IS PURE, for the reason every other title in this
        // console is: a screen that cannot be constructed cannot be asked what
        // it drew.
        await Assert.That(AirspaceViews.Title(AirspaceView.OnDisk)).IsEqualTo("on disk");
        await Assert.That(AirspaceViews.Title(AirspaceView.Applied)).IsEqualTo("applied");
        await Assert.That(AirspaceViews.Title(AirspaceView.Effective)).IsEqualTo("effective");
    }

    [Test]
    public async Task The_names_are_in_the_order_the_work_flows()
    {
        // THE ORDER IS THE ASK, and it is the order of the work: what you
        // wrote, what landed, what governs. Offered already returns it; this
        // holds the titles to the same walk so a reader of the bar and a
        // reader of the enum see one sequence.
        var walked = ((AirspaceView[])[
            AirspaceView.OnDisk, AirspaceView.Applied, AirspaceView.Effective])
            .Select(AirspaceViews.Title);

        await Assert.That(walked).IsEquivalentTo(["on disk", "applied", "effective"]);
    }

    [Test]
    public async Task The_pane_is_a_bar_along_the_bottom()
    {
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("_airspaceViews", StringComparison.Ordinal)
            .Because("the three views are a bar on the pane, not a modal over the tree.");

        await Assert.That(screen).Contains("TabSide = Side.Bottom", StringComparison.Ordinal)
            .Because("the window's own tabs run along the top, and a second row up there "
                   + "would read as more of that bar rather than as a question about the "
                   + "row the cursor is on.");
    }

    [Test]
    public async Task Both_bars_follow_their_offered_set_through_one_method()
    {
        // THE LESSON FROM THE CRASH, APPLIED BEFORE IT HAPPENS AGAIN. The top
        // bar's offered set changed once in the life of a console and threw.
        // This one changes every time the cursor moves to a row with different
        // views, which is constantly - so a second copy of the reconcile is a
        // second place to get the same thing wrong, and this asserts there is
        // one.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen).Contains("Follow(_bar,", StringComparison.Ordinal)
            .Because("the window's bar reconciles through the shared method.");

        await Assert.That(screen).Contains("Follow(_airspaceViews,", StringComparison.Ordinal)
            .Because("and so does the pane's, rather than through a copy of it.");
    }

    [Test]
    public async Task The_pane_draws_what_PaneText_says_about_the_row()
    {
        // THE PANE HOLDS NOTHING IT DID NOT ASK FOR. One producer, already
        // tested over the three views, and the screen is the thing that puts
        // its lines in a list.
        var screen = Sources.Read("Gg.Console", "Views", "ConsoleScreen.cs");

        await Assert.That(screen)
            .Contains("PaneText.AirspaceDocument(State,", StringComparison.Ordinal)
            .Because("the right-hand pane renders the producer the view tests already hold "
                   + "to the three answers, rather than a second rendering of them.");
    }
}
