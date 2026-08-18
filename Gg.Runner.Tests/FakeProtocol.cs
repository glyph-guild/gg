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

    /// <summary>
    /// What this control plane says when a claim is taken.
    /// </summary>
    /// <remarks>
    /// An acceptance by default, because that is what a control plane serving
    /// this contract answers. A test that wants the tolerated older shape - a
    /// lease answered inline - sets an <see cref="ClaimAcceptance.Inline"/>.
    /// </remarks>
    internal ClaimAcceptance? Acceptance { get; set; }

    /// <summary>How many seconds this control plane tells the runner to wait.</summary>
    internal int PollAfterSeconds { get; set; } = 2;

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

    public Task<ClaimAcceptance> RequestClaimAsync(
        string runnerId, IReadOnlyList<string> labels, int maxWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"claim:{maxWaitSeconds}");
        Record(new LeaseClaimRequest { RunnerId = runnerId, Labels = labels, MaxWaitSeconds = maxWaitSeconds });

        return Task.FromResult(Acceptance ?? new ClaimAcceptance.Accepted(
            $"request-{Calls.Count(c => c.StartsWith("claim:", StringComparison.Ordinal))}",
            TimeSpan.FromSeconds(PollAfterSeconds)));
    }

    /// <summary>
    /// Answers from <see cref="Claims"/>, and pends once it is empty.
    /// </summary>
    /// <remarks>
    /// Pending rather than granted-with-nothing, because that is what an idle
    /// control plane reports and a test that ran out of queued answers is
    /// describing an idle one.
    /// </remarks>
    public Task<ClaimResult> ReadClaimAsync(string requestId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"claim-status:{requestId}");
        return Task.FromResult(Claims.Count > 0 ? Claims.Dequeue() : new ClaimResult.Nothing());
    }

    public Task<RenewResult> RenewAsync(string leaseId, int generation, CancellationToken cancellationToken = default)
    {
        Calls.Add($"renew:{generation}");
        Record(new LeaseRenewalRequest { Generation = generation });
        return Task.FromResult(Renewals.Count > 0 ? Renewals.Dequeue() : new RenewResult.Renewed(DateTimeOffset.MaxValue));
    }

    /// <summary>
    /// The landing decision this control plane answers with, if any.
    /// </summary>
    /// <remarks>
    /// Null by default, because that is what a flight with no destination and a
    /// flight whose obligations are unmet both get. A test that wants a landing
    /// has to say so, exactly as an envelope does.
    /// </remarks>
    public DestinationAdmission? Admission { get; set; }

    /// <summary>
    /// The first gate, set independently of the second.
    /// </summary>
    /// <remarks>
    /// Independent on purpose: a fake that derived one from the other could not
    /// express the case that matters - cleared to push and not to propose - and a
    /// test using it would be asserting against its own convenience.
    /// </remarks>
    public BranchPush? Push { get; set; }

    public Task<FactBatchAccepted> ShipFactsAsync(
        string leaseId, int generation, Gg.Runner.Facts.FilteredFacts facts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);

        Calls.Add($"facts:{generation}:{facts.Items.Count}");
        ShippedFacts.Add(facts);
        Record(new FactBatch { Generation = generation, Facts = facts.Items });

        return Task.FromResult(new FactBatchAccepted { Rejected = [] });
    }

    /// <summary>
    /// How many times the runner has to ask before this control plane settles.
    /// </summary>
    /// <remarks>
    /// Zero by default, so a test that does not care about the wait does not pay
    /// for one. A test that DOES care sets it and gets the real shape: unsettled
    /// answers first, carrying neither permission, and a runner that must not
    /// read those as refusals.
    /// </remarks>
    public int UnsettledAnswers { get; set; }

    public Task<LandingDecision> ReadAdmissionAsync(
        string leaseId, CancellationToken cancellationToken = default)
    {
        Calls.Add("admission");

        if (UnsettledAnswers > 0)
        {
            UnsettledAnswers--;

            // NEITHER PERMISSION, and not settled. This is the state that used
            // to be unrepresentable: absent push and absent admission, which a
            // runner reading absence as refusal would land on - wrongly.
            return Task.FromResult(new LandingDecision { Settled = false });
        }

        return Task.FromResult(new LandingDecision
        {
            Settled = true,
            Push = Push,
            Admission = Admission,
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

    /// <summary>
    /// Records everything; wakes a waiting test only on a LIFECYCLE event.
    /// </summary>
    /// <remarks>
    /// Narration - a tree appearing, facts shipping - is recorded and asserted
    /// on, but it does not advance the counter a test is waiting on. Otherwise
    /// every test that waits for "claim then release" would have to know how
    /// many lines the runner happens to print in between, and adding a line
    /// would break tests that are about something else entirely.
    /// </remarks>
    private void Record(string what, bool lifecycle = true)
    {
        Events.Add(what);
        if (lifecycle)
        {
            OnEvent?.Invoke(what);
        }
    }

    public void Claimed(LeaseGranted lease) => Record($"claimed:{lease.LeaseId}");
    public void Renewed(string leaseId, DateTimeOffset expiresAt) => Record($"renewed:{leaseId}");
    public void Fenced(string leaseId) => Record($"fenced:{leaseId}");
    public void Released(string leaseId, string disposition) => Record($"released:{disposition}");
    public void Idle() => Record("idle");

    /// <summary>
    /// The repositories, because WHICH one is the whole content of the report.
    /// </summary>
    /// <remarks>
    /// A count would tell a person that something is wrong. A name tells them
    /// which credential to go and register, which is the only action available.
    /// </remarks>
    public void Waiting(IReadOnlyList<string> repos) => Record($"waiting:{string.Join(",", repos)}");

    /// <summary>The reference, and nothing that came out of resolving it.</summary>
    public void CredentialUnresolved(CredentialResolutionFailure failure) =>
        Record($"unresolved:{failure.Reference.Locator}");

    /// <summary>A repository was put on disk. The path and the commit, never a byte of it.</summary>
    public void Materialized(string slug, string headCommit, long bytes) =>
        Record($"materialized:{headCommit}:{bytes}", lifecycle: false);

    /// <summary>The workspace could not be prepared, and this is why.</summary>
    public void WorkspaceFailed(string diagnosis) => Record($"workspace:{diagnosis}");

    /// <summary>Facts left the machine.</summary>
    public void FactsShipped(int count) => Record($"shipped:{count}", lifecycle: false);

    public void LoopFinished(string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed) =>
        Record($"loop:{loopId}:{outcome}:{attempts}", lifecycle: false);

    /// <summary>The whole sentence, because what it SAYS is the criterion.</summary>
    public void MoveRefused(string diagnosis) => Record($"moves:{diagnosis}", lifecycle: false);

    public void Landed(string outcome, string detail) =>
        Record($"landed:{outcome}", lifecycle: false);

    public void Held(string flightNumber, string path, long bytes) =>
        Record($"held:{flightNumber}:{bytes}", lifecycle: false);
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
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = expiresAt,
        RenewWithinSeconds = renewWithin,
    };
}
