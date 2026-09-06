using System.Diagnostics;
using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Local;
using Gg.Runner;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A person flies the flight, at a Claude Code prompt, in the flight's own
/// tree — and the runner around them is the same runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>The terminal is inherited, not redirected.</b> This is the opposite of
/// the headless executor, which redirects everything precisely so no terminal
/// is involved. Here a person is at the keyboard and the child owns the screen
/// until it exits — so there is no stream to parse, and everything the headless
/// path learns by reading one is unavailable.
/// </para>
/// <para>
/// <b>Which is why it answers null.</b> <c>ExecutorRun</c> requires an outcome,
/// an attempt count and a moves list, and an attended session measured none of
/// them. <c>RunnerLoop.ShipAsync</c> already ships the environment, the change
/// manifest and the source provenance when the run is null, so the loop is
/// already the right shape for rule 3 and the port's return type was not.
/// </para>
/// <para>
/// <b>Asserted over the argument list and the start info</b>, because a flag
/// this runner meant to pass and did not is invisible to a test that stops at
/// the configuration — and on this path the flag that matters most is the one
/// that clears the operator's own settings.
/// </para>
/// </remarks>
public class AttendedExecutorTests
{
    private static readonly IntentReader Tracker = new(
        "jira", "jira-mcp", ["--stdio"], "JIRA_TOKEN", null);

    private static readonly SelfInvocation Self = SelfInvocation.For("/bin/gg", null)!;

    private static ExecutorRequest Request(
        IReadOnlyList<string>? moves = null, string? provider = null) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "implement",
        Moves = moves ?? [LoopMoves.Read, LoopMoves.Edit],
        IntentProvider = provider,
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    private static IReadOnlyList<string> Arguments(
        ExecutorRequest request, string? secret = null) =>
        AttendedExecutor.StartInfoFor(request, [Tracker], secret, Self).ArgumentList!;

    // ---- S26.1-02: the terminal is inherited ----

    [Test]
    public async Task The_terminal_is_inherited_rather_than_redirected()
    {
        // THE PROPERTY, AND IT IS THE WHOLE DIFFERENCE. A later tidy-up toward
        // the headless executor's ProcessStartInfo - they are two dozen lines
        // apart and look alike - would silently take the screen away from the
        // person this executor exists for, and the flight would then hang on a
        // child waiting for a terminal nobody is at.
        var info = AttendedExecutor.StartInfoFor(Request(), [Tracker], null, Self);

        await Assert.That(info.RedirectStandardOutput).IsFalse();
        await Assert.That(info.RedirectStandardError).IsFalse();
        await Assert.That(info.RedirectStandardInput).IsFalse();

        // No shell, the same as the headless path: a shell would decide for
        // itself what the child inherits.
        await Assert.That(info.UseShellExecute).IsFalse();

        // The tree, so whatever they run is already looking at the work.
        await Assert.That(info.WorkingDirectory).IsEqualTo("/work/flight");
    }

    // ---- S26.1-03: nothing is added to the capability declaration ----

    [Test]
    public async Task It_declares_what_the_headless_executor_declares()
    {
        await Assert.That(((IExecutorPort)Executor()).Capabilities)
            .IsEqualTo(ClaudeCodeExecutor.Capabilities);
    }

    [Test]
    public async Task The_capability_record_is_still_one_member()
    {
        // THE RATCHET, AND THE REASON IT POINTS THIS WAY. Seven members were
        // deleted at slice twenty because nothing ever degraded against any of
        // them, and IExecutorPort.Capabilities has never been called by
        // production at all. A second adapter is precisely the moment somebody
        // starts declaring things about it again - which is what this slice's
        // brief asked for before step 0 opened the type.
        //
        // What an attended session cannot measure goes on a FACT, where a
        // reader is, rather than on a declaration nothing consults.
        var members = typeof(ExecutorCapabilities)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => !string.Equals(n, "EqualityContract", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[] { nameof(ExecutorCapabilities.Rung) });
    }

    // ---- S26.1-04: the operator's settings are cleared ----

