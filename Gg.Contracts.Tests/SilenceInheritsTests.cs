using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A layer that says nothing about a root-only field inherits it.
/// </summary>
/// <remarks>
/// <para>
/// <b>IT USED TO BE READ AS MOVING THE FIELD TO NOTHING, which made a root
/// bound unusable.</b> A tenant declaring <c>environments: [dev, staging]</c> at
/// root found every work-kind layer refused - "moves environments from 'dev,
/// staging' to 'nothing'" - unless each one repeated the value. So the only way
/// to have a usable bound was to duplicate it into every layer, which drifts
/// the moment somebody edits one, and the only way to avoid duplicating it was
/// not to have a bound.
/// </para>
/// <para>
/// <b>Found by needing it.</b> Nothing exercised the combination until a
/// destination had to permit a nomination to choose an environment, which needs
/// a root bound AND layers below it. The rule had been correct-looking and
/// unreachable.
/// </para>
/// <para>
/// <b>It changes no composed envelope.</b> The composed value is root's either
/// way - <c>environments = floor.Environments</c> - so this only stops refusing
/// documents that were always going to compose to the same thing. Echoing the
/// governing value stays legal; declaring a DIFFERENT value is still the move
/// that asks for the floor's authority.
/// </para>
/// </remarks>
public class SilenceInheritsTests
{
    private static Envelope Layer(
        string obligation,
        IReadOnlyList<string>? environments = null,
        string? constitution = null) => new()
    {
        Context = new ContextBinding
        {
            Scope = "src/**",
            Constitution = constitution ?? "1.0.0",
        },
        Environments = environments,
        Obligations =
        [
            new Obligation
            {
                Id = obligation,
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = [obligation],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "20m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = [obligation],
            },
        ],
    };

    private static Composition Composed(Envelope root, Envelope workKind) =>
        EnvelopeComposition.Compose(
        [
            new EnvelopeLayer
            {
                Role = Roles.Root,
                Name = "root",
                Parent = null,
                Document = root,
                Version = "v1",
            },
            new EnvelopeLayer
            {
                Role = Roles.WorkKind,
                Name = "fix",
                Parent = "root",
                Document = workKind,
                Version = "v1",
            },
        ]);

    [Test]
    public async Task A_layer_declaring_no_bound_inherits_the_root_bound()
    {
        var composition = Composed(
            Layer("in-scope", environments: ["dev", "staging"]), Layer("fix-scope"));

        await Assert.That(composition.Refused).IsNull()
            .Because("a work kind that says nothing about where flights run has not moved "
                   + "the bound - and refusing it would mean the only usable root bound is "
                   + "one duplicated into every layer beneath it.");
        await Assert.That(composition.Composed!.Environments)
            .IsEquivalentTo((string[])["dev", "staging"]);
    }

    [Test]
    public async Task Echoing_the_root_bound_is_still_allowed()
    {
        var composition = Composed(
            Layer("in-scope", environments: ["dev"]), Layer("fix-scope", environments: ["dev"]));

        await Assert.That(composition.Refused).IsNull()
            .Because("saw: " + (composition.Refused ?? "null"));
        await Assert.That(composition.Composed!.Environments)
            .IsEquivalentTo((string[])["dev"]);
    }

    [Test]
    public async Task Declaring_a_different_bound_is_still_the_move_that_is_refused()
    {
        // THE RULE THIS DOES NOT RELAX. A layer naming an environment root did
        // not permit is asking for the floor's authority, and that is still the
        // refusal - naming both values, so an author can see which is which.
        var composition = Composed(
            Layer("in-scope", environments: ["dev"]), Layer("fix-scope", environments: ["production"]));

        await Assert.That(composition.Refused).IsNotNull();
        await Assert.That(composition.Refused!).Contains("production");
        await Assert.That(composition.Refused!).Contains("dev");
    }

    [Test]
    public async Task Silence_inherits_for_the_scalar_root_only_fields_too()
    {
        // The same reading, applied where the same reasoning holds: a layer
        // that omits the constitution has not moved it. Asserted because the
        // fix is in the shared helper, so it must be right for every caller
        // rather than only for the one that needed it.
        var composition = Composed(
            Layer("in-scope", constitution: "1.2.0"), Layer("fix-scope", constitution: "1.2.0"));

        await Assert.That(composition.Refused).IsNull();
    }
}
