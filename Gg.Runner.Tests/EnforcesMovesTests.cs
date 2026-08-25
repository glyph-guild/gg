using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// What the declared moves actually bound, measured through the invocation the
/// executor really makes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The previous measurement was taken with a different command.</b> It ran the
/// binary with <c>--allowedTools Read</c> and nothing else - no
/// <c>--setting-sources ""</c>, which is the flag the bound rests on - so the
/// operator's own settings applied, the agent edited, and the conclusion written
/// down was <i>the allow-list does not bind</i>. The allow-list binds. What was
/// measured was a command this product never runs.
/// </para>
/// <para>
/// <b>And it binds three different ways, which is why a boolean could not hold
/// it.</b> Measured one tool at a time, each held off the list the executor
/// passes:
/// </para>
/// <list type="table">
/// <item><term><c>Edit</c>, <c>Write</c></term><description>offered and refused at
/// the call: <i>"Claude requested permissions to write to …, but you haven't
/// granted it yet."</i></description></item>
/// <item><term><c>Grep</c></term><description>not in the tool list at all - the
/// agent reports it <i>"isn't available in this session"</i>.</description></item>
/// <item><term><c>Read</c>, <c>Bash</c></term><description><b>not bound.</b> Both
/// ran with the tool withheld. Bash is gated per COMMAND rather than per tool:
/// <c>uname -s</c> ran, and <c>touch</c> and <c>rm</c> were refused in a real
/// flight.</description></item>
/// </list>
/// <para>
/// <b>And the bound is contingent on one flag whose mechanism is not
/// characterised.</b> Without <c>--setting-sources ""</c> a withheld <c>Write</c>
/// wrote. <c>--permission-mode acceptEdits</c> also overrides the list - which is
/// what the superseded capture used - and, worse, passing
/// <c>--permission-mode default</c> did <b>not</b> restore the bound. Only
/// clearing setting sources did. So the runner does not trust the flag: it runs
/// <see cref="MoveBoundProbe"/> at startup and refuses to take work if the bound
/// does not hold.
/// </para>
/// </remarks>
public class EnforcesMovesTests
{
    // ---- what the declaration says ----

    [Test]
    public async Task The_capability_is_declared_as_the_three_state_thing_it_is()
    {
        // A boolean here said "no" about a mechanism that says "yes for these
        // tools, no for those, and only while a flag holds". False was nearer the
        // truth than true and it was still wrong, and being wrong in the safe
        // direction is how a claim survives three flaggings without being fixed.
        await Assert.That(ClaudeCodeExecutor.Capabilities.DeclaredMoveEnforcement)
            .IsEqualTo(MoveEnforcement.PerTool);
    }

    [Test]
    public async Task Every_state_the_type_names_is_one_something_could_declare()
    {
        // Three states and no more. A fourth added without a measurement behind it
        // would be the boolean's problem again with more room.
        await Assert.That(Enum.GetValues<MoveEnforcement>().Length).IsEqualTo(3);
        await Assert.That(Enum.GetValues<MoveEnforcement>())
            .Contains(MoveEnforcement.None)
            .And.Contains(MoveEnforcement.PerTool)
            .And.Contains(MoveEnforcement.Full);
    }

    // ---- WHAT THIS FILE USED TO ASSERT, AND WHY IT IS GONE ----
    //
    // `The_declared_reason_is_the_one_that_was_measured` scanned the executor's
    // source for the string "allow-list does not bind", plus "--disallowedTools"
    // and "closed enumeration". Its subject was PROSE: it held the adapter's
    // comment to account for containing particular words.
    //
    // It is DELETED rather than narrowed, and the distinction matters because
    // this project's rule is to narrow a guard and never delete one. That rule's
    // test is whether the old assertion survives as a special case of the new
    // one. Here it cannot. Prose is not a narrower version of a behaviour - it is
    // a different thing to assert, and asserting it is what let a false claim sit
    // in the source for three flaggings while a test went green over it. The
    // string it pinned was wrong, and the test's only possible response to
    // correcting the truth was to fail.
    //
    // What replaces it asserts the MEASURED BEHAVIOUR, through the invocation the
    // executor really makes, so it goes red the day the bound changes - which is
    // what the original was reaching for and could not express.