    [Test]
    public async Task The_operators_settings_and_servers_are_cleared()
    {
        // MEASURED, NOT ASSUMED, and the measurement is this slice's step 0.
        // With a two-tool allowlist and these flags the session reports
        // permissionMode `default` and no MCP servers; WITHOUT them, the same
        // allowlist, and the session reports permissionMode `auto` - the
        // operator's own - with the operator's plugin servers attached.
        //
        // So this is the only thing standing between an attended flight and a
        // machine whose ~/.claude/settings.json says skipAutoPermissionPrompt.
        // The bound is not `--allowedTools`; the bound is this.
        var arguments = Arguments(Request());

        var sources = arguments.ToList().IndexOf("--setting-sources");
        await Assert.That(sources).IsGreaterThanOrEqualTo(0)
            .Because("without it the operator's permission mode decides what this session may do.");
        await Assert.That(arguments[sources + 1]).IsEqualTo("");

        await Assert.That(arguments).Contains("--strict-mcp-config");
    }

    // ---- S26.1-05: the moves reach the tool list ----

    [Test]
    public async Task The_declared_moves_reach_the_allowed_tools_flag()
    {
        var arguments = Arguments(Request([LoopMoves.Read, LoopMoves.Edit]));

        await Assert.That(arguments).Contains("--allowedTools");
        await Assert.That(arguments).Contains(ClaudeCodeExecutor.ToolFor(LoopMoves.Edit));
    }

    [Test]
    public async Task A_loop_that_declares_no_moves_grants_no_move_tools()
    {
        // A HUMAN-RUNG LOOP ARRIVES EXACTLY THIS WAY, and the contract is what
        // makes it so: Envelope.Validate refuses a loop at the `human` rung that
        // declares any move, because "moves are bound by the executor a runner
        // starts, and this rung starts none - so the move would be granted by
        // the envelope and enforced by nothing."
        //
        // Which means an attended flight is a FRONTIER loop a person operates,
        // not a human-rung loop. The two readings are mutually exclusive and
        // this is the one that has an agent in it.
        var arguments = Arguments(Request(moves: []));

        await Assert.That(arguments).DoesNotContain(ClaudeCodeExecutor.ToolFor(LoopMoves.Edit));
        await Assert.That(arguments).DoesNotContain(ClaudeCodeExecutor.ToolFor(LoopMoves.Write));
    }

    // ---- S26.1-09: nothing is typed into the child ----

    [Test]
    public async Task Nothing_is_typed_into_the_child()
    {
        // SANDCASTLE'S FINDING, KEPT. No flag pre-fills the composer without
        // submitting, and screen-scraping another program's terminal breaks
        // whenever that program is improved. The person is TOLD what the flight
        // is about; they are not driven.
        //
        // -p is the tell: it is the headless flag, it carries the prompt, and
        // its presence here would mean the person had been handed a session
        // that already answered for them.
        var arguments = Arguments(Request());

        await Assert.That(arguments).DoesNotContain("-p");
        await Assert.That(arguments).DoesNotContain("--print");
        await Assert.That(arguments).DoesNotContain("--output-format");

        await Assert.That(arguments.Any(a => a.Contains("Work ", StringComparison.Ordinal))).IsFalse()
            .Because("the headless prompt begins that way, and nothing composes one here.");
    }

    // ---- S26.4-04: what clearing the operator's settings costs them ----

