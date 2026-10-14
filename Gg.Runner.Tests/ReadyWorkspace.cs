using Gg.Contracts;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>A workspace whose tree is materialized before the claim, so it is ready at once.</summary>
/// <remarks>
/// <b>For a test about what happens while the agent works</b>, since the clone renews too
/// (GG-1123). A test clock's waits are instant, so a real clone of even a tiny tree lasts long
/// enough to renew - and a renewal queued for the agent would be answered to the clone instead.
/// </remarks>
internal sealed class ReadyWorkspace(IWorkspace inner, WorkspaceResult ready) : IWorkspace
{
    public static async Task<IWorkspace> Of(IWorkspace inner, LeaseGranted lease) =>
        new ReadyWorkspace(
            inner, await inner.PrepareAsync(lease.FlightId, lease.Repos, new Dictionary<string, string>()));

    public Task<WorkspaceResult> PrepareAsync(
        string flightId,
        IReadOnlyList<LeaseRepoRef> repos,
        IReadOnlyDictionary<string, string> secretsByLocator,
        CancellationToken cancellationToken = default) => Task.FromResult(ready);

    public void Release(string flightId) => inner.Release(flightId);

    public HeldTree? Hold(string flightId) => inner.Hold(flightId);

    public int SweepOrphans() => inner.SweepOrphans();
}
