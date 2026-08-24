using Gg.Cli;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg airspace show</c>: the topology, rendered - root included, always.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reader that ships with the stream.</b> The topology decides which
/// envelope names are reachable at all, and a registry nobody can look at is
/// shelf-ware with a green suite - registered-is-not-invoked number fourteen,
/// pre-booked by the slice. This verb is the person-facing half of the
/// same-PR-readers rule; the control plane's apply refusal is the other.
/// </para>
/// <para>
/// <b>Root renders without ever being declared</b>, because root is
/// synthesized by the read - a topology listing that omitted the floor would
/// show a tenant their overlays and hide what the overlays narrow.
/// </para>
/// </remarks>
public class AirspaceShowVerbTests
{
    private static EnvelopeTopology TwoNames() => new()
    {
        Names =
        [
            new TopologyName
            {
                Name = "root",
                Role = "root",
                Parent = null,
                SubjectBinding = null,
                DeclaredBy = "the floor exists; nobody declares it",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
            new TopologyName
            {
                Name = "payments",
                Role = "work-kind",
                Parent = "root",
                SubjectBinding = null,
                DeclaredBy = "kevin",
                DeclaredAt = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero),
            },
        ],
    };

    [Test]
    public async Task The_parse_arm_exists_and_names_its_one_subcommand()
    {
        await Assert.That(CliArgs.Parse(["airspace", "show"]))
            .IsEqualTo(new CliAction.AirspaceShow(false));
        await Assert.That(CliArgs.Parse(["airspace", "show", "--json"]))
            .IsEqualTo(new CliAction.AirspaceShow(true));

        var bare = CliArgs.Parse(["airspace"]) as CliAction.Unknown;
        await Assert.That(bare).IsNotNull();
        await Assert.That(bare!.Message).Contains("show")
            .Because("an error that names the subcommand is the difference between a verb "
                   + "somebody uses and one they give up on.");
    }

    [Test]
    public async Task The_topology_renders_root_first_with_role_and_parent()
    {
        var text = VerbOutput.ToText(new VerbResult.AirspaceTopology(TwoNames()));

        await Assert.That(text).Contains("root");
        await Assert.That(text).Contains("payments");
        await Assert.That(text).Contains("work-kind");
        await Assert.That(text.IndexOf("root", StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("payments", StringComparison.Ordinal))
            .Because("the floor first: everything below it narrows it, and the reading "
                   + "order is the authority order.");
    }

    [Test]
    public async Task Rendering_twice_gives_identical_text()
    {
        var once = VerbOutput.ToText(new VerbResult.AirspaceTopology(TwoNames()));
        var twice = VerbOutput.ToText(new VerbResult.AirspaceTopology(TwoNames()));

        await Assert.That(once).IsEqualTo(twice);
    }

    [Test]
    public async Task Json_is_the_topology_document_unchanged()
    {
        var json = VerbOutput.ToJson(new VerbResult.AirspaceTopology(TwoNames()));

        await Assert.That(json).Contains("\"payments\"");
        await Assert.That(json).Contains("\"work-kind\"");

        var back = VerbOutput.Parse(
            VerbResultKinds.AirspaceTopology, json) as VerbResult.AirspaceTopology;
        await Assert.That(back).IsNotNull()
            .Because("--json is the document, not a summary of it - a script reads back "
                   + "exactly what a person was shown.");
        await Assert.That(back!.Value.Names.Count).IsEqualTo(2);
    }
}
