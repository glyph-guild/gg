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
    /// Which repositories, because that is the sentence somebody can act on.
    /// </summary>
    /// <remarks>
    /// A slug is not a secret and not a line of anybody's code - it is the thing
    /// the person reading this would have to type into `gg credential add`.
    /// </remarks>
    public void Waiting(IReadOnlyList<string> repos) =>
        System.Console.WriteLine(
            $"waiting on a credential for {string.Join(", ", repos)} (work is ready; this is not)");

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
    /// <summary>
    /// Where the work went, or why it did not go anywhere.
    /// </summary>
    /// <remarks>
    /// The branch and the proposal. Never a line of what was in them - stdout is
    /// what a customer pastes into a ticket, and a refusal's diagnosis is the
    /// thing they most need to be able to paste.
    /// </remarks>
    public void Landed(string outcome, string detail) =>
        System.Console.WriteLine($"destination {outcome}: {detail}");

    /// <summary>
    /// Said out loud, because a kept tree is disk this process decided to spend.
    /// </summary>
    public void Held(string flightNumber, string path, long bytes) =>
        System.Console.WriteLine(
            $"kept {flightNumber}'s tree for handoff: {bytes / 1024} KiB at {path}");

    public void LoopFinished(string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed) =>
        System.Console.WriteLine(
            $"loop {loopId} {outcome} after {attempts} attempt(s), used {movesUsed.Count} move(s)");

    /// <summary>
    /// The envelope is one word short, and this says which word and where.
    /// </summary>
    public void MoveRefused(string diagnosis) => System.Console.WriteLine($"moves: {diagnosis}");

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
        CancellationToken cancellationToken,
        IReadOnlyList<Vcs.IDestinationAdapter>? destinations = null,
        Execution.IExecutorPort? executor = null)
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

        // BEFORE ANY WORK IS CLAIMED, and only when this runner can run a loop.
        // A runner with no executor cannot invoke an agent, so it cannot break a
        // move bound and has nothing to prove; one that can has to prove it here,
        // because a governed flight on a machine where moves are not enforceable
        // is not governed, and flying one anyway makes the claim
        // this product is sold on false on somebody's laptop.
        // What the probe proved, carried onto every fact set this runner ships.
        (string Enforcement, IReadOnlyList<string> Withheld)? bound = null;

        if (Execution.MoveBoundProbe.Required(executor) is { } why)
        {
            System.Console.WriteLine($"probing whether declared moves bound this executor. {why}");

            var probe = await Execution.MoveBoundProbe.RunAsync(executor!, cancellationToken);
            bound = (Execution.MoveEnforcementNames.Of(executor!.Capabilities.EnforcesMoves),
                     probe.Withheld);

            System.Console.WriteLine(
                $"move bound: {(probe.Bound ? "held" : "NOT HELD")} "
              + $"in {probe.Took.TotalSeconds:F1}s - {probe.Diagnosis}");

            if (!probe.Bound)
            {
                System.Console.Error.WriteLine(
                    "This runner will not take work. Nothing is claimed, nothing is cloned and "
                  + "no agent is invoked.");
                return 69;
            }
        }

        var loop = new RunnerLoop(
            new RunnerProtocolClient(http, runnerToken),
            new SystemClock(),
            (span, token) => Task.Delay(span, token),
            new ConsoleObserver(),
            credentials,
            workspace,
            executor,
            destinations: destinations,
            moveBound: bound)
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
