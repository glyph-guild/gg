using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// this provider spells a repository, a proposal and a link differently, and
/// each difference was measured against the real service before it was encoded.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three differences, not two.</b> A probe opened and abandoned a real pull
/// request on a real project, and the third one was a surprise:
/// </para>
/// <list type="bullet">
/// <item>The clone url is <c>{host}/{project}/_git/{repo}</c>, and
/// <b><c>.git</c> is rejected</b> — <c>HttpsGitVcsAdapter</c> hardcodes that
/// suffix, so this could never have been a slug spelled differently.</item>
/// <item>A proposal is <c>repositories/{repo}/pullrequests</c> with
/// <c>sourceRefName</c>/<c>targetRefName</c>, against
/// <c>repos/{slug}/pulls</c> with <c>head</c>/<c>base</c>.</item>
/// <item><b>There is no human-facing url in the response.</b> the other convention returns
/// <c>html_url</c>; this provider returns <c>url</c> — an api address full of
/// guids — and ten <c>_links</c> members, none of them <c>web</c>.</item>
/// </list>
/// <para>
/// <b>That third one makes the adapter compose a url</b>, which is the move this
/// project keeps refusing. It is unavoidable here — the provider does not return
/// the value — so it happens inside this adapter, from two members the response
/// really gives, and never in shared code. <c>Landed</c>'s contract is unchanged:
/// a url a person can open. Only who builds it moves.
/// </para>
/// <para>
/// <b>The org lives in the host declaration, not in the slug.</b>
/// <c>RepositoryEntry.Path</c> is "the display path a flight's intent names",
/// and an organisation is deployment knowledge — the same argument that keeps
/// hosts out of policy documents. So <c>GG_VCS_HOSTS</c> carries
/// <c>{host}/{org}</c> and a flight names <c>{project}/{repo}</c>.
/// </para>
/// </remarks>
public class PathScopedAdapterTests
{
    private const string Host = "forge.example/an-org";

    private static PathScopedGitVcsAdapter Adapter() =>
        new("forge.example", Host);

    // ---- the read side ----

    [Test]
    public async Task The_clone_url_carries_no_git_suffix()
    {
        // MEASURED, not assumed: `.git` on an this provider clone url is refused
        // by the service, which is why this is a second adapter rather than a
        // slug spelled differently.
        var url = Adapter().CloneUrlFor(new RepoTarget
        {
            Provider = "forge.example",
            Slug = "a-project/a-repo",
            PinnedRef = "refs/heads/main",
        });

        await Assert.That(url).IsEqualTo("https://forge.example/an-org/a-project/_git/a-repo");
        await Assert.That(url).DoesNotEndWith(".git")
            .Because("the service rejects it, and HttpsGitVcsAdapter appends it unconditionally.");
    }

    [Test]
    public async Task It_declares_that_it_does_not_serve_pull_request_heads()
    {
        // this provider publishes refs/pull/<id>/merge; the base-repository head
        // convention is the other convention's. Declared rather than guessed, so a flight
        // about a pull request is refused by name instead of failing at git.
        await Assert.That(Adapter().Capabilities.PullRequestHeadsFromBase).IsFalse();

        var resolution = Adapter().Resolve("refs/pull/7/head");

        await Assert.That(resolution).IsTypeOf<RefResolution.Unsupported>();
        await Assert.That(((RefResolution.Unsupported)resolution).Diagnosis)
            .Contains("forge.example")
            .Because("a capability gap names the provider it belongs to, or a person reads it "
                   + "as a statement about every provider.");
    }

    [Test]
    public async Task An_ordinary_ref_still_resolves()
    {
        // The half a capability declaration threatens: refusing what it does
        // serve. `refs/heads/main` is what every this provider flight is about.
        await Assert.That(Adapter().Resolve("refs/heads/main"))
            .IsTypeOf<RefResolution.Ref>();
    }

    // ---- the seam that lets it be registered ----

    [Test]
    public async Task A_second_read_adapter_registers_through_the_same_variable()
    {
        // The mirror of what slice nineteen did for destinations.
        // VcsConfiguration named the class it built, so a second read adapter
        // could be dispatched to and never wired.
        var adapters = VcsConfiguration.FromEnvironment(
            "forge.example=forge.example/an-org",
            adapterFor: (provider, host, _) => new PathScopedGitVcsAdapter(provider, host));

        await Assert.That(adapters.Single()).IsTypeOf<PathScopedGitVcsAdapter>();
        await Assert.That(adapters.Single().Provider).IsEqualTo("forge.example");
    }

    [Test]
    public async Task Production_still_gets_the_adapter_it_always_did()
    {
        // Every runner anybody has configured takes this path.
        var adapters = VcsConfiguration.FromEnvironment("forge=forge.example.com");

        await Assert.That(adapters.Single()).IsTypeOf<HttpsGitVcsAdapter>();
    }

    [Test]
    public async Task The_local_provider_is_still_special_and_still_first()
    {
        // A factory must not take precedence over the filesystem provider,
        // which takes a ROOT rather than a host and bounds itself to it.
        var adapters = VcsConfiguration.FromEnvironment(
            "local=/tmp/roots",
            adapterFor: (provider, host, _) => new PathScopedGitVcsAdapter(provider, host));

        await Assert.That(adapters.Single()).IsTypeOf<LocalVcsAdapter>()
            .Because("`local` is answered before any factory, or a runner configured for a "
                   + "bare repository on disk starts talking to a forge.");
    }

    // ---- the write side, and the url nobody returns ----

    [Test]
    public async Task The_proposal_url_is_composed_because_the_provider_returns_none()
    {
        // THE THIRD DIFFERENCE, and the one that was a surprise. this provider
        // returns `url` - an api address full of guids - and ten `_links`
        // members, none of them `web`. A person cannot open any of them.
        //
        // Composed from two members the response DOES give, inside this
        // adapter, because the alternative is Landed carrying something nobody
        // can click.
        var link = RefNamedDestinationAdapter.ProposalUrl(
            repositoryWebUrl: "https://forge.example/an-org/a-project/_git/a-repo",
            pullRequestId: 8473);

        await Assert.That(link)
            .IsEqualTo("https://forge.example/an-org/a-project/_git/a-repo/pullrequest/8473");
    }

    [Test]
    public async Task A_proposal_that_describes_no_repository_is_refused_rather_than_guessed()
    {
        // The composition needs `repository.webUrl`. Absent, there is nothing
        // honest to build from - and a url assembled from an api address with
        // guids in it would look like a link and open nothing.
        await Assert.That(() => RefNamedDestinationAdapter.ProposalUrl(
                repositoryWebUrl: "", pullRequestId: 8473))
            .Throws<InvalidOperationException>();
    }
}
