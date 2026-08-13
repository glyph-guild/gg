using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The manifest, from a real diff between two real commits.
/// </summary>
/// <remarks>
/// <para>
/// Reading file content to count lines is fine: it happens here, on the
/// runner's own disk, and never leaves. What crosses is paths, counts and
/// hashes - and the fixture's files carry distinctive strings so that claim is
/// checked rather than asserted.
/// </para>
/// <para>
/// Real git again, because what "changed between these two commits" means is
/// git's answer and not ours.
/// </para>
/// </remarks>
public class ChangeExtractionTests
{
    private static async Task<Materialized> MaterializeAsync(
        DiffFixture fixture, ScratchTreeRoot trees, string pinnedRef = "refs/heads/feature") =>
        await new Materializer(new LocalVcsAdapter(fixture.Directory), trees.Root)
            .MaterializeAsync("flight-1", new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = pinnedRef,
                BaseRef = "refs/heads/main",
            }, secret: null);

    [Test]
    public async Task A_manifest_names_every_changed_path_and_what_happened_to_it()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        var byPath = manifest.Paths.ToDictionary(p => p.Path);
        await Assert.That(byPath["src/Added.cs"].Change).IsEqualTo(ChangeKinds.Added);
        await Assert.That(byPath["src/Program.cs"].Change).IsEqualTo(ChangeKinds.Modified);
        await Assert.That(byPath["src/Gone.cs"].Change).IsEqualTo(ChangeKinds.Deleted);
    }

    [Test]
    public async Task Line_counts_come_from_the_diff_rather_than_from_the_file()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;
        var modified = manifest.Paths.Single(p => p.Path == "src/Program.cs");

        await Assert.That(modified.LinesAdded).IsGreaterThan(0);
        await Assert.That(manifest.LinesAdded).IsEqualTo(manifest.Paths.Sum(p => p.LinesAdded));
        await Assert.That(manifest.FilesChanged).IsEqualTo(manifest.Paths.Count);
    }

    [Test]
    public async Task The_manifest_records_which_two_commits_it_is_between()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.HeadCommit).IsEqualTo(fixture.FeatureCommit);
        await Assert.That(manifest.BaseCommit).IsEqualTo(fixture.MainCommit)
            .Because("a manifest that does not say what it is a diff FROM is one nobody can check.");
    }

    [Test]
    public async Task Languages_are_broken_down_by_what_the_extension_says()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Languages.Select(l => l.Language)).Contains("csharp");
        await Assert.That(manifest.Languages.Sum(l => l.Files)).IsEqualTo(manifest.FilesChanged);
    }

    [Test]
    public async Task A_repository_with_no_base_ref_produces_no_manifest_rather_than_a_wrong_one()
    {
        // Article XI. Without a base there is nothing to diff against, and a
        // manifest computed against a guessed default branch would be a false
        // statement about what a flight examined.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await new Materializer(new LocalVcsAdapter(fixture.Directory), trees.Root)
            .MaterializeAsync("flight-1", new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/feature",
                BaseRef = null,
            }, secret: null);

        await Assert.That(ChangeExtractor.Extract(tree, ClassificationRules.Default)).IsNull();
    }

    [Test]
    public async Task Every_path_carries_the_classification_the_rules_gave_it()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var rules = (IReadOnlyList<ClassificationRule>)
        [
            new ClassificationRule { PathGlob = "**/*.pem", Classification = Classifications.Restricted },
            new ClassificationRule { PathGlob = "docs/**", Classification = Classifications.Public },
        ];

        var manifest = ChangeExtractor.Extract(tree, rules)!;

        await Assert.That(manifest.Paths.Single(p => p.Path == "deploy/key.pem").Classification)
            .IsEqualTo(Classifications.Restricted);
        await Assert.That(manifest.Paths.Single(p => p.Path == "docs/notes.md").Classification)
            .IsEqualTo(Classifications.Public);
        await Assert.That(manifest.Paths.Single(p => p.Path == "src/Program.cs").Classification)
            .IsEqualTo(ClassificationRules.Unmatched);
    }

    [Test]
    public async Task No_line_of_any_file_appears_in_the_manifest()
    {
        // The claim, at the one fact that reads files at all. The markers are
        // really in the fixture's files and the extractor really opened them
        // to count lines.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;
        var rendered = System.Text.Json.JsonSerializer.Serialize(
            manifest, System.Text.Json.JsonSerializerOptions.Web);

        await Assert.That(rendered).DoesNotContain(DiffFixture.ContentMarker);

        // The twin, through the real path: the marker is on this disk, in a
        // file the extractor counted.
        await Assert.That(File.ReadAllText(Path.Combine(tree.Path, "src", "Program.cs")))
            .Contains(DiffFixture.ContentMarker);
        await Assert.That(rendered).Contains("src/Program.cs")
            .Because("the path crossed and the line it contains did not, which is the whole rule.");
    }

    // ---- resolution, when the manifest would not fit ----

    [Test]
    public async Task A_manifest_that_fits_stays_at_file_resolution()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Resolution).IsEqualTo(ChangeResolution.Files);
        await Assert.That(manifest.Directories).IsEmpty();
    }

    [Test]
    public async Task A_manifest_too_large_for_the_budget_becomes_a_labelled_rollup()
    {
        // Degrade resolution. A per-directory rollup is a true statement at
        // lower resolution; a truncated file list is a false one, and ingress
        // already makes exactly that distinction about facts cut in half.
        using var fixture = new DiffFixture(wideFiles: FactBudget.ManifestFilesWithinBudget * 2);
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Resolution).IsEqualTo(ChangeResolution.Directories);
        await Assert.That(manifest.Paths).IsEmpty();
        await Assert.That(manifest.Directories).IsNotEmpty();
        await Assert.That(manifest.FilesChanged).IsGreaterThanOrEqualTo(900)
            .Because("the rollup states what it summarises; a consumer must never have to guess "
                   + "whether it is looking at everything.");
        await Assert.That(ChangeManifest.Validate(manifest)).IsNull();
    }

    [Test]
    public async Task The_rollup_fits_where_the_file_list_did_not()
    {
        // Otherwise it is a slower way to be rejected.
        using var fixture = new DiffFixture(wideFiles: FactBudget.ManifestFilesWithinBudget * 2);
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;
        var digested = FactPipeline.Digest(FactHygiene.Clean(
            new GatheredFacts([new FactPayload.Change(manifest)])), "flight-1", DateTimeOffset.UnixEpoch);

        await Assert.That(FactPipeline.OverBudget(digested.Items[0])).IsFalse();
    }

    [Test]
    public async Task The_line_totals_survive_the_rollup()
    {
        // A lower-resolution TRUE statement. If the totals changed it would be
        // a different statement, not a coarser one.
        using var fixture = new DiffFixture(wideFiles: FactBudget.ManifestFilesWithinBudget * 2);
        using var trees = new ScratchTreeRoot();
        var tree = await MaterializeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Directories.Sum(d => d.Files)).IsEqualTo(manifest.FilesChanged);
        await Assert.That(manifest.Directories.Sum(d => d.LinesAdded)).IsEqualTo(manifest.LinesAdded);
    }
}
