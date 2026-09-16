using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A runner whose agent is not logged in holds: it beats, it never claims, it
/// says why, and it resumes the moment a credential arrives.
/// </summary>
/// <remarks>
/// <para>
/// <b>Holding rather than exiting, and the difference is who can fix it.</b>
/// A pool member with no agent login exits 69 at startup once the move-bound
/// probe reads a run that never happened honestly (gg#502) - and an exited
/// container is a machine nobody can reach over the channel to give a
/// credential to. A held runner keeps beating, keeps answering introductions,
/// and is exactly as reachable as a working one. The hold is the parked
/// runner's shape - <i>"it keeps beating and takes no work"</i> - for a cause
/// the machine measured rather than a person declared.
/// </para>
/// <para>
/// <b>The runner's not-claiming is the mechanism; the reading only tells a
/// person why.</b> It is posted when the hold begins and when the standing
/// changes, never on every re-probe: a machine repeating the same sentence
/// every thirty seconds for a week is a control plane's table growing for no
/// reader. And a runner that is ready is not probed at all while idle - a token
/// that dies is discovered by the run that fails, which is the demotion below.
/// </para>
/// <para>
/// <b>A run that could not log in is the flight's misfortune, not its
/// fault.</b> It is given back <c>abandoned</c> - <i>"the flight is untouched"</i>
/// - and the machine holds, rather than routing to <c>BoundBroken</c>, which is
/// terminal and means the machine's governance is unproven. Nothing about the
/// bound was measured wrong; the agent simply could not start.
/// </para>
/// </remarks>
public class AnUnauthenticatedAgentHoldsTheRunnerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private const string Token = "sk-ant-oat01-a-token-the-loop-hands-to-the-probe";

    /// <summary>An adapter whose standing the test sets, and that counts its probes.</summary>
    private sealed class FakeAgent : IAuthenticateAnAgent
    {
        public string Provider => "claude";

        public string Locator => CredentialLocator.ForAgent("claude");

        public string TokenVariable => "CLAUDE_CODE_OAUTH_TOKEN";

        public AgentStanding Standing { get; set; } = HeldStanding(T0);

        public int Probes { get; private set; }

        public List<string?> TokensSeen { get; } = [];

        public Task<AgentStanding> ProbeAsync(string? token, CancellationToken cancellationToken)
        {
            Probes++;
            TokensSeen.Add(token);
            return Task.FromResult(Standing);
        }

        public bool NeedsLogin(string? said) =>
            said?.Contains("/login", StringComparison.Ordinal) == true;
    }

    private static AgentStanding HeldStanding(DateTimeOffset at) => new(
        Authenticated: false,
        Source: AgentCredentialSources.None,
        Diagnosis: "the agent is not logged in and gg holds no token for it",
        MeasuredAt: at);

    private static AgentStanding ReadyStanding(DateTimeOffset at) => new(
        Authenticated: true,
        Source: AgentCredentialSources.Token,
        Diagnosis: "",
        MeasuredAt: at);

    /// <summary>An executor whose every run failed the same way, with no probe to measure.</summary>
    private sealed class CannotLogIn : IExecutorPort
    {
        public ExecutorCapabilities Capabilities => ClaudeCodeExecutor.Capabilities;

        // No session probe: the point of this executor is what the RUN says.
        public bool BoundIsMeasurable => false;

        public Task<ExecutorRun?> ExecuteAsync(
            ExecutorRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<ExecutorRun?>(ExecutorRun.Failed(
                request.LoopId, "Invalid API key · Please run /login",
                attempts: 1, took: TimeSpan.FromMilliseconds(137), movesUsed: []));
    }

    /// <summary>A workspace that prepares an empty tree, so a flight reaches its loop.</summary>
    private sealed class EmptyWorkspace : IWorkspace
    {
        public Task<WorkspaceResult> PrepareAsync(
            string flightId,
            IReadOnlyList<LeaseRepoRef> repos,
            IReadOnlyDictionary<string, string> secretsByLocator,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspaceResult([]) { Reused = false, Root = Path.GetTempPath() });

        public void Release(string flightId) { }

        public HeldTree? Hold(string flightId) => null;

        public int SweepOrphans() => 0;
    }

    private static LeaseGranted ALease() => new()
    {
        LeaseId = "lease-1",
        Generation = 1,
        FlightId = "flight-1",
        FlightNumber = FlightRef.Format(1042),
        Repos = [new LeaseRepoRef { Provider = "local", Slug = "acme/widgets", PinnedRef = "main" }],
        Credentials = [],
        UnresolvedRepos = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        // A LOOP RUNS ONLY FOR A LEASE THAT NAMES WORK; without this the
        // executor is never asked and the test would be about nothing.
        IntentText = "make the greeting say hello",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = "fixture-agent",
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = 60,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    private sealed record Rig(
        RunnerLoop Loop, FakeProtocol Protocol, RecordingObserver Observer,
        FakeAgent Agent, List<TimeSpan> Waited, CancellationTokenSource Stop);

    /// <summary>A loop that stops after the delay delegate has been asked <paramref name="turns"/> times.</summary>
    private static Rig Build(
        FakeAgent agent, AgentStanding? initial, int turns,
        IExecutorPort? executor = null, IWorkspace? workspace = null, string? token = Token)
    {
        var protocol = new FakeProtocol();
        var observer = new RecordingObserver();
        var clock = new MovableClock(T0);
        var stop = new CancellationTokenSource();
        var waited = new List<TimeSpan>();

        var loop = new RunnerLoop(
            protocol, clock,
            (span, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                waited.Add(span);
                clock.Advance(span);
                if (waited.Count >= turns)
                {
                    stop.Cancel();
                }

                return Task.CompletedTask;
            },
            observer, new NoCredentialResolver(), workspace ?? new NoWorkspace(),
            executor: executor,
            agent: agent,
            agentToken: () => token,
            initialStanding: initial);

        return new Rig(loop, protocol, observer, agent, waited, stop);
    }

    [Test]
    public async Task A_held_runner_beats_and_never_asks_for_work()
    {
        var agent = new FakeAgent();
        var rig = Build(agent, HeldStanding(T0), turns: 3);

        var exit = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        await Assert.That(exit).IsEqualTo(0)
            .Because("holding is a session that is still going, not one that ended badly.");
        await Assert.That(rig.Observer.Beats).IsGreaterThan(0)
            .Because("a held runner is exactly as reachable as a working one, and the beat "
                   + "is what carries introductions to it.");
        await Assert.That(rig.Protocol.Calls.Any(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("a runner whose agent cannot start must not take a flight it cannot fly.");
        await Assert.That(rig.Observer.Events.Any(e => e.StartsWith("agent-held:", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task The_hold_is_reported_once_and_not_on_every_re_probe()
    {
        var agent = new FakeAgent();
        var rig = Build(agent, HeldStanding(T0), turns: 4);

        _ = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        var readings = rig.Protocol.AgentReadings;

        await Assert.That(readings.Count).IsEqualTo(1)
            .Because("the standing did not change, so nothing new was said: a machine "
                   + "repeating one sentence every thirty seconds is a table growing for "
                   + "no reader. Re-probes: " + agent.Probes);
        await Assert.That(readings[0].Standing).IsEqualTo(AgentStandings.NeedsLogin);
        await Assert.That(readings[0].Provider).IsEqualTo("claude");
        await Assert.That(readings[0].Source).IsEqualTo(AgentCredentialSources.None);
        await Assert.That(agent.Probes).IsGreaterThan(1)
            .Because("it kept looking - the hold is not a decision made once.");
    }

    [Test]
    public async Task A_credential_kept_for_the_agent_is_probed_at_once_and_claims_resume()
    {
        var agent = new FakeAgent();
        var rig = Build(agent, HeldStanding(T0), turns: 3);

        // What the channel's configure-credential arm does after it writes:
        // tells the loop, which is the one thing the store cannot.
        rig.Observer.OnEvent = said =>
        {
            // The moment the hold is announced, the credential lands.
            if (said.StartsWith("agent-held:", StringComparison.Ordinal))
            {
                agent.Standing = ReadyStanding(T0);
                rig.Loop.CredentialKept(agent.Locator);
            }
        };

        _ = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        await Assert.That(rig.Protocol.Calls.Any(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a runner given the credential it was holding for takes work again "
                   + "without anybody restarting it.");
        await Assert.That(rig.Protocol.AgentReadings.Select(r => r.Standing))
            .IsEquivalentTo([AgentStandings.NeedsLogin, AgentStandings.Ready])
            .Because("once for the hold and once for the recovery, which is what lets the "
                   + "gate open and then close.");
        await Assert.That(rig.Observer.Events).Contains("agent-ready");
        await Assert.That(agent.TokensSeen.Last()).IsEqualTo(Token)
            .Because("the probe measures the credential gg holds, read through the same "
                   + "lookup the launch uses.");
    }

    [Test]
    public async Task A_ready_agent_is_not_probed_while_idle()
    {
        // A token that dies is discovered by the run that fails. Probing an idle
        // runner every turn would be a process launch per poll on every machine
        // in the fleet, to learn a thing the next flight will learn anyway.
        var agent = new FakeAgent { Standing = ReadyStanding(T0) };
        var rig = Build(agent, ReadyStanding(T0), turns: 3);

        _ = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        await Assert.That(agent.Probes).IsEqualTo(0);
        await Assert.That(rig.Protocol.AgentReadings).IsEmpty()
            .Because("nothing changed, so nothing was said.");
    }

    [Test]
    public async Task A_run_that_could_not_log_in_gives_the_flight_back_and_holds()
    {
        // MID-LIFE DEMOTION. The token was fine at startup and is dead now - a
        // revocation, an expiry - and the first flight to find out must not be
        // marked failed for it, and the machine must not read as a broken bound.
        var agent = new FakeAgent { Standing = ReadyStanding(T0) };
        var rig = Build(
            agent, ReadyStanding(T0), turns: 4,
            executor: new CannotLogIn(), workspace: new EmptyWorkspace());
        rig.Protocol.Claims.Enqueue(new ClaimResult.Granted(ALease()));

        _ = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        await Assert.That(rig.Observer.Events).Contains($"released:{RunnerDisposition.Abandoned}")
            .Because("the flight is untouched and somebody else can fly it; failed would say "
                   + "the work was wrong. Seen: " + string.Join(" | ", rig.Observer.Events));
        await Assert.That(rig.Observer.Events.Any(e => e.StartsWith("bound-broke:", StringComparison.Ordinal)))
            .IsFalse()
            .Because("nothing about the bound was measured wrong - the agent could not start.");
        await Assert.That(rig.Observer.Events.Any(e => e.StartsWith("agent-held:", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(rig.Protocol.AgentReadings.Any(r => r.Standing == AgentStandings.NeedsLogin))
            .IsTrue();
        await Assert.That(rig.Protocol.Calls.Count(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("it took one flight, learned, and asked for no more.");
    }

    [Test]
    public async Task A_control_plane_that_does_not_serve_the_route_yet_does_not_stop_the_hold()
    {
        // The pin lags the contract: a control plane at 0.164.0 answers 404.
        // The report is best-effort, like the allowance reading - the runner's
        // not-claiming is the mechanism, and it does not depend on being heard.
        var agent = new FakeAgent();
        var rig = Build(agent, HeldStanding(T0), turns: 3);
        rig.Protocol.AgentThrows.Enqueue(new HttpRequestException(
            "Response status code does not indicate success: 404 (Not Found).",
            inner: null, statusCode: System.Net.HttpStatusCode.NotFound));

        var exit = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        await Assert.That(exit).IsEqualTo(0);
        await Assert.That(rig.Observer.Beats).IsGreaterThan(0);
        await Assert.That(rig.Protocol.Calls.Any(c => c.StartsWith("claim:", StringComparison.Ordinal)))
            .IsFalse();
    }

    [Test]
    public async Task A_reading_nobody_heard_is_said_again_until_it_lands()
    {
        // FOUND IN PRODUCTION, and the reason an agent-login gate never
        // appeared for a member that was holding perfectly correctly. The
        // reading was marked said at the top of the method, before the send -
        // so one lost POST convinced the loop the control plane knew, and it
        // stayed silent for the life of the process. Nothing on either side
        // recorded that anything had gone wrong.
        //
        // A RUNNER MAY BE WRONG ABOUT ITS AGENT; it must not be wrong about
        // whether anybody was told. Best-effort is about the hold not
        // depending on being heard - not about giving up on being heard.
        var agent = new FakeAgent();
        var rig = Build(agent, HeldStanding(T0), turns: 4);
        rig.Protocol.AgentThrows.Enqueue(new HttpRequestException(
            "Response status code does not indicate success: 404 (Not Found).",
            inner: null, statusCode: System.Net.HttpStatusCode.NotFound));

        _ = await rig.Loop.RunAsync("runner-1", [], rig.Stop.Token);

        await Assert.That(rig.Protocol.AgentReadings.Count).IsEqualTo(1)
            .Because("the first attempt was lost and the next one was heard - and once it "
                   + "had been heard the standing had not changed, so the runner went quiet "
                   + "again. Both halves matter: a retry that never stops is the table "
                   + "growing for no reader that the test above forbids.");
        await Assert.That(rig.Protocol.AgentReadings[0].Standing)
            .IsEqualTo(AgentStandings.NeedsLogin);
        await Assert.That(rig.Observer.Events.Count(
                e => e.StartsWith("agent-held:", StringComparison.Ordinal)))
            .IsEqualTo(1)
            .Because("saying it again to the control plane is not saying it again to the "
                   + "person watching the runner: the hold began once, and a line repeated "
                   + "every thirty seconds is how a log stops being read.");
    }

    // ---- the startup decision, pure ----

    private static ProbeResult AProbe(bool bound, string diagnosis) => new()
    {
        Bound = bound,
        Diagnosis = diagnosis,
        Took = TimeSpan.FromSeconds(1),
        MeasuredAt = T0,
        Workspace = "/tmp/probe",
        Held = bound ? ["Edit", "Write"] : [],
        Broke = [],
    };

    [Test]
    public async Task The_startup_decision_holds_for_a_login_and_refuses_everything_else_unmeasured()
    {
        var agent = new FakeAgent();

        await Assert.That(StartupDecision.Decide(standing: null, probe: null, agent: null))
            .IsEqualTo(StartupOutcome.Fly)
            .Because("a runner with no agent and nothing to measure flies as it always did.");

        await Assert.That(StartupDecision.Decide(HeldStanding(T0), probe: null, agent))
            .IsEqualTo(StartupOutcome.Hold)
            .Because("the agent said so before any probe ran, and a probe would only "
                   + "measure the absence again.");

        await Assert.That(StartupDecision.Decide(ReadyStanding(T0), AProbe(true, "held"), agent))
            .IsEqualTo(StartupOutcome.Fly);

        await Assert.That(StartupDecision.Decide(
                ReadyStanding(T0),
                AProbe(false, "could not be measured: Invalid API key · Please run /login"),
                agent))
            .IsEqualTo(StartupOutcome.Hold)
            .Because("the probe's agent could not start for want of a login, which is the "
                   + "one unmeasured cause a person on a gate can fix without visiting.");

        await Assert.That(StartupDecision.Decide(
                ReadyStanding(T0), AProbe(false, "could not be measured: the binary is not there"), agent))
            .IsEqualTo(StartupOutcome.Refuse)
            .Because("gg#502's rule stands for every other cause: an unmeasured bound is a "
                   + "machine whose governance is unproven, and it exits 69.");

        await Assert.That(StartupDecision.Decide(
                ReadyStanding(T0), AProbe(false, "An agent with only 'read' declared put bytes on disk"), agent))
            .IsEqualTo(StartupOutcome.Refuse)
            .Because("a bound that BROKE is never a hold.");
    }
}