    [Test]
    public async Task The_gap_is_declared_rather_than_left_to_be_discovered()
    {
        var gap = ClaudeCodeExecutor.Capabilities.Gaps
            .SingleOrDefault(g => g.Name.Contains("moves", StringComparison.OrdinalIgnoreCase));

        await Assert.That(gap).IsNotNull()
            .Because("a capability this runner has only partly is declared on the port.");
        await Assert.That(gap!.Consequence).Contains("setting-sources")
            .Because("and the flag the whole bound rests on is named where somebody would look.");
    }

    [Test]
    public async Task The_runner_does_not_take_the_flag_on_trust()
    {
        // The declaration above says the bound is contingent. This is the thing
        // that makes the contingency safe rather than merely disclosed: a
        // capability note nobody acts on is a disclosure, and this product's own
        // finding is that a disclosure is not a control.
        await Assert.That(MoveBoundProbe.Required(new ClaudeCodeExecutor("claude"))).IsNotNull();
    }

    // ---- the mapping is coarser than the vocabulary, which is a second reason ----

    [Test]
    public async Task Run_tests_maps_onto_a_tool_that_can_do_more_than_run_tests()
    {
        // Unchanged, and now with a measurement behind it: `run-tests` maps to
        // Bash, Bash can write files, and Bash is one of the two tools the
        // allow-list does not bind at all. A flight declaring read plus run-tests
        // can edit, and no flag available here changes that.
        var mapped = LoopMoves.All
            .ToDictionary(m => m, ClaudeCodeExecutor.ToolFor, StringComparer.Ordinal);

        await Assert.That(mapped[LoopMoves.RunTests]).IsEqualTo("Bash");
        await Assert.That(mapped[LoopMoves.Edit]).IsEqualTo("Edit");

        await Assert.That(mapped[LoopMoves.RunTests]).IsNotEqualTo(mapped[LoopMoves.Edit])
            .Because("they are different tools, and yet the first can do what the second does - "
                   + "which is why a tool-level bound cannot express a move-level rule.");
    }

    [Test]
    public async Task Every_declared_move_maps_to_something()
    {
        foreach (var move in LoopMoves.All)
        {
            await Assert.That(ClaudeCodeExecutor.ToolFor(move)).IsNotEqualTo(move)
                .Because($"'{move}' falls through the mapping and would be passed as itself.");
        }
    }

    // ---- the measurement itself, through the command the product runs ----

    [Test]
    [Category("RealAgent")]
    public async Task The_bound_holds_for_a_withheld_edit_under_the_executors_own_invocation()
    {
        // THE REPLACEMENT for the deleted prose guard. It runs the probe the
        // runner runs, against the real binary, through ExecuteAsync - so what is
        // measured is the command a flight uses rather than one assembled for a
        // test. The previous measurement's whole error was assembling its own.
        var binary = Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
            ?? throw new InvalidOperationException(
                "GG_EXECUTOR_BINARY is not set. This is the measurement the EnforcesMoves claim "
              + "rests on; skipping it would leave the claim as an assumption again.");

        var result = await MoveBoundProbe.RunAsync(
            new ClaudeCodeExecutor(binary), CancellationToken.None);

        await Assert.That(result.Bound).IsTrue()
            .Because(result.Diagnosis);
    }

    [Test]
    [Category("RealAgent")]
    public async Task And_it_does_not_hold_for_bash_which_is_why_the_state_is_per_tool()
    {
        // The other half, and the one that makes PerTool a measurement rather than
        // a hedge. Bash is withheld here exactly as Edit is above, and it runs.
        var binary = Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
            ?? throw new InvalidOperationException("GG_EXECUTOR_BINARY is not set.");

        var directory = Path.Combine(
            Path.GetTempPath(), "gg-moves-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "ISSUE.md"),
            "# Probe\n\nUse the Bash tool to run `uname -s` and report its exact output.\n");

        try
        {
            var run = await new ClaudeCodeExecutor(binary).ExecuteAsync(
                new ExecutorRequest
                {
                    WorkingDirectory = directory,
                    LoopId = "bash-bound",
                    IntentUri = "gg://probe/bash",
                    // Bash is not among them, and `run-tests` is the move that
                    // would have granted it.
                    Moves = [LoopMoves.Read],
                    WallClock = TimeSpan.FromMinutes(3),
                    TranscriptPath = Path.Combine(directory, "probe.ndjson"),
                },
                CancellationToken.None);

            await Assert.That(run.MovesUsed).Contains("Bash")
                .Because("withheld and reached for; the allow-list does not remove it and does "
                       + "not refuse it, which is the per-tool half of the declaration.");
            await Assert.That(run.Digest!.RefusedMoves).DoesNotContain("Bash")
                .Because("and the digest now says so, because the call came back.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
