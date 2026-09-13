using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Narrowing the work list from what the tracker offers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Picked from a list, never typed.</b> An area path is a tree with a team's
/// own punctuation in it. Typed one character wrong it answers an empty listing
/// that looks exactly like a sprint with no work in it - which is the confusion
/// the browse endings exist to end, arriving through a new door.
/// </para>
/// <para>
/// <b>One list with a cursor, not a key per answer.</b> The choices are a
/// tenant's and there may be any number of them, so a binding each would be a
/// keymap whose SHAPE depends on somebody's tracker - the argument
/// <c>WorkKindChoice</c> already makes, over a list that is longer.
/// </para>
/// <para>
/// <b>Choosing is not applying.</b> Every browse tears the session down and
/// starts a child holding a credential, so a keystroke that re-queried per
/// toggle would spawn a reader per cursor move. Picks accumulate in the modal
/// and one key runs the listing - which is also what makes the difference
/// between what is picked and what is on screen a thing the pane has to say.
/// </para>
/// </remarks>
public class ABrowseFilterIsPickedTests
{
    private static AppState Offering() => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.BrowseFilter,
        Facets = new BrowseFacets
        {
            AreaPaths = ["Widgets", @"Widgets\Platform"],
            Iterations = [@"Widgets\Sprint 42"],
            States = ["Active", "Closed"],
        },
    };

    [Test]
    public async Task The_browse_tab_has_a_key_that_opens_the_filter()
    {
        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('/'), new KeymapContext(UiMode.Normal, TabId.Browse)))
            .IsEqualTo(Command.FilterBrowse);
    }

    [Test]
    public async Task The_key_is_free_everywhere_else_it_could_have_shadowed_something()
    {
        // A KEY CHOSEN FOR ITS MNEMONIC THAT SILENTLY SHADOWS ANOTHER IS WORSE
        // THAN ONE CHOSEN FOR BEING FREE AND SAID TO BE. On every other tab it
        // must still resolve to nothing rather than to somebody else's command.
        foreach (var tab in Enum.GetValues<TabId>())
        {
            if (tab == TabId.Browse)
            {
                continue;
            }

            await Assert.That(Keymap.Resolve(
                KeyStroke.Char('/'), new KeymapContext(UiMode.Normal, tab)))
                .IsNull()
                .Because($"'/' must not mean anything on {tab} that it did not mean before.");
        }
    }

    [Test]
    public async Task Opening_the_filter_is_the_shells_work_because_it_starts_a_reader()
    {
        // NOT A BACKGROUND READ. The choices come from a child process holding a
        // credential, and a UI session may not spawn one - the same four guards
        // that keep ToggleBrowse out of ShellCommands.Reads.
        await Assert.That(ShellCommands.Handled).Contains(Command.FilterBrowse);
        await Assert.That(ShellCommands.Reads).DoesNotContain(Command.FilterBrowse);

        await Assert.That(ShellCommands.Handled).Contains(Command.BrowseFiltered);
    }

    [Test]
    public async Task The_filter_is_a_modal_that_draws_itself()
    {
        await Assert.That(Modals.Drawn).Contains(UiMode.BrowseFilter);

        await Assert.That(Keymap.Resolve(
            KeyStroke.Esc, new KeymapContext(UiMode.BrowseFilter)))
            .IsEqualTo(Command.CloseModal);
    }

    [Test]
    public async Task Every_offered_value_is_a_row_under_the_dimension_it_narrows()
    {
        var rows = BrowseFilters.Rows(Offering());

        await Assert.That(rows.Count).IsEqualTo(8)
            .Because("three dimensions, each with an 'any' row of its own, over two area "
                   + "paths, one sprint and two states.");

        await Assert.That(rows[0].Value).IsNull()
            .Because("clearing one dimension is a choice a person makes without clearing the "
                   + "other two, so every group starts with the row that does it.");

        await Assert.That(rows.Select(row => row.Value)).Contains(@"Widgets\Platform");
        await Assert.That(rows.Select(row => row.Value)).Contains("Active");
    }

    [Test]
    public async Task Picking_a_row_narrows_the_dimension_it_belongs_to()
    {
        var rows = BrowseFilters.Rows(Offering());
        var platform = rows.ToList().FindIndex(row => row.Value == @"Widgets\Platform");

        var picked = Reducer.Reduce(
            Offering() with { FilterSelected = platform }, Command.PickFilterValue);

        await Assert.That(picked.ChosenAreaPath).IsEqualTo(@"Widgets\Platform");
        await Assert.That(picked.ChosenIteration).IsNull()
            .Because("a person narrowing to a team has not said anything about a sprint.");
    }

    [Test]
    public async Task Picking_a_second_area_path_replaces_the_first()
    {
        // ONE AREA PATH, BECAUSE A QUERY TAKES ONE. Accumulating them would be a
        // list this console could not send, so the modal cannot offer to build
        // one.
        var rows = BrowseFilters.Rows(Offering());
        var first = rows.ToList().FindIndex(row => row.Value == "Widgets");
        var second = rows.ToList().FindIndex(row => row.Value == @"Widgets\Platform");

        var state = Reducer.Reduce(
            Offering() with { FilterSelected = first }, Command.PickFilterValue);
        state = Reducer.Reduce(state with { FilterSelected = second }, Command.PickFilterValue);

        await Assert.That(state.ChosenAreaPath).IsEqualTo(@"Widgets\Platform");
    }

    [Test]
    public async Task A_state_is_a_set_so_picking_two_keeps_both()
    {
        var rows = BrowseFilters.Rows(Offering());
        var active = rows.ToList().FindIndex(row => row.Value == "Active");
        var closed = rows.ToList().FindIndex(row => row.Value == "Closed");

        var state = Reducer.Reduce(
            Offering() with { FilterSelected = active }, Command.PickFilterValue);
        state = Reducer.Reduce(state with { FilterSelected = closed }, Command.PickFilterValue);

        await Assert.That(state.ChosenStates).IsEquivalentTo((string[])["Active", "Closed"])
            .Because("a person wants what is active AND what is resolved, and a tracker's "
                   + "query takes a set for exactly that reason.");

        var again = Reducer.Reduce(state with { FilterSelected = active }, Command.PickFilterValue);

        await Assert.That(again.ChosenStates).IsEquivalentTo((string[])["Closed"])
            .Because("the same key on the same row is how a person takes one back; there is "
                   + "no other key for it and there should not be two.");
    }

    [Test]
    public async Task The_any_row_of_a_dimension_clears_only_that_dimension()
    {
        var rows = BrowseFilters.Rows(Offering());
        var anySprint = rows.ToList()
            .FindIndex(row => row.Facet == BrowseFacet.Iteration && row.Value is null);

        var state = Offering() with
        {
            ChosenAreaPath = @"Widgets\Platform",
            ChosenIteration = @"Widgets\Sprint 42",
            FilterSelected = anySprint,
        };

        var cleared = Reducer.Reduce(state, Command.PickFilterValue);

        await Assert.That(cleared.ChosenIteration).IsNull();
        await Assert.That(cleared.ChosenAreaPath).IsEqualTo(@"Widgets\Platform")
            .Because("clearing the sprint says nothing about the team.");
    }

    [Test]
    public async Task The_filter_in_force_is_one_sentence_or_nothing_at_all()
    {
        await Assert.That(BrowseFilters.Said(Offering())).IsNull()
            .Because("null is 'nobody narrowed', which is what an unfiltered listing is - and "
                   + "a pane that printed 'no filter' over every list would be noise.");

        var said = BrowseFilters.Said(Offering() with
        {
            ChosenAreaPath = @"Widgets\Platform",
            ChosenStates = ["Active"],
        });

        await Assert.That(said).IsNotNull();
        await Assert.That(said!).Contains(@"Widgets\Platform");
        await Assert.That(said).Contains("Active");
    }

    [Test]
    public async Task The_modal_lists_the_choices_and_marks_the_ones_that_are_picked()
    {
        var drawn = PaneText.Modal(
            Offering() with { ChosenAreaPath = @"Widgets\Platform" });

        await Assert.That(drawn).Contains(@"Widgets\Platform");
        await Assert.That(drawn).Contains("Sprint 42");
        await Assert.That(drawn).Contains("Active");
    }

    [Test]
    public async Task A_tracker_that_offered_nothing_says_so_rather_than_drawing_an_empty_box()
    {
        var drawn = PaneText.Modal(
            new AppState
            {
                ActiveTab = TabId.Browse,
                Mode = UiMode.BrowseFilter,
                Facets = new BrowseFacets
                {
                    Why = "The reader for 'a-tracker' does not declare it.",
                },
            });

        await Assert.That(drawn).Contains("a-tracker")
            .Because("an empty list of choices and a reader that could not be asked look the "
                   + "same on screen, and one of them means go and look at the reader.");
    }

    [Test]
    public async Task A_long_list_of_sprints_does_not_draw_a_modal_taller_than_the_screen()
    {
        // FOUND IN A PTY AGAINST A REAL TRACKER, which had ninety sprints. The
        // modal is sized to its body, so a body of a hundred lines asked for a
        // box a hundred rows tall - and a terminal with forty drew the tail of
        // it, with the cursor, the heading and every area path off the top.
        var many = new AppState
        {
            ActiveTab = TabId.Browse,
            Mode = UiMode.BrowseFilter,
            Facets = new BrowseFacets
            {
                AreaPaths = ["Widgets"],
                Iterations = [.. Enumerable.Range(1, 90).Select(n => $@"Widgets\Sprint {n}")],
            },
            FilterSelected = 60,
        };

        var lines = PaneText.Modal(many).Split('\n');

        await Assert.That(lines.Length).IsLessThanOrEqualTo(24)
            .Because("the modal is sized to its body and a terminal is not, so the body is "
                   + "what has to be bounded.");

        await Assert.That(PaneText.Modal(many)).Contains("Sprint 60")
            .Because("a window that does not contain the cursor is a list a person moves "
                   + "through blind.");

        await Assert.That(PaneText.Modal(many)).Contains("more")
            .Because("a truncated list that does not say it is truncated is a tracker that "
                   + "appears to have thirty sprints.");
    }

    [Test]
    public async Task A_reader_that_cannot_narrow_is_reported_as_that_and_not_as_a_gap()
    {
        // THE ARM THAT WAS MISSING. BrowseOutcome gained an ending and the
        // reducer's catch-all answered it with "this console does not have a
        // sentence for that" - which is true of the console and false of the
        // reader, who said exactly what was wrong.
        var state = Reducer.Browsed(
            new AppState(), "a-tracker",
            new BrowseOutcome.NotFilterable("The reader for 'a-tracker' cannot narrow."));

        await Assert.That(state.Browse!.Absence!).Contains("cannot narrow");
        await Assert.That(state.Browse.Absence!).DoesNotContain("does not have a sentence");
    }
}
