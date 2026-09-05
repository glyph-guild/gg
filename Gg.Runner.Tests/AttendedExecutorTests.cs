using System.Diagnostics;
using System.Reflection;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

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
            spawn: info => { spawned.Add(info); return 0; });

        var run = await executor.ExecuteAsync(Request(), CancellationToken.None);

        await Assert.That(run).IsNull();
        await Assert.That(spawned).HasCount(1);
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

    private static AttendedExecutor Executor() =>
        new("claude", [Tracker], announce: TextWriter.Null, spawn: _ => 0);
}
