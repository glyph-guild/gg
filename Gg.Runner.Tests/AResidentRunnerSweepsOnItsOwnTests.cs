using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Sweeps;

namespace Gg.Runner.Tests;

/// <summary>
/// A resident runner sweeps on its own, on by default and off per machine,
/// and asks only for sweeps it can serve.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner, running slice thirty-nine's walk:</b> <i>"today a watch sweeps
/// only while someone keeps that process running - we need to have the runner
/// automatically check."</i> And then: <i>"we should be able to turn off the
/// runner automated sweep as well. it's on by default."</i> Both are here.
/// </para>
/// <para>
/// <b>Off is a state, not a silence.</b> A machine with sweeps off never asks,
/// so it is never handed one - and a watch no runner will sweep is caught by
/// rule 11, which names it <c>watch-missed</c> at twice its period. The board
/// never reads as quiet because the machines stopped looking.
/// </para>
/// <para>
/// <b>It asks only for what it can do.</b> The claim carries the tracker pairs
/// this machine's operator declared, because a sweep it could not serve would
/// attest a FALSE unreachable - rule 18. A machine with none declared does not
/// ask at all: the contract refuses an empty claim, and asking would be the
/// runner's mistake rather than a quiet period.
/// </para>
/// </remarks>
public class AResidentRunnerSweepsOnItsOwnTests
{
    private static readonly IReadOnlyList<ServedTracker> OneTracker =
        [new ServedTracker("ado", "https://tracker.example/acme", "local:acme/triage")];

    [Test]
    public async Task Sweeping_is_on_when_nobody_said_otherwise()
    {
        var claim = ResidentSweeps.ClaimFor(setting: null, OneTracker);

        await Assert.That(claim).IsNotNull()
            .Because("on by default - the owner's words - so a runner stood up from nothing sweeps "
                   + "without anybody remembering to turn it on.");
        await Assert.That(claim!.Serves.Single().Host).IsEqualTo("https://tracker.example/acme");
        await Assert.That(claim.Serves.Single().Credential).IsEqualTo("local:acme/triage")
            .Because("the pair rule 18 compares - a host alone would let a runner holding another "
                   + "credential for that tracker claim a sweep it cannot serve.");
    }

    [Test]
    public async Task Off_means_it_never_asks()
    {
        await Assert.That(ResidentSweeps.ClaimFor("off", OneTracker)).IsNull()
            .Because("a machine that is not asked to sweep spends none of its allowance on it, "
                   + "which is the reason an operator turns it off.");
    }

    [Test]
    public async Task On_said_explicitly_is_the_same_as_unset()
    {
        await Assert.That(ResidentSweeps.ClaimFor("on", OneTracker)).IsNotNull();
    }

    [Test]
    public async Task A_machine_that_declared_no_tracker_does_not_ask()
    {
        // NOT AN EMPTY CLAIM. The contract refuses one, and a runner sending it
        // every idle cycle would be a 400 in the journal forever for a machine
        // that simply has nothing to sweep with.
        await Assert.That(ResidentSweeps.ClaimFor(setting: null, [])).IsNull();
    }

    [Test]
    public async Task A_tracker_declared_with_no_credential_is_not_offered()
    {
        // HALF A PAIR MATCHES NOTHING rule 18 would accept, so it is left out
        // rather than sent - and a machine whose only tracker is half-declared
        // does not ask.
        IReadOnlyList<ServedTracker> half = [new ServedTracker("ado", "https://tracker.example/acme", null)];

        await Assert.That(ResidentSweeps.ClaimFor(setting: null, half)).IsNull();
    }

    [Test]
    public async Task A_setting_that_is_neither_on_nor_off_is_refused_by_name()
    {
        // A TYPO IS NOT "OFF". Reading anything but `on` as off would turn a
        // misspelt `of` into a silent decision to stop sweeping.
        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Task.FromResult(ResidentSweeps.ClaimFor("of", OneTracker)));

