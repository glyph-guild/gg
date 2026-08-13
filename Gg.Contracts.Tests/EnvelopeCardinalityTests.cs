using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Obligations are many; everything else is still one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two obligations is where the interaction between obligations first
/// exists.</b> A flight can carry the facts for one and not the other, which is
/// the case that could not be written at cardinality one and is the case a gate
/// stands on.
/// </para>
/// <para>
/// Everything else stays at one, checked rather than aspired to. The pressure to
/// add a second of each is constant and the failure mode is a slice that ships a
/// schema instead of a handoff.
/// </para>
/// </remarks>
public class EnvelopeCardinalityTests
{
    private static Obligation InScope() => new()
    {
        Id = "in-scope",
        Check = ObligationChecks.Machine,
        Rule = ObligationPredicates.NoFileOutsideScope,
    };

    private static Obligation NotExhausted() => new()
    {
        Id = "not-exhausted",
        Check = ObligationChecks.Machine,
        Rule = ObligationPredicates.LoopNotExhausted,
    };

    private static Envelope AtTwo() => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = [InScope(), NotExhausted()],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                // Both discharged by the one loop: it is the loop's work that
                // either stays in scope or runs out of time.
                Discharges = ["in-scope", "not-exhausted"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope", "not-exhausted"],
            },
        ],
    };

    [Test]
    public async Task An_envelope_with_two_obligations_validates()
    {
        await Assert.That(Envelope.Validate(AtTwo())).IsNull();
    }

    [Test]
    public async Task The_two_obligations_read_different_facts()
    {
        // The property that makes this a second obligation rather than the first
        // one twice. Asserted on the predicates because that is where "what does
        // this read" is declared.
        var rules = AtTwo().Obligations.Select(o => o.Rule).ToList();

        await Assert.That(rules.Distinct().Count()).IsEqualTo(2);
        await Assert.That(rules).Contains(ObligationPredicates.NoFileOutsideScope)
            .Because("one reads a change.manifest fact.");
        await Assert.That(rules).Contains(ObligationPredicates.LoopNotExhausted)
            .Because("and the other reads a loop.outcome fact, which is the whole point.");
    }

    [Test]
    public async Task A_second_loop_is_still_the_slice_slipping()
    {
        var two = AtTwo() with { Loops = [.. AtTwo().Loops, .. AtTwo().Loops] };

        var diagnosis = Envelope.Validate(two);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("one loop");
    }

    [Test]
    public async Task A_second_destination_is_still_the_slice_slipping()
    {
        var two = AtTwo() with { Destinations = [.. AtTwo().Destinations, .. AtTwo().Destinations] };

        var diagnosis = Envelope.Validate(two);

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("one destination");
    }

    [Test]
    public async Task An_envelope_governing_nothing_is_refused()
    {
        var none = AtTwo() with
        {
            Obligations = [],
            Loops = [AtTwo().Loops[0] with { Discharges = [] }],
            Destinations = [AtTwo().Destinations[0] with { Requires = [] }],
        };

        await Assert.That(Envelope.Validate(none)!).Contains("governs nothing");
    }

    // ---- closure, at the cardinality where it can be partial ----

    [Test]
    public async Task A_destination_requiring_an_obligation_that_does_not_exist_is_refused_naming_it()
    {
        // The mechanism is slice two's. What is new is that ONE of two required
        // obligations can exist while the other does not - which at cardinality
        // one could not happen, and is the shape a real envelope gets wrong.
        var broken = AtTwo() with
        {
            Destinations = [AtTwo().Destinations[0] with { Requires = ["in-scope", "not-a-rule"] }],
        };

        var diagnosis = Envelope.Validate(broken);

        await Assert.That(diagnosis!).Contains("not-a-rule");
        await Assert.That(diagnosis!).DoesNotContain("in-scope")
            .Because("the one that is wrong is named and the one that is fine is not, or a person "
                   + "reads four names and checks all of them.");
    }

    [Test]
    public async Task A_loop_discharging_an_obligation_that_does_not_exist_is_refused_naming_it()
    {
        var broken = AtTwo() with
        {
            Loops = [AtTwo().Loops[0] with { Discharges = ["in-scope", "invented"] }],
        };

        await Assert.That(Envelope.Validate(broken)!).Contains("invented");
    }

    [Test]
    public async Task An_obligation_nothing_discharges_is_still_a_closed_graph()
    {
        // The other direction, and it is deliberately ALLOWED. An obligation no
        // loop discharges is one a person satisfies, or one a later slice's
        // check: human route reaches - and refusing it here would refuse the
        // shape step 3 is built on.
        //
        // Left as a shape rather than a rule, and said out loud: the closure
        // check is about names that do not resolve, not about who satisfies what.
        var orphan = AtTwo() with
        {
            Loops = [AtTwo().Loops[0] with { Discharges = ["in-scope"] }],
        };

        await Assert.That(Envelope.Validate(orphan)).IsNull()
            .Because("'not-exhausted' is discharged by nothing and named by the destination, which "
                   + "is a governed flight waiting on something other than this loop.");
    }

    [Test]
    public async Task The_steel_thread_at_two_still_validates()
    {
        // Liveness. Every refusal above is satisfied by a validator that refuses
        // everything, and the steel thread at two has to pass.
        await Assert.That(Envelope.Validate(AtTwo())).IsNull();
    }
}
