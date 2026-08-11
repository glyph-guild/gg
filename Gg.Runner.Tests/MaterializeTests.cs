using System.Reflection;
using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>A tree root of this test's own, under the temp directory.</summary>
internal sealed class ScratchTreeRoot : IDisposable
{
    internal WorkingTreeRoot Root { get; }

    internal ScratchTreeRoot() =>
        Root = new WorkingTreeRoot(Path.Combine(
            Path.GetTempPath(), "gg-tree-tests", Guid.NewGuid().ToString("n")));

    public void Dispose()
    {
        if (Directory.Exists(Root.Path))
        {
            Directory.Delete(Root.Path, recursive: true);
        }
    }
}

/// <summary>
/// Materialize: a pinned ref, an ephemeral tree, and nothing left behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the step that puts a customer's source code on disk.</b>
/// Everything before it moved metadata, so this is the first place there is
/// something real to leak and the first place disk is a resource we consume in
/// somebody else's environment.
/// </para>
/// <para>
/// Every mechanical assertion here runs against the LOCAL adapter: a bare
/// repository over <c>file://</c>, no credential, no network. The https
/// adapter is the same port with a host from configuration and a credential,
/// exercised separately and more slowly.
/// </para>
/// </remarks>
public class MaterializeTests
{
    private static RepoTarget ABranch(GitFixture fixture) => new()
    {
        Provider = LocalVcsAdapter.ProviderKey,
        Slug = fixture.BarePath,
        PinnedRef = "refs/heads/main",
    };

    private static RepoTarget APullRequest(GitFixture fixture) => new()
    {
        Provider = LocalVcsAdapter.ProviderKey,
        Slug = fixture.BarePath,
        PinnedRef = $"refs/pull/{GitFixture.PullNumber}/head",
    };

    /// <summary>
    /// A materializer rooted at the fixture, which is the whole allowed subtree.
    /// </summary>
    /// <remarks>
    /// Rooted rather than unbounded, because that is the only form this adapter
    /// has: the slug is a path and the control plane supplies slugs, so a
    /// runner agrees to one subtree or to none.
    /// </remarks>
    private static Materializer Build(GitFixture fixture, ScratchTreeRoot trees) =>
        new(new LocalVcsAdapter(fixture.Directory), trees.Root);

    [Test]
    public async Task A_branch_materializes_at_the_pinned_ref()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", ABranch(fixture), secret: null);

