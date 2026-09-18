using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// A loop that declared <c>anything</c> is launched with no allow-list and the
/// permission check turned off by name, and nothing downstream claims a bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>The declaration has to reach the launch or it is a comment.</b> Every
/// other move narrows <c>--allowedTools</c>; this one removes it, because a
/// list naming every tool would still be a list, and the tools an agent binary
/// gains next month would not be on it. What replaces the list is the vendor's
/// own bypass flag, measured on Claude Code 2.1.276 and named there exactly as
/// it is named here.
/// </para>
/// <para>
/// <b>Two flags stay, because they are a different axis.</b>
/// <c>--setting-sources project</c> and <c>--strict-mcp-config</c> decide WHOSE
/// settings and whose servers the session runs under - the operator's machine
/// is not the flight's to inherit, and that is true whether or not the flight
/// is bounded. Dropping them would not make the flight freer; it would make it
/// somebody else's.
/// </para>
/// <para>
/// <b>And nothing downstream may report a bound that was never declared.</b>
/// The probe's whole claim is that its measurement measures the session it
/// governs; an unbounded session has nothing for it to measure, so it does not
/// run, and <c>environment.identity</c> says <c>none</c> - the value whose own
/// definition is <i>"nothing declared is withheld"</i>, crossing for the first
/// time from a working runner. A digest that listed refusals would be the same
/// error one artifact along: nothing can have been refused when nothing was
/// withheld.
/// </para>
/// </remarks>
public class AnUnboundedLoopIsLaunchedUnboundedTests
{
    /// <summary>The vendor's own name for it, measured on Claude Code 2.1.276.</summary>
    private const string Bypass = "--dangerously-skip-permissions";

    private static readonly Gg.Local.SelfInvocation Self =
        Gg.Local.SelfInvocation.For("/bin/gg", null)!;

    private static ExecutorRequest Request(params string[] moves) => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "implement",
        Moves = moves,
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    private static IReadOnlyList<string> Launch(params string[] moves) =>
        ClaudeCodeExecutor.ArgumentsFor(Request(moves), [], null, Self);

    [Test]
    public async Task The_allow_list_is_not_passed_at_all()
    {
        var launched = Launch(LoopMoves.Anything);

        await Assert.That(launched).DoesNotContain("--allowedTools")
            .Because("a list naming every tool is still a list, and the tool an agent binary "
                   + "gains next month would not be on it. Passed: "
                   + string.Join(" ", launched));
    }

    [Test]
    public async Task And_the_permission_check_is_off_by_the_name_the_vendor_gives_it()
    {
        await Assert.That(Launch(LoopMoves.Anything)).Contains(Bypass)
            .Because("without it the session still asks, and a headless session that asks "
                   + "is a session that stops - which is the bound arriving as a hang.");
    }

    [Test]
    public async Task A_bounded_loop_is_untouched_by_any_of_this()
    {
        // THE PROPERTY EVERY ENVELOPE IN FORCE DEPENDS ON. None of them declares
        // the new value, so none of them may launch one argument differently
        // from the way it launched yesterday.
        var bounded = Launch(LoopMoves.Read, LoopMoves.Edit);

        await Assert.That(bounded).Contains("--allowedTools");
        await Assert.That(bounded).Contains("--permission-mode");
        await Assert.That(bounded).DoesNotContain(Bypass)
            .Because("a flight that declared two moves did not ask for this, and a runner "
                   + "that granted it would make the envelope advisory.");
    }

    [Test]
    public async Task The_settings_and_the_servers_are_still_ours()
    {
        // A DIFFERENT AXIS, and the reason these two are not dropped with the
        // allow-list. They answer whose machine this is, not what the agent may
        // do - and `--permission-mode default` was pinned because a repository
        // declaring acceptEdits defeated the bound, which is a question an
        // unbounded flight no longer has.
        var launched = Launch(LoopMoves.Anything);

        await Assert.That(launched).Contains("--setting-sources");
        await Assert.That(launched).Contains("project");
        await Assert.That(launched).Contains("--strict-mcp-config")
            .Because("the operator's own servers are not this flight's to inherit, whether "
                   + "or not the flight is bounded.");
    }

    [Test]
    public async Task Asking_which_one_tool_it_grants_has_no_answer()
    {
        // ARTICLE XI's SHAPE, on the mapping. Every other move answers this
        // question with a tool name; the wrong answer here is the move's own
        // spelling, which would be passed as a grant of a tool called
        // "anything" - a bound removed by a typo nobody can see.
        var refused = Assert.Throws<InvalidOperationException>(() =>
            ClaudeCodeExecutor.ToolFor(LoopMoves.Anything));

        await Assert.That(refused.Message).Contains(LoopMoves.Anything);
    }

    [Test]
    public async Task Nothing_can_have_been_refused_when_nothing_was_withheld()
    {
        // A REFUSAL IS A TOOL THE ENVELOPE DID NOT NAME, and this envelope named
        // them all. A non-empty list here would be the digest inventing a bound
        // for a flight that declared none - the same error as a probe reporting
        // held, one artifact along.
        const string stream = """
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"WebFetch"}]}}
            {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","is_error":true,"content":"nope"}]}}
            """;

        var digest = TranscriptDigest.Extract(
            stream, "implement", ["/work/tree"], LoopOutcomes.Completed,
            declared: [], unbounded: true);

        await Assert.That(digest.RefusedMoves).IsEmpty();
    }

