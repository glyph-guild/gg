using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Asking a reader for a page of work, and telling apart the ways it can fail.
/// </summary>
/// <remarks>
/// <para>
/// <b>THREE SILENCES, AND A PANE HAS TO NAME WHICH ONE.</b> <c>ILiveSource</c>
/// already makes this argument for the live view - <i>"a pane that shows an
/// empty box for both is a pane that cannot tell a person which one they are
/// looking at"</i> - and browsing has more of them, not fewer. A reader that
/// declares no browse tool, a tracker with no work in it, and a server that
/// answered gibberish are three different things to do next, and exactly one
/// of them is "there is nothing for you".
/// </para>
/// <para>
/// <b>So this returns an outcome rather than a page.</b>
/// <see cref="IWorkItemSource"/> is what a reader IMPLEMENTS and it throws;
/// a console must render the failure, not propagate it, so the wrapper the
/// console asks through names every way the conversation can end.
/// </para>
/// <para>
/// <b>Driven over streams, deliberately.</b> The protocol is strictly one
/// request then one reply, so a scripted reader and a recording writer exercise
/// the whole conversation with no process anywhere - the mirror of
/// <c>PlatformToolServerTests</c>, which drives the server the same way from the
/// other side. Owning a child process is a separate concern and a separate
/// test.
/// </para>
/// </remarks>
public class BrowsingAReaderTests
{
    private static string Initialized(int id = 0) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"protocolVersion\":\"2024-11-05\","
      + "\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"tracker\",\"version\":\"1\"}}}";

    private static string Declares(int id, params string[] tools) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"tools\":["
      + string.Join(',', tools.Select(t => "{\"name\":\"" + t + "\"}"))
      + "]}}";

    private static string Answered(int id, string body) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"content\":[{\"type\":\"text\","
      + "\"text\":" + System.Text.Json.JsonSerializer.Serialize(body) + "}],\"isError\":false}}";

    private const string OnePage = """
        {"items":[{"id":"18515","title":"Oz asks guided questions","state":"Active",
          "url":"https://tracker.example/acme/_workitems/edit/18515","updated":"2026-09-05T01:06:13Z"}],
         "nextCursor":"1"}
        """;

    private static (ReaderConversation Asking, StringWriter Sent) Answering(params string[] replies)
    {
        var sent = new StringWriter();
        return (new ReaderConversation(
            new StringReader(string.Join('\n', replies) + "\n"), sent, "a-tracker"), sent);
    }

    [Test]
    public async Task A_browsable_reader_answers_with_a_page()
    {
        var (asking, _) = Answering(
            Initialized(), Declares(1, "get_work_item", BrowseTool.Name), Answered(2, OnePage));

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        var listed = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Listed>();
        await Assert.That(listed!.Page.Items).Count().IsEqualTo(1);
        await Assert.That(listed.Page.Items[0].Id).IsEqualTo("18515");
        await Assert.That(listed.Page.NextCursor).IsEqualTo("1");
    }

