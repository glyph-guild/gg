using Gg.Contracts;
using Gg.Runner.Execution;
using Gg.Runner.Facts;

namespace Gg.Runner.Tests;

/// <summary>
/// The move that lets a flight create a file, and the refusal that says so when
/// it does not have it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it cost a version.</b> Adding a value to a closed enumeration is not
/// additive: the only safe response to an unknown value is to halt, so an added
/// value breaks every prior reader by design. The alternative was widening
/// <c>edit</c>, which would have changed what an already-declared move permits
/// for every envelope in force, with nothing marking the day.
/// </para>
/// <para>
/// <b>What it does not close.</b> The move vocabulary and the tool vocabulary are
/// still not in correspondence - <c>run-tests</c> maps to <c>Bash</c>, and
/// <c>Bash</c> can write files, and the allow-list does not bind it. A flight
/// declaring <c>read</c> and <c>run-tests</c> can still create a file without
/// declaring <c>write</c>. This closes the new-file gap and not that one.
/// </para>
/// </remarks>
public class WriteMoveTests
{
    [Test]
    public async Task The_vocabulary_has_a_word_for_putting_bytes_at_a_path()
    {
        await Assert.That(LoopMoves.All).Contains(LoopMoves.Write);
        await Assert.That(LoopMoves.Write).IsEqualTo("write");
        await Assert.That(LoopMoves.All).DoesNotContain("create")
            .Because("the tool overwrites, so `create` would be a name true of one of its two "
                   + "uses - the argument that ruled out reusing destination.landed.");
    }

