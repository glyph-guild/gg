namespace Gg.Runner.Environments;

/// <summary>
/// What is in one environment instance's daemon, and removing it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Narrow on purpose, and the narrowness is a guarantee.</b> There is no
/// method here that removes an image, so no reclaim can remove one — slice
/// fifty-six rule 4, made unrepresentable rather than asserted. Warmth is the
/// image store, and a reclaim that took it would make every flight cold and
/// defeat the slice that built this.
/// </para>
/// <para>
/// <b>Not <c>IPoolAdapter</c>, which asks a different question.</b> That port
/// lists a pool's members BY NAME PREFIX, because that is how a pool identifies
/// its own containers on a shared daemon. An instance's daemon holds only that
/// instance's things (ADR-0034), so the right question here is "everything", and
/// borrowing a port that needs a prefix would mean inventing one — the very
/// bookkeeping ADR-0034 replaced.
/// </para>
/// <para>
/// <b>Everything, in every state.</b> A stopped container still holds its name
/// and its ports on restart, and Aspire reuses names derived from the checkout
/// path — so listing only the running ones leaves exactly the collisions this
/// exists to prevent.
/// </para>
/// </remarks>
public interface IInstanceDaemon
{
    /// <summary>Every container id in this daemon, running or not.</summary>
    Task<IReadOnlyList<string>> ContainersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks one container to stop, and waits for it.
    /// </summary>
    /// <remarks>
    /// <b>The polite half.</b> It sends the app its termination signal and waits,
    /// so it flushes what it was writing and closes what it held. Only the
    /// tear-down uses it: the reclaim is cleaning up after something already
    /// dead, and a timeout per dead container is paid by the flight waiting to
    /// start.
    /// </remarks>
    Task StopContainerAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Removes one container, running or not.</summary>
    Task RemoveContainerAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Removes every network nothing is attached to, and says how many.</summary>
    Task<int> PruneNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes every volume nothing mounts, and says how many.</summary>
    Task<int> PruneVolumesAsync(CancellationToken cancellationToken = default);
}

/// <summary>What a reclaim took out of an instance.</summary>
/// <remarks>
/// Counted rather than named, because the numbers are what a person reads and a
/// list of container ids is a list of things that no longer exist. It is also
/// what tells "this instance was already clean" from "this instance held the
/// last flight's stack", which S56.2-04 reports.
/// </remarks>
public sealed record Emptied
{
    public required int Containers { get; init; }

    public required int Networks { get; init; }

    public required int Volumes { get; init; }

    /// <summary>Whether there was anything to remove at all.</summary>
    /// <remarks>
    /// Nothing is the ordinary state of a fresh slot and of one whose last flight
    /// brought its own stack down politely, so it is an answer rather than a
    /// surprise.
    /// </remarks>
    public bool RemovedAnything => Containers > 0 || Networks > 0 || Volumes > 0;
}

/// <summary>
/// Empties an environment instance before a flight stands its stack up in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the way in, never on the way out</b> — slice fifty-six rule 2. A flight
/// that is killed keeps no promises: no <c>finally</c> runs and no handler fires,
/// so a tear-down at landing cannot be what makes the next flight's environment
/// trustworthy. The runner reached the same conclusion for working trees, where
/// a <c>SIGKILL</c> mid-clone leaves one behind and a sweep on the way in is what
/// answers it.
/// </para>
/// <para>
/// <b>It never falls back to another daemon.</b> The implementation handed in is
/// bound to one socket path, so an unreachable instance raises rather than
/// quietly emptying whatever an ambient <c>DOCKER_HOST</c> pointed at — which on
/// a pool host is every member on the machine.
/// </para>
/// </remarks>
public static class InstanceReclaim
{
    /// <summary>Removes everything this instance holds except its images.</summary>
    public static Task<Emptied> EmptyAsync(
        IInstanceDaemon daemon, CancellationToken cancellationToken = default) =>
        ClearAsync(daemon, politely: false, cancellationToken);

    /// <summary>
    /// Brings this instance's stack down, letting each container stop first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A courtesy, never the mechanism</b> — slice fifty-six rule 3. The
    /// reclaim on the way in is what makes the next flight's environment
    /// trustworthy; this is for the time BETWEEN flights, when an instance
    /// holding thirteen idle containers holds their ports and their memory and a
    /// person looking at the host sees a stack that finished hours ago.
    /// </para>
    /// <para>
    /// <b>Nothing reports it.</b> No fact and no lease member says a stack came
    /// down, so no later decision can read it as a precondition — which is how
    /// the courtesy stays one.
    /// </para>
    /// </remarks>
    public static Task<Emptied> BringDownAsync(
        IInstanceDaemon daemon, CancellationToken cancellationToken = default) =>
        ClearAsync(daemon, politely: true, cancellationToken);

    private static async Task<Emptied> ClearAsync(
        IInstanceDaemon daemon, bool politely, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(daemon);

        var containers = await daemon.ContainersAsync(cancellationToken);

        // CONTAINERS FIRST, AND THE ORDER IS NOT COSMETIC. A network a running
        // container is attached to cannot be pruned, and neither can a volume one
        // mounts - so pruning first reports success having removed nothing, and
        // the next flight collides with names it was told were gone.
        foreach (var container in containers)
        {
            if (politely)
            {
                await daemon.StopContainerAsync(container, cancellationToken);
            }

            await daemon.RemoveContainerAsync(container, cancellationToken);
        }

        return new Emptied
        {
            Containers = containers.Count,
            Networks = await daemon.PruneNetworksAsync(cancellationToken),
            Volumes = await daemon.PruneVolumesAsync(cancellationToken),
        };
    }
}
