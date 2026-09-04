namespace Gg.Runner.Vcs;

/// <summary>
/// Git over https, against whichever host the deployment names.
/// </summary>
/// <remarks>
/// <para>
/// <b>No provider is named in this binary.</b> gg is public and distributed,
/// and which forge a tenant uses is the control plane's knowledge, not ours:
/// the lease carries a provider key, the deployment maps that key to a host,
/// and this adapter speaks git to it. When a second forge arrives, nothing
/// here changes - which is the same property the identity port has, for the
/// same reason.
/// </para>
/// <para>
/// The capabilities are supplied rather than assumed, because they differ by
/// forge and this class cannot know which one it is pointed at. The one that
/// matters is whether the BASE repository serves pull-request heads: where it
/// does, a fork's head is fetched from the base and <b>the runner needs no
/// credential for the fork at all</b>, because it never speaks to it.
/// </para>
/// <para>
/// A provider key the deployment has no host for is a declared capability gap,
/// refused before anything is fetched. That is the same path a forge which
/// cannot serve pull-request heads takes, and it is a much better answer than
/// a clone that fails at DNS.
/// </para>
/// </remarks>
public sealed class HttpsGitVcsAdapter(string provider, string host, VcsCapabilities capabilities)
    : IVcsAdapter
{
    private readonly string _host = host;

    public string Provider { get; } = provider;

    public VcsCapabilities Capabilities { get; } = capabilities;

    /// <summary>
    /// Passes a ref through, and refuses anything that is not one.
    /// </summary>
    /// <remarks>
    /// A pull-request ref is refused unless the deployment declared that this
    /// forge serves them. That declaration is the whole point of the
    /// capability: <c>refs/pull/&lt;n&gt;/head</c> is one forge's convention
    /// and others spell it differently or not at all, so the answer comes from
    /// configuration rather than from a guess made here.
    /// </remarks>
    public RefResolution Resolve(string pinnedRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pinnedRef);

        if (!pinnedRef.StartsWith("refs/", StringComparison.Ordinal))
        {
            return new RefResolution.Unsupported(
                nameof(VcsCapabilities.RefScheme),
                $"'{pinnedRef}' is not a ref. This adapter fetches {Capabilities.RefScheme}.");
        }

        if (pinnedRef.StartsWith("refs/pull/", StringComparison.Ordinal)
            && !Capabilities.PullRequestHeadsFromBase)
        {
            return new RefResolution.Unsupported(
                nameof(VcsCapabilities.PullRequestHeadsFromBase),
                $"'{Provider}' is not declared to publish pull-request heads on the base repository, "
              + $"so '{pinnedRef}' cannot be fetched without a credential for the head's own "
              + "repository - which this design deliberately never asks for.");
        }

        return new RefResolution.Ref(pinnedRef, ForkOrigin: null);
    }

    public Task<CloneOutcome> CloneAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        // The BASE repository. A fork's head is served from here too, which is
        // the decision that removes a whole class of problem.
        return GitWorkingTree.FetchAsync(
            $"https://{_host}/{target.Slug}.git", resolvedRef, intoDirectory, secret, cancellationToken);
    }

    /// <summary>Brings the base ref onto the same disk, so a diff has two points.</summary>
    public Task<string> FetchAlsoAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        return GitWorkingTree.FetchAlsoAsync(
            $"https://{_host}/{target.Slug}.git", resolvedRef, intoDirectory, secret, cancellationToken);
    }
}

/// <summary>
/// Which provider keys this runner can serve, and where they live.
/// </summary>
/// <remarks>
/// <para>
/// Read from the environment because it is deployment knowledge: the same
/// binary runs against a public forge, a self-hosted one, and an air-gapped
/// mirror, and none of those is a code change.
/// </para>
/// <para>
/// <c>GG_VCS_HOSTS</c> is a comma-separated list of <c>key=host</c>, optionally
/// suffixed <c>!nopr</c> for a forge that does not publish pull-request heads
/// on the base, and <c>!pathscoped</c> for one that scopes repositories by path.
/// The suffixes are parsed by <see cref="HostDeclaration"/>, which is shared
/// with the landing side because both read this variable. The key <c>local</c>
/// takes a filesystem ROOT rather than a
/// host, and clones only from inside it. Absent entirely means this runner
/// serves no provider -
/// which is an honest state for a runner nobody has configured, and produces a
/// capability gap rather than a clone that fails at DNS.
/// </para>
/// </remarks>
public static class VcsConfiguration
{
    /// <summary>The variable naming which provider keys resolve where.</summary>
    public const string HostsVariable = "GG_VCS_HOSTS";

