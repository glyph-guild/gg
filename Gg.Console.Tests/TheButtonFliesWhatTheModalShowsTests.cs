using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The actions tab flies the item the modal is about, not the one the cursor
/// happens to be on behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two are usually the same and once they are not, the tab lies.</b>
/// <c>Command.OpenTheTicket</c> opens this modal on a flight's ticket - an
/// item that is "usually nowhere on the page somebody browsed", in the
/// reducer's own words - and it deliberately leaves the listing alone so a
/// filtered page is not thrown away. So the modal shows the ticket while
/// <c>BrowseSelected</c> still points at whatever was under the cursor.
/// </para>
/// <para>
/// <b>Before the actions tab that cost nothing</b>, because the only way to fly
/// was <c>f</c> on the tab underneath, where the cursor IS the subject. A
/// button inside the modal makes the difference visible: the pane names one id
/// and the flight would carry another.
/// </para>
/// <para>
/// <b>So the fly resolves the same way the modal does</b> - through
/// <see cref="WorkItemDetails.Item"/>, which is already the one function that
/// answers for both ways in. Pressing <c>f</c> over a listing is unchanged,
/// because with nothing held that function returns the row under the cursor.
/// </para>
/// </remarks>
public class TheButtonFliesWhatTheModalShowsTests
{
    private static AppState Browsing() => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.WorkItemDetail,
        WorkItemTab = WorkItemTab.Actions,

        // THE CURSOR IS ON THE SECOND ROW, so a fly that reads the listing and
        // one that reads the modal cannot agree by accident.
        BrowseSelected = 1,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items =
            [
                new BrowseRow { Id = "1001", Title = "the first row", State = "Active" },
                new BrowseRow { Id = "2002", Title = "the row under the cursor", State = "Active" },
            ],
        },
    };

    [Test]
    public async Task Opened_from_a_listing_it_flies_the_row_the_cursor_is_on()
    {
        // UNCHANGED, and that is the half worth pinning: `f` over a page has
        // always meant the row under the cursor and still does.
        var actions = new ConsoleDoubles.Records();

        ConsoleLoop.FlewPicked(Browsing(), actions);

        await Assert.That(actions.Flown.Select(f => f.Id)).IsEquivalentTo(["2002"]);
    }

    [Test]
    public async Task Opened_from_a_flights_ticket_it_flies_the_ticket()
    {
        // WHAT THE MODAL IS ABOUT. The listing is still there and its cursor is
        // still on 2002; the modal is showing 3003 because a flight named it.
        var held = Browsing() with
        {
            WorkItemId = "3003",
            WorkItemRow = new BrowseRow { Id = "3003", Title = "", State = "" },
        };

        var actions = new ConsoleDoubles.Records();

        ConsoleLoop.FlewPicked(held, actions);

        await Assert.That(actions.Flown.Select(f => f.Id)).IsEquivalentTo(["3003"])
            .Because("the pane says 'Open a flight on a-tracker 3003' and the button under it "
                   + "has to mean that sentence - a button that opens a flight on something "
                   + "else is worse than no button.");
    }

    [Test]
    public async Task What_the_pane_promises_is_what_the_button_does()
    {
        // THE TWO READ THE SAME FUNCTION, asserted rather than assumed: this is
        // the pairing that went wrong, so it is the pairing that is held.
        var held = Browsing() with
        {
            WorkItemId = "3003",
            WorkItemRow = new BrowseRow { Id = "3003", Title = "", State = "" },
        };

        var actions = new ConsoleDoubles.Records();

        ConsoleLoop.FlewPicked(held, actions);

        await Assert.That(WorkItemDetails.ActionsSaid(held)).Contains(actions.Flown[0].Id);
    }
}
