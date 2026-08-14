using System.Reflection;
using System.Runtime.CompilerServices;
using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Guards on the instrument itself, so the two defects that shipped cannot ship
/// again by a different route.
/// </summary>
/// <remarks>
/// <para>
/// Both failures were invisible for the same reason: every test that read a
/// manifest was handed one, and the one test that extracted a manifest committed
/// its work first. A behavioural test alone would have to think of the case; these
/// are shaped so that a future extractor which reintroduces either failure cannot
/// pass, whatever cases anybody thinks of.
/// </para>
/// <para>
/// Each assertion names the sentence it enforces, and the sentence is the point:
/// a guard whose claim nobody wrote down is a guard nobody can narrow correctly.
/// </para>
/// </remarks>
public class ManifestInstrumentTests
{
    private static string SourceOf(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;

        return string.Join('\n', Directory
            .EnumerateFiles(Path.Combine(root, project), file, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .Select(File.ReadAllText));
    }

    /// <summary>Code with comments stripped, so a rule is not satisfied by prose about it.</summary>
    private static string CodeOf(string project, string file) =>
        string.Join('\n', SourceOf(project, file).Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("/*", StringComparison.Ordinal)));

    // ---- the sentence: a manifest is never a diff between two commits ----

    [Test]
    public async Task The_extractor_never_asks_git_to_diff_one_commit_against_another()
    {
        // THE SENTENCE THIS ENFORCES, and it is written in ChangeExtractor beside
        // the code: "The head of this diff is the WORKING TREE, never a commit."
        //
        // Structural because the behavioural twin cannot be complete. A diff of
        // base against head reports the agent's work correctly on any fixture that
        // commits first, and every fixture in this repository used to. So what is
        // asserted is that the second commit is not there to pass.
        var code = CodeOf("Gg.Runner", "ChangeExtractor.cs");

        await Assert.That(code).Contains("tree.BaseCommit")
            .Because("the diff has to name what it measures from, or this scan is vacuous.");
        await Assert.That(code).DoesNotContain("tree.HeadCommit")
            .Because("naming the head commit in a diff is the defect: it makes the manifest a "
                   + "statement about two commits, and the agent's work is in neither of them.");
        await Assert.That(code).Contains("--cached")
            .Because("the working tree reaches a diff through an index, and this is the only "
                   + "index in the story.");
    }

    [Test]
    public async Task The_index_the_extractor_builds_is_never_the_repositorys_own()
    {
        // THE SENTENCE: "The tree may be handed to a person, so measuring it
        // changes nothing in it." Staging into the real index would be the
        // cheapest way to make untracked files visible, and it would leave a
        // customer's working copy with somebody else's staged changes in it.
        var code = CodeOf("Gg.Runner", "ChangeExtractor.cs");

        await Assert.That(code).Contains("GIT_INDEX_FILE")
            .Because("a scratch index is what makes the measurement non-destructive, and its "
                   + "absence is how the destructive version would arrive.");
        await Assert.That(code).DoesNotContain("intent-to-add");
    }

    // ---- the sentence: there is no such thing as a flight with no base ----

    [Test]
    public async Task Extract_cannot_be_reached_with_no_base_because_the_type_has_no_way_to_say_it()
    {
        // THE DEFECT THAT ACTUALLY SHIPPED. Extract used to return null when
        // BaseCommit was null, which is a correct rule about a state that should
        // not exist - and the state existed on every real flight, because nothing
        // populated the lease's base ref. A guard against the wrong-diff defect
        // alone would have passed on every flight this product has ever flown.
        //
        // Enforced by the TYPE rather than by a test, so there is no case to
        // think of: the base is set where the tree is materialized, and a
        // Materialized without one does not compile.
        var property = typeof(Materialized).GetProperty(nameof(Materialized.BaseCommit))!;

        await Assert.That(new NullabilityInfoContext().Create(property).WriteState)
            .IsEqualTo(NullabilityState.NotNull)
            .Because("a nullable base is a manifest this product can silently fail to produce.");
        await Assert.That(property.GetCustomAttribute<RequiredMemberAttribute>()).IsNotNull()
            .Because("required, so it cannot be left off at a construction site either.");
        await Assert.That(CodeOf("Gg.Runner", "ChangeExtractor.cs")).DoesNotContain("return null")
            .Because("there is no longer a state in which a manifest cannot be produced.");
    }

