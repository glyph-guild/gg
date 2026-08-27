namespace Gg.Contracts.Tests;

/// <summary>
/// A narrowing's obligations bind: they are unioned into every destination's
/// <c>requires</c> by the composer, so a narrowing gate that blocks nothing is
/// inexpressible.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifteen's step 0 found this missing, and found it missing in the
/// worst possible way — as a declaration nothing honoured.</b>
/// <see cref="Destination.Requires"/> has carried
/// <c>[Composes(MergeOperators.Union)]</c> since slice nine, and
/// <see cref="EnvelopeComposition.Compose"/> has never unioned it: destinations
/// arrive wholesale from the base layer and no lower layer touches them. So the
/// operator table said one thing and the composer did another, and the drift
/// guard could not see it because the guard checks that every field DECLARES an
/// operator, not that every operator is APPLIED.
/// </para>
/// <para>
/// <b>Why it matters is ADR-0014's own sentence.</b> <i>"A narrowing must be
/// able to union `requires`, or it is decorative"</i> — because admission
/// iterates a destination's <c>requires</c> and nothing else, so an obligation
/// absent from it is evaluated, recorded, and <b>cannot block</b>. A narrowing
/// that may only add obligations therefore blocks nothing and produces verdicts
/// nobody has to honour, which is the *obligations that nothing discharges*
/// risk wearing its other face.
/// </para>
/// <para>
/// <b>The binding is the composer's, not the author's.</b> The alternative was
/// a member on <see cref="EnvelopeNarrowing"/> naming destination ids, which is
/// a cross-document reference that can dangle: a narrowing naming a destination
/// its work kind does not declare, or failing to name one it does. There is no
/// such member and there is not going to be one — the question <i>why would you
/// author a narrowing obligation that blocks nothing</i> has no answer ADR-0014
/// accepts.
/// </para>
/// <para>
/// <b>Root and the work kind are deliberately NOT auto-bound.</b> They author
/// their own <c>requires</c> and <i>"a destination requiring nothing is a real
/// envelope"</i> is a sentence in <c>AdmissionEngine</c>. An obligation a floor
/// records without blocking on is a choice its author made in the same document;
/// a narrowing has no field in which to make that choice, which is exactly why
/// the default has to be the constraining one.
/// </para>
/// </remarks>
public class NarrowingBindsTests
{
    private const string InScope = "in-scope";

