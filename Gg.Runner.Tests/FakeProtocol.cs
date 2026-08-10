using Gg.Contracts;
using Gg.Runner;

namespace Gg.Runner.Tests;

/// <summary>Time the test moves. No sleeps anywhere in this project.</summary>
internal sealed class MovableClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>
/// A control plane the test drives directly.
/// </summary>
/// <remarks>
/// Records every call in order, so "heartbeat and renew are different things"
/// is asserted against what the loop actually did rather than against how it
/// reads.
/// </remarks>
internal sealed class FakeProtocol : IRunnerProtocol
{
    internal List<string> Calls { get; } = [];

    internal Queue<ClaimResult> Claims { get; } = new();

    internal Queue<RenewResult> Renewals { get; } = new();

    internal ReleaseResult Release { get; set; } = new ReleaseResult.Released();

    internal int HeartbeatSeconds { get; set; } = 1;

    public Task<HeartbeatAccepted> HeartbeatAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        Calls.Add("heartbeat");
        return Task.FromResult(new HeartbeatAccepted { NextHeartbeatSeconds = HeartbeatSeconds });
    }

    public Task<ClaimResult> ClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"claim:{maxWaitSeconds}");
        return Task.FromResult(Claims.Count > 0 ? Claims.Dequeue() : new ClaimResult.Nothing());
    }

    public Task<RenewResult> RenewAsync(string leaseId, int generation, CancellationToken cancellationToken = default)
    {
        Calls.Add($"renew:{generation}");
        return Task.FromResult(Renewals.Count > 0 ? Renewals.Dequeue() : new RenewResult.Renewed(DateTimeOffset.MaxValue));
    }

    public Task<ReleaseResult> ReleaseAsync(
        string leaseId, int generation, string disposition, string? detail = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"release:{generation}:{disposition}");
        return Task.FromResult(Release);
    }
}

/// <summary>
/// Records what the loop reported, and lets a test WAIT on a condition rather
/// than on a duration.
/// </summary>
/// <remarks>
/// The callback is the whole point: a test that polled this list in a spin
/// loop would be a sleep with extra steps, and would starve the very loop it
/// is waiting for.
/// </remarks>
internal sealed class RecordingObserver : IRunnerObserver
{
    internal List<string> Events { get; } = [];

    internal Action<string>? OnEvent { get; set; }

    private void Record(string what)
    {
        Events.Add(what);
        OnEvent?.Invoke(what);
    }

    public void Claimed(LeaseGranted lease) => Record($"claimed:{lease.LeaseId}");
    public void Renewed(string leaseId, DateTimeOffset expiresAt) => Record($"renewed:{leaseId}");
    public void Fenced(string leaseId) => Record($"fenced:{leaseId}");
    public void Released(string leaseId, string disposition) => Record($"released:{disposition}");
    public void Idle() => Record("idle");
}

internal static class Leases
{
    internal static LeaseGranted At(DateTimeOffset expiresAt, int generation = 1, int renewWithin = 5) => new()
    {
        LeaseId = "lease-1",
        Generation = generation,
        FlightId = "flight-1",
        FlightNumber = "GG-1042",
        Repos = [],
        ClassificationCeiling = "internal",
        ExpiresAt = expiresAt,
        RenewWithinSeconds = renewWithin,
    };
}
