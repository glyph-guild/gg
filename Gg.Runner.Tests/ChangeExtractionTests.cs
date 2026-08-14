using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The manifest, from a real diff between a real commit and a real working tree.
/// </summary>
/// <remarks>
/// <para>
/// Reading file content to count lines is fine: it happens here, on the
/// runner's own disk, and never leaves. What crosses is paths, counts and
/// hashes - and the fixture's files carry distinctive strings so that claim is
/// checked rather than asserted.
/// </para>
/// <para>
/// Real git again, because what "changed since this commit" means is git's
/// answer and not ours.
/// </para>
/// <para>
/// <b>Every test here now materializes and then works the tree</b>, in that order,
/// because that is the runner's order. These tests used to diff two commits, which
/// no flight ever does: the agent's edits are uncommitted when the manifest is
/// taken, and the destination adapter commits them later, during the push.
/// </para>
/// </remarks>
public class ChangeExtractionTests
{
    /// <summary>Materialize the way a flight does, then let the agent work.</summary>
    private static async Task<Materialized> AWorkedTreeAsync(
        DiffFixture fixture, ScratchTreeRoot trees, string pinnedRef = "refs/heads/main")
    {
        var tree = await new Materializer(new LocalVcsAdapter(fixture.Directory), trees.Root)
            .MaterializeAsync("flight-1", new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = pinnedRef,
            }, secret: null);