    /// <summary>Suffix on a host declaring that pull-request heads are NOT served from the base.</summary>
    /// <remarks>
    /// The declaration itself moved to <see cref="HostDeclaration"/>, where both
    /// readers of this variable share one parser. This stays because it is the
    /// spelling anybody configuring a runner already knows, and a public binary
    /// does not rename what a deployment depends on to tidy a namespace.
    /// </remarks>
    public const string NoPullRequestHeads = HostDeclaration.NoPullRequestHeads;

    /// <summary>The adapters this environment describes.</summary>
    /// <param name="adapterFor">
    /// How to build an adapter for a provider key, given its host and the
    /// capabilities its declaration implies. Omitted, the DECLARATION chooses:
    /// <see cref="PathScopedGitVcsAdapter"/> for a host declared
    /// <c>!pathscoped</c>, <see cref="HttpsGitVcsAdapter"/> otherwise.
    /// <para>
    /// <b>The mirror of the destination side's seam, and it exists for a
    /// measured reason.</b> Another provider rejects the <c>.git</c> suffix this
    /// class appends unconditionally, so a provider that spells a repository
    /// differently is a second adapter — and until this parameter, such an
    /// adapter could be dispatched to and never registered.
    /// </para>
    /// <para>
    /// <b>And then the parameter was passed only from tests.</b> Which is the
    /// same bug one layer up, so the choice moved into the default where
    /// <c>Gg.Cli</c> reaches it. A path-scoped adapter declares its own
    /// capabilities and ignores the ones computed here, which is why
    /// <c>!nopr</c> beside <c>!pathscoped</c> is redundant rather than
    /// contradictory — that forge never serves base heads.
    /// </para>
    /// </param>
    /// <summary>
    /// The host declarations this environment holds, parsed once.
    /// </summary>
    /// <remarks>
    /// <b>The same variable the adapters come from, read by the same parser.</b>
    /// The runner needs the DECLARATIONS as well as the adapters - to tell
    /// whether a flight's link comes from a host it serves, and which tracker
    /// can read a link-shaped work item - and a second reader of GG_VCS_HOSTS
    /// would be the two-computations problem this file's own history records.
    /// </remarks>
    public static IReadOnlyList<HostDeclaration> DeclaredHosts(string? declaration = null) =>
        [.. HostDeclaration.ParseAll(
            declaration ?? Environment.GetEnvironmentVariable(HostsVariable) ?? "", HostsVariable)];

    public static IReadOnlyList<IVcsAdapter> FromEnvironment(
        string? declaration = null,
        Func<string, string, VcsCapabilities, IVcsAdapter>? adapterFor = null)
    {
        var raw = declaration ?? Environment.GetEnvironmentVariable(HostsVariable) ?? "";
        var adapters = new List<IVcsAdapter>();

        // ONE PARSER, shared with the landing side. Both read this variable, and
        // each used to strip suffixes with its own copy of the same lines - which
        // held for exactly as long as there was one suffix.
        foreach (var declared in HostDeclaration.ParseAll(raw, HostsVariable))
        {
            // The filesystem provider takes a ROOT rather than a host, and
            // bounds itself to it. Same variable because it is the same
            // question - which providers does this runner serve, and where.
            if (declared.Key == LocalVcsAdapter.ProviderKey)
            {
                adapters.Add(new LocalVcsAdapter(declared.Host));
                continue;
            }

            var servesPullRequests = declared.ServesPullRequestHeads;

            // THE DECLARATION CHOOSES, and it chooses in the DEFAULT rather than
            // in the caller. Gg.Cli calls this without a factory, so putting the
            // choice here is what finally reaches the second adapter; putting it
            // in the CLI would name a forge in this binary, which is the one
            // thing PathScopedGitVcsAdapter says must not happen. The parameter
            // stays exactly what its own documentation says it is - a way for a
            // test to substitute.
            var build = adapterFor ?? (declared.IsPathScoped
                ? static (provider, host, _) => (IVcsAdapter)new PathScopedGitVcsAdapter(provider, host)
                : static (provider, host, capabilities) =>
                    new HttpsGitVcsAdapter(provider, host, capabilities));

            adapters.Add(build(declared.Key, declared.Host, new VcsCapabilities
            {
                PullRequestHeadsFromBase = servesPullRequests,
                RefScheme = servesPullRequests
                    ? "refs/heads/<branch>, refs/tags/<tag>, and refs/pull/<n>/head from the base "
                    + "repository for forks and branches alike"
                    : "refs/heads/<branch> and refs/tags/<tag>",
            }));
        }

        return adapters;
    }
}
