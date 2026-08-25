namespace Gg.Contracts.Tests;

/// <summary>
/// The merge operators are data on the schema, composition is generic over
/// them, and the ranking is gone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order-freedom is a property of the operators, not a claim about the
/// code</b> (ADR-0014). Every operator except the two <c>-only</c> members is
/// commutative and associative, so there is no ranking to consult and
/// <c>ordered[0]</c> has nothing to say. What replaces it is per-field data:
/// a new field on the schema fails the guard below until somebody declares
/// how it composes - the fact-vocabulary drift-guard shape, applied to
/// composition.
/// </para>
/// <para>
/// <b>A document declaring a field it may not MOVE is refused, not
/// ignored.</b> Today's silent wholesale-discard would, at chain depth four,
/// silently discard three documents' context - the silent-no-op class this
/// product exists to name, arriving through the layering machinery itself.
/// An ECHO of the governing value is not a move: work kinds are full
/// envelopes and <c>Validate</c> requires their members, so an echo has to
/// be expressible.
/// </para>
/// </remarks>
public class EnvelopeOperatorTests
{
    private static Envelope Full(
        string constitution = "1.0.0", string scope = "src/**",
        params string[] obligationIds)
    {
        var ids = obligationIds.Length > 0 ? obligationIds : ["in-scope"];

        return new Envelope
        {
            Context = new ContextBinding { Scope = scope, Constitution = constitution },
            Obligations =
            [
                .. ids.Select(id => new Obligation
                {
                    Id = id,
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                }),
            ],
            Loops =
            [
                new Loop
                {
                    Id = "implement",
                    Executor = ExecutorRungs.Frontier,
                    Discharges = [.. ids],
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
                    Requires = [.. ids],
                },
            ],
        };
    }

    private static EnvelopeLayer Root(Envelope? document = null) => new()
    {
        Role = Roles.Root,
        Name = "root",
        Parent = null,
        Document = document ?? Full(),
        Version = "v1",
    };

    private static EnvelopeLayer WorkKind(string name, Envelope document) => new()
    {
        Role = Roles.WorkKind,
        Name = name,
        Parent = "root",
        Document = document,
        Version = "v1",
    };

