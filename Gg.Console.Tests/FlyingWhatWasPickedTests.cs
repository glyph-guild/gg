using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Picking a work item and flying it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.5-01 and -02. Declared, never parsed.</b> What is sent is a provider
/// key and an id, as two values — not a string somebody formats and something
/// else takes apart again. <c>FlightIntent.Id</c> already states the rule, and
/// a round trip through <c>provider#id</c> would break the first time an id
/// contained the separator.
/// </para>
/// <para>
/// <b>THE TITLE DOES NOT CROSS.</b> A person picked a row by reading it; what
/// the flight is called is what a person types or what ingress derives. Sending
/// the title would put customer content into a flight name that a different
/// tenant's screen may show, and it is the one thing on this path that looks
/// harmless and is not.
/// </para>
/// <para>
/// <b>Selection is the browse pane's own.</b> <c>SelectedRow</c> is the queue's
/// and stays the queue's: a person scrolling a work list must not move the
/// queue selection underneath the flight pane they will go back to.
/// </para>
/// </remarks>
public class FlyingWhatWasPickedTests
{
    private static AppState Browsing(params string[] ids)
    {
        // THROUGH THE TOGGLE, not by setting the flag. BrowseVisible used to
        // mean the browser had the screen; under tabs it means the view is
        // open, and what decides where the cursor keys go is which tab is
        // showing. A fixture that sets the flag by hand describes a state the
        // console cannot reach - open, and not looked at - and then asserts
        // the cursor behaves as though somebody were looking at it.
        var state = Reducer.BrowseToggled(new AppState { SelectedRow = 2 });

        return Reducer.Browsed(state, "a-tracker", new BrowseOutcome.Listed(
            new WorkItemPage(
                [.. ids.Select(id => new WorkItemSummary(
                    id, $"Something about {id}", "Active", $"https://tracker/{id}", null))],
                null)));
    }


    [Test]
    public async Task Flying_a_picked_item_sends_a_provider_and_an_id()
    {
        var actions = new ConsoleDoubles.Records();

        var state = ConsoleLoop.FlewPicked(Browsing("18398", "18471"), actions);

        await Assert.That(actions.Flown).Count().IsEqualTo(1);
        await Assert.That(actions.Flown[0].Provider).IsEqualTo("a-tracker");
        await Assert.That(actions.Flown[0].Id).IsEqualTo("18398");
        await Assert.That(state.LastFlightOpened).Contains("a-tracker#18398");
    }

    [Test]
    public async Task The_second_row_is_the_one_that_flies_when_it_is_the_one_picked()
    {
        var actions = new ConsoleDoubles.Records();
        var picked = Browsing("18398", "18471") with { BrowseSelected = 1 };

        _ = ConsoleLoop.FlewPicked(picked, actions);

        await Assert.That(actions.Flown[0].Id).IsEqualTo("18471");
    }

    [Test]
    public async Task The_title_the_person_read_does_not_cross()
    {
        // RULE 2, asserted on the request rather than on the rendering. The
        // title is in state because choosing without it is choosing by number;
        // it stops there.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Browsing("18398"), actions);

        var sent = string.Join(" ", actions.Flown.Select(f => f.Provider + " " + f.Id));

        await Assert.That(sent).DoesNotContain("Something about")
            .Because("what the flight is called is what a person types or what ingress "
                   + "derives, never what a tracker happened to say.");
    }

    [Test]
    public async Task The_url_does_not_cross_either()
    {
        // It is not even in the state to send, and this is the assertion that
        // keeps it that way: a future convenience that put it back would fail
        // here rather than in a review.
        var actions = new ConsoleDoubles.Records();

        _ = ConsoleLoop.FlewPicked(Browsing("18398"), actions);

        await Assert.That(string.Join(" ", actions.Flown.Select(f => f.Id)))
            .DoesNotContain("https://");
    }