    [Test]
    public async Task The_person_is_told_what_was_taken_away()
    {
        // RULE 10, AND IT IS SAID BEFORE THE CHILD STARTS, because once it
        // starts the screen is its own and nothing of ours is read again.
        //
        // A person whose plugins, servers and permission mode have silently
        // vanished concludes the tool is broken - and they are the one who chose
        // those settings, so the disappearance is startling in a way it is not
        // for a fleet runner nobody is watching. Step 0 measured that this is
        // the whole bound on an attended session: the allowlist shrinks the tool
        // surface not at all, and `--setting-sources ""` is the only lever.
        //
        // So the cost is not a footnote. It is the sentence that makes the
        // difference between a governed session and a broken one.
        var said = new StringWriter();
        var executor = new AttendedExecutor(
            "claude", [Tracker], announce: said,
            spawn: (_, _) => Task.FromResult<int?>(0));

        await executor.ExecuteAsync(Request(), CancellationToken.None);

        var spoken = said.ToString();

        await Assert.That(spoken).Contains("settings");
        await Assert.That(spoken.Contains("plugin", StringComparison.OrdinalIgnoreCase)
                       || spoken.Contains("tool server", StringComparison.OrdinalIgnoreCase))
            .IsTrue()
            .Because("naming only 'settings' leaves a person wondering which - and the two they "
                   + "notice missing are their plugins and their servers.");

        await Assert.That(spoken).Contains("/work/flight")
            .Because("where the work is, because a person about to type in a tree should be told "
                   + "which tree it is.");
    }

    // ---- S26.1-07: a child that will not start ----

    [Test]
    public async Task A_child_that_will_not_start_is_a_refusal_naming_the_command()
    {
        var executor = new AttendedExecutor(
            "/does/not/exist/gg-no-such-agent", [Tracker], announce: TextWriter.Null);

        var run = await executor.ExecuteAsync(Request(), CancellationToken.None);

        await Assert.That(run).IsNotNull()
            .Because("a flight that looks flown and was not is worse than one that refused.");
        await Assert.That(run!.Outcome).IsEqualTo(LoopOutcomes.Failed);
        await Assert.That(run.Reason).Contains("gg-no-such-agent");
    }

    // ---- rule 3: a session that ran measures nothing ----

    [Test]
    public async Task A_session_that_ran_answers_with_no_measurement_at_all()
    {
        // NULL IS THE ANSWER, and it is a real one. Attempts, moves used and an
        // outcome are all REQUIRED on ExecutorRun, and an attended session
        // measured none of them: there is no stream to read, because the child
        // owned the screen.
        //
        // A helpful default here is the danger this whole step is written
        // around - Attempts = 0 and MovesUsed = [] are both expressible and
        // both false, and [] in particular would silently detach every
        // move-gate on every hand-flown flight.
        var spawned = new List<ProcessStartInfo>();
        var executor = new AttendedExecutor(
            "claude", [Tracker], announce: TextWriter.Null,
            spawn: (info, _) => { spawned.Add(info); return Task.FromResult<int?>(0); });

        var run = await executor.ExecuteAsync(Request(), CancellationToken.None);

        await Assert.That(run).IsNull();
        await Assert.That(spawned.Count).IsEqualTo(1);
    }

    // ---- S26.1-10: the ratchet on the widening ----

    [Test]
    public async Task The_headless_executor_still_answers_with_a_run()
    {
        // WHAT THE WIDENING COSTS IF NOBODY WATCHES IT. Rule 3 is implemented by
        // letting the port answer "no measurement at all", and a null that means
        // "a person was at the keyboard" is one keystroke from meaning "the fleet
        // stopped reporting". The difference is not in the type.
        //
        // This is how `accepts:` reached the operator table and never the
        // widening comparison: a field added on one side of a pair, with the
        // guard on the other side asserting something that could not fail.
        //
        // Two of the headless executor's six endings are reachable without a
        // real binary, and both are asserted below. The other four - completed,
        // failed, blocked and exhausted - need an agent, so what holds them here
        // is the declared return type: ClaudeCodeExecutor's own ExecuteAsync is
        // NOT nullable, and making it so is what this would catch.
        var method = typeof(ClaudeCodeExecutor).GetMethod(
            nameof(ClaudeCodeExecutor.ExecuteAsync),
            BindingFlags.Public | BindingFlags.Instance)!;

        var returned = new NullabilityInfoContext().Create(method.ReturnParameter);

        await Assert.That(returned.GenericTypeArguments[0].ReadState)
            .IsEqualTo(NullabilityState.NotNull)
            .Because("the headless path has no ending that measures nothing, and the day it "
                   + "does the fleet stops reporting without anybody being told.");
    }