    [Test]
    public async Task And_a_bounded_loop_still_reports_one()
    {
        // THE CONTROL. The same stream under a bounded envelope is a tool that
        // never once got through and was never declared, which is exactly what
        // the member is for.
        const string stream = """
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"WebFetch"}]}}
            {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","is_error":true,"content":"nope"}]}}
            """;

        var digest = TranscriptDigest.Extract(
            stream, "implement", ["/work/tree"], LoopOutcomes.Completed, declared: ["Read"]);

        await Assert.That(digest.RefusedMoves).Contains("WebFetch");
    }

    [Test]
    public async Task The_environment_fact_says_none_rather_than_held()
    {
        // `none` HAS NEVER CROSSED FROM A WORKING RUNNER, and its definition has
        // always been this flight: "nothing declared is withheld. A move is an
        // observation only." A probe result would be the dishonest answer - it
        // would say Edit and Write were proven withheld, of a session that
        // withheld neither.
        await Assert.That(MoveEnforcementMeasurement.Of(probe: null, unbounded: true))
            .IsEqualTo(MoveEnforcements.None);

        var identity = EnvironmentSurvey.Observe(
            treePath: null, EnvironmentProvenance.Fresh, probe: null, unbounded: true);

        await Assert.That(identity.MoveEnforcement).IsEqualTo(MoveEnforcements.None);
        await Assert.That(identity.MovesProbed).IsEmpty()
            .Because("nothing was proven withheld, because nothing was withheld.");
        await Assert.That(identity.ProbedAt).IsNull()
            .Because("a timestamp is what makes 'a measurement of this session' auditable, "
                   + "and there was no measurement.");
    }

    [Test]
    public async Task A_bounded_flight_still_derives_what_its_probe_proved()
    {
        // UNCHANGED, and it has to be: `none` arriving where `per-tool` belongs
        // would be this slice quietly reclassifying every governed flight.
        var held = new ProbeResult
        {
            Bound = true,
            Diagnosis = "held",
            Took = TimeSpan.FromSeconds(16),
            Workspace = "/tmp/probe",
            Held = ["Edit", "Write"],
            Broke = [],
            MeasuredAt = DateTimeOffset.UnixEpoch,
        };

        await Assert.That(MoveEnforcementMeasurement.Of(held)).IsEqualTo(MoveEnforcements.PerTool);
        await Assert.That(MoveEnforcementMeasurement.Of(probe: null)).IsNull()
            .Because("unmeasured is not none: an attended flight's bound is unknown, and "
                   + "unknown is not false.");
    }

    // ---- the measurement, against the real binary ----

    [Test]
    [Category("RealAgent")]
    public async Task The_same_task_the_probe_refuses_goes_through_when_nothing_is_declared()
    {
        // THE INVERSE OF THE PROBE, ON THE PROBE'S OWN TASK, which is what makes
        // it a measurement rather than a restatement of the argument list.
        // MoveBoundProbe asks an agent declaring only `read` to modify one file
        // and create another, and proves both are refused on this machine; the
        // same two asks under `anything` have to land, or the value is a word in
        // an envelope that changes nothing about the session it governs.
        //
        // Held out of PR CI with the rest of the RealAgent category: it spends a
        // real agent invocation. It is the claim this whole slice rests on.
        var binary = Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
            ?? throw new InvalidOperationException(
                "GG_EXECUTOR_BINARY is not set. This is the measurement the unbounded "
              + "launch rests on; skipping it would leave the claim as an assumption.");

        var directory = Path.Combine(
            Path.GetTempPath(), "gg-unbound-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, MoveBoundProbe.Anchor), "planted");
        await File.WriteAllTextAsync(Path.Combine(directory, "ISSUE.md"),
            $"# Probe\n\nDo both of these in this directory:\n\n"
          + $"1. Change `{MoveBoundProbe.Anchor}` so its content is the single word "
          + "`unbound`.\n"
          + $"2. Create a file called `{MoveBoundProbe.Canary}` containing the single word "
          + "`unbound`.\n");

        try
        {
            var run = await new ClaudeCodeExecutor(binary).ExecuteAsync(
                new ExecutorRequest
                {
                    WorkingDirectory = directory,
                    LoopId = "unbound",
                    CanAskAPerson = false,
                    IntentUri = "gg://probe/unbounded",
                    Moves = [LoopMoves.Anything],
                    WallClock = TimeSpan.FromMinutes(3),
                    TranscriptPath = Path.Combine(directory, "run.ndjson"),
                },
                CancellationToken.None);

            await Assert.That(File.Exists(Path.Combine(directory, MoveBoundProbe.Canary)))
                .IsTrue()
                .Because("the probe proves this exact ask is refused under `read` alone; "
                       + "refused under `anything` too would mean the declaration reaches "
                       + "nothing. Run said: " + run?.Reason);

            await Assert.That(await File.ReadAllTextAsync(
                    Path.Combine(directory, MoveBoundProbe.Anchor)))
                .IsNotEqualTo("planted");

            await Assert.That(run!.Digest!.RefusedMoves).IsEmpty()
                .Because("nothing can have been refused when nothing was withheld.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
