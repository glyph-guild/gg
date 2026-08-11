using Gg.Contracts;

namespace Gg.Runner.Vcs;

/// <summary>One repository, on disk, at a known commit.</summary>
public sealed record Materialized
{
    public required string Slug { get; init; }

    public required string Path { get; init; }

    public required string RequestedRef { get; init; }

    public required string ResolvedRef { get; init; }

    public required string HeadCommit { get; init; }

    /// <summary>The commit the change is measured from, when there is one.</summary>
    public string? BaseCommit { get; init; }

    /// <summary>Whether the head belongs to a fork rather than to the base repository.</summary>
    public required bool HeadIsFork { get; init; }

    /// <summary>Whose fork, when the adapter could say.</summary>
    public string? ForkSlug { get; init; }

    public required int FileCount { get; init; }

    public required long Bytes { get; init; }
}

/// <summary>
/// Resolve, then clone, and never the other way round.
/// </summary>
/// <remarks>
/// <para>
/// The order is the whole point of the capability declaration. A repository
/// whose pull requests cannot be fetched from the base is a <b>declared
/// capability gap</b>, refused here with the capability named - not a clone
/// that fails somewhere in a network stack with a message about a ref nobody
/// can map back to a decision.
/// </para>
/// <para>
/// Nothing in here interprets the credential. It is handed through to the
/// adapter, which puts it in a child process's environment and never in an
/// argument.
/// </para>
/// </remarks>
public sealed class Materializer(IVcsAdapter adapter, WorkingTreeRoot trees)
{
    private readonly IVcsAdapter _adapter = adapter;
    private readonly WorkingTreeRoot _trees = trees;

    public async Task<Materialized> MaterializeAsync(
        string flightId, RepoTarget target, string? secret, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Before anything is fetched, and before a directory is made.
        var resolution = _adapter.Resolve(target.PinnedRef);
        if (resolution is RefResolution.Unsupported(var capability, var diagnosis))
        {
            throw new VcsCapabilityException(capability, diagnosis);
        }

        var resolved = (RefResolution.Ref)resolution;

        // The adapter's own resolution may already know the origin; the local
        // adapter can only answer for a specific repository on this disk, so it
        // is asked separately. Either way the fact records what was found.
        var origin = resolved.ForkOrigin
            ?? (_adapter is LocalVcsAdapter ? LocalVcsAdapter.OriginOf(target.Slug, target.PinnedRef) : null);

        var directory = _trees.Prepare(flightId, target.Slug);

        var outcome = await _adapter.CloneAsync(
            target, resolved.Value, directory, secret, cancellationToken);

        // The base, when the flight named one. Fetched as a second shallow ref
        // into the same tree: there is no common ancestor on this disk to find
        // a merge base from, so what a manifest can honestly describe is the
        // difference between two commits.
        string? baseCommit = null;
        if (target.BaseRef is { Length: > 0 } baseRef
            && _adapter.Resolve(baseRef) is RefResolution.Ref(var resolvedBase, _))
        {
            baseCommit = await _adapter.FetchAlsoAsync(
                target, resolvedBase, directory, secret, cancellationToken);
        }

        return new Materialized
        {
            Slug = target.Slug,
            Path = directory,
            RequestedRef = target.PinnedRef,
            ResolvedRef = resolved.Value,
            HeadCommit = outcome.HeadCommit,
            BaseCommit = baseCommit,
            HeadIsFork = origin is not null,
            ForkSlug = origin?.Slug,
            FileCount = outcome.FileCount,
            Bytes = outcome.Bytes,
        };
    }
}