    [Test]
    public async Task A_headless_child_that_will_not_start_still_answers_with_a_run()
    {
        var executor = new ClaudeCodeExecutor(
            "/does/not/exist/gg-no-such-agent", [Tracker], secretFor: null, Self);

        var run = await executor.ExecuteAsync(Request(), CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Failed);
    }

    [Test]
    public async Task A_headless_reader_that_will_not_resolve_still_answers_with_a_run()
    {
        // The second reachable ending: a tracker whose credential is declared
        // and absent is refused before the process starts, and that refusal is
        // a run rather than a silence.
        var executor = new ClaudeCodeExecutor(
            "claude", [Tracker], secretFor: _ => null, Self);

        var run = await executor.ExecuteAsync(Request(provider: "jira"), CancellationToken.None);

        await Assert.That(run.Outcome).IsEqualTo(LoopOutcomes.Failed);
    }

    // ---- the loop around it: S26.1-01, S26.1-06, S26.1-08 ----

    private static readonly DateTimeOffset T0 = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A lease whose loop an attended session will fly.</summary>
    [Test]
    public async Task A_hand_flown_flight_ships_what_it_could_not_measure()
    {
        // RULE 3, AT THE FAR END. The executor answers null because it measured
        // no loop; this is where that absence becomes something a person on the
        // other side can read. Without it the control plane sees a flight that
        // shipped an environment and a manifest and simply never mentioned a
        // loop - which is indistinguishable from a runner that crashed before
        // invoking one.
        var (_, protocol) = await FlownAsync();

        var attended = Shipped(protocol, FactKinds.LoopAttended);

        await Assert.That(attended).Count().IsEqualTo(1);

        var declared = attended[0].Attended!;
        await Assert.That(declared.Unmeasured).IsEquivalentTo(AttendedGaps.All)
            .Because("all three: nothing counted a turn, nothing saw a move, and the bound "
                   + "was not probed because probing means handing a person the canary task.");
        await Assert.That(declared.LoopId).IsEqualTo("implement");
        await Assert.That(declared.Binary).IsEqualTo("claude");
        await Assert.That(declared.BinaryVersion).IsNotEmpty();
        await Assert.That(declared.BudgetSeconds).IsEqualTo(600);
    }

    [Test]
    public async Task It_records_the_rung_the_loop_declared_rather_than_that_a_person_was_there()
    {
        // THE LEASE DECLARES frontier AND A PERSON FLEW IT. Recording `human`
        // here because somebody sat at the terminal would make every later count
        // of how much the machine did wrong in the flattering direction, on the
        // one measurement this product exists to be honest about.
        var (_, protocol) = await FlownAsync();

        await Assert.That(Shipped(protocol, FactKinds.LoopAttended)[0].Attended!.Rung)
            .IsEqualTo(ExecutorRungs.Frontier);
    }

    [Test]
    public async Task It_names_the_settings_sources_it_actually_cleared()
    {
        // RULE 10, AND READ FROM THE ARGUMENTS RATHER THAN BELIEVED. What this
        // reports is what the launch actually passed, so a flag dropped in a
        // refactor changes the fact rather than leaving it asserting a bound
        // that stopped being applied.
        var (_, protocol) = await FlownAsync();

        var cleared = Shipped(protocol, FactKinds.LoopAttended)[0].Attended!.SettingsCleared;

        await Assert.That(cleared).Contains("setting-sources");
        await Assert.That(cleared).Contains("mcp-servers");
    }

    [Test]
    public async Task A_hand_flown_flight_ships_no_loop_outcome_and_no_digest()
    {
        // ASSERTED AS AN ABSENCE, because the failure mode is a helpful default
        // rather than an error. Attempts = 0 and MovesUsed = [] are both
        // expressible and both false, and an empty moves list DISCHARGES a move
        // obligation rather than halting it - so a loop.outcome invented here
        // would switch off every move-gate on every hand-flown flight with
        // nothing thrown and nothing logged.
        var (_, protocol) = await FlownAsync();

        await Assert.That(Shipped(protocol, FactKinds.LoopOutcome)).IsEmpty();
        await Assert.That(Shipped(protocol, FactKinds.LoopDigest)).IsEmpty();
        await Assert.That(Shipped(protocol, FactKinds.LoopTranscript)).IsEmpty();
    }

