using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A sweep pages through its watch's query, and cannot write its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.0-01, measured: the tracker server could not serve a watch.</b> It
/// answered <c>get_work_item</c> and <c>list_work_items</c>, and the listing
/// narrows by area path, iteration and states - no query language, and no
/// revision on a row. A watch's filter is a query in the tracker's own
/// language, and its mapping keys a nomination on the item's version.
/// </para>
/// <para>
/// <b>The query is BOUND when the server starts, and the tool takes none.</b>
/// A watch's filter is reviewed - any change to it is a widening - so an agent
/// that could pass its own would be sweeping something nobody approved. The
/// runner starts the server with the watch's filter; the agent pages through
/// it with a cursor. A server started without one offers no such tool at all.
/// </para>
/// <para>
/// <b>Each row carries its revision</b>, because the nomination the executor
/// makes is keyed on it, and asking for it item by item would be a round trip
/// per row.
/// </para>
/// </remarks>
public class AWatchsQueryIsBoundTests
{
    private const string Bound =
        "SELECT [System.Id] FROM WorkItems WHERE [System.Tags] CONTAINS 'needs-review'";

    /// <summary>A source that remembers what it was asked to query.</summary>
    private sealed class QueryingSource : IWorkItemSource
    {
        internal List<(string Query, string? Cursor, int Limit)> Queried { get; } = [];

        public Task<WorkItem?> ReadAsync(string id, CancellationToken token) =>
            Task.FromResult<WorkItem?>(null);

        public Task<WorkItemFacets> FacetsAsync(CancellationToken token) =>
            Task.FromResult(WorkItemFacets.Nothing);

        public Task<WorkItemPage> BrowseAsync(
            string? cursor, int limit, WorkItemFilter? filter, CancellationToken token) =>
            Task.FromResult(new WorkItemPage([], null));

        public Task<IReadOnlyList<WorkItemChange>> HistoryAsync(
            string id, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<WorkItemChange>>([]);

        public Task<IReadOnlyList<WorkItemField>> FieldsAsync(
            string id, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<WorkItemField>>([]);

        public Task<WorkItemPage> QueryAsync(
            string query, string? cursor, int limit, CancellationToken token)
        {
            Queried.Add((query, cursor, limit));

            return Task.FromResult(new WorkItemPage(
                [new WorkItemSummary(
                    "4242", "The runner drops a lease", "Active",
                    "https://tracker.example/acme/_workitems/edit/4242",
                    "2026-09-05T01:06:13Z",
                    Revision: "7")],
                "1"));
        }
    }

    private static async Task<IReadOnlyList<JsonDocument>> ExchangeAsync(
        IWorkItemSource source, string? bound, params string[] lines)
    {
        var output = new StringWriter();
        await WorkItemToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, source, bound);

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private const string List =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}";

    private static string Call(string arguments) =>
        "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\""
      + QueryTool.Name + "\",\"arguments\":" + arguments + "}}";

    private static IEnumerable<JsonElement> Tools(JsonDocument document) =>
        document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray();

    [Test]
    public async Task A_server_started_without_a_query_offers_no_query_tool()
    {
        var documents = await ExchangeAsync(new QueryingSource(), bound: null, List);

        await Assert.That(Tools(documents[0]).Select(t => t.GetProperty("name").GetString()))
            .DoesNotContain(QueryTool.Name)
            .Because("a flight's reader has no watch, and a tool that could only answer 'there "
                   + "is no query' is one an agent would call to find that out.");
    }

    [Test]
    public async Task A_bound_server_offers_the_tool_and_it_takes_no_query()
    {
        var documents = await ExchangeAsync(new QueryingSource(), Bound, List);

        var tool = Tools(documents[0])
            .Single(t => t.GetProperty("name").GetString() == QueryTool.Name);
        var properties = tool.GetProperty("inputSchema").GetProperty("properties")
            .EnumerateObject().Select(p => p.Name).ToList();

        await Assert.That(properties).IsEquivalentTo(
            (string[])[BrowseTool.Paging.Cursor, BrowseTool.Paging.Limit])
            .Because("the watch's filter is reviewed, and a tool that took a query would let "
                   + "an agent sweep something nobody approved.");
    }

    [Test]
    public async Task Calling_it_pages_the_bound_query_and_each_row_carries_its_revision()
    {
        var source = new QueryingSource();

        var documents = await ExchangeAsync(
            source, Bound, Call("{\"cursor\":\"0\",\"limit\":25}"));

        await Assert.That(source.Queried.Single()).IsEqualTo((Bound, (string?)"0", 25))
            .Because("the query the source runs is the one the server was started with, "
                   + "verbatim.");

        var text = documents[0].RootElement.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString()!;
        using var answered = JsonDocument.Parse(text);

        var item = answered.RootElement.GetProperty(BrowseTool.Paging.Items)[0];

        await Assert.That(item.GetProperty(BrowseTool.Fields.Id).GetString()).IsEqualTo("4242");
        await Assert.That(item.GetProperty(QueryTool.Revision).GetString()).IsEqualTo("7")
            .Because("the executor keys its nomination on the version, and a row without it "
                   + "is a round trip per item to find out.");
        await Assert.That(answered.RootElement.GetProperty(BrowseTool.Paging.NextCursor)
            .GetString()).IsEqualTo("1");
    }

    [Test]
    [Arguments("{\"query\":\"SELECT [System.Id] FROM WorkItems\"}")]
    [Arguments("{\"filter\":\"SELECT [System.Id] FROM WorkItems\"}")]
    public async Task A_query_the_agent_supplies_is_refused_and_nothing_is_asked(string arguments)
    {
        var source = new QueryingSource();

        var documents = await ExchangeAsync(source, Bound, Call(arguments));

        await Assert.That(source.Queried).IsEmpty()
            .Because("an argument ignored silently would teach an agent that its query ran.");

        var result = documents[0].RootElement;
        var refused = result.TryGetProperty("error", out _)
                      || (result.GetProperty("result").TryGetProperty("isError", out var flag)
                          && flag.GetBoolean());

        await Assert.That(refused).IsTrue();
    }

    [Test]
    public async Task An_unbound_server_refuses_the_call()
    {
        var source = new QueryingSource();

        var documents = await ExchangeAsync(source, bound: null, Call("{}"));

        await Assert.That(documents[0].RootElement.TryGetProperty("error", out _)).IsTrue();
        await Assert.That(source.Queried).IsEmpty();
    }

    [Test]
    public async Task The_runner_starts_a_reader_with_a_query()
    {
        var parsed = CliArgs.Parse(
            ["runner", "read", "--provider", "ado", "--host", "https://tracker.example/acme",
             "--query", Bound]);

        await Assert.That(parsed).IsTypeOf<CliAction.RunnerRead>();
        await Assert.That(((CliAction.RunnerRead)parsed).Query).IsEqualTo(Bound);

        var unbound = (CliAction.RunnerRead)CliArgs.Parse(
            ["runner", "read", "--provider", "ado", "--host", "https://tracker.example/acme"]);

        await Assert.That(unbound.Query).IsNull()
            .Because("a flight's reader is started as it always was, with no query.");
    }
}
