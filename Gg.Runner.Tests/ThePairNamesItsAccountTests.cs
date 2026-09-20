using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Sweeps;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A declared pair may name the account its credential acts as, and a sweep
/// attests it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S42.6-01; slice forty-two rules 16 and 17.</b> A sweep reads a tracker
/// with a credential this machine holds, and the attestation has never said as
/// WHOM. "Swept, 3 nominated" is the same sentence whether it read as a service
/// account nobody minds or as a person whose queue it emptied - and the second
/// is a thing somebody would want to know without going to the tracker to find
/// out.
/// </para>
/// <para>
/// <b>A fact, never a secret.</b> An account name is what a credential acts as;
/// knowing it grants nothing, which is <c>CredentialReference</c>'s own rule
/// for the locator beside it. It is declared where the pair is declared,
/// because the machine that holds the credential is the only side that knows.
/// </para>
/// <para>
/// <b>Not part of the match.</b> A watch names a host and a credential, and
/// those two pair it with a runner; adding a third field to that comparison
/// would silently unpair every watch whose operator had not yet written one.
/// </para>
/// </remarks>
public class ThePairNamesItsAccountTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private const string Host = "https://tracker.example/acme";
    private const string Credential = "tracker-read";

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Noon;
    }

    private sealed class RecordingProtocol(params WatchAction[] decided) : ISweepProtocol
    {
        private readonly Queue<WatchAction[]> _pulls = new([decided]);

        internal List<WatchAttestation> Attested { get; } = [];

        public Task<WatchActionList> PullSweepsAsync(
            string watch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WatchActionList
            {
                Actions = _pulls.TryDequeue(out var next) ? next : [],
            });

        public Task<WatchActionList> ClaimSweepsAsync(
            SweepClaim claim, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("this double serves the named pull, not a claim.");

        public Task AttestSweepAsync(
            string watch, WatchAttestation attestation, CancellationToken cancellationToken = default)
        {
            Attested.Add(attestation);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExecutor(SweepExecution answer) : ISweepExecutor
    {
        public Task<SweepExecution> RunAsync(
            SweepRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(answer);
    }

    private static WatchDocument AWatch() => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = Host,
        Credential = Credential,
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
        TheRunnerReadsTheSkillAtItsPinTests.SkillRepository repository) => new()
    {
        ActionId = Guid.CreateVersion7(Noon),
        Watch = "nightly-triage",
        WatchVersion = "nightly-triage@v1",
        Document = AWatch(),
        Executor = WatchExecutors.Instructions,
        Moves = [LoopMoves.Read, LoopMoves.Propose],
        Skill = new LeaseRepoRef
        {
            Provider = LocalVcsAdapter.ProviderKey,
            Slug = repository.BarePath,
            PinnedRef = repository.Reviewed,
        },
        DecidedAt = Noon.AddMinutes(-5),
    };

    private static async Task<WatchAttestation> SweptAsync(string declared)
    {
        using var repository = new TheRunnerReadsTheSkillAtItsPinTests.SkillRepository();
        var protocol = new RecordingProtocol(AnAction(repository));

        var loop = new SweepLoop(
            protocol,
            new SkillReader(
                [new LocalVcsAdapter(repository.Directory)],
                Path.Combine(Path.GetTempPath(), "gg-account-cache", Guid.NewGuid().ToString("n")),
                secretFor: _ => Task.FromResult<string?>(null)),
            new RecordingExecutor(new SweepExecution.Ran([])),
            new FixedClock(),
            transcripts: Path.Combine(
                Path.GetTempPath(), "gg-account-transcripts", Guid.NewGuid().ToString("n")),
            trackers: IntentConfiguration.ServedTrackers(declared));

        await loop.PassAsync("nightly-triage", CancellationToken.None);

        return protocol.Attested.Single();
    }

    [Test]
    public async Task An_entry_may_name_the_account_its_credential_acts_as()
    {
        var trackers = IntentConfiguration.ServedTrackers(
            $"tracker={Host}|{Credential}|svc-triage");

        await Assert.That(trackers.Single().Account).IsEqualTo("svc-triage");
    }

    [Test]
    public async Task An_entry_without_one_attests_no_account()
    {
        // TWO FIELDS IS EVERY PAIR DECLARED BEFORE THIS, and a parser that
        // demanded three would refuse every machine already sweeping.
        var trackers = IntentConfiguration.ServedTrackers($"tracker={Host}|{Credential}");

        await Assert.That(trackers.Single().Account).IsNull();
        await Assert.That(trackers.Single().Locator).IsEqualTo(Credential);
    }

    [Test]
    public async Task The_account_is_not_part_of_the_match()
    {
        // A WATCH PAIRS ON A HOST AND A CREDENTIAL. Comparing a third field
        // would unpair every watch whose operator had not written one yet, and
        // a sweep that stops being served has no sentence anywhere saying why.
        var claim = ResidentSweeps.ClaimFor(
            "on", IntentConfiguration.ServedTrackers($"tracker={Host}|{Credential}|svc-triage"));

        await Assert.That(claim!.Serves.Single().Host).IsEqualTo(Host);
        await Assert.That(claim.Serves.Single().Credential).IsEqualTo(Credential);
    }

    [Test]
    public async Task A_sweep_attests_the_account_its_pair_declared()
    {
        var attestation = await SweptAsync($"tracker={Host}|{Credential}|svc-triage");

        await Assert.That(attestation.Account).IsEqualTo("svc-triage")
            .Because("\"swept, 3 nominated\" is the same sentence whoever it read as, and "
                   + "which one it was is a thing somebody would want to know without going "
                   + "to the tracker to find out.");
    }

    [Test]
    public async Task A_sweep_whose_pair_names_no_account_attests_none()
    {
        var attestation = await SweptAsync($"tracker={Host}|{Credential}");

        await Assert.That(attestation.Account).IsNull()
            .Because("an entry without one attests none rather than guessing, which is what "
                   + "a machine that named the locator instead would be doing.");
    }
}
