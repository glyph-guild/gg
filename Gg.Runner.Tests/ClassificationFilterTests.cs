using Gg.Contracts;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// The filter drops what may not leave, and says that it did.
/// </summary>
/// <remarks>
/// <para>
/// One of the two controls, and neither covers the other: <b>the runner filters
/// so that unclassified content never leaves the customer's network; the
/// control plane re-validates so that a patched runner cannot make us store
/// what we promised not to.</b>
/// </para>
/// <para>
/// The asymmetry that follows is worth being explicit about, because somebody
/// will eventually propose deleting one as redundant. <b>Re-validation catches
/// over-submission only.</b> An item this filter dropped never arrives, so the
/// control plane cannot know it existed - it has no way to notice a runner that
/// withheld too much, and this filter has no way to stop a runner that was
/// modified to withhold nothing.
/// </para>
/// </remarks>
public class ClassificationFilterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static ChangedPath APath(string path, string classification) => new()
    {
        Path = path,
        Change = ChangeKinds.Modified,
        LinesAdded = 1,
        LinesRemoved = 0,
        Classification = classification,
    };

    private static ChangeManifest AManifest(params ChangedPath[] paths) => new()
    {
        BaseCommit = new string('a', 40),
        HeadCommit = new string('b', 40),
        Resolution = ChangeResolution.Files,
        DiffBasis = Gg.Contracts.DiffBasis.TwoPoint,
        Paths = paths,
        Directories = [],
        Languages = [],
        FilesChanged = paths.Length,
        LinesAdded = paths.Length,
        LinesRemoved = 0,
        PathsWithheld = 0,
    };

    private static FilteredFacts FilterAt(string ceiling, params ChangedPath[] paths) =>
        FactPipeline.Filter(
            FactPipeline.Digest(
                new GatheredFacts([new FactPayload.Change(AManifest(paths))]), "flight-1", T0),
            ceiling);

    [Test]
    public async Task A_path_above_the_ceiling_does_not_leave()
    {
        var filtered = FilterAt(
            Classifications.Internal,
            APath("src/Program.cs", Classifications.Internal),
            APath("deploy/key.pem", Classifications.Restricted));

        var manifest = filtered.Items.Single().Change!;

        await Assert.That(manifest.Paths.Select(p => p.Path)).IsEquivalentTo((string[])["src/Program.cs"]);
    }

    [Test]
    public async Task What_was_withheld_is_counted_rather_than_quietly_missing()
    {
        // Never silently. A manifest whose list is shorter than its own count,
        // with nothing saying why, is the false statement a truncation would
        // have been.
        var manifest = FilterAt(
            Classifications.Internal,
            APath("src/Program.cs", Classifications.Internal),
            APath("deploy/key.pem", Classifications.Restricted)).Items.Single().Change!;

        await Assert.That(manifest.PathsWithheld).IsEqualTo(1);
        await Assert.That(manifest.FilesChanged).IsEqualTo(2)
            .Because("the total is the truth about the change; the list is what may cross.");
        await Assert.That(ChangeManifest.Validate(manifest)).IsNull();
    }

    [Test]
    public async Task The_same_repository_gives_two_tenants_different_results()
    {
        // The test that must be able to fail. With one classification level
        // everything is permitted, nothing is filtered, and "the filter works"
        // passes on a system with no filter.
        ChangedPath[] paths =
        [
            APath("docs/notes.md", Classifications.Public),
            APath("src/Program.cs", Classifications.Internal),
            APath("deploy/key.pem", Classifications.Restricted),
        ];

        var strict = FilterAt(Classifications.Public, paths).Items.Single().Change!;
        var relaxed = FilterAt(Classifications.Confidential, paths).Items.Single().Change!;

        await Assert.That(strict.Paths.Select(p => p.Path)).IsEquivalentTo((string[])["docs/notes.md"]);
        await Assert.That(relaxed.Paths.Select(p => p.Path))
            .IsEquivalentTo((string[])["docs/notes.md", "src/Program.cs"]);

        await Assert.That(strict.PathsWithheld).IsEqualTo(2);
        await Assert.That(relaxed.PathsWithheld).IsEqualTo(1);
    }

    [Test]
    public async Task A_ceiling_that_permits_everything_withholds_nothing()
    {
        // The other end of the range, so the assertions above are about the
        // ceiling rather than about the filter always removing something.
        var manifest = FilterAt(
            Classifications.Restricted,
            APath("docs/notes.md", Classifications.Public),
            APath("deploy/key.pem", Classifications.Restricted)).Items.Single().Change!;

        await Assert.That(manifest.PathsWithheld).IsEqualTo(0);
        await Assert.That(manifest.Paths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task The_digest_is_of_what_was_observed_rather_than_of_what_survived()
    {
        // The order the pipeline's types enforce, and the reason for it: a
        // digest computed after the filter would describe already-redacted
        // material, and every later conclusion would be about a document nobody
        // produced. So the digest of a filtered fact deliberately does NOT
        // match its payload, and that mismatch is evidence something was
        // withheld rather than a defect.
        ChangedPath[] paths =
        [
            APath("src/Program.cs", Classifications.Internal),
            APath("deploy/key.pem", Classifications.Restricted),
        ];

        var unfiltered = FactPipeline.Digest(
            new GatheredFacts([new FactPayload.Change(AManifest(paths))]), "flight-1", T0);
        var filtered = FactPipeline.Filter(unfiltered, Classifications.Internal);

        await Assert.That(filtered.Items.Single().Digest).IsEqualTo(unfiltered.Items.Single().Digest);
        await Assert.That(filtered.Items.Single().Change!.Paths.Count).IsLessThan(paths.Length);
    }

    [Test]
    public async Task A_fact_with_no_classified_items_passes_through_untouched()
    {
        // The environment fact is about the machine, not about a customer's
        // files, and a filter that started dropping it would be a filter
        // applying a rule to something the rule is not about.
        var environment = new EnvironmentIdentity
        {
            HostFingerprint = new string('a', 64),
            ImageDigest = null,
            Locks = [],
            Tools = [],
            Provenance = EnvironmentProvenance.Fresh,
        };

        var filtered = FactPipeline.Filter(
            FactPipeline.Digest(
                new GatheredFacts([new FactPayload.Environment(environment)]), "flight-1", T0),
            Classifications.Public);

        await Assert.That(filtered.Items.Single().Environment).IsEqualTo(environment);
    }

    [Test]
    public async Task A_manifest_whose_every_path_is_withheld_still_ships()
    {
        // The count is the fact. "Everything changed here is above your
        // ceiling" is a true and useful statement, and dropping the manifest
        // would make it indistinguishable from a flight that changed nothing.
        var manifest = FilterAt(
            Classifications.Public,
            APath("deploy/key.pem", Classifications.Restricted)).Items.Single().Change!;

        await Assert.That(manifest.Paths).IsEmpty();
        await Assert.That(manifest.PathsWithheld).IsEqualTo(1);
        await Assert.That(manifest.FilesChanged).IsEqualTo(1);
    }

    [Test]
    public async Task A_ceiling_nobody_declared_halts_rather_than_permitting_everything()
    {
        // Article XI, at the control that matters most. A typo in a tenant's
        // ceiling must not be the same as switching the filter off.
        await Assert.That(() => FilterAt("unclassified", APath("a.cs", Classifications.Public)))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task The_pipeline_still_has_exactly_three_stages()
    {
        // This step adds a fact, not a pipeline. Step 6 made the ordering a
        // property of the types; if this file had needed to change that, the
        // fact would have been the wrong shape.
        await Assert.That(typeof(FactPipeline).GetMethod(nameof(FactPipeline.Digest))!.ReturnType)
            .IsEqualTo(typeof(DigestedFacts));
        await Assert.That(typeof(FactPipeline).GetMethod(nameof(FactPipeline.Filter))!.ReturnType)
            .IsEqualTo(typeof(FilteredFacts));
        await Assert.That(typeof(FactPipeline).GetMethod(nameof(FactPipeline.Filter))!
            .GetParameters().Select(p => p.ParameterType)).Contains(typeof(DigestedFacts));
    }
}
