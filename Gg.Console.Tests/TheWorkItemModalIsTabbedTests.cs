using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The work item modal is tabbed, and its history reads the way the flight
/// log does: the table above, what the change actually says below.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three regions in one view, each paying for the others.</b> What the
/// tracker said is prose somebody wrote and is capped at 45% of the modal so
/// the fields and the history fit under it; the history then takes whatever is
/// left. A long description and a long history are the two cases somebody
/// opens this modal for, and neither is readable when the other is present.
/// That is the arrangement the flight modal had before the log got a tab.
/// </para>
/// <para>
/// <b>So the same split, one modal over.</b> Details is what the item SAYS and
/// what it IS — the prose and the scalars, each with room. History is what has
/// happened to it, and gets the log's layout because it has the log's problem:
/// <c>WorkItemChangeRow.What</c> is a sentence a tracker wrote and a cell
/// truncates it.
/// </para>
/// <para>
/// <b>The keys are the ones a person already knows.</b> <c>v</c> cycles the
/// flight modal's tabs, so it cycles this one's; a second word for one act is
/// a second thing to learn. Three absences stay three absences — a reader that
/// could not be asked, a tracker with nothing to report, and a history with
/// rows are different facts and the sentence that tells them apart is kept.
/// </para>
/// </remarks>
public class TheWorkItemModalIsTabbedTests
{
    private static AppState Reading(int change = 0) => new()
    {
        ActiveTab = TabId.Browse,
        Mode = UiMode.WorkItemDetail,
        BrowseSelected = 0,
        WorkItemSelected = change,
        Browse = new BrowseListing
        {
            ProviderKey = "a-tracker",
            Items =
            [
                new BrowseRow
                {
                    Id = "18515",
                    Title = "Oz asks guided questions",
                    State = "Active",
                    Updated = "2026-09-05T01:06:13Z",
                    Url = "https://tracker.example/acme/_workitems/edit/18515",
                    Where = "Platform",
                    Sprint = @"Widgets\Sprint 42",
                },
            ],
        },
        WorkItemSaid = "## Oz asks guided questions\n\nA body somebody wrote.",
        WorkItemChanges =
        [
            new WorkItemChangeRow
            {
                When = "2026-09-05 01:06",
                Who = "Kevin",
                What = "State: New -> Active, and the reason was that the spike answered the "
                     + "question it was opened for.",
            },
            new WorkItemChangeRow { When = "2026-09-04 11:20", Who = "Sam", What = "Title changed" },
        ],
    };

    [Test]
    public async Task The_modal_has_a_tab_for_what_it_says_and_one_for_what_happened()
    {
        await Assert.That(Enum.GetValues<WorkItemTab>()).Contains(WorkItemTab.Details);
        await Assert.That(Enum.GetValues<WorkItemTab>()).Contains(WorkItemTab.History);
    }

    [Test]
    public async Task The_same_key_cycles_them_as_cycles_a_flights()
    {
        // `v` is already the console's word for "show me the other half" in the
        // modal next door. A different key here would be a second word for one
        // act, learned twice.
        var details = Reading();

        await Assert.That(Reducer.Reduce(details, Command.NextWorkItemTab).WorkItemTab)
            .IsEqualTo(WorkItemTab.History);

        var history = details with { WorkItemTab = WorkItemTab.History };

        await Assert.That(Reducer.Reduce(history, Command.NextWorkItemTab).WorkItemTab)
            .IsEqualTo(WorkItemTab.Details)
            .Because("a person who overshoots with no way back is a person stuck in a modal.");
    }

    [Test]
    public async Task What_the_change_under_the_cursor_says_is_read_in_full()
    {
        // THE LOG'S PROBLEM, ONE MODAL OVER. `What` is a sentence a tracker
        // wrote; a cell shows as much of it as the column is wide.
        await Assert.That(WorkItemDetails.ChangeDetail(Reading(change: 0)))
            .Contains("the spike answered the question it was opened for")
            .Because("the end of the sentence is exactly what the table cannot show, and it is "
                   + "why there is a pane.");
    }

    [Test]
    public async Task Moving_the_cursor_changes_what_the_pane_says()
    {
        await Assert.That(WorkItemDetails.ChangeDetail(Reading(change: 1)))
            .Contains("Title changed");
        await Assert.That(WorkItemDetails.ChangeDetail(Reading(change: 1)))
            .DoesNotContain("the spike answered")
            .Because("the pane is about the row under the cursor and no other.");
    }

    [Test]
    public async Task A_history_nobody_could_fetch_says_so_rather_than_going_blank()
    {
        // The three-absence rule this modal already keeps: a reader that could
        // not be asked, a tracker with nothing to report, and a history with
        // rows are different facts. A blank pane claims the second when it may
        // be the first.
        var none = Reading() with { WorkItemChanges = [], WorkItemHistorySaid = "the reader refused" };

        await Assert.That(WorkItemDetails.ChangeDetail(none)).Contains("the reader refused");
    }

    [Test]
    public async Task The_prose_stops_paying_for_the_regions_under_it()
    {
        // STRUCTURAL, BECAUSE IT IS A LAYOUT. What the tracker said was capped
        // at 45% of the modal to leave room for the fields and the history
        // below it - and with those on another tab there is nothing to leave
        // room for. A test over the model could not see this; the cap is a
        // number in the view.
        var screen = System.IO.File.ReadAllText(
            System.IO.Path.Combine(Root(), "Gg.Console", "Views", "ConsoleScreen.cs"));

        var said = screen[screen.IndexOf("_itemSaidPane = new FrameView", StringComparison.Ordinal)..];

        await Assert.That(said[..said.IndexOf('}', StringComparison.Ordinal)])
            .DoesNotContain("Dim.Percent(45)")
            .Because("the prose shares its tab with the fields now and the history has its own, "
                   + "so a cap that existed to make room for a region that left is a cap on "
                   + "nothing.");
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }
}
