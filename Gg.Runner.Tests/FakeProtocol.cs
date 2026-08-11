using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;
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

    /// <summary>
    /// Every request body this fake was handed, serialized.
    /// </summary>
    /// <remarks>
    /// So "the resolved secret never leaves this machine" is asserted against
    /// what would actually go on the wire, rather than against the parameter
    /// list of the method that would send it.
    /// </remarks>
    internal List<string> Serialized { get; } = [];

    /// <summary>The credential diagnosis carried by the most recent release.</summary>
    internal CredentialResolutionFailure? LastCredentialFailure { get; private set; }

    /// <summary>Every batch of facts the loop shipped, in order.</summary>
    internal List<Gg.Runner.Facts.FilteredFacts> ShippedFacts { get; } = [];

    internal Queue<ClaimResult> Claims { get; } = new();

    internal Queue<RenewResult> Renewals { get; } = new();

    internal ReleaseResult Release { get; set; } = new ReleaseResult.Released();

    internal int HeartbeatSeconds { get; set; } = 1;

    private void Record<T>(T body) => Serialized.Add(JsonSerializer.Serialize(body, JsonSerializerOptions.Web));

    public Task<HeartbeatAccepted> HeartbeatAsync(
        string runnerId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        Calls.Add("heartbeat");
        Record(new RunnerHeartbeat { Labels = labels });
        return Task.FromResult(new HeartbeatAccepted { NextHeartbeatSeconds = HeartbeatSeconds });
    }

    public Task<ClaimResult> ClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"claim:{maxWaitSeconds}");
        Record(new LeaseClaimRequest { RunnerId = runnerId, Labels = labels, MaxWaitSeconds = maxWaitSeconds });
        return Task.FromResult(Claims.Count > 0 ? Claims.Dequeue() : new ClaimResult.Nothing());
    }

    public Task<RenewResult> RenewAsync(string leaseId, int generation, CancellationToken cancellationToken = default)
    {
        Calls.Add($"renew:{generation}");
        Record(new LeaseRenewalRequest { Generation = generation });
        return Task.FromResult(Renewals.Count > 0 ? Renewals.Dequeue() : new RenewResult.Renewed(DateTimeOffset.MaxValue));
    }

    public Task<FactBatchAccepted> ShipFactsAsync(
        string leaseId, int generation, Gg.Runner.Facts.FilteredFacts facts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);

        Calls.Add($"facts:{generation}:{facts.Items.Count}");
        ShippedFacts.Add(facts);
        Record(new FactBatch { Generation = generation, Facts = facts.Items });

        return Task.FromResult(new FactBatchAccepted
        {
            Accepted = facts.Items.Count,
            Duplicates = 0,
            Rejected = [],
        });
    }

    public Task<ReleaseResult> ReleaseAsync(
        string leaseId, int generation, string disposition, string? detail = null,
        CredentialResolutionFailure? credentialFailure = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"release:{generation}:{disposition}");
        LastCredentialFailure = credentialFailure;
        Record(new LeaseReleaseRequest
        {
            Generation = generation,
            Disposition = disposition,
            Detail = detail,
            CredentialFailure = credentialFailure,
        });
        // The lease id is on the path rather than in the body, and the secret
        // assertion sweeps everything the runner produced - so it goes in too.
        Serialized.Add(leaseId);
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

    /// <summary>The reference, and nothing that came out of resolving it.</summary>
    public void CredentialUnresolved(CredentialResolutionFailure failure) =>
        Record($"unresolved:{failure.Reference.Locator}");

    /// <summary>A repository was put on disk. The path and the commit, never a byte of it.</summary>
    public void Materialized(string slug, string headCommit, long bytes) =>
        Record($"materialized:{headCommit}:{bytes}");

    /// <summary>The workspace could not be prepared, and this is why.</summary>
    public void WorkspaceFailed(string diagnosis) => Record($"workspace:{diagnosis}");

    /// <summary>Facts left the machine.</summary>
    public void FactsShipped(int count) => Record($"shipped:{count}");
}

internal static class Leases
{
    internal static LeaseGranted At(DateTimeOffset expiresAt, int generation = 1, int renewWithin = 5) => new()
    {
        LeaseId = "lease-1",
        Generation = generation,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
        Repos = [],
        // Nothing to resolve. A flight only carries references once somebody
        // has registered one for a repository it touches.
        Credentials = [],
        ClassificationCeiling = "internal",
        ExpiresAt = expiresAt,
        RenewWithinSeconds = renewWithin,
    };
}