    [Test]
    public async Task The_facts_a_person_and_an_agent_both_produce_still_ship()
    {
        // THE SINGLE STRONGEST REASON THIS DESIGN IS CHEAP. A person editing a
        // tree measures identically to an agent editing it, because the
        // extractor reads the TREE and not the actor - so the halt is scoped to
        // what is genuinely unmeasurable rather than to attended flights as a
        // class.
        var (_, protocol) = await FlownAsync();

        await Assert.That(Shipped(protocol, FactKinds.EnvironmentIdentity)).IsNotEmpty();
        await Assert.That(Shipped(protocol, FactKinds.SourceProvenance)).IsNotEmpty();
    }

    [Test]
    public async Task A_headless_flight_declares_no_attended_session()
    {
        // THE RATCHET ON THE OTHER SIDE. A loop.attended beside a loop.outcome
        // would be a session claiming both that it measured a loop and that it
        // could not - and the port answering a declaration by default is how
        // that would arrive.
        await Assert.That(
            await new ClaudeCodeExecutor("claude", []).AttendedAsync(
                Request(), TimeSpan.FromMinutes(5), CancellationToken.None)).IsNull();
    }

    /// <summary>Every fact of one kind the runner put on the wire.</summary>
    private static IReadOnlyList<FactEnvelope> Shipped(FakeProtocol protocol, string kind) =>
    [
        .. protocol.ShippedFacts
            .SelectMany(batch => batch.Items)
            .Where(fact => string.Equals(fact.Kind, kind, StringComparison.Ordinal)),
    ];

