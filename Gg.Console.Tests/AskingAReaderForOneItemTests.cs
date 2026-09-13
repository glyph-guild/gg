using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Asking a reader about one work item, and telling apart the ways that ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tool is already there and nothing ever called it.</b>
/// <c>get_work_item</c> has been declared and dispatched by the tool server
/// since it shipped, for an agent that needs to know what a flight is about.
/// The console only ever called <c>list_work_items</c>, so a person could pick
/// a row and never see what it said.
/// </para>
/// <para>
/// <b>The reader's own words, not a shape this end reassembles.</b> The server
/// renders an item as text - type, state, title, description, acceptance
/// criteria, tags, in the order a person reads them - and that rendering is a
/// decision somebody made once, in the place that has the item. Parsing it back
/// into fields here would be a second opinion about the same bytes, and the
/// first thing to drift.
/// </para>
/// <para>
/// <b>Two endings, where browsing has five, and the difference is what a person
/// does next.</b> A browse pane distinguishes "no tracker", "no browse tool",
/// "nothing in the backlog", "refused" and "not this protocol" because each is
/// a different thing to go and do. A detail that could not be read has one:
/// read the sentence. So it is carried whole rather than sorted.
/// </para>
/// </remarks>
public class AskingAReaderForOneItemTests
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

    private static string Refused(int id, string why) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"content\":[{\"type\":\"text\","
      + "\"text\":" + System.Text.Json.JsonSerializer.Serialize(why) + "}],\"isError\":true}}";

    private const string OneItem = """
        Type: Product Backlog Item
        State: Active
        Title: Oz asks guided questions
        Description: The wizard should ask rather than assume.
        """;

    private static (ReaderConversation Asking, StringWriter Sent) Answering(params string[] replies)
    {
        var sent = new StringWriter();
        return (new ReaderConversation(
            new StringReader(string.Join('\n', replies) + "\n"), sent, "a-tracker"), sent);
    }

    [Test]
    public async Task A_reader_that_reads_one_item_answers_with_its_own_words()
    {
        var (asking, sent) = Answering(
            Initialized(), Declares(1, ItemTool.Name, BrowseTool.Name), Answered(2, OneItem));

        var outcome = await asking.ReadAsync("18515");

        var read = await Assert.That(outcome).IsTypeOf<ItemOutcome.Read>();

        await Assert.That(read!.Said).Contains("Oz asks guided questions");
        await Assert.That(read.Said).Contains("The wizard should ask rather than assume.")
            .Because("the body is why somebody pressed enter: a title and a state were "
                   + "already on the row they pressed it from.");

        await Assert.That(sent.ToString()).Contains(ItemTool.Name)
            .Because("and it asks for it by the name the reader declared.");
    }

    [Test]
    public async Task The_id_it_was_given_is_the_id_it_asks_about()
    {
        var (asking, sent) = Answering(
            Initialized(), Declares(1, ItemTool.Name), Answered(2, OneItem));

        _ = await asking.ReadAsync("18515");

        await Assert.That(sent.ToString()).Contains("18515")
            .Because("a detail for the wrong row is worse than no detail, and the row is the "
                   + "only thing the person chose.");
    }

    [Test]
    public async Task A_reader_that_does_not_read_one_item_says_so_by_name()
    {
        var (asking, _) = Answering(Initialized(), Declares(1, BrowseTool.Name));

        var outcome = await asking.ReadAsync("18515");

        var nothing = await Assert.That(outcome).IsTypeOf<ItemOutcome.Nothing>();

        await Assert.That(nothing!.Why).Contains(ItemTool.Name)
            .Because("naming the tool is what turns `it did not work' into something the "
                   + "person who wrote the reader can act on.");
    }

    [Test]
    public async Task A_refusal_is_carried_in_the_readers_own_words()
    {
        var (asking, _) = Answering(
            Initialized(), Declares(1, ItemTool.Name),
            Refused(2, "the credential expired on Tuesday"));

        var outcome = await asking.ReadAsync("18515");

        var nothing = await Assert.That(outcome).IsTypeOf<ItemOutcome.Nothing>();

        await Assert.That(nothing!.Why).Contains("the credential expired on Tuesday")
            .Because("it already said why, and rewording it here would be a second answer to "
                   + "one question.");
    }

    [Test]
    public async Task A_reader_that_says_nothing_at_all_is_not_an_empty_item()
    {
        var (asking, _) = Answering();

        var outcome = await asking.ReadAsync("18515");

        var nothing = await Assert.That(outcome).IsTypeOf<ItemOutcome.Nothing>();

        await Assert.That(nothing!.Why).IsNotEmpty()
            .Because("a child that died at startup writes nothing and closes, and a modal "
                   + "that drew an empty box for that would send somebody to the tracker "
                   + "rather than to a log.");
    }
}
