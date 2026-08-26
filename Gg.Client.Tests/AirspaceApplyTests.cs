using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Reading the tree back: which documents a working copy holds, and which changed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Apply is per document, and that is ADR-0016 § 3 rather than an
/// implementation choice.</b> One changed file, one flight, one gate, one minted
/// version, one attribution — a changeset spanning documents stays several applies
/// and gains an order rather than atomicity.
/// </para>
/// <para>
/// <b>An unchanged document is not submitted at all.</b> The control plane would
/// answer <i>nothing changed</i>, which is correct and costs a round trip per
/// document per apply; at estate scale that is the difference between a verb a
/// person runs and one they avoid. It is also what makes "pull, apply, mint
/// nothing" observable from this side.
/// </para>
/// <para>
/// <b>A deleted file is an intent to retire, and it is reported as one.</b> Never
/// a silent unlinking: the tree is a rendering, so a document missing from it is a
/// statement about the stream that somebody has to mean.
/// </para>
/// </remarks>
public class AirspaceApplyTests
{
    private static string Scratch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-apply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task Every_document_in_the_tree_reads_back_with_its_precondition()
    {
        var root = Scratch();
        try
        {
            _ = AirspaceTree.Write(root, PullTests.Estate());

            var read = AirspaceTree.Read(root);

            await Assert.That(read.Documents.Select(d => d.Name)
                .OrderBy(n => n, StringComparer.Ordinal))
                .IsEquivalentTo(new[] { "migrate-data", "payments-pool", "pci", "root" });

            var narrowing = read.Documents.Single(d => d.Name == "pci");
            await Assert.That(narrowing.BasedOn).IsEqualTo("pci@v1")
                .Because("the precondition travels with the document, which is the whole "
                       + "reason pull wrote it into the file.");
            await Assert.That(narrowing.Narrowing).IsNotNull();
            await Assert.That(narrowing.Role).IsEqualTo(Roles.Narrowing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_file_that_is_not_a_document_is_not_read_as_one()
    {
        var root = Scratch();
        try
        {
            _ = AirspaceTree.Write(root, PullTests.Estate());
            await File.WriteAllTextAsync(
                Path.Combine(root, "airspace", "README.md"), "how we do things\n");

            await Assert.That(AirspaceTree.Read(root).Documents.Count).IsEqualTo(4);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_document_that_does_not_parse_is_named_rather_than_skipped()
    {
        // A file the tool cannot read is not a file the tool ignores. Applying
        // the rest and saying nothing would land a partial changeset somebody
        // believed was whole.
        var root = Scratch();
        try
        {
            _ = AirspaceTree.Write(root, PullTests.Estate());
            await File.WriteAllTextAsync(
                Path.Combine(root, "airspace", "narrowings", "pci.yaml"),
                "obligations:\n  - id: pci-review\n    check: banana\n");

            var read = AirspaceTree.Read(root);

            await Assert.That(read.Unreadable.Select(u => u.Path))
                .Contains("airspace/narrowings/pci.yaml");
            await Assert.That(read.Unreadable.Single().Diagnosis).IsNotEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task An_unchanged_tree_has_nothing_to_apply()
    {
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);

            var changed = AirspaceTree.Changed(AirspaceTree.Read(root), estate);

            await Assert.That(changed).IsEmpty()
                .Because("pull, change nothing, apply - and nothing is submitted, which is "
                       + "what makes 'the estate round trip mints nothing' true from here "
                       + "rather than true only at the far end.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task An_edited_document_is_the_only_one_submitted()
    {
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);

            var edited = Path.Combine(root, "airspace", "narrowings", "pci.yaml");
            var text = await File.ReadAllTextAsync(edited);
            await File.WriteAllTextAsync(
                edited,
                text.Replace("approver: an-architect", "approver: two-architects"));

            var changed = AirspaceTree.Changed(AirspaceTree.Read(root), estate);

            await Assert.That(changed.Select(c => c.Name)).IsEquivalentTo(new[] { "pci" });
            await Assert.That(changed.Single().BasedOn).IsEqualTo("pci@v1");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_deleted_file_reads_as_an_intent_to_retire()
    {
        var root = Scratch();
        try
        {
            var estate = PullTests.Estate();
            _ = AirspaceTree.Write(root, estate);
            File.Delete(Path.Combine(root, "airspace", "narrowings", "pci.yaml"));

            var retiring = AirspaceTree.Retiring(AirspaceTree.Read(root), estate);

            await Assert.That(retiring).IsEquivalentTo(new[] { "pci" })
                .Because("there is no delete verb - retiring a name is applying a terminal "
                       + "version of it, so a missing file is an INTENT somebody has to "
                       + "mean, never a silent unlinking.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task An_empty_tree_is_not_an_intent_to_retire_the_whole_estate()
    {
        // THE POISON TWIN, and the failure mode that would matter most. A person
        // who runs apply in the wrong directory has not asked to retire their
        // entire airspace, and a tool that read it that way would be one nobody
        // could safely run.
        var root = Scratch();
        try
        {
            var retiring = AirspaceTree.Retiring(AirspaceTree.Read(root), PullTests.Estate());

            await Assert.That(retiring).IsEmpty()
                .Because("no tree at all is not a tree that says everything should stop "
                       + "applying - it is somebody standing in the wrong directory.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
