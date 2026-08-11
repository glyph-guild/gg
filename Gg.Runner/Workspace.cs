using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner;

/// <summary>What a flight's repositories became on this machine.</summary>
public sealed record WorkspaceResult(IReadOnlyList<Materialized> Trees)
{
    /// <summary>Whether this flight already had trees here before we started.</summary>
    public required bool Reused { get; init; }
}

/// <summary>
/// Puts a flight's repositories on disk, and takes them off again.
/// </summary>
/// <remarks>
/// A port because there is more than one honest implementation: a runner with
/// no adapter configured must refuse rather than silently materialize nothing,
/// and a flight that names no repository must be allowed to proceed.
/// </remarks>
public interface IWorkspace
{
    Task<WorkspaceResult> PrepareAsync(
        string flightId,
        IReadOnlyList<LeaseRepoRef> repos,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken = default);

    /// <summary>Removes everything this flight put on disk.</summary>
    void Release(string flightId);

    /// <summary>Removes what a previous life left behind. Returns how many.</summary>
    int SweepOrphans();
}

/// <summary>
/// Materializes through one adapter, into the ephemeral tree root.
/// </summary>
/// <remarks>
/// <para>
/// The credential for a repository is found by the LOCATOR the contract derives
/// from its slug. That derivation lives in the contract precisely so this
/// lookup and the one <c>gg credential add</c> used cannot drift: two
/// derivations that agree today is how a runner ends up looking for a file the
/// CLI never wrote.
/// </para>
/// <para>
/// A repository with no credential is materialized without one. That is not a
/// gap: a public repository needs none, and a private one fails at the
/// provider with the provider's own words, which is a better diagnosis than a
/// guess made here.
/// </para>
/// </remarks>
public sealed class Workspace : IWorkspace
{
    private readonly IReadOnlyList<IVcsAdapter> _adapters;
    private readonly WorkingTreeRoot _trees;

    public Workspace(IReadOnlyList<IVcsAdapter> adapters, WorkingTreeRoot trees)
    {
        _adapters = adapters;
        _trees = trees;
    }

    /// <summary>One adapter, for a runner that serves one provider.</summary>
    public Workspace(IVcsAdapter adapter, WorkingTreeRoot trees) : this([adapter], trees) { }

    public async Task<WorkspaceResult> PrepareAsync(
        string flightId,
        IReadOnlyList<LeaseRepoRef> repos,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(secretsByLocator);

        var reused = _trees.AlreadyHeld(flightId);
        var materialized = new List<Materialized>(repos.Count);

        foreach (var repo in repos)
        {
            // A provider key this runner has no adapter for is a declared
            // capability gap, named before anything is fetched. The alternative
            // is a clone that fails at DNS, which says nothing about the fact
            // that this runner was never configured to serve that forge.
            var adapter = _adapters.FirstOrDefault(a => a.Provider == repo.Provider)
                ?? throw new VcsCapabilityException(
                    "provider",
                    $"This runner serves no provider named '{repo.Provider}'. It is configured for "
                  + $"[{string.Join(", ", _adapters.Select(a => a.Provider))}]; see "
                  + $"{VcsConfiguration.HostsVariable}.");

            secretsByLocator.TryGetValue(CredentialLocator.ForRepo(repo.Slug), out var secret);

            materialized.Add(await new Materializer(adapter, _trees).MaterializeAsync(
                flightId,
                new RepoTarget
                {
                    Provider = repo.Provider,
                    Slug = repo.Slug,
                    PinnedRef = repo.PinnedRef,
                    BaseRef = repo.BaseRef,
                },
                secret,
                cancellationToken));
        }

        return new WorkspaceResult(materialized) { Reused = reused };
    }

    public void Release(string flightId) => _trees.Release(flightId);

    public int SweepOrphans() => _trees.SweepOrphans();
}

/// <summary>
/// Materializes nothing, and refuses loudly if asked to.
/// </summary>
/// <remarks>
/// The default for a runner with no adapter wired up, and for every test whose
/// subject is not materialize. Article XI: a flight naming a repository gets a
/// refusal rather than an empty workspace, because a flight that examined
/// nothing and reported success is the failure this whole system exists to
/// prevent.
/// </remarks>
public sealed class NoWorkspace : IWorkspace
{
    public Task<WorkspaceResult> PrepareAsync(
        string flightId,
        IReadOnlyList<LeaseRepoRef> repos,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repos);

        return repos.Count == 0
            ? Task.FromResult(new WorkspaceResult([]) { Reused = false })
            : throw new VcsCapabilityException(
                "workspace",
                $"This runner has no version-control adapter configured, so the {repos.Count} "
              + "repository(ies) this flight names cannot be put on disk here.");
    }

    public void Release(string flightId) { }

    public int SweepOrphans() => 0;
}
