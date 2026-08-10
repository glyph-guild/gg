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
    public static async Task<int> RunAsync(
        Uri controlPlane,
        string runnerId,
        string runnerToken,
        IReadOnlyList<string> labels,
        TimeSpan holdFor,
        CancellationToken cancellationToken)
    {
        // Longer than the claim's long poll, or the client aborts every idle
        // claim and the long poll becomes a busy loop with extra steps.
        using var http = new HttpClient
        {
            BaseAddress = controlPlane,
            Timeout = TimeSpan.FromSeconds(RunnerLoop.ClaimWaitSeconds + 30),
        };

        var loop = new RunnerLoop(
            new RunnerProtocolClient(http, runnerToken),
            new SystemClock(),
            (span, token) => Task.Delay(span, token),
            new ConsoleObserver())
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
