using Gg.Contracts;
using Gg.Runner.Sweeps;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// Every sweep the runner is handed ends in an attestation, and an empty one is
/// a result rather than a silence.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.3-02, and <c>MaintainLoop</c>'s scar:</b> <i>"this loop reported
/// NOTHING for its whole life, so hours of crash-looping looked identical to
/// hours of quietly working."</i> A sweep that nominated nothing attests that it
/// swept; a sweep that could not run attests why; and the two are different rows
/// a person can tell apart without reading a sentence.
/// </para>
/// <para>
/// <b>Nothing is launched that cannot do its job.</b> A sweep handed a
/// diagnosis, a skill that will not read, and a grant with no way to nominate
/// each attest unreachable before an agent is started - an agent with no
/// instructions, or no tool to report with, would spend tokens to say nothing.
/// </para>
/// <para>
/// <b>What is sent is what the contract accepts.</b> The runner checks its own
/// report with <c>WatchAttestation.Validate</c> before it sends it, so a report
/// the control plane would refuse is never the thing a sweep's work ends in.
/// </para>
/// </remarks>
public class AnEmptySweepStillAttestsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Noon;
    }

    private sealed class RecordingProtocol(params WatchAction[] decided) : ISweepProtocol
    {
        private readonly Queue<WatchAction[]> _pulls = new([decided]);

        internal List<(string Watch, WatchAttestation Attestation)> Attested { get; } = [];

        public Task<WatchActionList> PullSweepsAsync(
            string watch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WatchActionList
            {
                Actions = _pulls.TryDequeue(out var next) ? next : [],
            });

        // THIS DOUBLE IS ABOUT THE NAMED PULL. A claim reaching it would be a
        // resident runner's path wandering into a test of the manual one, so it
        // says so rather than answering empty and passing.
        public Task<WatchActionList> ClaimSweepsAsync(
            SweepClaim claim, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("this double serves the named pull, not a claim.");

        public Task AttestSweepAsync(
            string watch, WatchAttestation attestation, CancellationToken cancellationToken = default)
        {
            Attested.Add((watch, attestation));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExecutor(SweepExecution answer) : ISweepExecutor
    {
        internal List<SweepRequest> Launched { get; } = [];

        public Task<SweepExecution> RunAsync(
            SweepRequest request, CancellationToken cancellationToken = default)
        {
            Launched.Add(request);
            return Task.FromResult(answer);
        }
    }

    private static WatchDocument AWatch() => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = "https://tracker.example/acme",
        Credential = "tracker-read",
        Filter = "SELECT [System.Id] FROM WorkItems",
        Repository = "payments",
        Skill = TheRunnerReadsTheSkillAtItsPinTests.SkillPath,
        Ref = "refs/heads/main",
        Mapping = new WatchMapping { Subject = "id", Version = "rev", IntentKey = "url" },
        PullPoint = PullPoints.ResidentRunner,
        Nominates = new Destination
        {
            Id = "what-a-sweep-opens",
            Kind = DestinationKinds.Flight,
            Requires = [],
            Opens = ["review"],
        },
    };

    private static WatchAction AnAction(
        TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository,
        string? diagnosis = null,
        IReadOnlyList<string>? moves = null) => new()
    {
        ActionId = Guid.CreateVersion7(Noon),
        Watch = "nightly-triage",
        WatchVersion = "nightly-triage@v1",
        Document = AWatch(),
        Executor = WatchExecutors.Instructions,
        Moves = moves ?? [LoopMoves.Read, LoopMoves.Propose],
        Skill = diagnosis is null
            ? new LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = repository.BarePath,
                PinnedRef = repository.Reviewed,
            }
            : null,
        Diagnosis = diagnosis,
        DecidedAt = Noon.AddMinutes(-5),
    };

    private static SweepLoop Loop(
        TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository,
        RecordingProtocol protocol,
        RecordingExecutor executor) =>
        new(
            protocol,
            new SkillReader(
                [new LocalVcsAdapter(repository.Directory)],
                Path.Combine(Path.GetTempPath(), "gg-sweep-cache", Guid.NewGuid().ToString("n")),
                secretFor: _ => Task.FromResult<string?>(null)),
            executor,
            new FixedClock(),
            transcripts: Path.Combine(Path.GetTempPath(), "gg-sweep-transcripts", Guid.NewGuid().ToString("n")));

    [Test]
    public async Task A_sweep_that_nominated_nothing_attests_that_it_swept()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var action = AnAction(repository);
        var executor = new RecordingExecutor(new SweepExecution.Ran([]));

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(action, CancellationToken.None);

        await Assert.That(attestation.Outcome).IsEqualTo(WatchOutcomes.Swept);
        await Assert.That(attestation.Nominated).IsEmpty();
        await Assert.That(attestation.ActionId).IsEqualTo(action.ActionId);
        await Assert.That(attestation.SkillSha)
            .IsEqualTo(repository.BlobAt(repository.Reviewed, TheRunnerReadsTheSkillAtItsPinTests.SkillPath))
            .Because("the digest of the words it followed is the record of what ran, even when "
                   + "what ran found nothing.");
        await Assert.That(WatchAttestation.Validate(attestation)).IsNull();

        var launched = executor.Launched.Single();
        await Assert.That(launched.Skill).IsEqualTo("the reviewed words\n")
            .Because("the executor is handed the words at the pin, not whatever the branch holds.");
        await Assert.That(launched.Action).IsEqualTo(action);
    }

    [Test]
    public async Task What_the_executor_nominated_is_what_the_sweep_reports()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var nominated = new SweepNomination
        {
            Subject = "4242",
            Version = "7",
            IntentKey = "https://tracker.example/acme/4242",
            Reason = "needs a person",
        };
        var executor = new RecordingExecutor(new SweepExecution.Ran([nominated]));

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(AnAction(repository), CancellationToken.None);

        await Assert.That(attestation.Nominated).IsEquivalentTo([nominated]);
        await Assert.That(WatchAttestation.Validate(attestation)).IsNull();
    }

    [Test]
    public async Task A_report_is_held_to_what_the_contract_accepts()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var many = Enumerable.Range(0, WatchAttestation.MaxNominations + 10)
            .Select(i => new SweepNomination { Subject = $"{i}", Version = "1", Reason = "why" })
            .ToList();
        var executor = new RecordingExecutor(new SweepExecution.Ran(many));

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(AnAction(repository), CancellationToken.None);

        await Assert.That(attestation.Nominated.Count).IsEqualTo(WatchAttestation.MaxNominations)
            .Because("a report the control plane refuses loses every nomination in it; the first "
                   + "ones are kept, and the watch's own cap is the board's to apply.");
        await Assert.That(WatchAttestation.Validate(attestation)).IsNull();
    }

    [Test]
    public async Task A_sweep_handed_a_diagnosis_attests_it_and_launches_nothing()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var executor = new RecordingExecutor(new SweepExecution.Ran([]));

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(AnAction(repository, diagnosis: "'refs/heads/main' does not resolve"),
                CancellationToken.None);

        await Assert.That(attestation.Outcome).IsEqualTo(WatchOutcomes.Unreachable);
        await Assert.That(attestation.Diagnosis!).Contains("does not resolve");
        await Assert.That(executor.Launched).IsEmpty();
        await Assert.That(WatchAttestation.Validate(attestation)).IsNull();
    }

    [Test]
    public async Task A_skill_that_will_not_read_attests_why_and_launches_nothing()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var executor = new RecordingExecutor(new SweepExecution.Ran([]));
        var action = AnAction(repository) with
        {
            Document = AWatch() with { Skill = ".goodgrief/skills/missing.md" },
        };

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(action, CancellationToken.None);

        await Assert.That(attestation.Outcome).IsEqualTo(WatchOutcomes.Unreachable);
        await Assert.That(attestation.Diagnosis!).Contains("missing.md");
        await Assert.That(executor.Launched).IsEmpty()
            .Because("an agent with no instructions would run on nothing.");
    }

    [Test]
    public async Task A_sweep_granted_no_way_to_nominate_attests_that_and_launches_nothing()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var executor = new RecordingExecutor(new SweepExecution.Ran([]));

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(AnAction(repository, moves: [LoopMoves.Read]), CancellationToken.None);

        await Assert.That(attestation.Outcome).IsEqualTo(WatchOutcomes.Unreachable);
        await Assert.That(attestation.Diagnosis!).Contains(LoopMoves.Propose)
            .Because("a sweep whose only output is nominations, granted no way to make one, "
                   + "would report an empty pass that was never able to be anything else.");
        await Assert.That(executor.Launched).IsEmpty();
    }

    [Test]
    public async Task An_executor_that_could_not_do_its_job_attests_unreachable()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var executor = new RecordingExecutor(
            new SweepExecution.Failed("the tracker refused the credential"));

        var attestation = await Loop(repository, new RecordingProtocol(), executor)
            .SweepAsync(AnAction(repository), CancellationToken.None);

        await Assert.That(attestation.Outcome).IsEqualTo(WatchOutcomes.Unreachable);
        await Assert.That(attestation.Diagnosis!).Contains("refused the credential");
        await Assert.That(attestation.Nominated).IsEmpty();
        await Assert.That(WatchAttestation.Validate(attestation)).IsNull();
    }

    [Test]
    public async Task A_pass_attests_every_sweep_it_was_handed_and_nothing_else()
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var first = AnAction(repository);
        var second = AnAction(repository, diagnosis: "no pin") with
        {
            ActionId = Guid.CreateVersion7(Noon.AddSeconds(1)),
        };
        var protocol = new RecordingProtocol(first, second);

        var swept = await Loop(repository, protocol, new RecordingExecutor(new SweepExecution.Ran([])))
            .PassAsync("nightly-triage", CancellationToken.None);

        await Assert.That(swept).IsEqualTo(2);
        await Assert.That(protocol.Attested.Select(a => a.Attestation.ActionId))
            .IsEquivalentTo([first.ActionId, second.ActionId]);
        await Assert.That(protocol.Attested.Select(a => a.Attestation.AttestationId).Distinct().Count())
            .IsEqualTo(2)
            .Because("each report is its own row, idempotent on its own id.");

        var again = await Loop(repository, protocol, new RecordingExecutor(new SweepExecution.Ran([])))
            .PassAsync("nightly-triage", CancellationToken.None);

        await Assert.That(again).IsEqualTo(0)
            .Because("nothing decided is nothing to attest - a sweep is attested because it was "
                   + "handed out, not because a pass ran.");
    }
}
