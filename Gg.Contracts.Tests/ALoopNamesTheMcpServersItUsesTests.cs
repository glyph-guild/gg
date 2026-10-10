using Gg.Contracts.Authoring;
using static Gg.Contracts.Tests.McpFixtures;

namespace Gg.Contracts.Tests;

/// <summary>
/// S72.2-01: a loop names the servers it uses and the tools it may call on
/// each; composing root and a work kind carries root's definitions (ADR-0040
/// Amendment 1).
/// </summary>
/// <remarks>
/// <b>Two layers, with the definitions on ROOT</b>, because one layer cannot
/// tell "composed" from "rode through as the base": the composer takes the work
/// kind as its base, and a root-only member it never mentions is dropped.
/// </remarks>
public class ALoopNamesTheMcpServersItUsesTests
{
    [Test]
    public async Task A_loops_servers_are_rendered_and_read_back_unchanged()
    {
        var written = EnvelopeText.Render(Kind(Uses("sonarqube", "search_sonar_issues_in_projects")));
        var read = EnvelopeYaml.Parse(written);

        await Assert.That(read.Diagnosis).IsNull().Because("Diagnosis: " + read.Diagnosis);

        var used = read.Envelope!.Loops.Single().Mcp!.Single();
        await Assert.That(used.Server).IsEqualTo("sonarqube");
        await Assert.That(used.Allow).IsEquivalentTo(["search_sonar_issues_in_projects"]);
        await Assert.That(EnvelopeText.Render(read.Envelope!)).IsEqualTo(written);
    }

    [Test]
    public async Task Composing_carries_roots_definitions_to_the_work_kinds_loop()
    {
        var composition = Compose(Root([Hosted()]), Kind(Uses("sonarqube")));

        await Assert.That(composition.Refused).IsNull().Because("Refused: " + composition.Refused);

        var composed = composition.Composed!;
        await Assert.That(composed.McpServers!.Select(s => s.Key)).IsEquivalentTo(["sonarqube"]);
        await Assert.That(McpDefinitions.For(composed, composed.Loops.Single()).Select(s => s.Key))
            .IsEquivalentTo(["sonarqube"])
            .Because("the lease carries the definitions a loop names, and this is that join.");
    }

    [Test]
    public async Task A_name_root_does_not_define_is_refused_naming_the_layer_and_the_field()
    {
        var composition = Compose(Root([Hosted()]), Kind(Uses("sonarqbue")));

        await Assert.That(composition.Composed).IsNull()
            .Because("a typo must not become a flight that launches without the tool.");
        await Assert.That(composition.Refused!).Contains("sonarqbue");
        await Assert.That(composition.Refused!).Contains("investigate");
        await Assert.That(composition.Refused!).Contains("mcp");
    }

    [Test]
    public async Task A_loop_that_names_no_server_is_given_none()
    {
        var composition = Compose(Root([Hosted()]), Kind(mcp: null));

        await Assert.That(composition.Refused).IsNull().Because("Refused: " + composition.Refused);
        await Assert.That(McpDefinitions.For(composition.Composed!, composition.Composed!.Loops.Single()))
            .IsEmpty();
    }

    [Test]
    public async Task An_empty_allow_is_refused()
    {
        var refusal = Envelope.Validate(
            Kind([new LoopMcp { Server = "sonarqube", Allow = [] }]), Roles.WorkKind);

        await Assert.That(refusal).IsNotNull()
            .Because("absent and everything are different words: '*' says everything, and an "
                   + "empty list is a server named and then forbidden.");
        await Assert.That(refusal!).Contains("sonarqube");
    }
}
