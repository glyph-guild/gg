using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// <b>S63.5-01</b> - <c>gg itinerary tools</c> is in the usage text, and
/// <c>--print-registration</c> prints the <c>claude mcp add</c> line that reaches it.
/// </summary>
/// <remarks>
/// <b>gg prints the line and never writes another tool's config</b> (slice sixty-three, out of
/// scope). The person runs it in their own Claude Code, which is the whole of "reachable from any
/// session" (ADR-0038 Decision 7).
/// </remarks>
public class TheItineraryToolServerIsDiscoverableTests
{
    [Test]
    public async Task The_usage_names_the_server_and_how_to_register_it()
    {
        var usage = ((CliAction.Unknown)CliArgs.Parse(["nonsense"])).Message;

        await Assert.That(usage).Contains("gg itinerary tools");
        await Assert.That(usage).Contains("--print-registration");
    }

    [Test]
    [Arguments(new[] { "itinerary", "tools", "--print-registration" }, "draft")]
    [Arguments(new[] { "itinerary", "tools", "--draft", "icons", "--print-registration" }, "icons")]
    public async Task The_flag_asks_for_the_line_rather_than_the_server(string[] line, string draft)
    {
        var action = CliArgs.Parse(line);

        var registration = await Assert.That(action).IsTypeOf<CliAction.ItineraryRegistration>();
        await Assert.That(registration!.Draft).IsEqualTo(draft);
    }

    [Test]
    public async Task With_gg_on_the_path_the_line_names_gg_so_an_update_does_not_strand_it()
    {
        var line = ItineraryToolServer.Registration("draft", ggOnPath: "/usr/local/bin/gg", self: null);

        await Assert.That(line).IsEqualTo("claude mcp add gg-itinerary -- gg itinerary tools");
    }

    [Test]
    public async Task A_named_draft_rides_on_the_line()
    {
        var line = ItineraryToolServer.Registration("icons", ggOnPath: "/usr/local/bin/gg", self: null);

        await Assert.That(line).IsEqualTo(
            "claude mcp add gg-itinerary-icons -- gg itinerary tools --draft icons")
            .Because("two drafts are two registrations, and two under one key would replace each other.");
    }

    [Test]
    public async Task Without_gg_on_the_path_the_line_names_this_binary()
    {
        var self = new SelfInvocation("/opt/gg/gg", ["runner", "tools"]);

        var line = ItineraryToolServer.Registration("draft", ggOnPath: null, self);

        await Assert.That(line).IsEqualTo("claude mcp add gg-itinerary -- /opt/gg/gg itinerary tools");
    }
}
