using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Two layers, composed add-only, with provenance the composer assigns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate documents, composed at evaluation.</b> Authorship is per layer with
/// different owners and different change processes - and applying at flight level
/// must not be able to touch org level, which is structural when they are separate
/// documents and a permission check when they are one. A permission check is
/// something somebody can get wrong; a document a writer cannot reach is not.
/// </para>
/// <para>
/// <b>Add-only rather than narrow-only.</b> The design says a lower layer may only
/// narrow. General widening detection over predicates is undecidable, and an
/// approximation would be wrong in ways nobody could characterise - so the rule
/// implemented is the decidable one with the same effect: a lower layer adds its
/// own and may not touch a higher layer's.
/// </para>
/// </remarks>
public class EnvelopeLayerTests
{
    private static Envelope Document(params string[] obligationIds) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            .. obligationIds.Select(id => new Obligation
            {
                Id = id,
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            }),
        ],
        Loops = [],
        Destinations = [],
    };

    /// <summary>
    /// A whole envelope that passes every other rule, so <c>when:</c> is the subject.
    /// </summary>
    /// <remarks>
    /// The loop discharges NOTHING, because the obligation under test is a human
    /// check and a loop that discharged one would be a runner answering for a
    /// person - which is a different refusal, and it would fire first.
    /// </remarks>
    private static Envelope Complete(Obligation obligation) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = [obligation],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
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
                Requires = [obligation.Id],
            },
        ],
    };

    private static EnvelopeLayer Layer(string layer, params string[] ids) => new()
    {
        Layer = layer,
        Document = Document(ids),
        Version = $"{layer}-1",
    };

    // ---- the layer vocabulary is a ranking ----

    [Test]
    public async Task Layers_are_ordered_so_reordering_them_moves_the_fingerprint()
    {
        // THE DEFECT THIS AVOIDS, found once already on the egress levels:
        // ClosedVocabularies sorts a vocabulary's values before hashing unless it
        // is declared ordered. Sorted, swapping org and flight would change which
        // layer outranks which and move no fingerprint at all.
        var membership = typeof(ObligationProvenances)
            .GetCustomAttributes(typeof(VocabularyOfAttribute), false)
            .Cast<VocabularyOfAttribute>()
            .Single();

        await Assert.That(membership.Ordered).IsTrue()
            .Because("the order IS the content here: it is the ranking, not a list somebody "
                   + "typed in some order.");
        await Assert.That(ObligationProvenances.All[0]).IsEqualTo(ObligationProvenances.Org)
            .Because("highest authority first, which is what Outranks reads.");
        await Assert.That(ObligationProvenances.Outranks(
            ObligationProvenances.Org, ObligationProvenances.Flight)).IsTrue();
    }

    [Test]
    public async Task There_is_no_team_layer_and_its_absence_is_asserted()
    {
        // The design has three layers and this step ships two. A value nothing can
        // produce is a value nobody maintains, so `team` is left out and its
        // arrival is a version move - which is honest, and this is where somebody
        // says so.
        await Assert.That(ObligationProvenances.All).DoesNotContain("team");
        await Assert.That(ObligationProvenances.All.Count).IsEqualTo(2);
    }

    // ---- composition ----

    [Test]
    public async Task A_lower_layer_may_add_its_own()
    {
        // THE POSITIVE CONTROL, and it comes first because without it the refusal
        // below is satisfiable by refusing everything.
        var composition = EnvelopeComposition.Compose(
            [Layer(ObligationProvenances.Org, "in-scope"),
             Layer(ObligationProvenances.Flight, "needs-a-person")]);

        await Assert.That(composition.Refused).IsNull();
        await Assert.That(composition.Composed!.Obligations.Select(o => o.Id).Order().ToList())
            .IsEquivalentTo((string[])["in-scope", "needs-a-person"]);
    }

    [Test]
    public async Task A_lower_layer_may_not_touch_a_higher_layers_obligation()
    {
        // Redeclaring an id is the only way a document can reach another layer's
        // obligation at all - there is no edit operation to secure, which is a
        // better outcome than securing one.
        var composition = EnvelopeComposition.Compose(
            [Layer(ObligationProvenances.Org, "in-scope"),
             Layer(ObligationProvenances.Flight, "in-scope")]);

        await Assert.That(composition.Composed).IsNull();
        await Assert.That(composition.Refused!).Contains("'in-scope'");
        await Assert.That(composition.Refused!).Contains("org layer introduced")
            .Because("it names the layer that introduced it, so somebody reading their own file "
                   + "is not sent looking for a mistake that is in another layer's.");
    }

    [Test]
    public async Task Strengthening_is_adding_and_needs_no_primitive()
    {
        // If org says a machine check and a flight wants a person as well, the
        // flight ADDS its own. Org's stays exactly as org wrote it, both attach,
        // and the stricter one binds because both must hold. That removes the only
        // case that looked like it needed an edit.
        var org = Layer(ObligationProvenances.Org, "contracts-intact");
        var flight = new EnvelopeLayer
        {
            Layer = ObligationProvenances.Flight,
            Version = "flight-1",
            Document = Document() with
            {
                Obligations =
                [
                    new Obligation
                    {
                        Id = "contracts-intact-reviewed",
                        Check = ObligationChecks.Human,
                        Approver = "platform-oncall",
                    },
                ],
            },
        };

        var composition = EnvelopeComposition.Compose([org, flight]);

        await Assert.That(composition.Refused).IsNull();
        await Assert.That(composition.Composed!.Obligations.Count).IsEqualTo(2)
            .Because("both are present, so both must hold - which is what stricter means when "
                   + "nothing may be edited.");
    }

    [Test]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(5)]
    public async Task Composition_is_order_independent_above_two_layers(int layers)
    {
        // ABOVE TWO EVEN THOUGH THE PRODUCT SHIPS AT TWO. Slice three retired
        // "two is not twenty" for the Engine on exactly this reasoning: a product
        // constraint is not a test constraint, and order-independence that only
        // holds at the shipping cardinality is a property nobody has tested.
        //
        // The extra layers use the real vocabulary's two values plus synthetic
        // ones, because what is under test is that the COMPOSER does not depend on
        // argument order - not that the vocabulary has five members.
        var built = Enumerable.Range(0, layers)
            .Select(i => new EnvelopeLayer
            {
                Layer = i < ObligationProvenances.All.Count
                    ? ObligationProvenances.All[i]
                    : $"synthetic-{i}",
                Document = Document($"obligation-{i}"),
                Version = $"v{i}",
            })
            .ToList();

        var real = built.Where(b => ObligationProvenances.All.Contains(b.Layer, StringComparer.Ordinal))
            .ToList();

        var forward = EnvelopeComposition.Compose(real);
        var backward = EnvelopeComposition.Compose([.. Enumerable.Reverse(real)]);

        await Assert.That(forward.Refused).IsEqualTo(backward.Refused);
        await Assert.That(forward.Composed!.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList())
            .IsEquivalentTo(backward.Composed!.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList())
            .Because("a caller must not be able to change what governs a flight by shuffling "
                   + "a list.");
    }

    [Test]
    public async Task Two_documents_claiming_one_layer_are_refused()
    {
        var composition = EnvelopeComposition.Compose(
            [Layer(ObligationProvenances.Org, "a"), Layer(ObligationProvenances.Org, "b")]);

        await Assert.That(composition.Refused!).Contains("same layer")
            .Because("which one governs would be decided by list order, and a list order is "
                   + "not an ownership model.");
    }

    // ---- provenance is derived ----

    [Test]
    public async Task Provenance_comes_from_where_the_document_sat()
    {
        var composition = EnvelopeComposition.Compose(
            [Layer(ObligationProvenances.Org, "in-scope"),
             Layer(ObligationProvenances.Flight, "needs-a-person")]);

        var byId = composition.Composed!.Obligations.ToDictionary(o => o.Id);

        await Assert.That(byId["in-scope"].Provenance).IsEqualTo(ObligationProvenances.Org);
        await Assert.That(byId["needs-a-person"].Provenance).IsEqualTo(ObligationProvenances.Flight);
    }

    [Test]
    public async Task A_document_claiming_a_layer_it_is_not_in_does_not_get_it()
    {
        // UNFORGEABLE. The document says org; it sat at flight; the composed
        // obligation says flight. The parser refuses the line outright, and this
        // is the second lock: even a document assembled in code cannot lie about
        // where it came from, because the composer never reads it.
        var lying = new EnvelopeLayer
        {
            Layer = ObligationProvenances.Flight,
            Version = "flight-1",
            Document = Document() with
            {
                Obligations =
                [
                    new Obligation
                    {
                        Id = "mine",
                        Check = ObligationChecks.Machine,
                        Rule = ObligationPredicates.NoFileOutsideScope,
                        Provenance = ObligationProvenances.Org,
                    },
                ],
            },
        };

        var composed = EnvelopeComposition.Compose([lying]).Composed!;

        await Assert.That(composed.Obligations.Single().Provenance)
            .IsEqualTo(ObligationProvenances.Flight)
            .Because("the thing being governed does not get to describe its own authority.");
    }

    // ---- what a pin has to be ----

    [Test]
    public async Task A_pinned_set_composes_the_same_way_after_both_layers_move()
    {
        // A PIN THAT RESOLVES TO "WHATEVER ORG IS TODAY" IS NOT A PIN. What the
        // flight pins is a SET of versions, and the composed result has to be
        // reproducible from that set alone - so this composes the pinned documents
        // again after newer ones exist and gets the same envelope.
        var pinned = new[]
        {
            Layer(ObligationProvenances.Org, "in-scope"),
            Layer(ObligationProvenances.Flight, "needs-a-person"),
        };

        var before = EnvelopeComposition.Compose(pinned).Composed!;

        // Both layers move on. Nothing about the pinned set changed.
        _ = EnvelopeComposition.Compose(
            [Layer(ObligationProvenances.Org, "in-scope", "and-another"),
             Layer(ObligationProvenances.Flight, "needs-a-person", "and-a-third")]);

        var after = EnvelopeComposition.Compose(pinned).Composed!;

        await Assert.That(after.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList())
            .IsEquivalentTo(before.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList());
        await Assert.That(pinned.Select(p => p.Version).ToList())
            .IsEquivalentTo((string[])["org-1", "flight-1"])
            .Because("the set is what is pinned, and it is what the composition is reproducible "
                   + "from.");
    }

    // ---- and the condition that is refused rather than left authorable ----

    [Test]
    public async Task A_condition_citing_a_verdict_is_refused_and_names_the_open_question()
    {
        var diagnosis = Envelope.Validate(Complete(new Obligation
        {
            Id = "gate",
            Check = ObligationChecks.Human,
            Approver = "platform-oncall",
            When = "obligations.contracts-intact == violated",
        }))!;

        await Assert.That(diagnosis).Contains("cites a VERDICT");
        await Assert.That(diagnosis).Contains("fixed point")
            .Because("it says what breaks rather than only that something does.");
        await Assert.That(diagnosis).Contains("something evaluation produced")
            .Because("and it names the open question's answer-in-waiting: the line is not how "
                   + "many things evaluation may read, it is that none may be something "
                   + "evaluation produced.");
    }

    [Test]
    public async Task A_condition_over_facts_is_still_accepted()
    {
        // The liveness twin. Without it the refusal above is satisfied by a
        // version that refuses every condition, which would take the gate with it.
        await Assert.That(Envelope.Validate(Complete(new Obligation
        {
            Id = "gate",
            Check = ObligationChecks.Human,
            Approver = "platform-oncall",
            When = "change.manifest touches migrations/**",
        }))).IsNull();
    }
}
