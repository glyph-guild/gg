using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// Three criteria, three tabs, three tables — and what is in force along the foot.
/// </summary>
/// <remarks>
/// <para>
/// <b>A label with a caret in it is not a list.</b> The first shape of this
/// modal drew its choices as wrapped text with a marker on one line, which
/// cannot be scrolled, cannot be clicked, and had to be windowed by hand to
/// fit a screen at all — a tracker with ninety sprints got fifteen of them and
/// a count of the rest. A table is the widget this console already uses
/// everywhere a person picks a row out of a list somebody else's system
/// decides the length of.
/// </para>
/// <para>
/// <b>Three tabs rather than one list of everything.</b> Area paths, sprints
/// and states are three questions, and one flat list interleaving them made a
/// person scroll through every sprint a project has ever run to reach the
/// states. The runner modal already answers three questions about one runner
/// this way, and this is that shape.
/// </para>
/// <para>
/// <b>And what is picked is along the foot, where all three show at once.</b>
/// Tabs hide two answers out of three by construction, so a modal that said
/// what was picked only on the tab it was picked in would be a person turning
/// the bar to remember what they had chosen.
/// </para>
/// </remarks>
public class TheFilterModalIsTabbedTests
{
    private static AppState Offering() => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.BrowseFilter,
        Facets = new BrowseFacets
        {
            AreaPaths = ["Widgets", @"Widgets\Platform"],
            Iterations = [@"Widgets\Sprint 42", @"Widgets\Sprint 43"],
            States = ["Active", "Closed"],
        },
    };

    [Test]
    public async Task The_three_criteria_are_three_views()
    {
        await Assert.That(FilterViews.All).IsEquivalentTo(
            (BrowseFacet[])[BrowseFacet.AreaPath, BrowseFacet.Iteration, BrowseFacet.State]);

        // LOWER CASE AND THE WORD A PERSON WOULD USE, which is what the tabs
        // one modal over say, and they sit along the same kind of foot.
        await Assert.That(FilterViews.Title(BrowseFacet.AreaPath)).IsEqualTo("area paths");
        await Assert.That(FilterViews.Title(BrowseFacet.Iteration)).IsEqualTo("sprints");
        await Assert.That(FilterViews.Title(BrowseFacet.State)).IsEqualTo("states");

        await Assert.That(FilterViews.Next(BrowseFacet.State)).IsEqualTo(BrowseFacet.AreaPath)
            .Because("the bar turns round rather than stopping at the end.");
    }

    [Test]
    public async Task A_key_turns_the_bar_the_way_the_runner_modal_does()
    {
        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('v'), new KeymapContext(UiMode.BrowseFilter)))
            .IsEqualTo(Command.NextFilterView);

        var turned = Reducer.Reduce(Offering(), Command.NextFilterView);

        await Assert.That(turned.FilterView).IsEqualTo(BrowseFacet.Iteration);
    }

    [Test]
    public async Task Each_view_keeps_its_own_cursor()
    {
        // A THIRD CURSOR, BECAUSE THERE ARE THREE LISTS. Sharing one would move
        // a person's place in a list they were not looking at - which is the
        // argument the browse pane's own cursor already makes against sharing
        // the queue's.
        var state = Reducer.Reduce(Offering(), Command.SelectNext);

        await Assert.That(state.AreaSelected).IsEqualTo(1);
        await Assert.That(state.IterationSelected).IsEqualTo(0);

        var sprints = Reducer.Reduce(
            state with { FilterView = BrowseFacet.Iteration }, Command.SelectNext);

        await Assert.That(sprints.IterationSelected).IsEqualTo(1);
        await Assert.That(sprints.AreaSelected).IsEqualTo(1)
            .Because("moving in the sprints must not move where somebody was in the areas.");
    }

    [Test]
    public async Task A_view_offers_exactly_what_the_tracker_said()
    {
        var rows = BrowseFilters.Offered(Offering(), BrowseFacet.Iteration);

        await Assert.That(rows.Select(row => row.Value)).IsEquivalentTo(
            (string[])[@"Widgets\Sprint 42", @"Widgets\Sprint 43"])
            .Because("no invented row: a table's first row is a value, and 'any' as a row was "
                   + "a thing to scroll past that the same key already does.");
    }

    [Test]
    public async Task Picking_the_one_that_is_picked_takes_it_back()
    {
        // ONE KEY, BOTH WAYS. An area path that could be chosen and not
        // unchosen would need a second key for the taking-back, or an invented
        // row to choose instead - and a person who narrowed to the wrong team
        // would have to clear the whole filter to fix one of three.
        var picked = Reducer.Reduce(Offering(), Command.PickFilterValue);

        await Assert.That(picked.ChosenAreaPath).IsEqualTo("Widgets");

        var back = Reducer.Reduce(picked, Command.PickFilterValue);

        await Assert.That(back.ChosenAreaPath).IsNull();
    }

    [Test]
    public async Task A_state_still_accumulates_and_the_others_still_replace()
    {
        var state = Offering() with { FilterView = BrowseFacet.State };

        state = Reducer.Reduce(state, Command.PickFilterValue);
        state = Reducer.Reduce(state with { StateSelected = 1 }, Command.PickFilterValue);

        await Assert.That(state.ChosenStates).IsEquivalentTo((string[])["Active", "Closed"]);
    }

    [Test]
    public async Task Each_view_is_a_table_with_a_mark_and_a_value()
    {
        await Assert.That(Rows.FilterColumns(BrowseFacet.AreaPath))
            .IsEquivalentTo((string[])["", "area path"]);
        await Assert.That(Rows.FilterColumns(BrowseFacet.Iteration))
            .IsEquivalentTo((string[])["", "sprint"]);
        await Assert.That(Rows.FilterColumns(BrowseFacet.State))
            .IsEquivalentTo((string[])["", "state"]);

        var rows = BrowseFilters.Offered(
            Offering() with { ChosenAreaPath = @"Widgets\Platform" }, BrowseFacet.AreaPath);

        await Assert.That(rows.Single(row => row.Chosen).Value).IsEqualTo(@"Widgets\Platform")
            .Because("the mark is how a person reading one tab knows what they picked in it.");
    }

    [Test]
    public async Task What_is_picked_is_said_along_the_foot_for_all_three_at_once()
    {
        var nothing = PaneText.FilterInForce(Offering());

        await Assert.That(nothing).Contains("everything")
            .Because("an empty foot under three empty tabs says nothing about whether the "
                   + "listing behind it is narrowed, and the answer is that it is not.");

        var some = PaneText.FilterInForce(Offering() with
        {
            ChosenAreaPath = @"Widgets\Platform",
            ChosenStates = ["Active"],
        });

        await Assert.That(some).Contains(@"Widgets\Platform");
        await Assert.That(some).Contains("Active");
    }

    [Test]
    public async Task The_box_is_the_size_of_a_document_because_it_holds_a_list()
    {
        await Assert.That(PaneText.ModalIsADocument(UiMode.BrowseFilter)).IsTrue()
            .Because("a question with two answers wants a box an eye takes in at once; a "
                   + "tracker's whole area tree wants the screen.");
    }

    [Test]
    public async Task The_screen_draws_it_as_tables_in_a_bar()
    {
        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("_filterViews")
            .Because("a bar, like the runner modal's, so a click on a header turns it too.");

        await Assert.That(screen).Contains("_filterInForce")
            .Because("and a foot, because tabs hide two answers out of three.");

        await Assert.That(screen).Contains("Rows.FilterColumns")
            .Because("the columns are the model's, or nothing can be asked what heading was "
                   + "drawn - which is the argument every other table here already made.");
    }
}
