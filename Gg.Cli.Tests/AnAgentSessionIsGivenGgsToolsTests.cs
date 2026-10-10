using Gg.Client;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// A session handed a delegated credential starts Claude with gg's manage and itinerary tool
/// servers, the reads granted and the acts left to ask (ADR-0039 Amendment 2).
/// </summary>
public class AnAgentSessionIsGivenGgsToolsTests
{
    private static AgentSessionStart Start(bool tools) =>
        new("/s/a1b2", 80, 24, "a1b2", Resume: false, new Dictionary<string, string>(), Tools: tools);

    [Test]
    public async Task With_a_credential_both_tool_servers_are_configured()
    {
        var argv = AgentSessionHost.ArgumentsFor(Start(tools: true), new Gg.Local.SelfInvocation("/usr/local/bin/gg", []));

        await Assert.That(argv.Take(2)).IsEquivalentTo(["--session-id", "a1b2"])
            .Because("the id comes first, as a local mux agent's does.");
        var config = argv[argv.ToList().IndexOf("--mcp-config") + 1];
        await Assert.That(config).Contains("\"gg-manage\"").And.Contains("\"gg-itinerary\"");
        await Assert.That(config).DoesNotContain("t0k3n").And.DoesNotContain(SessionStores.TokenVariable)
            .Because("an argument is visible to ps; the credential travels in the environment.");
        await Assert.That(argv).Contains("mcp__gg-manage__list_gates");
        await Assert.That(argv).DoesNotContain("mcp__gg-manage__fly")
            .Because("acts are declared, never granted, so the person is asked each time.");
    }

    [Test]
    public async Task Without_one_nothing_is_added()
    {
        await Assert.That(AgentSessionHost.ArgumentsFor(Start(tools: false), new Gg.Local.SelfInvocation("/usr/local/bin/gg", [])))
            .IsEquivalentTo(["--session-id", "a1b2"]);
    }

    [Test]
    public async Task The_runner_and_the_client_name_the_same_variable()
    {
        await Assert.That(AgentSessions.TokenVariable).IsEqualTo(SessionStores.TokenVariable);
    }
}
