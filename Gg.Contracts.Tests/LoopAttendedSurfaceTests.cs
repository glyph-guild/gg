using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// What an attended session did not measure, said out loud, with nothing on it
/// that could widen anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 3 in a type.</b> <i>No fact is emitted that an attended session did
/// not measure. Absence is declared here, never implied by a default.</i> A
/// person held the terminal, so there was no stream to read: turns, moves and
/// whether the declared move list was enforced are all unavailable, and every
/// one of them is expressible as a helpful lie — <c>Attempts = 0</c>,
/// <c>MovesUsed = []</c>, <c>MoveEnforcement = none</c>. Each would be accepted
/// by every reader and each would be false.
/// </para>
/// <para>
/// <b>The gap is a value, not the kind's mere existence.</b> Deriving "moves
/// were not measured" from the presence of a <c>loop.attended</c> fact would be
/// implying it from a default, which is the thing rule 3 names — and it would
/// freeze the set, so a later executor that DOES measure moves could not say so
/// without a new kind. A closed vocabulary instead: a reader that meets a gap
/// it does not know halts, which is the only safe answer to <i>something else
/// was not measured and I cannot tell you what</i>.
/// </para>
/// <para>
/// <b>A gap named without a consequence is a footnote</b>, which is why the
/// budget and the time actually held are here too. Rule 6 records the wall clock
/// and does not enforce it — nobody's terminal is killed — so an overrun is a
/// thing a person can SEE rather than a thing that happened to them.
/// </para>
/// <para>
/// <b>And the shape is a ratchet, on <c>FlightNomination</c>'s argument.</b>
/// This fact is the one place a hand-flown flight explains itself, so every
/// field somebody will want to add — the moves they actually used, the scope
/// they worked in, an approver — makes it more useful and makes it an
/// unmeasured claim wearing a measurement's clothes. What crosses is what was
/// measured about the SESSION, never anything about the work.
/// </para>
/// </remarks>
public class LoopAttendedSurfaceTests
{
    private static IReadOnlyList<string> Members() =>
    [
        .. typeof(LoopAttended)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name),
    ];

    /// <summary>A session that ran for its whole budget and measured nothing.</summary>
    private static LoopAttended Flown(
        string rung = ExecutorRungs.Frontier,
        int budgetSeconds = 1800,
        int heldSeconds = 900) => new()
    {
        LoopId = "implement",
        Rung = rung,
        Binary = "claude",
        BinaryVersion = "2.1.261",
        BudgetSeconds = budgetSeconds,
        HeldSeconds = heldSeconds,
        Unmeasured = [.. AttendedGaps.All],
        SettingsCleared = ["user", "project", "local", "mcp-servers"],
    };

    // ---- S26.6-07 ----

    [Test]
    public async Task It_declares_the_three_things_an_attended_session_cannot_measure()
    {
        // ENUMERATED EXACTLY rather than "contains". The set is what a control
        // plane halts on, and a fourth value appearing without anybody deciding
        // it is how a gap starts being reported that nothing downstream reads.
        await Assert.That(AttendedGaps.All).IsEquivalentTo(new[]
        {
            AttendedGaps.Turns,
            AttendedGaps.Moves,
            AttendedGaps.MoveBound,
        });
    }

    [Test]
    public async Task A_gap_it_does_not_know_is_refused_rather_than_carried()
    {
        var invented = Flown() with { Unmeasured = ["turns", "vibes"] };

        await Assert.That(LoopAttended.Validate(invented)).IsNotNull()
            .Because("a gap nothing downstream knows is a declaration of absence that no "
                   + "reader can act on, which is worse than not declaring it: it reads as "
                   + "having been handled.");
    }

    [Test]
    public async Task Declaring_no_gap_at_all_is_refused()
    {
        // THE CONTRADICTION, and it is the one a helpful default produces. A
        // loop.attended saying nothing was unmeasured is a session that measured
        // everything - which would have shipped a loop.outcome and never reached
        // this type at all.
        await Assert.That(LoopAttended.Validate(Flown() with { Unmeasured = [] })).IsNotNull();
    }

    [Test]
    public async Task It_names_which_settings_sources_were_cleared()
    {
        // RULE 10, RECORDED. An attended session runs with the operator's
        // setting sources cleared and their tool servers withheld - which is the
        // only reason the envelope's bound means anything here - and a person
        // whose own plugins silently vanished concludes the tool is broken. The
        // executor says it at the terminal; this is the same claim where a
        // reader can find it later.
        //
        // NOT a closed vocabulary, deliberately: these are a vendor's source
        // names and they will move. Nothing branches on them, so a value nobody
        // recognises costs a reader nothing - unlike a gap, where it costs
        // everything.
        await Assert.That(Members()).Contains(nameof(LoopAttended.SettingsCleared));
        await Assert.That(Flown().SettingsCleared).Contains("user");
    }

    // ---- S26.6-13 ----

    [Test]
    public async Task It_names_the_binary_and_the_version_it_was_measured_against()
    {
        // THE RUNNER PINS NO CLI VERSION - `binary = "claude"`, whatever is on
        // PATH - and the tool surface moved from 28 to 29 between the two
        // versions slices twenty-six and twenty-seven measured. So "the tool
        // bound was not enforced against a person" is a claim about a NAMED
        // BINARY AT A NAMED VERSION or it is a claim that expires quietly, on a
        // machine nobody upgraded on purpose.
        await Assert.That(Members()).Contains(nameof(LoopAttended.Binary));
        await Assert.That(Members()).Contains(nameof(LoopAttended.BinaryVersion));

        await Assert.That(LoopAttended.Validate(Flown() with { BinaryVersion = "" })).IsNotNull()
            .Because("an unversioned claim about a tool surface is the claim expiring quietly, "
                   + "which is the whole reason this member exists.");
    }

    // ---- S26.6-09 ----

    [Test]
    public async Task It_carries_the_budget_and_the_time_actually_held()
    {
        await Assert.That(Members()).Contains(nameof(LoopAttended.BudgetSeconds));
        await Assert.That(Members()).Contains(nameof(LoopAttended.HeldSeconds));
    }

    [Test]
    public async Task An_overrun_is_recorded_rather_than_refused()
    {
        // RULE 6, AND IT IS THE HALF THAT LOOKS LIKE A BUG. Held longer than the
        // budget is VALID here, because nothing killed the person's terminal -
        // the envelope's wall clock is enforced against an agent and recorded
        // against a person. A validator refusing it would mean the only flights
        // able to report an overrun are the ones that did not have one.
        var overrun = Flown(budgetSeconds: 1800, heldSeconds: 7200);

        await Assert.That(LoopAttended.Validate(overrun)).IsNull();
        await Assert.That(overrun.HeldSeconds).IsGreaterThan(overrun.BudgetSeconds);
    }

    // ---- S26.6-14 ----

    [Test]
    public async Task It_records_the_rung_the_loop_declared_and_does_not_coerce_it()
    {
        // A PERSON OPERATING AN AGENT IS NOT A PERSON DOING THE WORK. An
        // attended flight whose loop declares `frontier` records `frontier`:
        // somebody sat at the terminal, and an agent still did the work. Writing
        // `human` here because a person was present would make every later count
        // of how much the machine did wrong in the flattering direction, on the
        // one measurement this product exists to be honest about.
        //
        // ExecutorRungs.Human's own argument, run in the direction that does not
        // flatter it.
        await Assert.That(Flown(rung: ExecutorRungs.Frontier).Rung)
            .IsEqualTo(ExecutorRungs.Frontier);

        await Assert.That(LoopAttended.Validate(Flown(rung: ExecutorRungs.Human))).IsNull()
            .Because("a loop that declares human and is flown by hand is the other real case, "
                   + "and both are the loop's own declaration rather than this type's guess.");

        await Assert.That(LoopAttended.Validate(Flown(rung: "artisanal"))).IsNotNull();
    }

    // ---- S26.6-08 ----

    [Test]
    public async Task It_carries_these_members_and_no_others()
    {
        // ENUMERATED, because a test that only forbade a list of bad names
        // passes on the member nobody thought of - which is how a type grows.
        await Assert.That(Members()).IsEquivalentTo(new[]
        {
            nameof(LoopAttended.LoopId),
            nameof(LoopAttended.Rung),
            nameof(LoopAttended.Binary),
            nameof(LoopAttended.BinaryVersion),
            nameof(LoopAttended.BudgetSeconds),
            nameof(LoopAttended.HeldSeconds),
            nameof(LoopAttended.Unmeasured),
            nameof(LoopAttended.SettingsCleared),
        });
    }

    [Test]
    public async Task It_holds_nothing_that_could_widen_anything()
    {
        foreach (var forbidden in (string[])
            ["Moves", "MovesUsed", "Scope", "Obligations", "Destination", "Destinations",
             "Budget", "WallClock", "Requires", "Approver", "Opens", "Accepts", "Produces",
             "Environment", "Repository", "Layer", "Outcome", "Attempts", "MoveEnforcement"])
        {
            await Assert.That(Members().Contains(forbidden, StringComparer.Ordinal)).IsFalse()
                .Because($"'{forbidden}' on this fact is an unmeasured claim wearing a "
                       + "measurement's clothes. The session is what was measured here; the "
                       + "work was not, which is the entire subject of the fact.");
        }
    }

    [Test]
    public async Task It_names_nobody()
    {
        // RULE 8 AND THE HumanAccount PATTERN. Who flew it is derived from the
        // session control-plane-side and never from the body - a member here
        // would be a runner asserting an identity, which is the one thing a
        // runner may not do.
        foreach (var identity in (string[])["By", "Who", "Person", "Flew", "FlownBy", "Author"])
        {
            await Assert.That(Members().Contains(identity, StringComparer.Ordinal)).IsFalse();
        }
    }
}