    [Test]
    public async Task It_initializes_before_it_asks_what_the_reader_has()
    {
        // A server that is asked for its tools before initialize is a server
        // within its rights to refuse, and the failure would read as a reader
        // with no browse tool.
        var (asking, sent) = Answering(
            Initialized(), Declares(1, BrowseTool.Name), Answered(2, OnePage));

        await asking.BrowseAsync(cursor: null, limit: 50);

        var lines = sent.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines[0]).Contains("\"initialize\"");
        await Assert.That(lines[1]).Contains("tools/list");
        await Assert.That(lines[2]).Contains("tools/call");
    }

    [Test]
    public async Task A_reader_that_reads_one_item_by_id_is_not_browsable()
    {
        // THE STATE THE ONLY DEPLOYED READER WAS IN until gg served one itself.
        // It is not an error and not an empty tracker: it is a reader that can
        // answer a different question.
        var (asking, _) = Answering(Initialized(), Declares(1, "get_work_item"));

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        var refused = await Assert.That(outcome).IsTypeOf<BrowseOutcome.NotBrowsable>();
        await Assert.That(refused!.Why).Contains(BrowseTool.Name)
            .Because("a person told a reader cannot be browsed needs to know what it lacks.");
        await Assert.That(refused.Why).Contains("a-tracker")
            .Because("a tenant may configure more than one reader.");
    }

    [Test]
    public async Task A_tracker_with_no_work_in_it_is_an_empty_page_and_not_a_refusal()
    {
        // THE THIRD SILENCE, and the only one that means "there is nothing for
        // you". Collapsing it into either of the others is the defect this
        // whole file exists for.
        var (asking, _) = Answering(
            Initialized(), Declares(1, BrowseTool.Name), Answered(2, """{"items":[]}"""));

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        var listed = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Listed>();
        await Assert.That(listed!.Page.Items).IsEmpty();
        await Assert.That(listed.Page.NextCursor).IsNull();
    }

    [Test]
    public async Task A_reader_that_narrates_is_a_protocol_failure_and_not_an_empty_tracker()
    {
        // STDOUT IS THE PROTOCOL, read from this side. The server's own remark
        // warns that one stray line makes it look like it never initialized;
        // this is what noticing that looks like, and it must not be reported as
        // a tracker with no work.
        var (asking, _) = Answering("starting up...", Initialized(), Declares(1, BrowseTool.Name));

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        var nonsense = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Unintelligible>();
        await Assert.That(nonsense!.Why).Contains("a-tracker");
    }

    [Test]
    public async Task A_reader_that_says_nothing_at_all_is_named_as_such()
    {
        // A child that died at startup writes nothing and closes. Distinct from
        // gibberish, because the thing to check is different: one is a broken
        // server, the other is a server that is not there.
        var (asking, _) = Answering();

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(outcome).IsTypeOf<BrowseOutcome.Silent>();
    }

    [Test]
    public async Task A_tool_that_answers_with_an_error_is_carried_through_in_its_own_words()
    {
        // The reader already says why it could not answer - an unreachable
        // tracker, an expired credential. Rewording that here would be a second
        // answer to one question.
        var (asking, _) = Answering(
            Initialized(),
            Declares(1, BrowseTool.Name),
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"content\":[{\"type\":\"text\","
          + "\"text\":\"nodename nor servname provided\"}],\"isError\":true}}");

        var outcome = await asking.BrowseAsync(cursor: null, limit: 50);

        var refused = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Refused>();
        await Assert.That(refused!.Why).Contains("nodename nor servname provided");
    }

    [Test]
    public async Task The_cursor_a_page_returned_is_what_the_next_call_sends()
    {
        var (asking, sent) = Answering(
            Initialized(), Declares(1, BrowseTool.Name), Answered(2, OnePage));

        await asking.BrowseAsync(cursor: "1", limit: 25);

        var call = sent.ToString().Split('\n').Single(l => l.Contains("tools/call"));
        await Assert.That(call).Contains("\"" + BrowseTool.Paging.Cursor + "\":\"1\"");
        await Assert.That(call).Contains("\"" + BrowseTool.Paging.Limit + "\":25");
    }

    [Test]
    public async Task It_asks_what_the_reader_has_once_and_not_once_per_page()
    {
        // A tools/list per keystroke is a round trip a person feels while
        // scrolling, and the answer cannot change inside one conversation.
        var (asking, sent) = Answering(
            Initialized(),
            Declares(1, BrowseTool.Name),
            Answered(2, OnePage),
            Answered(3, OnePage));

        await asking.BrowseAsync(cursor: null, limit: 50);
        await asking.BrowseAsync(cursor: "1", limit: 50);

        var lines = sent.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Count(l => l.Contains("tools/list"))).IsEqualTo(1);
        await Assert.That(lines.Count(l => l.Contains("initialize"))).IsEqualTo(1);
        await Assert.That(lines.Count(l => l.Contains("tools/call"))).IsEqualTo(2);
    }
}
