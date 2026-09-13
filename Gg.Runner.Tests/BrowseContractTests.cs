using Gg.Local;

namespace Gg.Runner.Tests;

/// <summary>
/// A reader says whether it can be browsed, and the contract names no forge.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written because the walk found the answer is currently no.</b> The only
/// intent reader configured anywhere declares one tool - read one work item by
/// id - and nothing else. So "a reader that does not declare it is not
/// browsable" is the ORDINARY case today, not an edge one, and the sentence a
/// person gets has to be actionable rather than merely correct.
/// </para>
/// <para>
/// <b>This is a contract a third party implements</b>, which is what makes it
/// different from <c>NominationTool</c>: that names the platform's own tool
/// because three things internal to this repository have to agree. Renaming
/// anything here breaks every reader somebody wrote.
/// </para>
/// </remarks>
public class BrowseContractTests
{
    [Test]
    public async Task A_reader_declaring_the_tool_is_browsable()
    {
        await Assert.That(BrowseTool.IsBrowsable(["get_work_item", BrowseTool.Name])).IsTrue();
    }

    [Test]
    public async Task The_reader_that_exists_today_is_not_browsable()
    {
        // THE MEASURED CASE. tracker-mcp.py declares get_work_item and returns
        // {"tools": [TOOL]} - one dict. This is what the walk found, asserted so
        // the contract is written against reality rather than against a reader
        // somebody hopes exists.
        await Assert.That(BrowseTool.IsBrowsable(["get_work_item"])).IsFalse()
            .Because("a reader that reads one item by id cannot list, and calling a tool that "
                   + "is not there to find out costs a launch and returns an error a person "
                   + "reads as 'the tracker is empty'.");
    }

    [Test]
    public async Task A_reader_that_listed_nothing_is_not_browsable_either()
    {
        await Assert.That(BrowseTool.IsBrowsable([])).IsFalse();
        await Assert.That(BrowseTool.IsBrowsable(null)).IsFalse()
            .Because("a tools/list that failed and a tools/list that was empty are both 'not "
                   + "browsable', and neither is 'no work in the tracker'.");
    }

    [Test]
    public async Task The_refusal_names_the_tool_that_is_missing()
    {
        var said = BrowseTool.NotBrowsable("a-tracker");

        await Assert.That(said).Contains("a-tracker");
        await Assert.That(said).Contains(BrowseTool.Name)
            .Because("the person reading this is usually the operator who installed the "
                   + "reader, and 'not browsable' without the missing name is a sentence they "
                   + "cannot act on.");
        await Assert.That(said).Contains("read one item")
            .Because("it also has to say what still works, or it reads as the reader being "
                   + "broken rather than narrower than this feature wants.");
    }

    [Test]
    public async Task The_listed_item_says_where_it_is_filed_and_still_carries_no_body()
    {
        // An issue's text is customer content that does not cross and is not
        // needed to CHOOSE one: a person picking work reads a title and a state.
        // Where it is FILED is neither - it is the thing a filter narrows on,
        // and a filter whose effect cannot be seen on the row is one nobody can
        // tell took from one that silently did not.
        await Assert.That(BrowseTool.Fields.All).IsEquivalentTo(
            (string[])["id", "title", "state", "url", "updated", "areaPath", "iteration"]);
        await Assert.That(BrowseTool.Fields.All).DoesNotContain("description");
        await Assert.That(BrowseTool.Fields.All).DoesNotContain("body")
            .Because("get_work_item is what reads a body, on the runner, after a flight "
                   + "exists - which is the boundary LeaseGranted.IntentUri states.");
    }

    [Test]
    public async Task A_filter_is_named_the_way_the_row_it_narrows_is()
    {
        // THE ARGUMENT AND THE FIELD ARE ONE SPELLING. A caller filtering on
        // areaPath and reading a column called area would have to be told they
        // are the same thing, and every third party implementing this reader
        // would have to be told twice.
        await Assert.That(BrowseTool.Filters.AreaPath).IsEqualTo(BrowseTool.Fields.AreaPath);
        await Assert.That(BrowseTool.Filters.Iteration).IsEqualTo(BrowseTool.Fields.Iteration);
        await Assert.That(BrowseTool.Filters.States).IsEqualTo("states")
            .Because("a state filter is a set - a person wants Active and Resolved together - "
                   + "so it is plural, and it is the one that does not match a column.");
    }

    [Test]
    public async Task A_reader_that_declares_no_filter_arguments_cannot_filter()
    {
        // ALL THREE OR NONE. A reader that took an area path and ignored the
        // sprint would answer a list that is narrower than everything and wider
        // than what was asked for, and nothing on screen could say which.
        await Assert.That(BrowseTool.CanFilter(["cursor", "limit"])).IsFalse();
        await Assert.That(BrowseTool.CanFilter(null)).IsFalse();
        await Assert.That(BrowseTool.CanFilter(
            ["cursor", "limit", "areaPath", "iteration"])).IsFalse()
            .Because("half a filter applied and half ignored is the answer a person cannot "
                   + "check, which is worse than a filter that plainly did not run.");

        await Assert.That(BrowseTool.CanFilter(
            ["cursor", "limit", .. BrowseTool.Filters.All])).IsTrue();
    }

    [Test]
    public async Task Telling_a_person_their_reader_cannot_filter_names_the_reader()
    {
        var said = BrowseTool.NotFilterable("a-tracker");

        await Assert.That(said).Contains("a-tracker");
        await Assert.That(said).Contains(BrowseTool.Filters.AreaPath)
            .Because("the person reading it is usually the operator who installed the reader, "
                   + "and the argument names are what they would add.");
        await Assert.That(said).Contains("without a filter")
            .Because("it has to say what still works: an unfiltered list is still a list, and "
                   + "silence here reads as a reader that is broken.");
    }

    [Test]
    public async Task Paging_is_a_cursor_the_caller_hands_back()
    {
        // A tracker's paging is opaque and its ordering is its business, so the
        // caller never computes an offset - which is also what stops a listing
        // renumbering itself while somebody reads it.
        await Assert.That(BrowseTool.Paging.Cursor).IsEqualTo("cursor");
        await Assert.That(BrowseTool.Paging.NextCursor).IsEqualTo("nextCursor");
        await Assert.That(BrowseTool.Paging.Items).IsEqualTo("items")
            .Because("one key holding the items keeps a reader's answer one object, which a "
                   + "hand-written client can read without a schema.");
    }
}