    private static EnvelopeLayer Narrowing(string name, string parent, params string[] ids) => new()
    {
        Role = Roles.Narrowing,
        Name = name,
        Parent = parent,
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

    // ---- the drift guard (S9.4-01) ----

    [Test]
    public async Task Every_envelope_field_declares_its_merge_operator_or_carries_a_written_exemption()
    {
        // THE GUARD. The composer's own table is built by reflection over the
        // schema, so a new field with no declared operator throws before
        // anything composes - and this assertion is the readable version of
        // that throw. The exemptions are as load-bearing as the declarations:
        // each names a reason, and preserve-unadmitted's reason is ADR-0014's
        // open question, kept visibly open rather than closed by a default.
        var declared = EnvelopeComposition.Operators;
        var exempt = EnvelopeComposition.OperatorExemptions;

        await Assert.That(declared[$"{nameof(ContextBinding)}.{nameof(ContextBinding.Scope)}"])
            .IsEqualTo(MergeOperators.Intersect);
        await Assert.That(declared[$"{nameof(ContextBinding)}.{nameof(ContextBinding.Constitution)}"])
            .IsEqualTo(MergeOperators.RootOnly);
        await Assert.That(declared[$"{nameof(Envelope)}.{nameof(Envelope.Obligations)}"])
            .IsEqualTo(MergeOperators.Union);
        await Assert.That(declared[$"{nameof(Envelope)}.{nameof(Envelope.Loops)}"])
            .IsEqualTo(MergeOperators.WorkKindOnly);
        await Assert.That(declared[$"{nameof(Envelope)}.{nameof(Envelope.Destinations)}"])
            .IsEqualTo(MergeOperators.WorkKindOnly);
        await Assert.That(declared[$"{nameof(LoopBudget)}.{nameof(LoopBudget.WallClock)}"])
            .IsEqualTo(MergeOperators.Min);
        await Assert.That(declared[$"{nameof(Loop)}.{nameof(Loop.Moves)}"])
            .IsEqualTo(MergeOperators.Intersect);
        await Assert.That(declared[$"{nameof(Destination)}.{nameof(Destination.Requires)}"])
            .IsEqualTo(MergeOperators.Union);

        // ADR-0014's one undecided operator was decided 2026-08-24: and. The
        // exemption retires rather than being reworded, because the drift
        // guard is the stronger custodian - a field with an operator is
        // inside the sweep, a field with an exemption is beside it.
        await Assert.That(declared[$"{nameof(Destination)}.{nameof(Destination.PreserveUnadmitted)}"])
            .IsEqualTo("and");
        await Assert.That(exempt.Keys)
            .DoesNotContain($"{nameof(Destination)}.{nameof(Destination.PreserveUnadmitted)}")
            .Because("an operator AND an exemption is the staleness the composer's own "
                   + "constructor refuses; the question is answered and the entry is gone.");

        // LIVENESS: the walk covers the whole closed schema. Every public
        // property of every composable type is declared or exempted; a new
        // member fails here (and the composer's own constructor) until its
        // operator is decided.
        Type[] schema = [typeof(Envelope), typeof(ContextBinding), typeof(Loop),
                         typeof(LoopBudget), typeof(Destination), typeof(EnvelopeNarrowing)];
        var unaccounted = schema
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(m => !declared.ContainsKey(m) && !exempt.ContainsKey(m))
            .ToList();

        await Assert.That(unaccounted).IsEmpty()
            .Because("a field nobody declared an operator for would compose by accident. "
                   + "Found: " + string.Join(", ", unaccounted));
        await Assert.That(declared.ContainsKey("Envelope.NoSuchField")).IsFalse();
    }

    // ---- the declared-field refusal (S9.4-02) ----

    [Test]
    public async Task A_layer_moving_a_root_only_field_is_refused_naming_the_layer_the_field_and_the_operator()
    {
        var composition = EnvelopeComposition.Compose(
            [Root(Full(constitution: "1.0.0")),
             WorkKind("payments", Full(constitution: "2.0.0", obligationIds: "payments-scope"))]);

        await Assert.That(composition.Refused).IsNotNull()
            .Because("the silent discard dies with the ranking: at chain depth four it would "
                   + "silently drop three documents' context.");
        await Assert.That(composition.Refused!).Contains("payments");
        await Assert.That(composition.Refused).Contains("constitution");
        await Assert.That(composition.Refused).Contains(MergeOperators.RootOnly);
    }

    [Test]
    public async Task An_echo_of_the_governing_value_is_not_a_move()
    {
        // Work kinds are full envelopes and Validate requires their members,
        // so an echo has to compose - the refusal is for MOVING the field.
        var composition = EnvelopeComposition.Compose(
            [Root(Full(constitution: "1.0.0")),
             WorkKind("payments", Full(constitution: "1.0.0", obligationIds: "payments-scope"))]);

        await Assert.That(composition.Refused).IsNull();
        await Assert.That(composition.Composed!.Context.Constitution).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task The_sets_come_from_the_work_kind_and_the_meets_meet()
    {
        var kind = Full(scope: "src/payments/**", obligationIds: "payments-scope") with
        {
            Loops =
            [
                new Loop
                {
                    Id = "implement",
                    Executor = ExecutorRungs.Frontier,
                    Discharges = ["payments-scope"],
                    Moves = [LoopMoves.Edit, LoopMoves.Read, LoopMoves.RunTests],
                    Budget = new LoopBudget { WallClock = "10m" },
                    OnExhaustion = ExhaustionPolicies.HandoffToHuman,
                },
            ],
        };

        var composition = EnvelopeComposition.Compose([Root(Full(scope: "src/**")), WorkKind("payments", kind)]);

        await Assert.That(composition.Refused).IsNull();
        var composed = composition.Composed!;
        await Assert.That(composed.Loops.Single().Moves)
            .IsEquivalentTo((string[])[LoopMoves.Edit, LoopMoves.Read, LoopMoves.RunTests])
            .Because("one layer supplies the sets: the work kind's loops are the loops.");
        await Assert.That(composed.Context.Scope).IsEqualTo("src/payments/**")
            .Because("intersect keeps the narrower scope when one contains the other.");
    }

    [Test]
    public async Task Scopes_that_neither_contain_the_other_are_refused_rather_than_guessed()
    {
        var composition = EnvelopeComposition.Compose(
            [Root(Full(scope: "src/**")),
             WorkKind("payments", Full(scope: "services/**", obligationIds: "payments-scope"))]);

        await Assert.That(composition.Refused).IsNotNull();
        await Assert.That(composition.Refused!).Contains("src/**");
        await Assert.That(composition.Refused).Contains("services/**")
            .Because("an intersection nobody can express as one glob is a refusal naming "
                   + "both, never a silent pick.");
    }

    // ---- chain verification and provenance (S9.4-03) ----

    [Test]
    public async Task Compose_verifies_the_chain_it_was_handed()
    {
        // A parent that is not in the set: two tops, no chain.
        var dangling = EnvelopeComposition.Compose(
            [Root(), Narrowing("pci", parent: "payments", ids: "pci-review")]);
        await Assert.That(dangling.Refused).IsNotNull();
        await Assert.That(dangling.Refused!).Contains("payments");

        // Two documents claiming one name.
        var duplicate = EnvelopeComposition.Compose(
            [Root(), Narrowing("pci", "root", "a"), Narrowing("pci", "root", "b")]);
        await Assert.That(duplicate.Refused).IsNotNull();
        await Assert.That(duplicate.Refused!).Contains("pci");

        // A root that is not named root is a pointer wearing the floor's role.
        var mislabeled = EnvelopeComposition.Compose(
            [Root() with { Name = "floor" }]);
        await Assert.That(mislabeled.Refused).IsNotNull();
        await Assert.That(mislabeled.Refused!).Contains("root");
    }

    [Test]
    public async Task Provenance_answers_with_a_role_and_a_name_a_person_recognises()
    {
        var composition = EnvelopeComposition.Compose(
            [Root(), Narrowing("pci", "root", "pci-review")]);

        await Assert.That(composition.Refused).IsNull();
        var byId = composition.Composed!.Obligations.ToDictionary(o => o.Id);
        await Assert.That(byId["in-scope"].Provenance)
            .IsEqualTo(new ObligationProvenance { Role = Roles.Root, Name = "root" });
        await Assert.That(byId["pci-review"].Provenance)
            .IsEqualTo(new ObligationProvenance { Role = Roles.Narrowing, Name = "pci" })
            .Because("'why did this gate appear' answers with a word a person recognises, "
                   + "not a rank in a deleted list.");
    }

    [Test]
    public async Task Order_freedom_is_a_property_rather_than_a_claim()
    {
        EnvelopeLayer[] layers =
            [Root(), Narrowing("pci", "root", "pci-review"), Narrowing("sox", "root", "sox-review")];
        var forward = EnvelopeComposition.Compose(layers);
        var backward = EnvelopeComposition.Compose([.. layers.Reverse()]);

        await Assert.That(forward.Refused).IsNull();
        await Assert.That(forward.Composed!.Obligations.Select(o => o.Id).Order().ToList())
            .IsEquivalentTo(backward.Composed!.Obligations.Select(o => o.Id).Order().ToList())
            .Because("every operator except the -only pair is commutative and associative, "
                   + "so shuffling the list can change nothing.");
    }

    [Test]
    public async Task Composed_obligations_arrive_in_one_order_regardless_of_the_list_handed_in()
    {
        // THE PIN DIGEST'S REPRODUCIBILITY. Composition is order-free in
        // CONTENT, but the composed obligations keep an order and a digest is
        // over the bytes - so the composer owns a canonical order, rather
        // than every caller pre-sorting and hoping the next one knows to.
        EnvelopeLayer[] layers =
            [Root(), Narrowing("pci", "root", "pci-review"), Narrowing("sox", "root", "sox-review")];
        var forward = EnvelopeComposition.Compose(layers);
        var backward = EnvelopeComposition.Compose([.. layers.Reverse()]);

        await Assert.That(forward.Refused).IsNull();
        await Assert.That(forward.Composed!.Obligations.Select(o => o.Id)
                .SequenceEqual(backward.Composed!.Obligations.Select(o => o.Id))).IsTrue()
            .Because("two callers composing the same layers must hash the same bytes, or a "
                   + "pin's digest depends on who resolved it.");
    }

    [Test]
    public async Task Two_layers_declaring_one_obligation_id_are_refused_naming_both_names()
    {
        var collision = EnvelopeComposition.Compose(
            [Root(Full(obligationIds: "contracts-intact")),
             Narrowing("pci", "root", "contracts-intact")]);

        await Assert.That(collision.Refused).IsNotNull();
        await Assert.That(collision.Refused!).Contains("'contracts-intact'");
        await Assert.That(collision.Refused).Contains("root");
        await Assert.That(collision.Refused).Contains("pci")
            .Because("shadowing is removal with extra steps, and the refusal names both "
                   + "documents so the author knows whose obligation they collided with.");
    }

    [Test]
    public async Task Roles_are_a_closed_set_and_not_a_ranking()
    {
        await Assert.That(Roles.All)
            .IsEquivalentTo((string[])[Roles.Root, Roles.WorkKind, Roles.Narrowing, Roles.Strategy]);
        await Assert.That(MergeOperators.All).IsEquivalentTo((string[])
            [MergeOperators.RootOnly, MergeOperators.WorkKindOnly, MergeOperators.Intersect,
             MergeOperators.Min, MergeOperators.Union, "and"]);
    }
}