    [Test]
    public async Task Flying_with_nothing_picked_says_so_and_sends_nothing()
    {
        // ARTICLE XI. An empty pane with a key that appears to work is worse
        // than one without the key.
        var actions = new ConsoleDoubles.Records();

        var state = ConsoleLoop.FlewPicked(new AppState { BrowseVisible = true, ActiveTab = TabId.Browse }, actions);

        await Assert.That(actions.Flown).IsEmpty();
        await Assert.That(state.LastFlightOpened).IsNotNull();
    }

    [Test]
    public async Task A_console_that_cannot_open_flights_says_that_rather_than_nothing()
    {
        var state = ConsoleLoop.FlewPicked(Browsing("18398"), actions: null);

        await Assert.That(state.LastFlightOpened).IsNotNull();
    }

    [Test]
    public async Task Moving_through_the_work_list_does_not_move_the_queue()
    {
        // The queue selection is what the flight pane hangs off. A person
        // scrolling a work list and going back to a different flight than they
        // left is the bug this separates.
        var browsing = Browsing("18398", "18471", "18515");

        var down = Reducer.Reduce(browsing, Command.SelectNext);

        await Assert.That(down.BrowseSelected).IsEqualTo(1);
        await Assert.That(down.SelectedRow).IsEqualTo(2)
            .Because("the queue's selection is the queue's.");
    }

    [Test]
    public async Task The_work_list_selection_stays_inside_the_list()
    {
        var browsing = Browsing("18398", "18471");

        var up = Reducer.Reduce(browsing, Command.SelectPrevious);
        await Assert.That(up.BrowseSelected).IsEqualTo(0);

        var far = Reducer.Reduce(Reducer.Reduce(Reducer.Reduce(
            browsing, Command.SelectNext), Command.SelectNext), Command.SelectNext);
        await Assert.That(far.BrowseSelected).IsEqualTo(1)
            .Because("a selection past the end selects nothing that exists.");
    }

    [Test]
    public async Task A_new_listing_starts_at_the_top()
    {
        // A cursor left pointing at row nine of a list that now has two rows is
        // a selection that flies the wrong item.
        var browsing = Browsing("1", "2", "3") with { BrowseSelected = 2 };

        var again = Reducer.Browsed(browsing, "a-tracker", new BrowseOutcome.Listed(
            new WorkItemPage([new WorkItemSummary("9", "Only one", "New", "", null)], null)));

        await Assert.That(again.BrowseSelected).IsEqualTo(0);
    }

    [Test]
    public async Task Flying_is_the_shells_because_it_writes()
    {
        await Assert.That(ShellCommands.Handled).Contains(Command.FlyPicked);
    }

    [Test]
    public async Task The_fly_key_is_offered_only_while_the_work_list_is_showing()
    {
        var browsing = new KeymapContext(UiMode.Normal, TabId.Browse);

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), browsing))
            .IsEqualTo(Command.FlyPicked);
        await Assert.That(Keymap.Hints(browsing)).Contains("fly this");

        await Assert.That(Keymap.Resolve(KeyStroke.Char('f'), new KeymapContext(UiMode.Normal)))
            .IsNull()
            .Because("a key advertised against no list is a key that does nothing.");
    }

    [Test]
    public async Task Fly_and_freeze_never_claim_the_same_key_at_once()
    {
        // THE HAZARD OF REUSING f. It is safe only because browse and live
        // share one region and cannot both be on - which is a fact about the
        // reducer, asserted here rather than remembered.
        foreach (var context in (KeymapContext[])
        [
            new(UiMode.Normal, TabId.Live),
            new(UiMode.Normal, TabId.Browse),
            new(UiMode.Normal, TabId.Browse),
        ])
        {
            var onF = Keymap.Bindings(context).Where(b => b.Key == KeyStroke.Char('f')).ToList();

            await Assert.That(onF.Count).IsLessThanOrEqualTo(1)
                .Because("two meanings for one key is a key whose behaviour depends on which "
                       + "list was written first.");
        }
    }
}
