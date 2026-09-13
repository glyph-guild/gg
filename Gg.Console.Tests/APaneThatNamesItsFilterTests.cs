using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A listing says what it was narrowed by, and says when it was not.
/// </summary>
/// <remarks>
/// <para>
/// <b>AN UNFILTERED LIST AND A FILTERED ONE THAT FOUND TWO THINGS MUST NOT
/// LOOK ALIKE.</b> The browse pane already keeps a sentence for each of the
/// ways a listing can be empty, because an empty box cannot say which one it
/// is. This is the same rule where the box is full: two rows under a filter
/// nobody can see is a person concluding their backlog is nearly done.
/// </para>
/// <para>
/// <b>What the ROWS were fetched with, not what is picked.</b> Picking and
/// browsing are two keys, deliberately, so between them the two can differ -
/// and a heading that named the picks would describe a listing that does not
/// exist yet. The listing carries its own filter, recorded when it arrived.
/// </para>
/// </remarks>
public class APaneThatNamesItsFilterTests
{
    private static BrowseOutcome OneItem() =>
        new BrowseOutcome.Listed(new WorkItemPage(
            [
                new WorkItemSummary(
                    "18515", "Oz asks guided questions", "Active", "", "2026-09-05T01:06:13Z",
                    AreaPath: @"Widgets\Platform", Iteration: @"Widgets\Sprint 42"),
            ],
            null));

    [Test]
    public async Task A_listing_remembers_what_it_was_fetched_with()
    {
        var state = Reducer.Browsed(
            new AppState(), "a-tracker", OneItem(), @"Widgets\Platform · Active");

        await Assert.That(state.Browse!.FilterSaid).IsEqualTo(@"Widgets\Platform · Active");
    }

    [Test]
    public async Task An_unfiltered_listing_remembers_no_filter()
    {
        var state = Reducer.Browsed(new AppState(), "a-tracker", OneItem(), said: null);

        await Assert.That(state.Browse!.FilterSaid).IsNull()
            .Because("null is 'nobody narrowed', and a pane that printed 'no filter' over "
                   + "every ordinary listing would be noise on the common case.");
    }

    [Test]
    public async Task The_pane_names_the_filter_the_rows_came_from()
    {
        var state = Reducer.Browsed(
            new AppState(), "a-tracker", OneItem(), @"Widgets\Platform · Active");

        var drawn = PaneText.Browse(state);

        await Assert.That(drawn).Contains(@"Widgets\Platform")
            .Because("two rows under a filter nobody can see is a person concluding their "
                   + "backlog is nearly done.");
    }

    [Test]
    public async Task The_pane_says_when_what_is_picked_is_not_what_is_showing()
    {
        // PICKING AND BROWSING ARE TWO KEYS, so between them the listing on
        // screen is older than the filter in hand. A heading that named the
        // picks would describe a listing nobody has fetched.
        var state = Reducer.Browsed(new AppState(), "a-tracker", OneItem(), said: null)
            with { ChosenAreaPath = @"Widgets\Platform" };

        var drawn = PaneText.Browse(state);

        await Assert.That(drawn).Contains("not been listed")
            .Because("a pane that quietly showed the old rows under the new filter's name "
                   + "would be the full-box version of the lie the endings prevent.");
    }

    [Test]
    public async Task The_filter_is_on_the_pane_a_person_is_actually_looking_at()
    {
        // FOUND IN A PTY. PaneText.Browse is the pane's SENTENCE, drawn only
        // when there are no rows - so a filter named there is named on the one
        // screen where the rows it narrowed are not. The title is what a person
        // reading a full table can see, and it already carries the tracker for
        // the same reason.
        var state = Reducer.Browsed(
            new AppState(), "a-tracker", OneItem(), @"Widgets\Platform · Active");

        var title = PaneText.BrowseTitle(state);

        await Assert.That(title).Contains("a-tracker");
        await Assert.That(title).Contains(@"Widgets\Platform");

        await Assert.That(PaneText.BrowseTitle(
            Reducer.Browsed(new AppState(), "a-tracker", OneItem(), said: null)))
            .IsEqualTo("Browse — a-tracker")
            .Because("an unfiltered listing is the ordinary case and a title that said so "
                   + "every time would be noise where the tracker's name belongs.");

        var screen = ConsoleSource.Text("Gg.Console", Path.Combine("Views", "ConsoleScreen.cs"));

        await Assert.That(screen).Contains("PaneText.BrowseTitle")
            .Because("a title composed in the view is one the model cannot be asked about, "
                   + "which is how the filter came to be drawn only on an empty pane.");
    }

    [Test]
    public async Task A_row_says_where_the_tracker_files_it()
    {
        var state = Reducer.Browsed(new AppState(), "a-tracker", OneItem(), said: null);

        await Assert.That(state.Browse!.Items[0].Where).IsEqualTo("Platform")
            .Because("the leaf, not the whole path: every row of a project shares the root, "
                   + "and a column that repeats it is a column of one word.");

        await Assert.That(Rows.BrowseColumns).Contains("where")
            .Because("a value that crosses the wire and is dropped at the console boundary is "
                   + "a field assembled and discarded, which is the shape this repository has "
                   + "found twice.");
    }
}