        await Assert.That(refused!.Message).Contains("runner-sweeps");
    }

    [Test]
    public async Task What_a_claim_serves_it_sweeps_and_attests_under_its_own_watch()
    {
        // THE CLAIM-SHAPED PASS. It reuses the named pass's per-sweep work
        // unchanged; what differs is only how the sweep was found, and the
        // attestation goes to the watch the ACTION names - the runner never
        // had to know it.
        var protocol = new Claiming(AnAction("nightly-triage"));
        var loop = Loop(protocol);

        var swept = await loop.ClaimPassAsync(
            ResidentSweeps.ClaimFor(setting: null, OneTracker)!, CancellationToken.None);

        await Assert.That(swept).IsEqualTo(1);
        await Assert.That(protocol.Claimed.Single().Serves.Single().Credential)
            .IsEqualTo("local:acme/triage");
        await Assert.That(protocol.AttestedTo.Single()).IsEqualTo("nightly-triage");
    }

    [Test]
    public async Task A_claim_that_is_served_nothing_attests_nothing()
    {
        var protocol = new Claiming();
        var loop = Loop(protocol);

        var swept = await loop.ClaimPassAsync(
            ResidentSweeps.ClaimFor(setting: null, OneTracker)!, CancellationToken.None);

        await Assert.That(swept).IsEqualTo(0);
        await Assert.That(protocol.AttestedTo).IsEmpty();
    }

    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A served sweep carrying a diagnosis, so it attests without reading a
    /// skill or launching anything.
    /// </summary>
    /// <remarks>
    /// <b>The routing is the subject, not the sweep.</b> A diagnosis makes
    /// <c>SweepAsync</c> answer unreachable at once, which is enough to see
    /// which watch the report went to - and keeps this test free of a git
    /// repository and an agent.
    /// </remarks>
    private static WatchAction AnAction(string watch) => new()
    {
        ActionId = Guid.CreateVersion7(Noon),
        Watch = watch,
        WatchVersion = $"{watch}@v1",
        Document = new WatchDocument
        {
            Shape = WatchShapes.WorkItems,
            Trigger = new WatchTrigger { Every = "1h" },
            Host = "https://tracker.example/acme",
            Credential = "local:acme/triage",
            Filter = "SELECT [System.Id] FROM WorkItems",
            Repository = "payments",
            Skill = ".claude/skills/triage.md",
            Ref = "refs/heads/main",
            Mapping = new WatchMapping { Subject = "id", Version = "rev", IntentKey = "url" },
            PullPoint = PullPoints.ResidentRunner,
        },
        Executor = WatchExecutors.Instructions,
        Moves = [LoopMoves.Read, LoopMoves.Propose],
        Skill = null,
        Diagnosis = "the repository this watch names is not registered",
        DecidedAt = Noon.AddMinutes(-5),
    };

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Noon;
    }

    private sealed class NeverRuns : ISweepExecutor
    {
        public Task<SweepExecution> RunAsync(
            SweepRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("a sweep with a diagnosis launches nothing.");
    }

    private static SweepLoop Loop(ISweepProtocol protocol) => new(
        protocol,
        new SkillReader(
            [],
            Path.Combine(Path.GetTempPath(), "gg-sweep-cache", Guid.NewGuid().ToString("n")),
            secretFor: _ => Task.FromResult<string?>(null)),
        new NeverRuns(),
        new FixedClock(),
        transcripts: Path.Combine(Path.GetTempPath(), "gg-sweep-transcripts"));

    /// <summary>A control plane that answers a claim, and records what it was told.</summary>
    private sealed class Claiming(params WatchAction[] served) : ISweepProtocol
    {
        internal List<SweepClaim> Claimed { get; } = [];

        internal List<string> AttestedTo { get; } = [];

        public Task<WatchActionList> ClaimSweepsAsync(
            SweepClaim claim, CancellationToken cancellationToken = default)
        {
            Claimed.Add(claim);
            return Task.FromResult(new WatchActionList { Actions = served });
        }

        public Task<WatchActionList> PullSweepsAsync(
            string watch, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "a resident runner claims; it never pulls by name - that needed a person.");

        public Task AttestSweepAsync(
            string watch, WatchAttestation attestation, CancellationToken cancellationToken = default)
        {
            AttestedTo.Add(watch);
            return Task.CompletedTask;
        }
    }
}
