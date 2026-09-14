using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight whose push was refused has not landed.
/// </summary>
/// <remarks>
/// <para>
/// <b>GG-117, exactly.</b> An implement flight edited one file - its
/// <c>change.manifest</c> names one path and its <c>loop.digest</c> says
/// <c>1 edited</c> - the runner was cleared to push, and the credential
/// registered for the repository carried read. The runner said so:
/// <i>"the credential registered for &lt;repo&gt; carries read and pushing needs
/// write"</i>. Then it released the lease as <c>completed</c> and the flight
/// recorded <c>landed</c>. There is no pull request and there never was.
/// </para>
/// <para>
/// <b>The refusal had nowhere to go.</b> The landing observes it and returns
/// nothing, so the disposition is computed from the run and the landing
/// DECISION - neither of which knows whether the push that decision cleared
/// actually happened. A clearance is a permission, not a receipt.
/// </para>
/// <para>
/// <b>The same family as the failure one release back, and worth naming as a
/// family:</b> a disposition read from something other than what happened. That
/// one read the landing and never the loop's own outcome; this one reads the
/// landing's PERMISSION and never its RESULT. Both produce <c>landed</c> for a
/// flight that landed nowhere - and <c>completed</c> maps to <c>landed</c> with
/// the exit claim first-writer-wins, so nothing corrects it afterwards.
/// </para>
/// <para>
/// <b>A flight with nothing to push is a different thing</b> and must keep
/// completing: a scoring flight lands by writing a tracker field and never has
/// a branch. What makes this outstanding is a push that was CLEARED and refused.
/// </para>
/// </remarks>
public class ARefusedPushDoesNotLandTests
{
    /// <summary>An agent that did the work, so there is something to push.</summary>
    /// <remarks>
    /// The loop has to COMPLETE for this scenario: a run that failed would be
    /// outstanding for the reason the sibling ratchet already holds, and would
    /// pass this test without the push ever being reached.
    /// </remarks>
    private sealed class DidTheWork : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExecutorRun?>(ExecutorRun.Completed(
                "implement", "edited one file", 42, TimeSpan.FromMinutes(6), [LoopMoves.Edit]));
    }

    private static readonly DateTimeOffset T0 = new(2026, 9, 14, 21, 11, 0, TimeSpan.Zero);

    private static LeaseGranted ALease(GitFixture fixture, params string[] scopes) => new()
    {
        LeaseId = "lease-fixture",
        Generation = 1,
        FlightId = Guid.NewGuid().ToString(),
        FlightNumber = "GG-117",
        Repos =
        [
            // THE PROVIDER THAT AUTHENTICATES, wrapping the local one that does
            // not. file:// has nothing to authenticate to, so the local adapter
            // refuses a credential outright - and a lease carrying one cannot
            // even materialize, which is a refusal several steps short of the
            // landing this is about.
            new LeaseRepoRef
            {
                Provider = AuthenticatingProvider.Key,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials =
        [
            new CredentialReference
            {
                Kind = CredentialKinds.Local,
                Locator = CredentialLocator.ForRepo(fixture.BarePath),
                Identity = "gg-fixture",
                Scopes = scopes,
            },
        ],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = "fixture-agent",
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 60,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    /// <summary>
    /// Flies one turn whose push is cleared, with the credential this names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Through the whole loop rather than at the disposition helper</b>,
    /// because that helper is internal and nothing in this repository makes
    /// internals visible to a test - a deliberate absence, and not one to
    /// overturn for a convenience. What a person sees is the release, so the
    /// release is what this reads.
    /// </para>
    /// <para>
    /// <b>The resolver holds the secret and a destination is configured</b>, so
    /// the only thing between this flight and a pushed branch is the scope on
    /// the credential. An unresolvable credential and a runner with nowhere to
    /// land are each their own refusal, earlier and elsewhere, and either would
    /// pass these assertions without the defect being present at all.
    /// </para>
    /// </remarks>
    private static async Task<(RecordingObserver Observer, FakeProtocol Protocol,
        RecordingDestination Destination)> FlownAsync(string[] scopes, bool byHand = false)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);

        var resolver = new ScriptedResolver();
        resolver.Secrets[CredentialLocator.ForRepo(fixture.BarePath)] = "a-secret";

        var protocol = new FakeProtocol
        {
            // CLEARED TO PUSH, which is the premise: the control plane said the
            // work may land, and the machine could not.
            Push = new BranchPush
            {
                Branch = "gg/GG-117",
                BaseRef = "main",
                Slug = fixture.BarePath,
                Reason = "no machine obligation is violated",
            },
            Admission = new DestinationAdmission
            {
                DestinationId = "pull-request",
                Branch = "gg/GG-117",
                BaseRef = "main",
                Slug = fixture.BarePath,
                Reason = "every obligation holds",
            },
        };
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture, scopes)));

        var observer = new RecordingObserver();
        var destination = new RecordingDestination();
        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("released:", StringComparison.Ordinal))
            {
                stopping.Cancel();
            }
        };

        await new RunnerLoop(protocol, clock,
                (span, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    clock.Advance(span);
                    return Task.CompletedTask;
                },
                observer, resolver,
                trees.Workspace(new AuthenticatingProvider(new LocalVcsAdapter(fixture.Directory))),
                // THE OTHER EXECUTOR THROUGH THE SAME LANDING. A person held the
                // terminal instead of an agent and said they were done; the push
                // that follows is the runner's either way, and so is the refusal.
                executor: byHand
                    ? new AttendedExecutor(
                        "claude", [], announce: TextWriter.Null,
                        spawn: (_, _) => Task.FromResult<int?>(0),
                        versionOf: (_, _) => Task.FromResult("2.1.261"))
                    : new DidTheWork(),
                destinations: [destination],
                returns: byHand
                    ? (_, flight) => (new TakeoverReturn
                    {
                        FlightId = flight,
                        Outcome = TakeoverOutcomes.Completed,
                    }, null)
                    : null)
        {
            HoldFor = TimeSpan.FromSeconds(3),
        }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return (observer, protocol, destination);
    }

    [Test]
    public async Task A_cleared_push_that_was_refused_does_not_report_completed()
    {
        var (_, protocol, destination) = await FlownAsync([CredentialScopes.Read]);

        await Assert.That(destination.Calls).IsEmpty()
            .Because("the premise. A destination that was reached would make this a test "
                   + "about something else - what stops this flight is the scope on the "
                   + "credential, which is checked before the adapter is looked up.");

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsNotEqualTo(RunnerDisposition.Completed)
            .Because("`completed` maps to `landed` and the exit claim is first-writer-wins, "
                   + "so a flight whose work never left the machine is recorded as finished "
                   + "and nothing corrects it. GG-117 says landed and has no pull request.");
    }

    [Test]
    public async Task It_is_left_for_somebody_rather_than_concluded()
    {
        // OUTSTANDING AND NOT FAILED, which is the distinction the sibling
        // ratchet draws: a failed loop will fail the same way on the next
        // runner, and this one will not. The work is real, it is on a machine,
        // and a credential with the right scope is all it needs - which is a
        // thing somebody can do rather than a conclusion about the work.
        var (observer, protocol, destination) = await FlownAsync([CredentialScopes.Read]);

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsEqualTo(RunnerDisposition.Outstanding)
            .Because("what the runner did: " + string.Join(" | ", observer.Events));
    }

    [Test]
    public async Task A_hand_flown_flight_is_not_concluded_by_the_person_who_flew_it_either()
    {
        // THE SAME HOLE ON THE OTHER ARM, and the person's word is not what is
        // wrong with it. They say they finished THEIR work, and they did - what
        // they cannot answer is whether the runner's push reached a remote
        // afterwards, because it happens after they have given the terminal
        // back. `completed` is the one disposition that ENDS a flight, so
        // taking their word for a landing they never saw closes it.
        var (observer, protocol, _) = await FlownAsync([CredentialScopes.Read], byHand: true);

        await Assert.That(AttendedReturnTests.ReleasedWith(protocol))
            .IsEqualTo(RunnerDisposition.Outstanding)
            .Because("what the runner did: " + string.Join(" | ", observer.Events));
    }

    [Test]
    public async Task Nothing_says_it_landed()
    {
        // The fact and the state have to agree. A flight whose state says
        // landed and whose evidence carries no destination.landed is exactly
        // what took a journal on the runner to notice.
        var (_, protocol, _) = await FlownAsync([CredentialScopes.Read]);

        await Assert.That(protocol.ShippedFacts.SelectMany(b => b.Items)
                .Any(f => string.Equals(
                    f.Kind, FactKinds.DestinationLanded, StringComparison.Ordinal)))
            .IsFalse();
    }
}
