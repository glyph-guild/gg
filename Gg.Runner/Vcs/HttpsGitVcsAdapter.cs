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
/// on the base. The key <c>local</c> takes a filesystem ROOT rather than a
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
    public const string NoPullRequestHeads = "!nopr";

    /// <summary>The adapters this environment describes.</summary>
    public static IReadOnlyList<IVcsAdapter> FromEnvironment(string? declaration = null)
    {
        var raw = declaration ?? Environment.GetEnvironmentVariable(HostsVariable) ?? "";
        var adapters = new List<IVcsAdapter>();

        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('=', 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                // Article XI. A malformed entry is not silently skipped: a
                // runner that quietly serves fewer providers than somebody
                // configured fails much later, on one flight, for a reason
                // nothing connects back to a typo in a variable.
                throw new InvalidOperationException(
                    $"{HostsVariable} entry '{entry}' is not key=host. Expected a comma-separated list "
                  + $"like 'forge=forge.example.com', with '{NoPullRequestHeads}' appended to a host "
                  + "that does not publish pull-request heads on the base repository.");
            }

            var servesPullRequests = !parts[1].EndsWith(NoPullRequestHeads, StringComparison.Ordinal);
            var host = servesPullRequests ? parts[1] : parts[1][..^NoPullRequestHeads.Length];

            // The filesystem provider takes a ROOT rather than a host, and
            // bounds itself to it. Same variable because it is the same
            // question - which providers does this runner serve, and where.
            if (parts[0] == LocalVcsAdapter.ProviderKey)
            {
                adapters.Add(new LocalVcsAdapter(host));
                continue;
            }

            adapters.Add(new HttpsGitVcsAdapter(parts[0], host, new VcsCapabilities
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
