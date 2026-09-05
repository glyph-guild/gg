using System.Text.Json;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The reader this binary serves: one work item by id, and a page of them to
/// browse.
/// </summary>
/// <remarks>
/// <para>
/// <b>A SECOND SERVER, and the separation is the point.</b>
/// <c>PlatformToolServer</c> earns its safety by holding nothing: <i>"no
/// credential, no session, no round trip - it validates two strings and returns
/// a receipt. That is what makes it safe to run as a child of a process the
/// threat model treats as compromised."</i> A reader holds a credential and
/// speaks to a tracker, so putting it on that channel would spend exactly the
/// property that paragraph is claiming. Two servers, two justifications.
/// </para>
/// <para>
/// <b>Why this stops being somebody's script.</b> The reader a deployment runs
/// today declares <c>get_work_item</c> and nothing else, which is the entire
/// reason <c>A_reader_that_reads_one_item_by_id_is_not_browsable</c> exists:
/// the console cannot offer a list of work to pick from, because no reader
/// serves one. A reader this repository owns serves both, so browsing stops
/// waiting on somebody writing a second script on a second host.
/// </para>
/// <para>
/// <b>It names no forge, on the same terms as everything else here.</b> The
/// SHAPE is in the source - a path, a query, a field table - and the host is
/// configuration, exactly the split <c>PathScopedGitVcsAdapter</c> already
/// makes when it keeps its own path segment and takes its host in a
/// constructor.
/// </para>
/// <para>
/// <b>STDOUT IS THE PROTOCOL</b> here as much as on the other server, so this
/// file parses every line the server wrote rather than the lines it hoped for.
/// </para>
/// </remarks>
public class WorkItemToolServerTests
{
    /// <summary>What the tracker answered, without a tracker to ask.</summary>
    /// <remarks>
    /// A server that could only be tested against a live tracker would be a
    /// server nobody tests. The source is the seam; the protocol above it and
    /// the http below it are then separately checkable.
    /// </remarks>
    private sealed class StubSource : IWorkItemSource
    {
        public Task<WorkItem?> ReadAsync(string id, CancellationToken token) =>
            Task.FromResult<WorkItem?>(null);

        public Task<WorkItemPage> BrowseAsync(string? cursor, int limit, CancellationToken token) =>
            Task.FromResult(new WorkItemPage([], null));
    }

    private static async Task<IReadOnlyList<JsonDocument>> ExchangeAsync(params string[] lines)
    {
        var output = new StringWriter();
        await WorkItemToolServer.RunAsync(
            new StringReader(string.Join('\n', lines)), output, new StubSource());

        return output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();
    }

    private static string Initialize(int id = 0) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"initialize\",\"params\":"
      + "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},"
      + "\"clientInfo\":{\"name\":\"probe\",\"version\":\"1\"}}}";

    private static string List(int id) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/list\"}";

    private static IReadOnlyList<string> ToolNames(JsonDocument document) =>
        [.. document.RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)];

    [Test]
    public async Task It_declares_reading_one_item_and_browsing_many()
    {
        // THE GAP THIS CLOSES. Declaring only get_work_item is what makes a
        // reader unbrowsable, and BrowseTool.IsBrowsable is the check that says
        // so. A reader we own has no excuse to fail it.
        var documents = await ExchangeAsync(Initialize(), List(1));
        var names = ToolNames(documents[1]);

        await Assert.That(names).Contains("get_work_item")
            .Because("the reading a flight already depends on must not regress to add browsing.");
        await Assert.That(names).Contains(BrowseTool.Name)
            .Because("a reader this repository owns is the one reader that can be held to the "
                   + "browse contract, rather than asked politely to implement it.");
        await Assert.That(BrowseTool.IsBrowsable(names)).IsTrue()
            .Because("the contract's own predicate is what the console will ask, so it is what "
                   + "this must satisfy - not a list that merely looks right here.");
    }

    [Test]
    public async Task The_browse_tool_it_declares_takes_the_paging_the_contract_names()
    {
        // A tool that declares the right NAME and the wrong arguments is a
        // reader the console cannot page, and nothing would notice until a
        // second page was asked for.
        var documents = await ExchangeAsync(Initialize(), List(1));

        var browse = documents[1].RootElement.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == BrowseTool.Name);
        var properties = browse.GetProperty("inputSchema").GetProperty("properties");

        await Assert.That(properties.TryGetProperty(BrowseTool.Paging.Cursor, out _)).IsTrue();
        await Assert.That(properties.TryGetProperty(BrowseTool.Paging.Limit, out _)).IsTrue()
            .Because("the contract names both halves of paging; declaring one is a reader that "
                   + "pages once.");
    }

    [Test]
    public async Task Nothing_it_writes_is_unparseable_even_when_the_tracker_had_nothing()
    {
        // THE ANCHOR, and the failure it guards is silent: one narrated line
        // and the agent sees a server that never initialized rather than a tool
        // that found nothing. The stub answers empty on purpose - the barest
        // path is the one most likely to log an apology.
        var documents = await ExchangeAsync(
            Initialize(),
            List(1),
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":"
          + "{\"name\":\"get_work_item\",\"arguments\":{\"id\":\"26\"}}}");

        await Assert.That(documents).HasCount().EqualTo(3)
            .Because("three requests, three replies, and every line parsed to get here.");
    }
}
