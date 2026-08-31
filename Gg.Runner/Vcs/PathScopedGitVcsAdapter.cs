namespace Gg.Runner.Vcs;

/// <summary>
/// Reads a repository from a provider that spells one differently.
/// </summary>
/// <remarks>
/// <para>
/// <b>A second adapter, because the first one's own comment said so.</b>
/// <c>HttpsGitVcsAdapter</c> records that its path shapes are <i>one convention;
/// a provider that spells them differently is a second adapter, not a special
/// case in this one</i> — and that was tested rather than taken on trust. A
/// clone url with the <c>.git</c> suffix that class appends unconditionally is
/// <b>refused</b> by this provider, so a slug spelled differently could never
/// have worked.
/// </para>
/// <para>
/// <b>The organisation lives in the host declaration.</b>
/// <c>RepositoryEntry.Path</c> is <i>the display path a flight's intent names</i>,
/// and an organisation is deployment knowledge — the same argument that keeps
/// hosts out of policy documents at all. So <c>GG_VCS_HOSTS</c> carries
/// <c>{host}/{org}</c>, a flight names <c>{project}/{repo}</c>, and the
/// <c>_git</c> segment that belongs to this provider's spelling stays in here.
/// </para>
/// <para>
/// <b>No provider is named in this binary.</b> The class is named for a shape,
/// the key comes from configuration, and which forge a tenant uses remains the
/// control plane's knowledge.
/// </para>
/// </remarks>
public sealed class PathScopedGitVcsAdapter(string provider, string host) : IVcsAdapter
{
    private readonly string _host = host;

    public string Provider { get; } = provider;

    /// <summary>
    /// Branches and tags, and no pull-request heads.
    /// </summary>
    /// <remarks>
    /// This provider publishes <c>refs/pull/&lt;id&gt;/merge</c>; the
    /// base-repository head convention belongs to another forge. Declared
    /// rather than guessed, so a flight pinned to a pull request is refused by
    /// name instead of failing at git for a reason nothing connects back.
    /// </remarks>
    public VcsCapabilities Capabilities { get; } = new()
    {
        PullRequestHeadsFromBase = false,
        RefScheme = "refs/heads/<branch> and refs/tags/<tag>",
    };

    /// <summary>
    /// The clone url, which carries no <c>.git</c> suffix.
    /// </summary>
    /// <remarks>
    /// Public because it is the difference this class exists for, and a
    /// difference asserted through a private path is one somebody has to take
    /// on trust.
    /// </remarks>
    public string CloneUrlFor(RepoTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var slug = target.Slug.Trim('/');
        var separator = slug.IndexOf('/', StringComparison.Ordinal);

        if (separator <= 0 || separator == slug.Length - 1)
        {
            throw new VcsCapabilityException(
                "slug",
                $"'{target.Slug}' is not a repository this provider can name. It takes "
              + "<project>/<repository>; the organisation comes from the host this runner was "
              + $"configured with, which is deployment knowledge and not a flight's to state.");
        }

        return $"https://{_host}/{slug[..separator]}/_git/{slug[(separator + 1)..]}";
    }

    public RefResolution Resolve(string pinnedRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pinnedRef);

        if (!pinnedRef.StartsWith("refs/", StringComparison.Ordinal))
        {
            return new RefResolution.Unsupported(
                nameof(VcsCapabilities.RefScheme),
                $"'{pinnedRef}' is not a ref. This adapter fetches {Capabilities.RefScheme}.");
        }

        if (pinnedRef.StartsWith("refs/pull/", StringComparison.Ordinal))
        {
            return new RefResolution.Unsupported(
                nameof(VcsCapabilities.PullRequestHeadsFromBase),
                $"'{Provider}' publishes refs/pull/<id>/merge rather than a head on the base "
              + $"repository, so '{pinnedRef}' is not a ref this adapter can fetch. A flight "
              + "about a branch works; one pinned to a pull request needs a resolution this "
              + "adapter does not have.");
        }

        return new RefResolution.Ref(pinnedRef, ForkOrigin: null);
    }

    public Task<CloneOutcome> CloneAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default) =>
        GitWorkingTree.FetchAsync(
            CloneUrlFor(target), resolvedRef, intoDirectory, secret, cancellationToken);

    public Task<string> FetchAlsoAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default) =>
        GitWorkingTree.FetchAlsoAsync(
            CloneUrlFor(target), resolvedRef, intoDirectory, secret, cancellationToken);
}
