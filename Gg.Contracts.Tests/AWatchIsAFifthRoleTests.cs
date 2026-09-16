using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A watch is a declarable role, and the estate can render one.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.1-01.</b> ADR-0022 § 5 makes a watch <i>"a tenant-scoped, named,
/// versioned Airspace document"</i> — so it is a role, and every rule
/// <c>work-kind</c>, <c>narrowing</c> and <c>strategy</c> already follow apply
/// to it: declared in the topology before a document can reach it, rendered
/// into the working copy, applied per document, retired through a gate.
/// </para>
/// <para>
/// <b>A fifth role costs nine places and two of them argue</b>, which
/// S39.0-04 measured before this was written. The two that argue are
/// <c>StrategyContainmentTests</c> and <c>EnvelopeLayerTests</c>, which both
/// pin <c>Roles.All.Count</c> at four — the count-guard shape slice thirty-five
/// paid for four times. <c>NamePathTests.Every_declarable_role_has_a_directory</c>
/// picks a fifth up for free and says so in its own comment: <i>"a fifth role
/// fails here the day it is added rather than the day somebody notices an empty
/// tree."</i> This is that day.
/// </para>
/// </remarks>
public class AWatchIsAFifthRoleTests
{
    [Test]
    public async Task The_vocabulary_knows_it()
    {
        await Assert.That(Roles.All).Contains(Roles.Watch);

        await Assert.That(Roles.Watch).IsEqualTo("watch")
            .Because("the ADR's own noun, spelled as it spells it. A role whose word differs "
                   + "from the document that describes it is two vocabularies.");
    }

    [Test]
    public async Task It_renders_into_its_own_directory()
    {
        await Assert.That(AirspaceNames.PathFor(Roles.Watch, "nightly-triage"))
            .IsEqualTo("watches/nightly-triage.yaml")
            .Because("a role with no rendering is a document pull cannot write, which is the "
                   + "estate silently missing a class of policy.");
    }

    [Test]
    public async Task A_path_in_that_directory_names_a_watch()
    {
        var named = AirspaceNames.NameFrom("watches/nightly-triage.yaml");

        await Assert.That(named).IsNotNull();
        await Assert.That(named!.Value.Role).IsEqualTo(Roles.Watch);
        await Assert.That(named.Value.Name).IsEqualTo("nightly-triage");

        await Assert.That(AirspaceNames.RoleOfDirectory("watches/nightly-triage.yaml"))
            .IsEqualTo(Roles.Watch)
            .Because("the location decides the rules a document is read by - which is what "
                   + "catches a watch copied into `work-kinds/`, a legal document of the "
                   + "wrong type that would otherwise parse and validate.");
    }

    [Test]
    public async Task The_round_trip_holds_for_every_role_including_this_one()
    {
        // DISCOVERED FROM THE VOCABULARY rather than listed, so the sixth role
        // is held to this too without anybody remembering to add it.
        foreach (var role in Roles.All)
        {
            var name = role == Roles.Root ? "root" : "a-name";
            var path = AirspaceNames.PathFor(role, name);

            await Assert.That(AirspaceNames.NameFrom(path)!.Value.Role).IsEqualTo(role)
                .Because($"'{role}' renders to '{path}', so that path has to read back as it");
        }
    }
}
