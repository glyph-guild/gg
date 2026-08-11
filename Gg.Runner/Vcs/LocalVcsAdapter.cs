namespace Gg.Runner.Vcs;

/// <summary>
/// A bare repository on disk, over <c>file://</c>.
/// </summary>
/// <remarks>
/// <para>
/// Not a test double. It is a real adapter over real git, and it exists so that
/// every mechanical claim about materialize - pinned refs, pull-request heads,
/// ephemeral trees, cleanup, and what does and does not leave the machine - is
/// checked against git's actual behaviour without a credential, a network, or
/// somebody else's rate limit.
/// </para>
/// <para>
/// It declares the same pull-request capability a configured forge would,
/// because a bare repository genuinely can hold
/// <c>refs/pull/&lt;n&gt;/head</c>: that is how the fork story is exercised
/// here. What it cannot do is discover a fork's owner from an API, so it reads
/// that from the repository's own config - the local stand-in for what a
/// provider's pull-request metadata says.
/// </para>
/// </remarks>
public sealed class LocalVcsAdapter : IVcsAdapter
{
    /// <summary>The provider key a lease uses to ask for this adapter.</summary>
    public const string ProviderKey = "local";

    public string Provider => ProviderKey;

    public VcsCapabilities Capabilities { get; } = new()
    {
        PullRequestHeadsFromBase = true,
        RefScheme = "refs/heads/<branch>, refs/tags/<tag>, and refs/pull/<n>/head as published "
                  + "into the bare repository",
    };

    /// <summary>
    /// Passes the ref through, and looks up a fork owner when one is recorded.
    /// </summary>
    /// <remarks>
    /// Resolution is a no-op here because a bare repository serves whatever ref
    /// it holds. The interesting half is the origin: <c>gg.pull.&lt;n&gt;.origin</c>
    /// in the repository's config stands in for the pull-request metadata a
    /// real provider answers from an API. Inventing a ref to carry it would be
    /// cleverer and less honest.
    /// </remarks>
    public RefResolution Resolve(string pinnedRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pinnedRef);

        return new RefResolution.Ref(pinnedRef, ForkOrigin: null);
    }

    /// <summary>
    /// Resolves against one specific repository, which is where the origin lives.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Resolve"/> because the port's method is pure
    /// and offline by contract - it decides capability, not facts about a
    /// particular repository on this disk.
    /// </remarks>
    internal static ForkOrigin? OriginOf(string barePath, string pinnedRef)
    {
        if (PullNumberOf(pinnedRef) is not { } number)
        {
            return null;
        }

        try
        {
            var slug = GitInvocation
                .Plain("config", "--get", $"gg.pull.{number}.origin")
                .RunAsync(barePath).GetAwaiter().GetResult().Trim();

            return slug.Length > 0 ? new ForkOrigin { Slug = slug } : null;
        }
        catch (InvalidOperationException)
        {
            // git config exits non-zero when the key is absent, which means
            // "this pull request's head is not from a fork" rather than a
            // failure. A missing origin is a fact, not an error.
            return null;
        }
    }

    /// <summary>The number in <c>refs/pull/&lt;n&gt;/head</c>, or null.</summary>
    internal static int? PullNumberOf(string pinnedRef)
    {
        const string Prefix = "refs/pull/";
        const string Suffix = "/head";

        if (!pinnedRef.StartsWith(Prefix, StringComparison.Ordinal)
            || !pinnedRef.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var middle = pinnedRef[Prefix.Length..^Suffix.Length];
        return int.TryParse(middle, out var number) ? number : null;
    }

    public async Task<CloneOutcome> CloneAsync(
        RepoTarget target, string resolvedRef, string intoDirectory, string? secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        // file:// has nothing to authenticate to, and passing a secret to it
        // would be a secret handed to a path. Refused rather than ignored.
        if (!string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException(
                "The local adapter speaks file:// and has nothing to authenticate to. "
              + "A credential offered here would be one nobody could have needed.");
        }

        return await GitWorkingTree.FetchAsync(
            new Uri(Path.GetFullPath(target.Slug)).AbsoluteUri, resolvedRef, intoDirectory,
            secret: null, cancellationToken);
    }
}
