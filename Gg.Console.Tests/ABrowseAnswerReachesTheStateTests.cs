using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// What the reader answered becomes what the pane draws.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes is dead surface.</b> A field the pane reads and no
/// production path assigns is a feature that cannot happen, and it is
/// indistinguishable from one that works until somebody tries it. Slice 28 is
/// landing a ratchet that fails the build on exactly this shape.
/// </para>
/// <para>
/// <b>A static method, not a <c>Command</c>.</b> <c>StreamArrived</c> and
/// <c>Arrived</c> are already how data that came from outside enters the model
/// - a keystroke did not cause it, so it is not a command. A browse answer is
/// the same kind of thing.
/// </para>
/// <para>
/// <b>Every outcome maps, and the mapping is where five endings become four
/// sentences plus a list.</b> This is the one place the flattening happens, so
/// it is the one place to look when a pane says the wrong thing.
/// </para>
/// </remarks>
public class ABrowseAnswerReachesTheStateTests
{
    private static WorkItemPage APage(string? next = null) => new(
        [new WorkItemSummary("18398", "A draft job fails to load", "New",
            "https://tracker.example/acme/_workitems/edit/18398", "2026-09-05T01:06:13Z")],
        next);

    [Test]
    public async Task A_listing_becomes_rows_the_pane_can_draw()
    {
        var state = Reducer.Browsed(
            new AppState(), "a-tracker", new BrowseOutcome.Listed(APage(next: "1")));

        await Assert.That(state.Browse).IsNotNull();
        await Assert.That(state.Browse!.ProviderKey).IsEqualTo("a-tracker");
        await Assert.That(state.Browse.Items).Count().IsEqualTo(1);
        await Assert.That(state.Browse.Items[0].Id).IsEqualTo("18398");
        await Assert.That(state.Browse.Items[0].Title).IsEqualTo("A draft job fails to load");
        await Assert.That(state.Browse.NextCursor).IsEqualTo("1");
        await Assert.That(state.Browse.Absence).IsNull();
    }

    [Test]
    public async Task The_url_does_not_cross_into_the_state()
    {
        // A flight is opened from a provider and an id, never parsed out of a
        // url - FlightIntent.Id's own rule - so carrying one would be a
        // customer string in the dump that no reader of the screen wants.
        var state = Reducer.Browsed(
            new AppState(), "a-tracker", new BrowseOutcome.Listed(APage()));

        var written = System.Text.Json.JsonSerializer.Serialize(
            state, AppStateJsonContext.Default.AppState);

        await Assert.That(written).DoesNotContain("_workitems/edit");
    }

    [Test]
    [Arguments("NotBrowsable")]
    [Arguments("Refused")]
    [Arguments("Unintelligible")]
    [Arguments("Silent")]
    public async Task Every_way_of_answering_nothing_becomes_a_sentence(string kind)
    {
        // A WALK RATHER THAN FOUR ASSERTIONS, because four assertions pass on
        // the day the list was last complete. A sixth outcome added without a
        // mapping would land here as a null the pane renders as "no tracker
        // configured" - the most misleading sentence available.
        BrowseOutcome outcome = kind switch
        {
            "NotBrowsable" => new BrowseOutcome.NotBrowsable("it declares no list_work_items"),
            "Refused" => new BrowseOutcome.Refused("the tracker refused the credential"),
            "Unintelligible" => new BrowseOutcome.Unintelligible("it wrote a line that is not JSON-RPC"),
            _ => new BrowseOutcome.Silent("it said nothing at all"),
        };

        var state = Reducer.Browsed(new AppState(), "a-tracker", outcome);

        await Assert.That(state.Browse).IsNotNull()
            .Because("null means no reader was ever asked, and one was.");
        await Assert.That(state.Browse!.Absence).IsNotNull();
        await Assert.That(state.Browse.Items).IsEmpty();
        await Assert.That(PaneText.Browse(state)).IsNotEqualTo(
            PaneText.Browse(new AppState()))
            .Because("every one of these must read differently from 'nothing is configured'.");
    }

    [Test]
    public async Task An_empty_answer_is_a_listing_and_not_an_absence()
    {
        // THE DISTINCTION THE WHOLE PANE RESTS ON. A tracker that answered with
        // no work has answered. Recording that as an absence would make it read
        // as a reader that failed.
        var state = Reducer.Browsed(
            new AppState(), "a-tracker", new BrowseOutcome.Listed(new WorkItemPage([], null)));

        await Assert.That(state.Browse!.Absence).IsNull();
        await Assert.That(state.Browse.Items).IsEmpty();
        await Assert.That(PaneText.Browse(state)).Contains("no work");
    }

    [Test]
    public async Task A_second_answer_replaces_the_first_rather_than_appending()
    {
        // A page is a page, not a log. Appending would grow the state without
        // bound while a person paged, and show them a list they have already
        // scrolled past.
        var first = Reducer.Browsed(
            new AppState(), "a-tracker", new BrowseOutcome.Listed(APage(next: "1")));
        var second = Reducer.Browsed(
            first, "a-tracker", new BrowseOutcome.Listed(new WorkItemPage([], null)));

        await Assert.That(second.Browse!.Items).IsEmpty();
        await Assert.That(second.Browse.NextCursor).IsNull();
    }

    [Test]
    public async Task Nothing_else_in_the_state_moves()
    {
        // A reducer that quietly reset the selection or the focused pane while
        // a person was reading something else is a console that loses their
        // place because a background fetch returned.
        var before = new AppState { SelectedRow = 3, FocusedPane = PaneId.Flight };

        var after = Reducer.Browsed(
            before, "a-tracker", new BrowseOutcome.Listed(APage()));

        await Assert.That(after.SelectedRow).IsEqualTo(3);
        await Assert.That(after.FocusedPane).IsEqualTo(PaneId.Flight);
    }
}
