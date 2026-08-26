using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Pull renders the estate as files, and writes nothing the stream does not hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tree is a rendering.</b> ADR-0016's whole position is that the stream is
/// the record and the working copy is convenience — so a file pull writes that no
/// document backs is the tree quietly becoming a second source of truth, which is
/// the thing the manifest option was refused for one noun over.
/// </para>
/// <para>
/// <b>A name that predates the rule is reported rather than written.</b> Every
/// name check fires at declare, and a declare-time rule never re-examines a row
/// already stored — so an estate declared before slice thirteen can hold a name no
/// path can carry. Pull says so and writes no file, because a file it cannot write
/// back is worse than a name it names.
/// </para>
/// </remarks>
public class PullTests
{
    internal static NamedEnvelopeState Named(string name, string role) => new()
    {
        Name = name,
        Role = role,
        Version = $"{name}@v1",
        Envelope = role == Roles.Narrowing ? null : StrategyRoundTripTests.AnEnvelope(),
        Narrowing = role == Roles.Narrowing
            ? new EnvelopeNarrowing
            {
                Obligations =
                [
                    new Obligation
                    {
                        Id = "pci-review",
                        Check = ObligationChecks.Human,
                        Approver = "an-architect",
                    },
                ],
            }
            : null,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        UpdatedBy = "an-architect",
    };

    internal static AirspaceEstate Estate() => new()
    {
        Documents =
        [
            Named("root", Roles.Root),
            Named("migrate-data", Roles.WorkKind),
            Named("pci", Roles.Narrowing),
        ],
        Strategies =
        [
            new EnvironmentStrategyState
            {
                Name = "payments-pool",
                Version = "payments-pool@v1",
                AppliedAt = DateTimeOffset.UnixEpoch,
                Strategy = StrategyRoundTripTests.AStrategy(),
            },
        ],
    };

    private static string Scratch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-pull-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task Every_document_lands_at_the_path_its_name_and_role_give_it()
    {
        var root = Scratch();
        try
        {
            _ = AirspaceTree.Write(root, Estate());

            await Assert.That(File.Exists(Path.Combine(root, "airspace", "root.yaml"))).IsTrue();
            await Assert.That(File.Exists(
                Path.Combine(root, "airspace", "work-kinds", "migrate-data.yaml"))).IsTrue();
            await Assert.That(File.Exists(
                Path.Combine(root, "airspace", "narrowings", "pci.yaml"))).IsTrue();
            await Assert.That(File.Exists(
                Path.Combine(root, "airspace", "strategies", "payments-pool.yaml"))).IsTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task What_it_writes_is_what_the_renderer_renders()
    {
        var root = Scratch();
        try
        {
            _ = AirspaceTree.Write(root, Estate());

            var narrowing = await File.ReadAllTextAsync(
                Path.Combine(root, "airspace", "narrowings", "pci.yaml"));

            await Assert.That(narrowing).IsEqualTo(
                "based-on: pci@v1\n"
                + EnvelopeText.Render(Named("pci", Roles.Narrowing).Narrowing!))
                .Because("pull renders the stream and states which version it rendered; "
                       + "it has no second opinion about how the document itself is "
                       + "written down.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_document_the_estate_no_longer_holds_is_removed_from_the_tree()
    {
        var root = Scratch();
        try
        {
            _ = AirspaceTree.Write(root, Estate());

            var retired = Path.Combine(root, "airspace", "narrowings", "pci.yaml");
            await Assert.That(File.Exists(retired)).IsTrue();

            var without = Estate() with
            {
                Documents = [.. Estate().Documents.Where(d => d.Name != "pci")],
            };
            var written = AirspaceTree.Write(root, without);

            await Assert.That(File.Exists(retired)).IsFalse()
                .Because("a name whose stream has ended is a file that disappears - which "
                       + "is what makes a terminal version visible at all.");
            await Assert.That(written.Removed).Contains("airspace/narrowings/pci.yaml");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_file_the_tool_did_not_write_is_left_alone()
    {
        // A working copy is a directory somebody also keeps notes in. Pull owns
        // the documents it renders and nothing else - deleting a README because
        // it is not in the stream would be the tool claiming the repository.
        var root = Scratch();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "airspace"));
            var notes = Path.Combine(root, "airspace", "README.md");
            await File.WriteAllTextAsync(notes, "how we do things here\n");

            _ = AirspaceTree.Write(root, Estate());

            await Assert.That(File.Exists(notes)).IsTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task A_name_no_path_can_hold_is_reported_rather_than_written()
    {
        // The read-side answer for an estate that predates the name rule. The
        // refusal stays at the door, where the person who caused it is; here
        // the honest move is to name the document and write no file, because a
        // file that cannot be written back is worse than a name that is named.
        var root = Scratch();
        try
        {
            var legacy = Estate() with
            {
                Documents = [.. Estate().Documents, Named("Payments/EU", Roles.Narrowing)],
            };

            var written = AirspaceTree.Write(root, legacy);

            await Assert.That(written.Unrepresentable).Contains("Payments/EU");
            await Assert.That(Directory.EnumerateFiles(
                Path.Combine(root, "airspace", "narrowings")).Count()).IsEqualTo(1)
                .Because("only pci was representable, so only pci was written.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