    private static LeaseGranted ALease(GitFixture fixture, int number, int wallClockSeconds = 600)
        => new()
    {
        LeaseId = $"lease-{number}",
        Generation = number,
        FlightId = $"flight-{number}",
        FlightNumber = FlightRef.Format(1000 + number),
        Repos =
        [
            new LeaseRepoRef
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            },
        ],
        Credentials = [],
        ClassificationCeiling = Classifications.Internal,
        ClassificationRules = ClassificationRules.Default,
        ExpiresAt = T0.AddMinutes(10),
        RenewWithinSeconds = 5,
        IntentUri = "https://forge.example/acme/widgets/issues/1",
        Loop = new LeaseLoop
        {
            LoopId = "implement",
            Executor = ExecutorRungs.Frontier,
            Moves = [LoopMoves.Read, LoopMoves.Edit],
            WallClockSeconds = wallClockSeconds,
            OnExhaustion = ExhaustionPolicies.HandoffToHuman,
        },
    };

    /// <summary>
    /// The real attended executor with a spawn that answers instead of taking a
    /// terminal, driven by the real runner loop.
    /// </summary>
    /// <remarks>
    /// <b>The real type rather than a double, deliberately.</b> What these three
    /// assert is how the LOOP treats an attended executor, and a double
    /// declaring itself unprobeable would prove the loop reads a flag rather
    /// than that this executor sets it.
    /// </remarks>
    private static async Task<(List<ExecutorRequest> Seen, FakeProtocol Protocol)> FlownAsync(
        bool holdUntilRenewed = false, int wallClockSeconds = 600)
    {
        using var fixture = new GitFixture();
        using var trees = new ScratchTreeRoot();
        var clock = new MovableClock(T0);
        var protocol = new FakeProtocol();
        protocol.Claims.Enqueue(new ClaimResult.Granted(ALease(fixture, 1, wallClockSeconds)));

        var seen = new List<ExecutorRequest>();

        // A CHILD THAT IS STILL ALIVE, WITHOUT SLEEPING FOR IT. The person is at
        // the keyboard until this completes, and what completes it is the
        // renewal actually happening - so the test is a client of the loop's own
        // event rather than of a duration somebody guessed.
        var child = new TaskCompletionSource<int?>();
        if (!holdUntilRenewed)
        {
            child.SetResult(0);
        }

        var executor = new AttendedExecutor(
            "claude", [Tracker], announce: TextWriter.Null,
            spawn: (info, _) => { seen.Add(Watching(info)); return child.Task; });

        var observer = new RecordingObserver();
        using var stopping = new CancellationTokenSource();
        observer.OnEvent = e =>
        {
            if (e.StartsWith("renewed:", StringComparison.Ordinal))
            {
                child.TrySetResult(0);
            }

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
                observer, new NoCredentialResolver(),
                trees.Workspace(new LocalVcsAdapter(fixture.Directory)),
                executor: executor)
            {
                HoldFor = TimeSpan.FromSeconds(3),
            }
            .RunAsync("runner-1", ["linux"], stopping.Token);

        return (seen, protocol);
    }

    /// <summary>The launch, as a request, so a probe is recognisable by its loop id.</summary>
    private static ExecutorRequest Watching(ProcessStartInfo info) => new()
    {
        WorkingDirectory = info.WorkingDirectory,
        LoopId = info.WorkingDirectory.Contains("gg-move-probe", StringComparison.Ordinal)
            ? "gg-move-bound-probe"
            : "implement",
        Moves = [],
        WallClock = TimeSpan.FromMinutes(1),
        TranscriptPath = "",
    };

    [Test]
    public async Task No_probe_runs_for_an_attended_flight()
    {
        // NEITHER WAY ROUND WORKS, which is why this is a skip rather than a
        // substitution. MoveBoundProbe.RunAsync INVOKES the port it is handed,
        // against its own temp tree, with an ISSUE.md asking for two writes -
        // so through this executor it hands a person a canary task, and through
        // the headless one it measures a session other than the one it governs.
        //
        // The loop moved the probe to per-session precisely because "a
        // measurement taken at startup measures the machine as it was before
        // this session existed", and a headless reading stamped onto
        // environment.identity as this flight's moveEnforcement would break the
        // only claim the probe makes.
        //
        // So an attended flight's bound is UNMEASURED, and saying so is step 6's
        // job. Quietly probing something else is what this stops.
        var (seen, _) = await FlownAsync();

        await Assert.That(seen.Any(r => r.WorkingDirectory.Contains(
                "gg-move-probe", StringComparison.Ordinal))).IsFalse()
            .Because("a person would have been asked to perform the probe's canary task.");
    }

    [Test]
    public async Task The_loop_drives_it_through_the_same_call()
    {
        // S26.1-01. The loop holds ONE IExecutorPort and never asks which rung
        // it is - the choice is made at composition, in the CLI. A branch here
        // would mean the seam was in the wrong place, and the whole argument for
        // this design is that everything either side of the call is unchanged.
        var (seen, protocol) = await FlownAsync();

        await Assert.That(seen.Count).IsEqualTo(1)
            .Because("one session per lease, the same as the headless path.");

        // In the flight's own materialized tree, not a scratch one.
        await Assert.That(seen[0].WorkingDirectory).IsNotEmpty();

        await Assert.That(protocol.Calls.Any(c => c.StartsWith("release:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the lease goes back through the ordinary release, so the flight lands "
                   + "at its destination through the ordinary decision.");
    }

    [Test]
    public async Task The_lease_is_renewed_while_the_person_holds_the_terminal()
    {
        // S26.1-08, AND NO SLEEPS. The clock is injected and the child is a
        // delegate; a person holding a terminal past the lease's expiry is the
        // ordinary case rather than the exceptional one, and a lease that
        // lapsed under them would hand their flight to the fleet mid-edit.
        var (_, protocol) = await FlownAsync(holdUntilRenewed: true);

        await Assert.That(protocol.Calls.Any(c => c.StartsWith("renew:", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the child outlived the lease and nothing renewed it.");
    }

    private static AttendedExecutor Executor() =>
        new("claude", [Tracker], announce: TextWriter.Null, spawn: (_, _) => Task.FromResult<int?>(0));
}
