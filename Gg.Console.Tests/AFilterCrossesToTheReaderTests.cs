using System.Text.Json;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Narrowing a listing, across the pipe, and what a reader that cannot says.
/// </summary>
/// <remarks>
/// <para>
/// <b>A FULL BOX CAN LIE AS EASILY AS AN EMPTY ONE.</b> The five browse endings
/// exist because a pane showing nothing has to say which nothing it is. The
/// same rule inverted is this: a pane showing fifty rows under the heading
/// "Widgets\Platform, Sprint 42" when the reader ignored both is a worse lie,
/// because it looks like an answer. So the console asks whether the reader
/// declared the arguments, and where it did not it says so rather than calling
/// and hoping.
/// </para>
/// <para>
/// <b>Asked from <c>tools/list</c>, not probed.</b> The schema is already on
/// the wire and already read once per conversation; calling a tool with an
/// argument it never declared to find out is a round trip whose failure mode is
/// an unfiltered list nobody can tell apart from a filtered one.
/// </para>
/// <para>
/// <b>Scripted streams, as the conversation's other tests are.</b> The protocol
/// is one request then one reply, so the whole exchange runs with no process
/// anywhere.
/// </para>
/// </remarks>
public class AFilterCrossesToTheReaderTests
{
    private static string Initialized(int id = 0) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"protocolVersion\":\"2024-11-05\","
      + "\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"tracker\",\"version\":\"1\"}}}";

    /// <summary>A browse tool that takes paging and nothing else.</summary>
    private static string DeclaresPagingOnly(int id) =>
        Declaring(id, ["cursor", "limit"]);

    /// <summary>A browse tool that takes everything this contract names.</summary>
    private static string DeclaresFiltering(int id) =>
        Declaring(id, ["cursor", "limit", .. BrowseTool.Filters.All]);

    private static string Declaring(int id, IReadOnlyList<string> arguments) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"tools\":["
      + "{\"name\":\"" + BrowseTool.Name + "\",\"inputSchema\":{\"type\":\"object\","
      + "\"properties\":{"
      + string.Join(',', arguments.Select(name => "\"" + name + "\":{\"type\":\"string\"}"))
      + "}}}]}}";

    private static string Answered(int id, string body) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"content\":[{\"type\":\"text\","
      + "\"text\":" + JsonSerializer.Serialize(body) + "}],\"isError\":false}}";

    private const string OnePage = """
        {"items":[{"id":"18515","title":"Oz asks guided questions","state":"Active",
          "url":"https://tracker.example/acme/_workitems/edit/18515",
          "updated":"2026-09-05T01:06:13Z",
          "areaPath":"Widgets\\Platform","iteration":"Widgets\\Sprint 42"}],
         "nextCursor":"1"}
        """;

    private static (ReaderConversation Asking, StringWriter Sent) Answering(params string[] replies)
    {
        var sent = new StringWriter();
        return (new ReaderConversation(
            new StringReader(string.Join('\n', replies) + "\n"), sent, "a-tracker"), sent);
    }

    private static string Called(StringWriter sent) =>
        sent.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.Contains("tools/call", StringComparison.Ordinal));

    [Test]
    public async Task A_filter_rides_the_call_as_the_contract_names_it()
    {
        var (asking, sent) = Answering(
            Initialized(), DeclaresFiltering(1), Answered(2, OnePage));

        await asking.BrowseAsync(
            cursor: null, limit: 50,
            filter: new WorkItemFilter(@"Widgets\Platform", @"Widgets\Sprint 42", ["Active"]));

        var arguments = JsonDocument.Parse(Called(sent))
            .RootElement.GetProperty("params").GetProperty("arguments");

        await Assert.That(arguments.GetProperty(BrowseTool.Filters.AreaPath).GetString())
            .IsEqualTo(@"Widgets\Platform");
        await Assert.That(arguments.GetProperty(BrowseTool.Filters.Iteration).GetString())
            .IsEqualTo(@"Widgets\Sprint 42");

        var states = arguments.GetProperty(BrowseTool.Filters.States);
        await Assert.That(states.ValueKind).IsEqualTo(JsonValueKind.Array)
            .Because("a person wants Active and Resolved together, so the argument is a set "
                   + "and not a string somebody has to agree on a separator for.");
        await Assert.That(states.EnumerateArray().First().GetString()).IsEqualTo("Active");
    }

    [Test]
    public async Task An_unnarrowed_browse_sends_no_filter_at_all()
    {
        var (asking, sent) = Answering(
            Initialized(), DeclaresFiltering(1), Answered(2, OnePage));

        await asking.BrowseAsync(cursor: null, limit: 50);

        // AN ABSENT ARGUMENT IS NOT AN EMPTY ONE. A reader handed areaPath:""
        // would reasonably narrow to items filed nowhere, which is nothing.
        await Assert.That(Called(sent)).DoesNotContain(BrowseTool.Filters.AreaPath);
    }

    [Test]
    public async Task A_reader_that_declares_no_filter_says_so_rather_than_listing_everything()
    {
        var (asking, sent) = Answering(
            Initialized(), DeclaresPagingOnly(1), Answered(2, OnePage));

        var outcome = await asking.BrowseAsync(
            cursor: null, limit: 50, filter: new WorkItemFilter(@"Widgets\Platform"));

        var refused = await Assert.That(outcome).IsTypeOf<BrowseOutcome.NotFilterable>();
        await Assert.That(refused!.Why).Contains("a-tracker");
        await Assert.That(refused.Why).Contains(BrowseTool.Filters.AreaPath);

        await Assert.That(sent.ToString()).DoesNotContain("tools/call")
            .Because("calling anyway would answer a full page of everything under the name of "
                   + "a filter, which is the one failure this check exists to prevent.");
    }

    [Test]
    public async Task A_reader_that_cannot_filter_can_still_be_browsed()
    {
        // NARROWER, NOT BROKEN. The reader anybody has deployed takes paging
        // and nothing else, and an unfiltered list is what browsing has always
        // been - refusing that too would turn one new affordance into a
        // regression for every reader that exists.
        var (asking, _) = Answering(
            Initialized(), DeclaresPagingOnly(1), Answered(2, OnePage));

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(outcome).IsTypeOf<BrowseOutcome.Listed>();
    }

    [Test]
    public async Task A_listed_item_carries_where_it_is_filed()
    {
        var (asking, _) = Answering(
            Initialized(), DeclaresFiltering(1), Answered(2, OnePage));

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        var listed = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Listed>();
        await Assert.That(listed!.Page.Items[0].AreaPath).IsEqualTo(@"Widgets\Platform")
            .Because("the column is how a person sees that the filter took.");
        await Assert.That(listed.Page.Items[0].Iteration).IsEqualTo(@"Widgets\Sprint 42");
    }
}
