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
    public async Task The_listed_item_carries_five_fields_and_no_body()
    {
        // An issue's text is customer content that does not cross and is not
        // needed to CHOOSE one: a person picking work reads a title and a state.
        await Assert.That(BrowseTool.Fields.All).IsEquivalentTo(
            (string[])["id", "title", "state", "url", "updated"]);
        await Assert.That(BrowseTool.Fields.All).DoesNotContain("description");
        await Assert.That(BrowseTool.Fields.All).DoesNotContain("body")
            .Because("get_work_item is what reads a body, on the runner, after a flight "
                   + "exists - which is the boundary LeaseGranted.IntentUri states.");
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
