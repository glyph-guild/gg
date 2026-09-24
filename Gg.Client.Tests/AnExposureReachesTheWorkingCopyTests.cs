using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// An exposure survives the working copy: pull writes it, a diff of an untouched
/// tree reports nothing, and removing it is an intent to retire.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0027, the third step.</b> The role and its rendering landed, then the
/// reader and the comparator. This is the plumbing between them, and it is where
/// a role that looked finished usually turns out not to be.
/// </para>
/// <para>
/// <b>Each of these asserts a different way to be invisibly broken.</b> A role
/// pull does not write leaves the estate silently missing a class of document.
/// A role the diff cannot render reads as CHANGED on every comparison and is
/// re-sent on every apply, which the watch's own comment in
/// <c>AirspaceTree.Rendered</c> records as the reason that arm exists. A role
/// missing from the retirement census makes a deleted file mean nothing. And a
/// role pull writes but <c>Parse</c> cannot read is the worst of the four,
/// because the next apply reads its own output as an intent to retire every
/// document it just wrote.
/// </para>
/// <para>
/// <b>Written against a whole round trip rather than the seven sites</b>, because
/// the sites are the implementation and the round trip is the promise. A test per
/// site would pass with the parts wired to each other and to nothing.
/// </para>
/// </remarks>
public class AnExposureReachesTheWorkingCopyTests
{
    private const string Name = "jdapp";

    /// <summary>Where pull puts it: under the working copy's airspace directory.</summary>
    private static string Relative =>
        $"{AirspaceTree.Directory}/{AirspaceNames.PathFor(Roles.Exposure, Name)}";

    private static Exposure Eight() => new()
    {
        Kind = ExposureKinds.CloudflareTunnel,
        Inventory = new ExposureInventory
        {
            Size = 8,
            Hostnames = "jdapp-{slot}.example.dev",
            Credentials = "local:exposure/jdapp-{slot}",
        },
    };

    private static AirspaceEstate EstateWithOne() => new()
    {
        Documents = [],
        Strategies = [],
        Exposures =
        [
            new ExposureState
            {
                Name = Name,
                Version = "v1",
                AppliedAt = DateTimeOffset.UnixEpoch,
                Exposure = Eight(),
            },
        ],
    };

    private static string Tree()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "gg-exposure-tree", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Test]
    public async Task Pull_writes_it_into_its_own_directory()
    {
        var root = Tree();
        try
        {
            var written = AirspaceTree.Write(root, EstateWithOne());

            await Assert.That(written.Written).Contains(Relative)
                .Because("a role pull does not write is a class of document the estate holds "
                       + "and the working copy never shows.");
            await Assert.That(written.Unrepresentable).IsEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task An_untouched_tree_reports_no_change()
    {
        var root = Tree();
        try
        {
            _ = AirspaceTree.Write(root, EstateWithOne());

            var read = AirspaceTree.Read(root);
            var changed = AirspaceTree.Changed(read, EstateWithOne());

            await Assert.That(read.Unreadable).IsEmpty()
                .Because("pull wrote this file, so apply reading its own output as unreadable "
                       + "would be the round trip failing in the least visible way.");
            await Assert.That(changed.Select(c => c.Name)).DoesNotContain(Name)
                .Because("without a render arm an unrecognised document renders as the empty "
                       + "string, which never equals what the estate holds - so every exposure "
                       + "would read as CHANGED on every diff and be re-sent on every apply.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_tree_that_wrote_it_and_lost_it_is_an_intent_to_retire()
    {
        var root = Tree();
        try
        {
            _ = AirspaceTree.Write(root, EstateWithOne());
            File.Delete(Path.Combine(root, Path.Combine(Relative.Split('/'))));

            var retiring = AirspaceTree.Retiring(AirspaceTree.Read(root), EstateWithOne());

            await Assert.That(retiring).Contains(Name)
                .Because("a role absent from the census makes deleting its file mean nothing, "
                       + "so a tenant who removed a document would be told the estate agrees.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task What_pull_wrote_reads_back_as_the_document_it_was()
    {
        var root = Tree();
        try
        {
            _ = AirspaceTree.Write(root, EstateWithOne());

            var read = AirspaceTree.Read(root);
            var document = read.Documents.Single(d =>
                string.Equals(d.Name, Name, StringComparison.Ordinal));

            await Assert.That(document.Role).IsEqualTo(Roles.Exposure);
            await Assert.That(document.Exposure).IsEqualTo(Eight())
                .Because("a document pull writes and Parse cannot read is the worst of these: "
                       + "the next apply reads gg's own output as an intent to retire what it "
                       + "has just written.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
