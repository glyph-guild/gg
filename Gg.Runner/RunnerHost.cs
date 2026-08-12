using Gg.Contracts;

namespace Gg.Runner;

/// <summary>Narrates the loop to stdout. The only UI a runner has at this step.</summary>
internal sealed class ConsoleObserver : IRunnerObserver
{
    public void Claimed(LeaseGranted lease) =>
        System.Console.WriteLine(
            $"claimed {lease.FlightNumber} (lease {lease.LeaseId} gen {lease.Generation}) until {lease.ExpiresAt:O}");

    public void Renewed(string leaseId, DateTimeOffset expiresAt) =>
        System.Console.WriteLine($"renewed {leaseId} until {expiresAt:O}");

    public void Fenced(string leaseId) =>
        System.Console.WriteLine($"fenced on {leaseId}: this flight belongs to another runner now");

    public void Released(string leaseId, string disposition) =>
        System.Console.WriteLine($"released {leaseId} as {disposition}");

    public void Idle() => System.Console.WriteLine("nothing ready");

    /// <summary>
    /// The commit and the size. Never a path inside the tree.
    /// </summary>
    /// <remarks>
    /// The byte count is here because disk is the first resource this product
    /// consumes in somebody else's environment, and an operator watching a
    /// runner fill a laptop has no other number to look at.
    /// </remarks>
    public void Materialized(string slug, string headCommit, long bytes) =>
        System.Console.WriteLine($"materialized {headCommit} ({bytes:N0} bytes on disk)");

    public void WorkspaceFailed(string diagnosis) =>
        System.Console.WriteLine($"workspace: {diagnosis}");

    public void FactsShipped(int count) =>
        System.Console.WriteLine($"shipped {count} fact(s)");

    /// <summary>
    /// How the loop ended, and what it reached for.
    /// </summary>
    /// <remarks>
    /// Never a line of what the agent produced. This is stdout, and stdout is
    /// what a customer pastes into a ticket - the outcome and the counts are
    /// safe there and the work is not.
    /// </remarks>
    public void LoopFinished(string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed) =>
        System.Console.WriteLine(
            $"loop {loopId} {outcome} after {attempts} attempt(s), used {movesUsed.Count} move(s)");

    /// <summary>
    /// The reference and the problem. Never anything resolving it produced.
    /// </summary>
    /// <remarks>
    /// Which credential, where it should have been, who it acts as, and what
    /// went wrong - because this line is what somebody sends us when their
    /// flight will not start.
    /// </remarks>
    public void CredentialUnresolved(CredentialResolutionFailure failure) =>
        System.Console.WriteLine(
            $"credential {failure.Reference.Locator} (as {failure.Reference.Identity}, "
          + $"{failure.Reference.Kind}) could not be resolved: {failure.Problem}");
}

/// <summary>
/// The runner role, hosted by the `gg` binary.
/// </summary>
/// <remarks>
/// <para>
/// No Whizbang here, and no reference to the developer client either: this
/// assembly sees the wire contract and nothing else, so a runner physically
/// cannot hold a developer's session. The credential it does hold is passed
/// in, having been minted by a person.
/// </para>
/// <para>
/// Runs in the foreground as its own process. `gg` spawns it as a child at a
/// later step; the process boundary is the point, and re-adding the spawn does
/// not change anything in here.
/// </para>
/// </remarks>
public static class RunnerHost
{
    /// <param name="workspace">
    /// Where this runner puts a customer's source code, and what removes it
    /// again. Passed in for the same reason the resolver is: the adapter and
    /// the tree root are the deployment's business, not this assembly's.
    /// </param>
    /// <param name="credentials">
    /// How this runner turns a reference into a secret, on this machine.
    /// Passed in rather than constructed, because the adapter reads the local
    /// store and this assembly cannot see the developer client that owns it.
    /// </param>
    public static async Task<int> RunAsync(
        Uri controlPlane,
        string runnerId,
        string runnerToken,
        IReadOnlyList<string> labels,
        TimeSpan holdFor,
        ICredentialResolver credentials,
        IWorkspace workspace,
        CancellationToken cancellationToken)
    {
        // Longer than the claim's long poll, or the client aborts every idle
        // claim and the long poll becomes a busy loop with extra steps.
        using var http = new HttpClient
        {
            BaseAddress = controlPlane,
            Timeout = TimeSpan.FromSeconds(RunnerLoop.ClaimWaitSeconds + 30),
        };

        // Before anything else. A runner that is starting holds no lease, so
        // every tree under the root belongs to a process that is gone - most
        // likely one a SIGKILL took out mid-clone, which is the case no
        // finally block can cover.
        var swept = workspace.SweepOrphans();
        if (swept > 0)
        {
            System.Console.WriteLine($"swept {swept} working tree(s) left by a previous run");
        }

        var loop = new RunnerLoop(
            new RunnerProtocolClient(http, runnerToken),
            new SystemClock(),
            (span, token) => Task.Delay(span, token),
            new ConsoleObserver(),
            credentials,
            workspace)
        {
            HoldFor = holdFor,
        };

        System.Console.WriteLine(
            $"gg-runner {runnerId} (pid {Environment.ProcessId}) against {controlPlane} " +
            $"labels [{string.Join(", ", labels)}]");

        await loop.RunAsync(runnerId, labels, cancellationToken);
        return 0;
    }
}