    [Test]
    public async Task A_manifest_is_produced_for_the_shape_of_lease_the_control_plane_really_sends()
    {
        // THE LIVENESS TWIN for the guard above, and the case that shipped: a
        // lease with a provider, a slug and a pinned ref, and nothing else. This
        // used to produce no change.manifest fact at all.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();

        var tree = await new Materializer(new LocalVcsAdapter(fixture.Directory), trees.Root)
            .MaterializeAsync("flight-1", new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            }, secret: null);

        fixture.TheAgentWorks(tree.Path);

        await Assert.That(ChangeExtractor.Extract(tree, ClassificationRules.Default)).IsNotNull()
            .Because("a lease that names no base is the only shape LeaseEndpoints builds, so a "
                   + "manifest it cannot produce is a manifest that never exists.");
    }

    // ---- the sentence: a member nothing populates must be a member nothing reads ----

    [Test]
    public async Task Nothing_in_the_runner_reads_the_leases_base_ref()
    {
        // THE SENTENCE: "The lease's base ref is not populated, so nothing may
        // depend on it." A member nothing sets and something reads is a silent
        // null at the bottom of a decision, which is exactly how the manifest
        // came to be absent.
        //
        // THE TRIGGER FOR REVISITING THIS, named so it is a decision rather than
        // an omission: the base a flight is measured from is the commit it
        // checked out, which is right for a first attempt and wrong for a flight
        // pinned to a branch that was already ahead of its destination's base.
        // Computing that needs a merge base, a merge base needs history, and the
        // clone is --depth 1 - so it cannot be computed on this side at all. When
        // a flight has to be measured from somewhere the runner did not check
        // out, the base must be SUPPLIED: LeaseRepoRef.BaseRef gets populated, a
        // ledger entry records the vocabulary move, and the clone deepens or
        // fetches a second ref. That is the day this test is deleted, and it is
        // not this day.
        var code = CodeOf("Gg.Runner", "*.cs");

        await Assert.That(code).DoesNotContain("repo.BaseRef")
            .Because("copying it into a RepoTarget is how it got read.");
        await Assert.That(typeof(RepoTarget).GetProperty("BaseRef")).IsNull()
            .Because("the runner's own type must not carry a member the wire never fills.");

        // The liveness half: the OTHER BaseRef, which is a real member of a real
        // decision and must keep being read. Without this the assertion above
        // would pass on a runner that had stopped opening pull requests against
        // anything.
        await Assert.That(CodeOf("Gg.Runner", "RunnerLoop.cs")).Contains("push.BaseRef")
            .Because("a proposal opens against the ref the admission named, and that BaseRef is "
                   + "populated, read, and nothing to do with this one.");
    }

    [Test]
    public async Task The_base_is_the_commit_the_flight_checked_out()
    {
        // The behavioural half of the same sentence: the base is decided where
        // the tree is made, from what was actually put on disk.
        using var fixture = new DiffFixture();
        using var trees = new ScratchTreeRoot();

        var tree = await new Materializer(new LocalVcsAdapter(fixture.Directory), trees.Root)
            .MaterializeAsync("flight-1", new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            }, secret: null);

        await Assert.That(tree.BaseCommit).IsEqualTo(fixture.MainCommit);
        await Assert.That(tree.BaseCommit).IsEqualTo(tree.HeadCommit);
        await Assert.That(tree.Basis).IsEqualTo(DiffBasis.TwoPoint)
            .Because("one commit as the base rather than a merge base, which is what the label "
                   + "has always meant and still does.");
    }
}
