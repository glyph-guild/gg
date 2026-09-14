using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// A keypress that asks a reader says so at once, and does not ask twice for
/// what it already has.
/// </summary>
/// <remarks>
/// <para>
/// <b>The blink was the progress indicator, and taking it away left
/// silence.</b> Ending the session said something was happening; a read folded
/// in beside the console says nothing until it lands. Worse than the blink for
/// the modals, because those did not exist until the answer arrived — pressing
/// enter on an item did nothing visible at all while the tracker thought about
/// it, which reads as a dead key and gets pressed again.
/// </para>
/// <para>
/// <b>So the mode is the reducer's and the content is the read's.</b> The modal
/// opens on the keypress, empty and saying it is reading; the answer fills it
/// when it arrives. That split is what the rest of this console already does —
/// a command changes the model, a read patches it later.
/// </para>
/// <para>
/// <b>And already held is already paid for.</b> Leaving and re-entering the
/// same item asked the tracker again every time. The listing is deliberately
/// NOT reused: `b` is a toggle, so reopening the pane is the gesture a person
/// uses when they want to see what is there now, and a cache would make the one
/// obvious way to refresh do nothing.
/// </para>
/// </remarks>
public class AReadBesideTheConsoleSaysItIsHappeningTests
{
    private static AppState Listed() => new()
    {
        ActiveTab = TabId.Browse,
        BrowseVisible = true,
        BrowseSelected = 0,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items = [new BrowseRow { Id = "18515", Title = "an item", State = "Active" }],
        },
    };

    [Test]
    public async Task Pressing_enter_opens_the_modal_before_the_reader_answers()
    {
        // THE DEAD KEY. Until the mode moved to the reducer nothing happened on
        // screen between the press and the answer, which is the one thing the
        // blink did do.
        var opening = Reducer.Reduce(Listed(), Command.ShowWorkItem);

        await Assert.That(opening.Mode).IsEqualTo(UiMode.WorkItemDetail)
            .Because("the press opens the modal; what the reader says arrives afterwards.");
    }

    [Test]
    public async Task And_it_says_it_is_reading_rather_than_showing_the_last_one()
    {
        // STALE CONTENT IS WORSE THAN NONE, because it is indistinguishable
        // from an answer. Opening item B must not show item A's words while B
        // is being fetched.
        var held = Listed() with
        {
            WorkItemId = "18000",
            WorkItemSaid = "what the PREVIOUS item said",
        };

        var opening = Reducer.Reduce(held, Command.ShowWorkItem);

        await Assert.That(opening.WorkItemSaid).DoesNotContain("PREVIOUS")
            .Because("this modal is about the row under the cursor, and showing the last "
                   + "item's prose under this one's title is a lie with a title on it.");
        await Assert.That(WorkItemDetails.Said(opening)).Contains("Reading")
            .Because("and the gap has to say what it is, or an empty modal reads as an item "
                   + "with nothing written on it.");
    }

    [Test]
    public async Task The_filter_opens_on_the_press_too()
    {
        var opening = Reducer.Reduce(Listed(), Command.FilterBrowse);

        await Assert.That(opening.Mode).IsEqualTo(UiMode.BrowseFilter);
    }

    [Test]
    public async Task The_same_item_twice_is_read_once()
    {
        // ALREADY HELD, ALREADY PAID FOR - ConsoleFlightLog's rule, and the
        // reason leaving and re-entering an item used to cost a round trip
        // every time.
        var held = Listed() with
        {
            WorkItemId = "18515",
            WorkItemSaid = "what it says",
        };

        var patch = ConsoleBrowsing.ItemPatch(new ConsoleDoubles.NeverAsked(), held);
        var after = patch(held);

        await Assert.That(after.WorkItemSaid).IsEqualTo("what it says");
    }

    [Test]
    public async Task And_g_asks_again_anyway()
    {
        // THE WAY PAST THE CACHE, because a tracker moves and a person who
        // suspects it has needs one gesture that always costs a request.
        var held = Listed() with
        {
            Mode = UiMode.WorkItemDetail,
            WorkItemId = "18515",
            WorkItemSaid = "what it said an hour ago",
        };

        var refreshed = Reducer.Reduce(held, Command.Refresh);

        await Assert.That(refreshed.WorkItemId).IsNull()
            .Because("dropping what is held is what makes the next ask a real one - a cache "
                   + "with no way past it is a stale screen nobody can fix.");
    }

    [Test]
    public async Task The_listing_is_not_cached_because_reopening_is_how_people_refresh_it()
    {
        // THE DELIBERATE ASYMMETRY. `b` is a toggle, so closing and reopening
        // the pane is the gesture somebody uses to see what is there NOW.
        // Caching that would make the obvious way to refresh do nothing.
        var patch = ConsoleBrowsing.Patch(new ConsoleDoubles.NeverAsked(), Listed());

        await Assert.That(patch).IsNotNull();
    }
}
