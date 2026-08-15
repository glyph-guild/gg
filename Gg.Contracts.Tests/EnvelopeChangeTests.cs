namespace Gg.Contracts.Tests;

/// <summary>
/// A flight with no repository and no runner, landing somewhere that is not a
/// repository.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claim under test is that this is a DESTINATION and not a second
/// system.</b> An envelope change is work: somebody proposes it, an obligation
/// governs it, a person decides it, and it lands. If that needs a parallel set of
/// primitives then <i>flight</i> was never the universal unit - it was a synonym
/// for <i>agent run against a branch</i> - and this is where the two readings
/// come apart.
/// </para>
/// <para>
/// <b>Two values, both in closed vocabularies, so both cost a version.</b> A rung
/// that means "nothing automated runs this" and a destination kind that is not a
/// repository. The only safe response to an unknown value is to halt, so every
/// prior reader is broken by design - which is what the bump records.
/// </para>
/// </remarks>
public class EnvelopeChangeTests
{
    /// <summary>An envelope-change envelope: no runner, no repository.</summary>
    /// <remarks>
    /// The obligation is <c>check: human</c> and the loop discharges NOTHING.
    /// That is not a gap: an obligation with no loop is a gate, which the
    /// validator has allowed since the dangling-reference message was corrected,
    /// and a loop that discharged a human check would be a runner answering for a
    /// person.
    /// </remarks>
    private static Envelope Change() => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = "envelope-change-approved",
                Check = ObligationChecks.Human,
                Approver = "platform-oncall",
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "propose",
                Executor = ExecutorRungs.Human,
                Discharges = [],
                Moves = [],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "the-envelope",
                Kind = DestinationKinds.EnvelopeChange,
                Requires = ["envelope-change-approved"],
            },
        ],
    };

    // ---- the two values ----

    [Test]
    public async Task An_envelope_change_flight_is_a_valid_envelope()
    {
        // THE WHOLE CLAIM IN ONE ASSERTION. If this needed a second schema then
        // the destination model was never general.
        await Assert.That(Envelope.Validate(Change())).IsNull();
    }

    [Test]
    public async Task A_human_rung_is_named_rather_than_borrowed_from_frontier()
    {
        // NOT `frontier` with nobody listening. A rung says what discharges the
        // loop, and recording an agent rung for work no agent does would make
        // every count of "how much did the machine do" wrong in the flattering
        // direction.
        await Assert.That(ExecutorRungs.All).Contains(ExecutorRungs.Human);
        await Assert.That(ExecutorRungs.Human).IsNotEqualTo(ExecutorRungs.Frontier);
    }

    [Test]
    public async Task A_destination_that_is_not_a_repository_is_its_own_kind()
    {
        await Assert.That(DestinationKinds.All).Contains(DestinationKinds.EnvelopeChange);
        await Assert.That(DestinationKinds.All.Count).IsEqualTo(2)
            .Because("two, and the second one is deliberately not a repository - which is what "
                   + "makes 'add a destination' a real answer rather than a restatement.");
    }

    // ---- what a human rung may not declare ----

    [Test]
    public async Task A_human_loop_declaring_a_move_is_refused_naming_the_move()
    {
        // THE DEFECT THIS PROJECT KEEPS FIXING, arriving through a new door.
        // Moves are bound by a runner's executor and the runner refuses work when
        // it cannot bind them. There is no runner here, so a declared move would
        // be a permission nothing enforces and nothing could report on - declared
        // and unenforced, which is exactly what `write` was added to stop being.
        var refused = Envelope.Validate(Change() with
        {
            Loops = [Change().Loops[0] with { Moves = [LoopMoves.Edit] }],
        });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains($"'{LoopMoves.Edit}'");
        await Assert.That(refused).Contains("'propose'")
            .Because("naming the loop as well as the move, because an envelope has more than "
                   + "one place a move can be written.");
    }

    [Test]
    public async Task An_agent_loop_still_declares_moves()
    {
        // THE POSITIVE CONTROL. Without it the refusal above is satisfied by a
        // validator that refuses every declared move, which would break every
        // envelope in force.
        await Assert.That(Envelope.Validate(Change() with
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "in-scope",
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                },
            ],
            Loops =
            [
                Change().Loops[0] with
                {
                    Executor = ExecutorRungs.Frontier,
                    Discharges = ["in-scope"],
                    Moves = [LoopMoves.Read, LoopMoves.Edit],
                },
            ],
            Destinations = [Change().Destinations[0] with { Requires = ["in-scope"] }],
        })).IsNull();
    }

    [Test]
    public async Task An_unknown_rung_is_still_refused_by_name()
    {
        // The guard that already existed, kept as a special case of the wider
        // vocabulary rather than replaced by it.
        var refused = Envelope.Validate(Change() with
        {
            Loops = [Change().Loops[0] with { Executor = "intern" }],
        });

        await Assert.That(refused!).Contains("'intern'");
        await Assert.That(refused).Contains(ExecutorRungs.Human)
            .Because("the diagnosis lists what this version knows, so the new rung is "
                   + "discoverable from the refusal.");
    }
}
