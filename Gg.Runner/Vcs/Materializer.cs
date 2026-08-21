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

    /// <summary>
    /// The commit the change is measured from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Required, and there is no state in which a tree has no base.</b> It used
    /// to be nullable and it was null on every real flight, because the member the
    /// lease carries for it is never populated - so <c>ChangeExtractor</c> refused
    /// to produce a manifest at all and no flight this product flew ever shipped
    /// one. A nullable base is a manifest that can silently fail to exist.
    /// </para>
    /// <para>
    /// It is the commit this flight CHECKED OUT, which the materializer already
    /// holds, and for a first attempt that commit is exactly where the branch was
    /// cut. A flight pinned to a branch already ahead of its destination's base
    /// needs a merge base, a merge base needs history, and the clone is
    /// <c>--depth 1</c> - so that base cannot be computed here and has to be
    /// supplied. That is a contract move and its own step.
    /// </para>
    /// </remarks>
    public required string BaseCommit { get; init; }

    /// <summary>
    /// Which diff the base makes this, from <see cref="Gg.Contracts.DiffBasis"/>.
    /// </summary>
    /// <remarks>
    /// <b>Decided where the base is decided, and carried rather than recomputed.</b> The
    /// label and the commit it names must not be able to disagree: a manifest claiming
    /// the prior-attempt basis while measuring from somewhere else is worse than one
    /// claiming two-point, because it reads as more precise. One place chooses both.
    /// </remarks>
    public required string Basis { get; init; }

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

        // THE BASE IS WHAT WAS CHECKED OUT, decided here because here is where it
        // is known. It used to be a second ref fetched from what the lease named,
        // and the lease never named one - LeaseEndpoints populates provider, slug,
        // pinned ref and continues-from, and there is no member on the control
        // plane's own FlightRepo for a base to come from. So every flight
        // materialized with no base and shipped no manifest.
        //
        // What a manifest honestly describes is the difference between this commit
        // and what is on disk now. For a first attempt this commit is where the
        // branch was cut, which is the base a reader means.
        var baseCommit = outcome.HeadCommit;
        var basis = DiffBasis.TwoPoint;

        var headCommit = outcome.HeadCommit;

        if (target.ContinuesFrom is { Length: > 0 } priorAttempt)
        {
            // ATTEMPT TWO CONTINUES, and continuing means the tree IS the prior
            // attempt. The feedback this attempt acts on references files the last
            // one pushed; its next commit has to sit on top of that work so the push
            // fast-forwards; and the manifest describes what THIS attempt did from
            // there rather than re-reporting everything already on the branch -
            // which would make the second gate louder than the first about work
            // nobody changed.
            //
            // It was fetched WITHOUT being checked out once, and the head and the
            // base disagreed: the manifest measured from the prior commit while the
            // agent worked in a tree from before the work existed, so attempt one's
            // files read as deletions and attempt two's push was a second root the
            // remote refused.
            baseCommit = await _adapter.FetchAlsoAsync(
                target, priorAttempt, directory, secret, cancellationToken);
            headCommit = baseCommit;
            basis = DiffBasis.PriorAttempt;
        }

        return new Materialized
        {
            Slug = target.Slug,
            Path = directory,
            RequestedRef = target.PinnedRef,
            ResolvedRef = resolved.Value,
            HeadCommit = headCommit,
            BaseCommit = baseCommit,
            Basis = basis,
            HeadIsFork = origin is not null,
            ForkSlug = origin?.Slug,
            FileCount = outcome.FileCount,
            Bytes = outcome.Bytes,
        };
    }
}