        await Assert.That(materialized.HeadCommit).IsEqualTo(fixture.BranchCommit);
        await Assert.That(File.Exists(Path.Combine(materialized.Path, "README.md"))).IsTrue();
    }

    [Test]
    public async Task A_pull_request_head_materializes_the_fork_commit_from_the_base()
    {
        // The decision that removes a whole class of problem. The fork's head
        // is served by the BASE repository, so this works identically for forks
        // and branches - and the runner holds no credential for the fork,
        // because it never speaks to it.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", APullRequest(fixture), secret: null);

        await Assert.That(materialized.HeadCommit).IsEqualTo(fixture.ForkHeadCommit)
            .Because("cloning the base and reporting its branch head would be a false fact about "
                   + "which commit was examined, which this design treats as unrecoverable.");
        await Assert.That(File.Exists(Path.Combine(materialized.Path, "CHANGED.md"))).IsTrue();
    }

    [Test]
    public async Task The_fork_head_is_not_the_branch_head()
    {
        // Guards the test above. If the fixture's two commits were the same,
        // materializing the wrong one would still pass.
        using var fixture = new GitFixture();

        await Assert.That(fixture.ForkHeadCommit).IsNotEqualTo(fixture.BranchCommit);
    }

    [Test]
    public async Task Materializing_a_pull_request_records_that_it_was_a_fork_and_from_whom()
    {
        // Provenance is what a when: condition will want later, and it is free
        // to capture now.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", APullRequest(fixture), secret: null);

        await Assert.That(materialized.HeadIsFork).IsTrue();
        await Assert.That(materialized.ForkSlug).IsEqualTo(GitFixture.ForkSlug);
    }

    [Test]
    public async Task Materializing_a_branch_records_that_it_was_not_a_fork()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", ABranch(fixture), secret: null);

        await Assert.That(materialized.HeadIsFork).IsFalse();
        await Assert.That(materialized.ForkSlug).IsNull();
    }

    [Test]
    public async Task A_ref_the_adapter_cannot_resolve_is_a_capability_gap_rather_than_a_clone_error()
    {
        // Named before anything is fetched. A clone that fails at the network
        // for a reason the adapter already knew is a support call about the
        // wrong thing entirely.
        var resolution = new NoPullRequestsAdapter().Resolve($"refs/pull/{GitFixture.PullNumber}/head");

        var gap = resolution as RefResolution.Unsupported;
        await Assert.That(gap).IsNotNull();
        await Assert.That(gap!.Capability).IsEqualTo(nameof(VcsCapabilities.PullRequestHeadsFromBase));
        await Assert.That(gap.Diagnosis).IsNotEmpty();
    }

    [Test]
    public async Task A_capability_gap_never_reaches_the_network()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var adapter = new NoPullRequestsAdapter();

        await Assert.That(async () => await new Materializer(adapter, trees.Root)
                .MaterializeAsync("flight-1", APullRequest(fixture) with { Provider = "nopr" }, secret: null))
            .Throws<VcsCapabilityException>();

        await Assert.That(adapter.CloneAttempts).IsEqualTo(0)
            .Because("the adapter declared it could not do this; trying anyway is how a declared gap "
                   + "becomes a network error nobody can trace back to a capability.");
    }

    [Test]
    public async Task The_adapter_declares_what_it_can_do_rather_than_being_asked_to_try()
    {
        // refs/pull/<n>/head is one forge's convention. Another spells it
        // differently or not at all, so ref resolution lives behind the port as
        // a declared capability from the first adapter rather than the second.
        await Assert.That(new LocalVcsAdapter("/").Capabilities.PullRequestHeadsFromBase).IsTrue();
        await Assert.That(new NoPullRequestsAdapter().Capabilities.PullRequestHeadsFromBase).IsFalse();
        await Assert.That(new LocalVcsAdapter("/").Capabilities.RefScheme).IsNotEmpty()
            .Because("a capability nobody can read is a capability nobody checks.");
    }

    // ---- the tree is ephemeral, and disk is somebody else's ----

    [Test]
    public async Task The_tree_lives_under_the_known_root_keyed_by_flight()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", ABranch(fixture), secret: null);

        await Assert.That(materialized.Path).StartsWith(trees.Root.Path);
        await Assert.That(materialized.Path).Contains("flight-1")
            .Because("the sweep keys on the flight, so the flight has to be in the path.");
    }

    [Test]
    public async Task Releasing_a_flight_removes_its_tree()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", ABranch(fixture), secret: null);
        trees.Root.Release("flight-1");

        await Assert.That(Directory.Exists(materialized.Path)).IsFalse();
    }

    [Test]
    public async Task A_startup_sweep_removes_what_a_previous_life_left_behind()
    {
        // A SIGKILL mid-clone leaves a tree. This is the runner's own
        // reconciliation problem and it takes the same shape as the ready
        // queue: the reliable path, made fast enough. A runner starting up
        // holds no lease, so everything under the root is from a previous life.
        using var trees = new ScratchTreeRoot();
        var orphan = Path.Combine(trees.Root.Path, "flight-from-a-dead-runner", "repo");
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(orphan, "half-a-clone"), "x");

        var swept = trees.Root.SweepOrphans();

        await Assert.That(swept).IsEqualTo(1);
        await Assert.That(Directory.Exists(orphan)).IsFalse();
        await Assert.That(Directory.Exists(trees.Root.Path)).IsTrue()
            .Because("the root itself survives; it is the trees under it that are orphans.");
    }

    [Test]
    public async Task Sweeping_an_empty_root_removes_nothing_and_says_so()
    {
        using var trees = new ScratchTreeRoot();

        await Assert.That(trees.Root.SweepOrphans()).IsEqualTo(0);
    }

    [Test]
    public async Task Trees_live_under_a_cache_directory_rather_than_beside_the_credentials()
    {
        // Disk is the first resource this product consumes in a customer's
        // environment. A cache directory is the one place an operating system
        // and an operator both already understand to be removable.
        await Assert.That(WorkingTreeRoot.DefaultPath()).Contains("good-grief");
        await Assert.That(WorkingTreeRoot.DefaultPath().ToLowerInvariant()).Contains("cache");
    }

    [Test]
    public async Task Materialize_reports_how_much_disk_it_used()
    {
        // Nobody has thought about disk yet, and the first step towards
        // thinking about it is measuring it on the path that consumes it.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", ABranch(fixture), secret: null);

        await Assert.That(materialized.Bytes).IsGreaterThan(0);
        await Assert.That(materialized.FileCount).IsGreaterThan(0);
    }

    // ---- read-only, structurally ----

    [Test]
    public async Task The_vcs_port_has_no_write_operation()
    {
        // Asserted against the PORT's surface, not its call sites - the same
        // rule the provider adapter follows. Adding a write path is a scope
        // change, and it should fail here rather than pass review.
        string[] writeWords = ["push", "commit", "write", "create", "update", "delete", "merge", "tag"];

        var offenders = typeof(IVcsAdapter)
            .GetMethods()
            .Where(m => writeWords.Any(w => m.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("repo access is read-only and there is no write path. Found: "
                   + string.Join(", ", offenders));
        await Assert.That(typeof(IVcsAdapter).GetMethods()).IsNotEmpty();
    }

    [Test]
    public async Task The_poison_twin_the_surface_check_would_see_a_write()
    {
        // The absence above is also what a reflection walk over an empty
        // interface returns.
        string[] writeWords = ["push", "commit", "write", "create", "update", "delete", "merge", "tag"];

        var found = typeof(IWouldPush)
            .GetMethods()
            .Where(m => writeWords.Any(w => m.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Name)
            .ToList();

        await Assert.That(found).Contains("PushAsync");
    }

    private interface IWouldPush
    {
        Task PushAsync(string branch);
    }

    [Test]
    public async Task A_materialized_tree_has_no_remote_to_push_to()
    {
        // Belt and braces on top of the surface check: even a hand-typed git
        // push inside the tree has nowhere to go, and a credential helper
        // configured by the fetch does not outlive it.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();

        var materialized = await Build(fixture, trees).MaterializeAsync("flight-1", ABranch(fixture), secret: null);

        var remotes = GitFixture.Run(materialized.Path, "remote").Trim();

        await Assert.That(remotes).IsEmpty()
            .Because("a tree that knows where it came from is a tree something can push back to.");
    }

    [Test]
    public async Task No_secret_reaches_the_argument_list_of_any_git_command()
    {
        // The real risk, and the testable one: argv is readable by every
        // process on the machine, and a token in it is a token in ps output
        // before any code of ours could redact it. The environment of a child
        // we spawned is not.
        var plan = GitInvocation.Fetch(
            url: "https://example.invalid/acme/widgets.git",
            resolvedRef: "refs/pull/7/head",
            secret: "ghp-THE-SECRET-VALUE-nobody-should-see");

        foreach (var argument in plan.Arguments)
        {
            await Assert.That(argument).DoesNotContain("ghp-THE-SECRET-VALUE-nobody-should-see");
        }

        await Assert.That(plan.Arguments).IsNotEmpty();
        await Assert.That(plan.Environment.Values).Contains("ghp-THE-SECRET-VALUE-nobody-should-see")
            .Because("if the plan carried the secret nowhere at all, the absence above would be vacuous.");
    }

    [Test]
    public async Task A_fetch_with_no_secret_configures_no_credential_helper()
    {
        // The local adapter never has one. A helper configured with nothing
        // behind it would prompt, and a runner that prompts hangs forever.
        var plan = GitInvocation.Fetch(
            url: "file:///somewhere/base.git", resolvedRef: "refs/heads/main", secret: null);

        await Assert.That(plan.Environment).IsEmpty();
        await Assert.That(string.Join(' ', plan.Arguments)).DoesNotContain("credential.helper");
    }
}

/// <summary>An adapter that cannot serve pull-request heads, and says so.</summary>
/// <remarks>
/// Stands in for the second provider. Not every forge publishes
/// <c>refs/pull/&lt;n&gt;/head</c>, and the point of declaring the capability
/// from the FIRST adapter is that the second one does not have to change the
/// port to arrive.
/// </remarks>
internal sealed class NoPullRequestsAdapter : IVcsAdapter
{
    internal int CloneAttempts { get; private set; }

    public string Provider => "nopr";

    public VcsCapabilities Capabilities { get; } = new()
    {
        PullRequestHeadsFromBase = false,
        RefScheme = "branches and tags only",
    };

    public RefResolution Resolve(string pinnedRef) =>
        pinnedRef.StartsWith("refs/pull/", StringComparison.Ordinal)
            ? new RefResolution.Unsupported(
                nameof(VcsCapabilities.PullRequestHeadsFromBase),
                $"This provider does not publish pull-request heads on the base repository, so "
              + $"'{pinnedRef}' cannot be fetched without a credential for the head's own repository.")
            : new RefResolution.Ref(pinnedRef, ForkOrigin: null);

    public Task<CloneOutcome> CloneAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        CloneAttempts++;
        throw new InvalidOperationException("Nothing should have got this far.");
    }
}
