using Gg.Contracts;
using Gg.Runner;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A filesystem-rooted provider, configured like any other and bounded.
/// </summary>
/// <remarks>
/// <para>
/// A repository reachable over <c>file://</c> is a real deployment shape - an
/// air-gapped mirror, a bind-mounted checkout - and it is also the only way an
/// end-to-end test can materialize something real without a network or a
/// credential.
/// </para>
/// <para>
/// <b>It is bounded by a configured root, and that is the point.</b> The slug
/// for this provider is a PATH, and the control plane supplies slugs. Without
/// a root a compromised control plane could name any directory on the runner's
/// disk and have it cloned into a tree - so the deployment says which subtree
/// is fair game, and anything outside it is refused before a fetch.
/// </para>
/// </remarks>
public class LocalProviderConfigurationTests
{
    [Test]
    public async Task A_filesystem_root_becomes_an_adapter_for_the_local_key()
    {
        using var fixture = new GitFixture();

        var adapters = VcsConfiguration.FromEnvironment($"local={fixture.Directory}");

        await Assert.That(adapters.Select(a => a.Provider)).IsEquivalentTo(
            (string[])[LocalVcsAdapter.ProviderKey]);
        await Assert.That(adapters[0].Capabilities.PullRequestHeadsFromBase).IsTrue();
    }

    [Test]
    public async Task A_configured_root_materializes_a_repository_under_it()
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var workspace = new Workspace(
            VcsConfiguration.FromEnvironment($"local={fixture.Directory}"), trees.Root);

        var prepared = await workspace.PrepareAsync(
            "flight-1",
            [new LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            }],
            new Dictionary<string, string>());

        await Assert.That(prepared.Trees.Single().HeadCommit).IsEqualTo(fixture.BranchCommit);
    }

    [Test]
    public async Task A_path_outside_the_configured_root_is_refused_before_anything_is_fetched()
    {
        // The slug is a path and the control plane supplies slugs. A runner is
        // the least-trusted thing in its environment, and so is whatever is
        // telling it what to clone.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var workspace = new Workspace(
            VcsConfiguration.FromEnvironment($"local={Path.Combine(fixture.Directory, "allowed")}"),
            trees.Root);

        var refusal = await Assert.That(async () => await workspace.PrepareAsync(
                "flight-1",
                [new LeaseRepoRef
                {
                    Provider = LocalVcsAdapter.ProviderKey,
                    Slug = fixture.BarePath,
                    PinnedRef = "refs/heads/main",
                }],
                new Dictionary<string, string>()))
            .Throws<VcsCapabilityException>();

        await Assert.That(refusal!.Message).Contains("outside");
    }

    [Test]
    public async Task A_traversal_out_of_the_root_is_refused_too()
    {
        // The obvious way round a prefix check, and the reason the comparison
        // is on the RESOLVED path rather than on the string somebody sent.
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var workspace = new Workspace(
            VcsConfiguration.FromEnvironment($"local={fixture.Directory}"), trees.Root);

        await Assert.That(async () => await workspace.PrepareAsync(
                "flight-1",
                [new LeaseRepoRef
                {
                    Provider = LocalVcsAdapter.ProviderKey,
                    Slug = Path.Combine(fixture.Directory, "..", "..", "etc"),
                    PinnedRef = "refs/heads/main",
                }],
                new Dictionary<string, string>()))
            .Throws<VcsCapabilityException>();
    }

    [Test]
    public async Task An_unrooted_local_adapter_serves_nothing()
    {
        // The default. A runner nobody configured for a filesystem provider
        // must not quietly accept one, because the empty root would otherwise
        // read as "anywhere".
        using var fixture = new GitFixture();
        var unrooted = new LocalVcsAdapter();

        await Assert.That(unrooted.Resolve("refs/heads/main")).IsTypeOf<RefResolution.Ref>()
            .Because("resolution is about the ref; the root bounds the clone.");

        using var trees = new ScratchTreeRoot();
        await Assert.That(async () => await unrooted.CloneAsync(
                new RepoTarget
                {
                    Provider = LocalVcsAdapter.ProviderKey,
                    Slug = fixture.BarePath,
                    PinnedRef = "refs/heads/main",
                },
                "refs/heads/main", trees.Root.Path, secret: null))
            .Throws<VcsCapabilityException>();
    }
}
