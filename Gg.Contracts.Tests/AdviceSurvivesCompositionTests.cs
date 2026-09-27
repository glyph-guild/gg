using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Advice on the floor reaches a flight that has a work kind.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on GG-337, and it is the third time this feature has turned out to
/// have both ends and no middle.</b> Root carried four approved sentences at v13,
/// the flight was governed by v13, its repository slug matched the entry exactly —
/// and the prompt contained no advice at all. The composer never carried it:
/// <c>EnvelopeComposition</c> composes context, environments, repositories,
/// obligations, instructions and destinations, and <c>Learned</c> was not in that
/// list.
/// </para>
/// <para>
/// <b><c>[Composes(MergeOperators.Append)]</c> is documentation, not behaviour.</b>
/// The composer is hand-written and does not read the attribute, so declaring an
/// operator on a member composes nothing — which is the trap this repository already
/// knew about as "the composer arm nothing verifies" and which I walked into anyway.
/// </para>
/// <para>
/// <b>TWO LAYERS, because one cannot fail.</b>
/// <c>var @base = workKind ?? root!</c> — the base document is the work kind when
/// there is one, so root's members survive only where the composer appends them.
/// The lease test that was supposed to cover this applied root ALONE, which made
/// root the base and let <c>Learned</c> ride through untouched: it passed for a
/// reason that had nothing to do with composition, and a real flight with a work
/// kind is what exposed it.
/// </para>
/// </remarks>
public class AdviceSurvivesCompositionTests
{
    private static LearnedContext About(string repository, string advice) => new()
    {
        Against = new LearnedAgainst { Repository = repository },
        Advice = [advice],
    };

    private static Envelope Floor(params LearnedContext[] learned) => new()
    {
        Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" },
        Learned = learned.Length == 0 ? null : learned,
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops = [ALoop()],
        Destinations = [],
    };

    private static Envelope AWorkKind(params LearnedContext[] learned) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Learned = learned.Length == 0 ? null : learned,
        Obligations = [],
        Loops = [ALoop()],
        Destinations = [],
    };

    private static Loop ALoop() => new()
    {
        Id = "implement",
        Executor = ExecutorRungs.Frontier,
        Discharges = [],
        Moves = [LoopMoves.Read],
        Budget = new LoopBudget { WallClock = "20m" },
        OnExhaustion = ExhaustionPolicies.HandoffToHuman,
    };

    /// <remarks>
    /// A work kind sits under root and the composer refuses one that does not say so -
    /// "only root sits under nothing" - which is the first thing my fixture got wrong.
    /// </remarks>
    private static EnvelopeLayer Layer(string role, string name, Envelope document) => new()
    {
        Role = role,
        Name = name,
        Parent = string.Equals(role, Roles.Root, StringComparison.Ordinal)
            ? null
            : AirspaceNames.Root,
        Document = document,
        Version = "v1",
    };

    [Test]
    public async Task Advice_on_the_floor_reaches_a_flight_that_has_a_work_kind()
    {
        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, AirspaceNames.Root, Floor(About("acme/web", "npm install first."))),
            Layer(Roles.WorkKind, "ui-preview", AWorkKind()),
        ]);

        await Assert.That(composed.Refused).IsNull().Because($"{composed.Refused}");

        await Assert.That(composed.Composed!.Learned).IsNotNull()
            .Because("the floor's advice is the tenant's, and a flight with a work kind is "
                   + "still in the tenant - GG-337 read a prompt with none of it.");

        await Assert.That(composed.Composed.Learned!.Single().Advice.Single())
            .IsEqualTo("npm install first.");
    }

    [Test]
    public async Task Advice_from_both_layers_is_appended()
    {
        // APPEND, WHICH IS WHAT THE MEMBER DECLARES. A work kind may learn something
        // its floor has not, and neither shadows the other - the operator says so and
        // this is the assertion that makes the operator mean anything.
        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, AirspaceNames.Root, Floor(About("acme/web", "From the floor."))),
            Layer(Roles.WorkKind, "ui-preview", AWorkKind(About("acme/other", "From the kind."))),
        ]);

        await Assert.That(composed.Refused).IsNull().Because($"{composed.Refused}");
        await Assert.That(composed.Composed!.Learned!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_tenant_with_no_advice_composes_to_none()
    {
        // NULL RATHER THAN EMPTY, the rule the whole path keeps: a block over nothing
        // tells an agent something was learned when nothing was.
        var composed = EnvelopeComposition.Compose(
        [
            Layer(Roles.Root, AirspaceNames.Root, Floor()),
            Layer(Roles.WorkKind, "ui-preview", AWorkKind()),
        ]);

        await Assert.That(composed.Refused).IsNull().Because($"{composed.Refused}");
        await Assert.That(composed.Composed!.Learned).IsNull();
    }
}