    private static Envelope Base(params string[] requires) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation
            {
                Id = InScope,
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
                Discharges = [InScope],
                Moves = [LoopMoves.Edit, LoopMoves.Read],
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
                Requires = requires.Length > 0 ? [.. requires] : [InScope],
            },
        ],
    };

    private static Envelope TwoDestinations() => Base() with
    {
        Destinations =
        [
            new Destination
            {
                Id = "pull-request",
                Kind = DestinationKinds.PullRequest,
                Requires = [InScope],
            },
            new Destination
            {
                Id = "envelope-change",
                Kind = DestinationKinds.EnvelopeChange,
                Requires = [],
            },
        ],
    };

    private static EnvelopeLayer Root(Envelope? document = null) => new()
    {
        Role = Roles.Root,
        Name = "root",
        Parent = null,
        Document = document ?? Base(),
        Version = "v1",
    };

    private static EnvelopeLayer Narrowing(string name, params string[] ids) => new()
    {
        Role = Roles.Narrowing,
        Name = name,
        Parent = "root",
        Narrowing = new EnvelopeNarrowing
        {
            Obligations =
            [
                .. ids.Select(id => new Obligation
                {
                    Id = id,
                    Check = ObligationChecks.Human,
                    Approver = "lead",
                }),
            ],
        },
        Version = "v1",
    };

    private static Envelope Composed(params EnvelopeLayer[] layers)
    {
        var composition = EnvelopeComposition.Compose(layers);

        return composition.Composed
            ?? throw new InvalidOperationException(
                "These layers were meant to compose. Refused: " + composition.Refused);
    }

    [Test]
    public async Task A_narrowings_obligation_is_required_by_the_destination()
    {
        // THE WHOLE CLAIM. Without this the obligation is composed, evaluated,
        // recorded - and admission never looks at it.
        var composed = Composed(Root(), Narrowing("pci", "pci-review"));

        await Assert.That(composed.Obligations.Select(o => o.Id)).Contains("pci-review")
            .Because("the obligation still unions the way it always did.");
        await Assert.That(composed.Destinations.Single().Requires).Contains("pci-review")
            .Because("admission iterates requires and nothing else, so an obligation absent "
                   + "from it is a gate that cannot refuse anything.");
    }

    [Test]
    public async Task It_binds_to_every_destination_rather_than_to_the_first()
    {
        // A narrowing constrains the flight, not one exit from it. Binding only
        // to Destinations[0] would let a tenant with two destinations route
        // around a compliance gate by admitting against the other one.
        var composed = Composed(Root(TwoDestinations()), Narrowing("pci", "pci-review"));

        foreach (var destination in composed.Destinations)
        {
            await Assert.That(destination.Requires).Contains("pci-review")
                .Because($"'{destination.Id}' is an exit from the same flight, and a narrowing "
                       + "that binds to one exit is a narrowing somebody can walk around.");
        }
    }

    [Test]
    public async Task A_destination_that_required_nothing_now_requires_the_narrowing()
    {
        // The sharpest case, and the one an empty list makes easy to get wrong:
        // a destination naming no obligations admits everything, so a narrowing
        // that fails to reach it has no effect at all.
        var composed = Composed(Root(TwoDestinations()), Narrowing("pci", "pci-review"));
        var open = composed.Destinations.Single(d => d.Id == "envelope-change");

        await Assert.That(open.Requires).IsEquivalentTo(new[] { "pci-review" });
    }

    [Test]
    public async Task Two_narrowings_both_bind_and_the_result_is_order_free()
    {
        var forwards = Composed(Root(), Narrowing("pci", "pci-review"), Narrowing("sox", "sox-review"));
        var backwards = Composed(Root(), Narrowing("sox", "sox-review"), Narrowing("pci", "pci-review"));

        await Assert.That(forwards.Destinations.Single().Requires)
            .IsEquivalentTo(backwards.Destinations.Single().Requires)
            .Because("union is commutative, which is the property that lets a flight carry as "
                   + "many narrowings as apply with no ranking between them.");
        await Assert.That(forwards.Destinations.Single().Requires)
            .Contains("pci-review").And.Contains("sox-review");
    }

    [Test]
    public async Task Binding_is_a_union_rather_than_an_append()
    {
        // The base already requires `in-scope`; a narrowing declaring an
        // obligation with an id the base requires must not duplicate it. A
        // duplicate is not merely untidy: `requires` is iterated to build a
        // refusal sentence, and the same id twice reads as two outstanding
        // gates where there is one.
        var composed = Composed(Root(), Narrowing("dup", InScope));

        await Assert.That(composed.Destinations.Single().Requires.Count(r => r == InScope))
            .IsEqualTo(1);
    }

    [Test]
    public async Task Root_and_the_work_kind_are_not_auto_bound()
    {
        // The other half, and it is what stops this rule being "every
        // obligation blocks". A floor that records an obligation without
        // requiring it made that choice in the document it wrote; a narrowing
        // has no field in which to make it.
        var floor = Base(requires: InScope) with
        {
            Obligations =
            [
                .. Base().Obligations,
                new Obligation { Id = "advisory", Check = ObligationChecks.Human, Approver = "lead" },
            ],
        };

        var composed = Composed(Root(floor));

        await Assert.That(composed.Obligations.Select(o => o.Id)).Contains("advisory");
        await Assert.That(composed.Destinations.Single().Requires).DoesNotContain("advisory")
            .Because("root named the obligation and declined to require it, in one document, "
                   + "deliberately. A destination requiring nothing is a real envelope.");
    }

    [Test]
    public async Task The_per_flight_overlay_binds_too_because_it_is_a_narrowing()
    {
        // The layer a flight's own attachments compose to is role `narrowing`
        // and carries the name `flight`. It gets the same rule, and it is a
        // decision rather than a side effect: a gate that attached mid-flight
        // and cannot refuse the destination is the laundering shape ADR-0014
        // closes, arriving through the composer instead of through a deleted
        // file.
        var composed = Composed(Root(), new EnvelopeLayer
        {
            Role = Roles.Narrowing,
            Name = "flight",
            Parent = "root",
            Narrowing = new EnvelopeNarrowing
            {
                Obligations =
                [
                    new Obligation
                    {
                        Id = "migration-review",
                        Check = ObligationChecks.Human,
                        Approver = "dba",
                    },
                ],
            },
            Version = "v1",
        });

        await Assert.That(composed.Destinations.Single().Requires).Contains("migration-review");
    }

    [Test]
    public async Task A_narrowing_has_no_member_naming_a_destination()
    {
        // THE REASON THE BINDING IS THE COMPOSER'S. If a narrowing could name
        // the destinations it binds to, it could name one that does not exist
        // (a dangling reference nothing resolves) or fail to name one that does
        // (a gate silently not applying to an exit). Neither failure is
        // possible against a type with no such member, which is the same
        // argument ADR-0014 makes for scope and constitution one field over.
        var members = typeof(EnvelopeNarrowing).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsEquivalentTo(new[] { nameof(EnvelopeNarrowing.Obligations) })
            .Because("a narrowing carries what it adds and nothing about where it applies. "
                   + "Found: " + string.Join(", ", members));
    }

    [Test]
    public async Task Composing_with_no_narrowing_leaves_requires_exactly_as_authored()
    {
        // Liveness in the other direction: every assertion above would pass just
        // as well if the composer had started requiring EVERYTHING. This is the
        // twin that says it did not.
        var composed = Composed(Root(TwoDestinations()));

        await Assert.That(composed.Destinations.Single(d => d.Id == "pull-request").Requires)
            .IsEquivalentTo(new[] { InScope });
        await Assert.That(composed.Destinations.Single(d => d.Id == "envelope-change").Requires)
            .IsEmpty();
    }
}