    [Test]
    public async Task It_maps_to_the_tool_that_was_measured_as_bound()
    {
        // The mapping is what makes declaring it mean anything: withheld, this
        // tool is offered and refused at the call. A move mapping to something
        // the allow-list does not bind would be a declaration with no bound
        // behind it, which is what `run-tests` still is.
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.Write)).IsEqualTo("Write");
    }

    [Test]
    public async Task Edit_still_means_what_it_meant()
    {
        // THE PROPERTY THE WHOLE DECISION RESTS ON. Every envelope already in
        // force declares `edit` and none of them declared `write`, so if `edit`
        // had grown the power to create files, every one of those envelopes would
        // permit something new today that it did not permit yesterday, and nothing
        // in the record would say when.
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.Edit)).IsEqualTo("Edit");
        await Assert.That(ClaudeCodeExecutor.ToolFor(LoopMoves.Edit))
            .IsNotEqualTo(ClaudeCodeExecutor.ToolFor(LoopMoves.Write));
    }

    [Test]
    public async Task An_envelope_written_before_this_still_means_exactly_what_it_did()
    {
        // The other half of the same claim, over a whole moves list rather than
        // one mapping: an old envelope's tools are the tools it always had, and
        // none of them is the new one.
        var beforeThisStep = (IReadOnlyList<string>)[LoopMoves.Read, LoopMoves.Edit];
        var tools = beforeThisStep.Select(ClaudeCodeExecutor.ToolFor).ToList();

        await Assert.That(tools).IsEquivalentTo((string[])["Read", "Edit"]);
        await Assert.That(tools).DoesNotContain("Write")
            .Because("it cannot create files, which it could not before either - unchanged in "
                   + "meaning is the whole reason a new value was the right shape.");
    }

    // ---- the refusal names what was needed and where to add it ----

    [Test]
    public async Task A_refusal_names_the_move_and_the_place_in_the_envelope()
    {
        // THE OBLIGATION THIS DISCHARGES. A refusal that only says no teaches
        // people to want the middle option - a rejection reason that can widen an
        // envelope - and that was refused on governance grounds. So the way out
        // has to be a sentence somebody can act on through the envelope.
        var diagnosis = MoveRefusal.Diagnose(["Write"], "implement")!;

        await Assert.That(diagnosis).Contains("'write'")
            .Because("the move by name, not the tool, because the envelope is written in moves.");
        await Assert.That(diagnosis).Contains("loops.implement.moves")
            .Because("and where to put it, in the loop that wanted it.");
        await Assert.That(diagnosis).Contains("Write")
            .Because("and the tool it reached for, so the sentence matches what was observed.");
    }

    [Test]
    public async Task It_says_that_nothing_here_can_grant_it()
    {
        // Otherwise the obvious next question is whether the runner can be
        // persuaded, and the answer has to arrive with the refusal rather than
        // after somebody tries.
        var diagnosis = MoveRefusal.Diagnose(["Write"], "implement")!;

        await Assert.That(diagnosis).Contains("envelope's to say");
        await Assert.That(diagnosis).Contains("advisory");
    }

    [Test]
    public async Task Nothing_refused_says_nothing()
    {
        // A diagnosis on every flight is a line people learn to scroll past, and
        // this one has to be worth reading on the day it appears.
        await Assert.That(MoveRefusal.Diagnose([], "implement")).IsNull();
    }

    [Test]
    public async Task A_tool_no_move_could_grant_produces_no_advice()
    {
        // WebFetch maps to no move, so telling somebody to declare one would send
        // them looking for a word that does not exist. Saying nothing is the
        // honest answer, and it is a different answer from saying nothing was
        // refused.
        await Assert.That(MoveRefusal.Diagnose(["WebFetch"], "implement")).IsNull();
        await Assert.That(MoveRefusal.Diagnose(["WebFetch", "Write"], "implement")).IsNotNull()
            .Because("and one fixable refusal among unfixable ones still gets its sentence.");
    }

    // ---- what the environment records about the bound ----

    [Test]
    public async Task The_environment_fact_records_what_varies_and_not_what_cannot()
    {
        var observed = EnvironmentSurvey.Observe(
            treePath: null,
            provenance: EnvironmentProvenance.Fresh,
            probe: new Execution.ProbeResult
            {
                Bound = true,
                Diagnosis = "held",
                Took = TimeSpan.FromSeconds(17),
                MeasuredAt = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero),
                Workspace = "/tmp/probe",
                Held = ["Edit", "Write"],
                Broke = [],
            });

        await Assert.That(observed.MoveEnforcement).IsEqualTo(MoveEnforcements.PerTool);
        await Assert.That(observed.MovesProbed).IsEquivalentTo((string[])["Edit", "Write"]);

        // The member that is NOT there, and its absence is the point: whether the
        // bound held is constant across every flight that exists, because a runner
        // that found it did not hold refuses to take work. A member recording it
        // would record nothing.
        await Assert.That(typeof(EnvironmentIdentity).GetProperties().Select(p => p.Name))
            .DoesNotContain("MoveBoundHeld");
    }

    [Test]
    public async Task A_runner_with_no_executor_records_no_enforcement_rather_than_none()
    {
        // `none` is a measurement - an executor that bounds nothing. Null is the
        // absence of one, which is what a runner that cannot invoke an agent has.
        // Collapsing them would report every observe-only runner as a machine
        // where moves are unenforceable.
        var observed = EnvironmentSurvey.Observe(null, EnvironmentProvenance.Fresh);

        await Assert.That(observed.MoveEnforcement).IsNull();
        await Assert.That(observed.MovesProbed).IsEmpty();
        await Assert.That(MoveEnforcements.All).Contains(MoveEnforcements.None)
            .Because("and `none` remains sayable, so the distinction is real rather than "
                   + "an unused branch.");
    }

    [Test]
    public async Task The_two_spellings_of_an_enforcement_level_cannot_drift()
    {
        // The enum is what the runner reasons with and the vocabulary is what
        // crosses. Two spellings of one idea drift, and the drift is invisible.
        foreach (var level in Enum.GetValues<MoveEnforcement>())
        {
            await Assert.That(MoveEnforcements.All).Contains(MoveEnforcementNames.Of(level));
        }

        await Assert.That(Enum.GetValues<MoveEnforcement>().Length)
            .IsEqualTo(MoveEnforcements.All.Count);
    }

    // ---- and the budget the flight-level count will be spent against ----

    [Test]
    public async Task The_attempts_budget_is_flight_level_and_not_the_loops_own_count()
    {
        // THE TRAP, ASSERTED. A loop reports `attempts` of its own - the agent's
        // internal turns, and a real run printed "completed after 7 attempt(s)"
        // for ONE invocation. budget.attempts is the reject-and-rerun cycle. If
        // the two ever share a variable, a budget of five is spent by one loop
        // thinking, and the flight stops for a reason nobody chose.
        //
        // They are different types on different records, which is the strongest
        // form this can take here: LoopBudget.Attempts is nullable because
        // unbounded is a real state, and LoopOutcome.Attempts is required because
        // a loop that ran always took some number of turns.
        var budget = typeof(LoopBudget).GetProperty(nameof(LoopBudget.Attempts))!;
        var loop = typeof(LoopOutcome).GetProperty(nameof(LoopOutcome.Attempts))!;

        await Assert.That(budget.PropertyType).IsEqualTo(typeof(int?));
        await Assert.That(loop.PropertyType).IsEqualTo(typeof(int));
        await Assert.That(budget.DeclaringType).IsNotEqualTo(loop.DeclaringType);
    }

    [Test]
    public async Task An_envelope_that_names_no_attempt_budget_is_unbounded_rather_than_one()
    {
        // Null rather than a default, because a number nobody chose would be a
        // termination condition nobody agreed to - and every envelope in force
        // today names none.
        var budget = new LoopBudget { WallClock = "30m" };

        await Assert.That(budget.Attempts).IsNull();
    }
}