        fixture.TheAgentWorks(tree.Path);
        return tree;
    }

    [Test]
    public async Task A_manifest_names_every_changed_path_and_what_happened_to_it()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        var byPath = manifest.Paths.ToDictionary(p => p.Path);
        await Assert.That(byPath["src/Added.cs"].Change).IsEqualTo(ChangeKinds.Added);
        await Assert.That(byPath["src/Program.cs"].Change).IsEqualTo(ChangeKinds.Modified);
        await Assert.That(byPath["src/Gone.cs"].Change).IsEqualTo(ChangeKinds.Deleted);
    }

    [Test]
    public async Task A_file_git_has_never_seen_is_the_agents_work_and_is_reported()
    {
        // THE COMMONEST SHAPE OF NEW WORK, and the one a plain `git diff` cannot
        // see at all. `when: change.manifest touches migrations/**` exists for
        // new migrations; an extractor blind to untracked files never fires it.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        await Assert.That(GitFixture.Run(tree.Path, "status", "--porcelain"))
            .Contains("?? src/Added.cs")
            .Because("git itself calls it untracked, which is what makes this test's subject real.");

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Paths.Single(p => p.Path == "src/Added.cs").Change)
            .IsEqualTo(ChangeKinds.Added);
        await Assert.That(manifest.Paths.Single(p => p.Path == "src/Added.cs").LinesAdded)
            .IsGreaterThan(0)
            .Because("a new file's lines are added lines, and a count of zero would read as empty.");
    }

    [Test]
    public async Task A_rename_is_reported_as_both_halves_because_the_vocabulary_has_no_third_word()
    {
        // DELIBERATE, not a limitation nobody looked at. ChangeKinds.All is
        // [added, modified, deleted] - there is no rename - and adding one is a
        // contract move with a ledger entry. Decomposition is TRUE at this
        // vocabulary's resolution, and it is the safer answer for both readers of
        // a manifest: `in-scope` evaluates both paths, and `touches` matches the
        // new one.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;
        var byPath = manifest.Paths.ToDictionary(p => p.Path);

        await Assert.That(byPath[DiffFixture.RenamedFrom].Change).IsEqualTo(ChangeKinds.Deleted);
        await Assert.That(byPath[DiffFixture.RenamedTo].Change).IsEqualTo(ChangeKinds.Added);
        await Assert.That(ChangeKinds.All).DoesNotContain("renamed")
            .Because("the day this vocabulary gains a rename, this test is the one that should fail.");
    }

    [Test]
    public async Task An_ignored_file_is_not_part_of_the_change_being_proposed()
    {
        // .gitignore'd, so the destination adapter's `git add --all` would not
        // stage it and no push would carry it. A manifest naming it would report
        // as landing something that cannot land.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        await Assert.That(File.Exists(Path.Combine(
            tree.Path, DiffFixture.IgnoredPath.Replace('/', Path.DirectorySeparatorChar)))).IsTrue()
            .Because("the file is really on disk, so its absence below is a decision and not a miss.");

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Paths.Select(p => p.Path)).DoesNotContain(DiffFixture.IgnoredPath);
    }

    [Test]
    public async Task Line_counts_come_from_the_diff_rather_than_from_the_file()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;
        var modified = manifest.Paths.Single(p => p.Path == "src/Program.cs");

        await Assert.That(modified.LinesAdded).IsGreaterThan(0);
        await Assert.That(manifest.LinesAdded).IsEqualTo(manifest.Paths.Sum(p => p.LinesAdded));
        await Assert.That(manifest.FilesChanged).IsEqualTo(manifest.Paths.Count);
    }

    [Test]
    public async Task The_manifest_records_the_commit_it_is_measured_from_and_the_head_it_was_taken_at()
    {
        // THE OLD ASSERTION, NARROWED RATHER THAN DELETED. It used to say "which
        // two commits it is between", which was true of an instrument that
        // measured between two commits. The half that survives unchanged is the
        // one that mattered: a manifest that does not say what it is a diff FROM
        // is one nobody can check. What is added is the head - the tree's last
        // commit, which is the committed part of what was measured.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.BaseCommit).IsEqualTo(fixture.MainCommit)
            .Because("a manifest that does not say what it is a diff FROM is one nobody can check.");
        await Assert.That(manifest.HeadCommit).IsEqualTo(tree.HeadCommit);
        await Assert.That(manifest.BaseCommit).IsEqualTo(manifest.HeadCommit)
            .Because("the agent committed nothing, so the whole of this change is uncommitted - "
                   + "which is exactly the state the old two-commit diff could not see.");
    }

    [Test]
    public async Task A_commit_the_agent_made_mid_loop_is_still_inside_the_change()
    {
        // The case tree-vs-HEAD would get wrong, and the reason the base is a
        // commit rather than the tree's own head: an agent that commits does not
        // thereby remove its work from the change being proposed.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        GitFixture.Run(tree.Path, "add", "src/Added.cs");
        GitFixture.Run(tree.Path, "commit", "-m", "the agent committed half of it");

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Paths.Select(p => p.Path)).Contains("src/Added.cs")
            .Because("committed and uncommitted work are both the change being proposed.");
        await Assert.That(manifest.Paths.Select(p => p.Path)).Contains("src/Program.cs");
        await Assert.That(manifest.BaseCommit).IsEqualTo(fixture.MainCommit)
            .Because("the base is where the flight started, not wherever the agent left HEAD.");
    }

    [Test]
    public async Task A_loop_that_changed_nothing_produces_an_empty_manifest_and_that_is_correct()
    {
        // THE POSITIVE CONTROL. An empty manifest has to be reachable and right,
        // or "the manifest reports the agent's work" is satisfied by an
        // instrument that reports everything it can find.
        using var fixture = new DiffFixture(agentWorks: false);
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Paths).IsEmpty();
        await Assert.That(manifest.FilesChanged).IsEqualTo(0);
        await Assert.That(GitFixture.Run(tree.Path, "status", "--porcelain")).IsEmpty()
            .Because("the tree really is clean, so the empty manifest is a measurement "
                   + "rather than a failure to measure.");
    }

    [Test]
    public async Task Languages_are_broken_down_by_what_the_extension_says()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Languages.Select(l => l.Language)).Contains("csharp");
        await Assert.That(manifest.Languages.Sum(l => l.Files)).IsEqualTo(manifest.FilesChanged);
    }

    [Test]
    public async Task Every_path_carries_the_classification_the_rules_gave_it()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

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
        var tree = await AWorkedTreeAsync(fixture, trees);

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

    [Test]
    public async Task Nothing_the_extractor_does_is_visible_in_the_tree_it_measured()
    {
        // The tree may be HANDED TO A PERSON when a flight does not land, and it
        // is the customer's working copy. Measuring it must not stage anything,
        // move HEAD, or leave a file behind - a `git add` here would show up as
        // somebody else's staged changes in a tree they are about to take over.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

        var before = GitFixture.Run(tree.Path, "status", "--porcelain");
        var head = GitFixture.Run(tree.Path, "rev-parse", "HEAD");

        ChangeExtractor.Extract(tree, ClassificationRules.Default);

        await Assert.That(GitFixture.Run(tree.Path, "status", "--porcelain")).IsEqualTo(before);
        await Assert.That(GitFixture.Run(tree.Path, "rev-parse", "HEAD")).IsEqualTo(head);
    }

    // ---- resolution, when the manifest would not fit ----

    [Test]
    public async Task A_manifest_that_fits_stays_at_file_resolution()
    {
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();
        var tree = await AWorkedTreeAsync(fixture, trees);

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
        var tree = await AWorkedTreeAsync(fixture, trees);

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
        var tree = await AWorkedTreeAsync(fixture, trees);

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
        var tree = await AWorkedTreeAsync(fixture, trees);

        var manifest = ChangeExtractor.Extract(tree, ClassificationRules.Default)!;

        await Assert.That(manifest.Directories.Sum(d => d.Files)).IsEqualTo(manifest.FilesChanged);
        await Assert.That(manifest.Directories.Sum(d => d.LinesAdded)).IsEqualTo(manifest.LinesAdded);
    }
}
