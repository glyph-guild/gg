using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A name is its own path, and the path is enough to find the name again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mapping is the identity function, and that is the whole design.</b>
/// ADR-0016 renders the estate as a tree mirroring the topology, so every name
/// becomes a path and every path must identify exactly one document. Both
/// directions have to work: <c>pull</c> needs name to path, and <c>apply</c> reads
/// a file and must know which stream it belongs to. A path that could mean two
/// documents makes apply guess.
/// </para>
/// <para>
/// <b>The directory carries the role, and the role is not a permission boundary.</b>
/// ADR-0016 closes <i>one tree or two</i> by observing that a directory cannot own
/// anything - if it did, moving a file between directories would be a governance
/// act. The directory is a rendering of the role the topology already holds, which
/// is why <c>NameFrom</c> answers a role rather than trusting one.
/// </para>
/// </remarks>
public class NamePathTests
{
    [Test]
    public async Task Root_is_a_file_at_the_top_rather_than_a_directory_of_one()
    {
        // root is reserved, exactly one exists, and it never sits in a folder
        // named for its role - there is no second root to keep it company.
        await Assert.That(AirspaceNames.PathFor(Roles.Root, "root")).IsEqualTo("root.yaml");
    }

    [Test]
    [Arguments(Roles.WorkKind, "migrate-data", "work-kinds/migrate-data.yaml")]
    [Arguments(Roles.Narrowing, "team-payments", "narrowings/team-payments.yaml")]
    [Arguments(Roles.Narrowing, "pci", "narrowings/pci.yaml")]
    [Arguments(Roles.Strategy, "payments-pool", "strategies/payments-pool.yaml")]
    public async Task A_named_document_sits_under_its_role(string role, string name, string path)
    {
        await Assert.That(AirspaceNames.PathFor(role, name)).IsEqualTo(path);
    }

    [Test]
    [Arguments(Roles.WorkKind, "migrate-data")]
    [Arguments(Roles.Narrowing, "team-payments")]
    [Arguments(Roles.Strategy, "payments-pool")]
    [Arguments(Roles.Root, "root")]
    public async Task Every_name_round_trips_path_to_name_to_path(string role, string name)
    {
        var path = AirspaceNames.PathFor(role, name);
        var found = AirspaceNames.NameFrom(path);

        await Assert.That(found).IsNotNull()
            .Because($"'{path}' was rendered from a name, so it has to read back as one");
        await Assert.That(found!.Value.Name).IsEqualTo(name);
        await Assert.That(found.Value.Role).IsEqualTo(role);
        await Assert.That(AirspaceNames.PathFor(found.Value.Role, found.Value.Name))
            .IsEqualTo(path);
    }

    [Test]
    public async Task Every_declarable_role_has_a_directory()
    {
        // A role with no rendering is a document pull cannot write, which is
        // the estate silently missing a class of policy. Discovered from the
        // vocabulary rather than listed, so a fifth role fails here the day it
        // is added rather than the day somebody notices an empty tree.
        foreach (var role in Roles.All)
        {
            var path = AirspaceNames.PathFor(role, role == Roles.Root ? "root" : "a-name");

            await Assert.That(path).IsNotEmpty()
                .Because($"role '{role}' has no place in the tree, so pull cannot render it");
        }
    }

    [Test]
    [Arguments("README.md")]
    [Arguments("narrowings/pci.txt")]
    [Arguments("narrowings/nested/pci.yaml")]
    [Arguments("unknown-role/pci.yaml")]
    [Arguments("work-kinds/Payments.yaml")]
    [Arguments("work-kinds/.hidden.yaml")]
    public async Task A_path_that_is_not_a_document_reads_as_nothing(string path)
    {
        // A tree is a directory somebody also keeps notes in. Anything pull did
        // not write is not a document, and apply must not invent a stream for
        // it - including a file whose stem is a name this estate would refuse.
        await Assert.That(AirspaceNames.NameFrom(path)).IsNull();
    }

    [Test]
    public async Task The_separator_is_the_tree_s_own_rather_than_the_platform_s()
    {
        // The rendered path is what a git repository holds, and git holds
        // forward slashes on every platform. A path built with the running
        // machine's separator would round-trip on that machine and produce a
        // second file for the same document on somebody else's.
        await Assert.That(AirspaceNames.PathFor(Roles.Narrowing, "pci")).DoesNotContain("\\");
    }
}
