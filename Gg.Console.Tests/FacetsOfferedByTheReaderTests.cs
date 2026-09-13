using System.Text.Json;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// Asking a reader what there is to filter by, and what it means when it cannot say.
/// </summary>
/// <remarks>
/// <para>
/// <b>Picked, not typed.</b> An area path is a tree with somebody's punctuation
/// in it and a sprint is whatever a team named a fortnight; a person who spells
/// either one wrong gets an empty list that looks exactly like a sprint with no
/// work in it. So the choices come from the tracker.
/// </para>
/// <para>
/// <b>A reader that cannot offer them is not a broken reader.</b> It is the one
/// everybody has deployed. The pane says the choices are unavailable and
/// browsing carries on unfiltered, which is what browsing has always been -
/// the same shape as <c>NotBrowsable</c>, one noun over.
/// </para>
/// </remarks>
public class FacetsOfferedByTheReaderTests
{
    private static string Initialized(int id = 0) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"protocolVersion\":\"2024-11-05\","
      + "\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"tracker\",\"version\":\"1\"}}}";

    private static string Declares(int id, params string[] tools) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"tools\":["
      + string.Join(',', tools.Select(tool => "{\"name\":\"" + tool + "\"}"))
      + "]}}";

    private static string Answered(int id, string body) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"result\":{\"content\":[{\"type\":\"text\","
      + "\"text\":" + JsonSerializer.Serialize(body) + "}],\"isError\":false}}";

    private const string Offered = """
        {"areaPaths":["Widgets","Widgets\\Platform"],
         "iterations":["Widgets\\Sprint 42"],
         "states":["New","Active"]}
        """;

    private static (ReaderConversation Asking, StringWriter Sent) Answering(params string[] replies)
    {
        var sent = new StringWriter();
        return (new ReaderConversation(
            new StringReader(string.Join('\n', replies) + "\n"), sent, "a-tracker"), sent);
    }

    [Test]
    public async Task A_reader_that_offers_facets_answers_with_three_lists()
    {
        var (asking, _) = Answering(
            Initialized(), Declares(1, BrowseTool.Name, FacetTool.Name), Answered(2, Offered));

        var outcome = await asking.FacetsAsync();

        var offered = await Assert.That(outcome).IsTypeOf<FacetOutcome.Offered>();
        await Assert.That(offered!.Facets.AreaPaths).Contains(@"Widgets\Platform");
        await Assert.That(offered.Facets.Iterations).Contains(@"Widgets\Sprint 42");
        await Assert.That(offered.Facets.States).Contains("Active");
    }

    [Test]
    public async Task It_asks_the_facet_tool_by_name()
    {
        var (asking, sent) = Answering(
            Initialized(), Declares(1, BrowseTool.Name, FacetTool.Name), Answered(2, Offered));

        await asking.FacetsAsync();

        await Assert.That(sent.ToString()).Contains(FacetTool.Name);
    }

    [Test]
    public async Task A_reader_that_declares_no_facet_tool_says_so()
    {
        var (asking, sent) = Answering(Initialized(), Declares(1, BrowseTool.Name));

        var outcome = await asking.FacetsAsync();

        var nothing = await Assert.That(outcome).IsTypeOf<FacetOutcome.Nothing>();
        await Assert.That(nothing!.Why).Contains("a-tracker");
        await Assert.That(nothing.Why).Contains(FacetTool.Name)
            .Because("the person reading it is usually the operator who installed the reader, "
                   + "and the tool name is what they would add.");

        await Assert.That(sent.ToString()).DoesNotContain("tools/call")
            .Because("calling a tool a reader never declared costs a round trip whose only "
                   + "possible answer is an error.");
    }

    [Test]
    public async Task A_reader_that_answers_something_else_is_not_an_empty_tracker()
    {
        // AN EMPTY LIST OF CHOICES AND A BROKEN ANSWER LOOK THE SAME ON SCREEN,
        // and one of them means go and read a log.
        var (asking, _) = Answering(
            Initialized(), Declares(1, BrowseTool.Name, FacetTool.Name),
            Answered(2, "not json at all"));

        var outcome = await asking.FacetsAsync();

        var nothing = await Assert.That(outcome).IsTypeOf<FacetOutcome.Nothing>();
        await Assert.That(nothing!.Why).Contains("a-tracker");
    }
}
