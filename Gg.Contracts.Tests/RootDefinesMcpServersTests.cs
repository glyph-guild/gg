using Gg.Contracts.Authoring;
using static Gg.Contracts.Tests.McpFixtures;

namespace Gg.Contracts.Tests;

/// <summary>
/// S72.1-01: root defines the external MCP servers loops may use, in the agent's
/// own shape (ADR-0040 Decision 1, Amendment 1).
/// </summary>
/// <remarks>
/// <b>Read as written and rendered back unchanged</b>, because the runner passes
/// the definition through without knowing what it is. A field the reader dropped
/// would be a server that starts without its read-only switch.
/// </remarks>
public class RootDefinesMcpServersTests
{
    [Test]
    public async Task A_remote_server_is_rendered_and_read_back_unchanged()
    {
        var written = EnvelopeText.Render(Root([Hosted()]));
        var read = EnvelopeYaml.Parse(written);

        await Assert.That(read.Diagnosis).IsNull()
            .Because("root may define servers. Diagnosis: " + read.Diagnosis);
        await Assert.That(written).Contains("mcp-servers:");

        var server = read.Envelope!.McpServers!.Single();
        await Assert.That(server.Key).IsEqualTo("sonarqube");
        await Assert.That(server.Type).IsEqualTo(McpTransports.Http);
        await Assert.That(server.Url).IsEqualTo("https://api.sonarcloud.io/mcp");
        await Assert.That(server.Headers!.Select(h => $"{h.Name}={h.Value}"))
            .IsEquivalentTo(Hosted().Headers!.Select(h => $"{h.Name}={h.Value}"));
        await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written)
            .Because("a rendering that changes on re-reading reports a change nobody made.");
    }

    [Test]
    public async Task A_local_server_is_rendered_and_read_back_unchanged()
    {
        var written = EnvelopeText.Render(Root([Local()]));
        var read = EnvelopeYaml.Parse(written);

        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);

        var server = read.Envelope!.McpServers!.Single();
        await Assert.That(server.Command).IsEqualTo("dnx");
        await Assert.That(server.Args).IsEquivalentTo(["mcp-sonarqube@1.1.1", "--yes"]);
        await Assert.That(server.Env!.Select(e => e.Name))
            .IsEquivalentTo(["SONARQUBE_TOKEN", "SONARQUBE_ORG", "SONARQUBE_MCP_READ_ONLY"]);
        await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written);
    }

    [Test]
    public async Task A_document_that_defines_no_server_gains_no_section()
    {
        var written = EnvelopeText.Render(Root(servers: null));

        await Assert.That(written).DoesNotContain("mcp-servers")
            .Because("a pull of a document that never said anything must not report a change.");
        await Assert.That(EnvelopeYaml.Parse(written).Envelope!.McpServers).IsNull();
    }

    [Test]
    public async Task Only_root_may_define_a_server()
    {
        var refusal = Envelope.Validate(Kind(Uses("sonarqube"), servers: [Hosted()]), Roles.WorkKind);

        await Assert.That(refusal).IsNotNull()
            .Because("one file that says where every credential may go is what makes trusting "
                   + "airspace reviewable (ADR-0040 Amendment 1).");
        await Assert.That(refusal!).Contains("mcp-servers");
        await Assert.That(Envelope.Validate(Root([Hosted()]), Roles.Root)).IsNull();
    }

    [Test]
    public async Task A_server_is_a_command_or_a_url_and_not_both_or_neither()
    {
        var both = Hosted() with { Command = "dnx" };
        var neither = Hosted() with { Url = null };

        await Assert.That(Envelope.Validate(Root([both]), Roles.Root)).Contains("sonarqube");
        await Assert.That(Envelope.Validate(Root([neither]), Roles.Root)).Contains("sonarqube");
    }

    [Test]
    public async Task Two_servers_under_one_key_are_refused()
    {
        var refusal = Envelope.Validate(Root([Hosted(), Hosted()]), Roles.Root);

        await Assert.That(refusal).IsNotNull();
        await Assert.That(refusal!).Contains("sonarqube");
    }

    [Test]
    public async Task The_key_gg_serves_its_own_tools_under_is_refused()
    {
        // A server named `gg` would shadow the platform's own tools, including
        // the one an agent asks a person with.
        var refusal = Envelope.Validate(Root([Hosted() with { Key = "gg" }]), Roles.Root);

        await Assert.That(refusal).IsNotNull();
    }
}
