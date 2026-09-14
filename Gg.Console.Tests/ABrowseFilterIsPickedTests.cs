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
    public async Task Opening_the_filter_is_a_read_and_costs_no_screen()
    {
        // THE CHOICES COME FROM A CHILD PROCESS, and the sentence used to end
        // "holding a credential" - which SpawnedReader does not do. It places
        // no secret and the child talks over pipes, so asking a tracker what
        // there is to narrow by folds in beside the console like any other
        // read, including the press that has to start the reader first.
        foreach (var command in (Command[])[Command.FilterBrowse, Command.BrowseFiltered])
        {
            await Assert.That(ShellCommands.Reads).Contains(command);
            await Assert.That(ShellCommands.Handled).DoesNotContain(command);
        }
    }

    [Test]
    public async Task The_filter_is_a_modal_that_draws_itself()
    {
        await Assert.That(Modals.Drawn).Contains(UiMode.BrowseFilter);

        await Assert.That(Keymap.Resolve(
            KeyStroke.Esc, new KeymapContext(UiMode.BrowseFilter)))
            .IsEqualTo(Command.CloseModal);

        await Assert.That(Keymap.Resolve(
            KeyStroke.EnterKey, new KeymapContext(UiMode.BrowseFilter)))
            .IsEqualTo(Command.PickFilterValue);

        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('b'), new KeymapContext(UiMode.BrowseFilter)))
            .IsEqualTo(Command.BrowseFiltered);
    }

    [Test]
    public async Task Picking_a_second_area_path_replaces_the_first()
    {
        // ONE AREA PATH, BECAUSE A QUERY TAKES ONE. Accumulating them would be
        // a list this console could not send, so the modal cannot offer to
        // build one.
        var state = Reducer.Reduce(Offering(), Command.PickFilterValue);
        state = Reducer.Reduce(state with { AreaSelected = 1 }, Command.PickFilterValue);

        await Assert.That(state.ChosenAreaPath).IsEqualTo(@"Widgets\Platform");
    }

    [Test]
    public async Task Taking_one_criterion_back_says_nothing_about_the_others()
    {
        var state = Offering() with
        {
            ChosenAreaPath = @"Widgets\Platform",
            ChosenIteration = @"Widgets\Sprint 42",
            FilterView = BrowseFacet.Iteration,
        };

        var cleared = Reducer.Reduce(state, Command.PickFilterValue);

        await Assert.That(cleared.ChosenIteration).IsNull();
        await Assert.That(cleared.ChosenAreaPath).IsEqualTo(@"Widgets\Platform")
            .Because("clearing the sprint says nothing about the team.");
    }

    [Test]
    public async Task Clearing_takes_all_three_off_at_once()
    {
        var cleared = Reducer.Reduce(
            Offering() with
            {
                ChosenAreaPath = @"Widgets\Platform",
                ChosenIteration = @"Widgets\Sprint 42",
                ChosenStates = ["Active"],
            },
            Command.ClearFilter);

        await Assert.That(cleared.ChosenAreaPath).IsNull();
        await Assert.That(cleared.ChosenIteration).IsNull();
        await Assert.That(cleared.ChosenStates).IsEmpty()
            .Because("one key for the person who narrowed three ways and wants the backlog "
                   + "back; the per-row key is for fixing one of the three.");
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
    public async Task Ninety_sprints_do_not_make_the_body_ninety_lines()
    {
        // THE ORIGINAL DEFECT, NOW ANSWERED BY THE WIDGET. The dialog is sized
        // to its body, so a body that grew with the tracker asked for a box
        // ninety rows tall and drew the tail of one. The choices are a table
        // that scrolls; the body is the sentence beside it and nothing else.
        var many = Offering() with
        {
            Facets = new BrowseFacets
            {
                Iterations = [.. Enumerable.Range(1, 90).Select(n => $@"Widgets\Sprint {n}")],
            },
        };

        await Assert.That(PaneText.Modal(many).Split('\n').Length).IsLessThanOrEqualTo(4);
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
