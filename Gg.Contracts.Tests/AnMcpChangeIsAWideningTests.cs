using Gg.Contracts.Authoring;
using static Gg.Contracts.Tests.McpFixtures;

namespace Gg.Contracts.Tests;

/// <summary>
/// S72.3-01: what an agent may reach grows when a server is defined or changed,
/// or when a loop names one or a tool more (ADR-0040 Decision 5).
/// </summary>
/// <remarks>
/// gg cannot tell whether a changed definition reaches more or less, so a change
/// is a widening: where no order exists, "cannot be shown to tighten" is the
/// answer.
/// </remarks>
public class AnMcpChangeIsAWideningTests
{
    [Test]
    public async Task Defining_a_server_is_a_widening()
    {
        var widening = EnvelopeDirection.Widening(Root(servers: null), Root([Hosted()]));

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).Contains("mcp-servers");
    }

    [Test]
    public async Task Changing_a_definition_is_a_widening()
    {
        var moved = Hosted() with { Url = "https://api.sonarqube.us/mcp" };

        await Assert.That(EnvelopeDirection.Widening(Root([Hosted()]), Root([moved]))).IsNotNull();
    }

    [Test]
    public async Task Removing_a_server_is_not_a_widening()
    {
        await Assert.That(EnvelopeDirection.Widening(Root([Hosted(), Local()]), Root([Hosted()]))).IsNull();
    }

    [Test]
    public async Task Naming_a_server_on_a_loop_is_a_widening()
    {
        var widening = EnvelopeDirection.Widening(Kind(mcp: null), Kind(Uses("sonarqube")));

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).Contains("mcp");
    }

    [Test]
    public async Task Allowing_another_tool_or_every_tool_is_a_widening()
    {
        var one = Kind(Uses("sonarqube", "show_rule"));

        await Assert.That(EnvelopeDirection.Widening(one, Kind(Uses("sonarqube", "show_rule", "get_raw_source"))))
            .IsNotNull();
        await Assert.That(EnvelopeDirection.Widening(one, Kind(Uses("sonarqube")))).IsNotNull()
            .Because("'*' allows every tool the server has, now and after it grows.");
    }

    [Test]
    public async Task Allowing_fewer_tools_is_not_a_widening()
    {
        await Assert.That(EnvelopeDirection.Widening(
                Kind(Uses("sonarqube", "show_rule", "get_raw_source")), Kind(Uses("sonarqube", "show_rule"))))
            .IsNull();
        await Assert.That(EnvelopeDirection.Widening(Kind(Uses("sonarqube")), Kind(mcp: null))).IsNull();
    }
}
