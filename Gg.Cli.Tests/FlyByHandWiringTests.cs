using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// The order the pieces run in, which is the only place two of this slice's
/// rules are decided.
/// </summary>
/// <remarks>
/// <para>
/// <b>Machinery that exists and is never constructed is a defect this product
/// has shipped twice.</b> <c>ClaudeCodeExecutor</c> was built and no runner ever
/// held one, so no flight invoked an agent; <c>TakeSession</c> was built and the
/// console never constructed one. <c>ConsoleTakeWiringTests</c> exists because
/// of the second, and this class exists for the same reason: <c>HandRefusal</c>
/// answers correctly and answers nothing at all if nobody asks it.
/// </para>
/// <para>
/// <b>Rule 5 is an ORDERING claim and orderings have no home.</b> Whether the
/// refusal is right is <c>HandRefusalTests</c>; whether the flag parses is
/// <c>FlyByHandArgTests</c>; that the refusal is consulted <i>before anything is
/// created</i> is neither, and it is the half that leaves litter with a number
/// on it when it is wrong.
/// </para>
/// <para>
/// <b>The composition takes its collaborators, so the rule is testable without
/// a control plane.</b> That is <c>RunnerIdentity.EnsureAsync</c>'s own
/// arrangement and its own reason — it was untestable while it lived inside a
/// CLI entry point.
/// </para>
/// </remarks>
public class FlyByHandWiringTests
{
    private static Checklist Needing(params string[] labels) => new()
    {
        EnvelopeVersion = "v1",
        RequiredLabels = labels,
        Items = [.. labels.Select(l => new ChecklistItem
        {
            Requirement = l,
            Verification = "a runner advertises it",
            Satisfier = ChecklistSatisfiers.MatchingRunner,
            Disposition = LabelDispositions.Stated,
        })],
    };

    // ---- S26.3-01 ----

    [Test]
    public async Task A_machine_that_cannot_run_the_flight_creates_nothing()
    {
        // NOT "does not fly it" - creates NOTHING. A flight opened and then
        // abandoned because this laptop was wrong sits in the tenant's queue,
        // appears in `gg flights`, and somebody has to decide what became of it.
        var opened = 0;

        var outcome = await FlyByHand.FlyAsync(
            plan: _ => Task.FromResult(Needing("environment=aspire-payments")),
            advertised: [],
            open: _ => { opened++; return Task.FromResult<VerbResult>(null!); },
            CancellationToken.None);

        await Assert.That(opened).IsEqualTo(0)
            .Because("rule 5 is about what is left behind, not about what is flown.");

        await Assert.That(outcome.Refused).IsNotNull();
        await Assert.That(outcome.Refused!.Requirement).IsEqualTo("environment=aspire-payments");
    }

    [Test]
    public async Task A_machine_that_can_run_the_flight_opens_it()
    {
        // THE TWIN. A wiring that refused everything would satisfy the row above
        // perfectly, and would be indistinguishable from one that never asked.
        var opened = 0;

        var outcome = await FlyByHand.FlyAsync(
            plan: _ => Task.FromResult(Needing("environment=aspire-payments")),
            advertised: ["environment=aspire-payments"],
            open: _ => { opened++; return Task.FromResult<VerbResult>(new VerbResult.Nothing()); },
            CancellationToken.None);

        await Assert.That(opened).IsEqualTo(1);
        await Assert.That(outcome.Refused).IsNull();
    }

    [Test]
    public async Task The_plan_is_read_before_the_flight_is_opened()
    {
        // THE ORDER ITSELF, not its consequence. A wiring that opened the flight
        // and then checked would pass both rows above whenever the machine was
        // eligible - and would leave the litter on exactly the runs that matter.
        var order = new List<string>();

        await FlyByHand.FlyAsync(
            plan: _ => { order.Add("plan"); return Task.FromResult(Needing()); },
            advertised: [],
            open: _ =>
            {
                order.Add("open");
                return Task.FromResult<VerbResult>(new VerbResult.Nothing());
            },
            CancellationToken.None);

        await Assert.That(order).IsEquivalentTo(new[] { "plan", "open" });
    }

    // ---- S26.4-07 ----

    [Test]
    public async Task The_attended_runner_stops_after_one_flight()
    {
        // ITS LIFETIME IS ITS AVAILABILITY. A fleet runner is a service and
        // stays; this one is a person's session and a lingering process is a
        // runner nobody knows is there - which would then take fleet work onto
        // somebody's laptop after they walked away from it.
        using var stopping = new CancellationTokenSource();
        var inner = new CountingObserver();
        var observer = new StopsAfterOneFlight(inner, stopping);

        await Assert.That(stopping.IsCancellationRequested).IsFalse();

        observer.Claimed(null!);
        observer.Materialized("acme/widgets", "abc123", 10);

        await Assert.That(stopping.IsCancellationRequested).IsFalse()
            .Because("a runner that stopped at the first sign of work would never fly anything.");

        observer.Released("lease-1", "completed");

        await Assert.That(stopping.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Everything_it_wraps_still_reaches_the_person()
    {
        // A DECORATOR THAT SWALLOWED THE NARRATION would make a hand-flight
        // silent, which is worse than one that never stops: the person is AT the
        // terminal, and this is the only place they learn what happened.
        using var stopping = new CancellationTokenSource();
        var inner = new CountingObserver();
        var observer = new StopsAfterOneFlight(inner, stopping);

        observer.Materialized("acme/widgets", "abc123", 10);
        observer.FactsShipped(3);
        observer.Landed("proposed", "gg/GG-1042");
        observer.Released("lease-1", "completed");

        await Assert.That(inner.Calls).IsEquivalentTo(
            new[] { "materialized", "shipped", "landed", "released" });
    }

    /// <summary>Records what it was told, so the decorator can be shown to forward it.</summary>
    private sealed class CountingObserver : IRunnerObserver
    {
        internal List<string> Calls { get; } = [];

        public void Claimed(LeaseGranted lease) => Calls.Add("claimed");
        public void Renewed(string leaseId, DateTimeOffset expiresAt) => Calls.Add("renewed");
        public void Fenced(string leaseId) => Calls.Add("fenced");
        public void Released(string leaseId, string disposition) => Calls.Add("released");
        public void BoundBroken(string diagnosis) => Calls.Add("bound-broken");
        public void ControlPlaneRefused(string diagnosis, TimeSpan retryIn) => Calls.Add("refused");
        public void Idle() => Calls.Add("idle");
        public void Parked() => Calls.Add("parked");
        public void Waiting(IReadOnlyList<string> repos) => Calls.Add("waiting");
        public void Materialized(string slug, string headCommit, long bytes) =>
            Calls.Add("materialized");
        public void WorkspaceFailed(string diagnosis) => Calls.Add("workspace-failed");
        public void FactsShipped(int count) => Calls.Add("shipped");
        public void LoopFinished(
            string loopId, string outcome, int attempts, IReadOnlyList<string> movesUsed) =>
            Calls.Add("loop-finished");
        public void MoveRefused(string diagnosis) => Calls.Add("move-refused");
        public void Landed(string outcome, string detail) => Calls.Add("landed");
        public void Held(string flightNumber, string path, long bytes, bool preserved = false) =>
            Calls.Add("held");
        public void CredentialUnresolved(CredentialResolutionFailure failure) =>
            Calls.Add("credential-unresolved");
    }
}
