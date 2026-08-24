using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// Layers composed add-only, with provenance the composer assigns - now
/// (role, name), with no ranking anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate documents, composed at evaluation.</b> Authorship is per layer
/// with different owners and different change processes - and a narrowing must
/// not be able to touch root, which is structural when they are separate
/// documents and a permission check when they are one. A permission check is
/// something somebody can get wrong; a document a writer cannot reach is not.
/// </para>
/// <para>
/// <b>This file's ranking tests are deliberately gone.</b>
/// <c>Layers_are_ordered_so_reordering_them_moves_the_fingerprint</c> guarded
/// <c>Ordered = true</c> on the provenance vocabulary; ADR-0014 records the
/// ranking itself as wrongly reasoned - an artifact of replacement semantics -
/// so the guard now points the other way: the roles are a SET, and which role
/// may move which field is per-field data (<see cref="EnvelopeOperatorTests"/>).
/// <c>There_is_no_team_layer</c> flips the same way: a team is a narrowing
/// with a name, not a rank.
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

    private static EnvelopeLayer Root(params string[] ids) => new()
    {
        Role = Roles.Root,
        Name = "root",
        Parent = null,
        Document = Document(ids),
        Version = "root-1",
    };

    private static EnvelopeLayer Narrow(string name, params string[] ids) => new()
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
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                }),
            ],
        },
        Version = $"{name}-1",
    };

    // ---- the roles are a set, and the ranking is gone ----

    [Test]
    public async Task Roles_are_a_set_and_deliberately_not_a_ranking()
    {
        // THE INVERSE of the guard this replaces. The predecessor vocabulary
        // was Ordered = true because its order WAS the ranking; ADR-0014
        // records the ranking as wrongly reasoned - an artifact of
        // ordered[0]'s replacement semantics - so the roles must NOT be
        // ordered: position-as-authority is exactly what was deleted, and an
        // ordered attribute would be the first step back toward it.
        var membership = typeof(Roles)
            .GetCustomAttributes(typeof(VocabularyOfAttribute), false)
            .Cast<VocabularyOfAttribute>()
            .Single();

        await Assert.That(membership.Ordered).IsFalse()
            .Because("which role may move which field is per-field data, never list position.");
    }

    [Test]
    public async Task There_is_no_team_role_because_a_team_is_a_narrowing_with_a_name()
    {
        // The old assertion held `team` out of a closed layer list so its
        // arrival would be a visible version move. The day arrived and the
        // answer is better than a third rank: a team is a NAME in the open
        // half of (role, name), so no vocabulary moves when one forms.
        await Assert.That(Roles.All).DoesNotContain("team");
        await Assert.That(Roles.All.Count).IsEqualTo(3);
    }

    // ---- composition ----

    [Test]
    public async Task A_narrowing_may_add_its_own()
    {
        // THE POSITIVE CONTROL, and it comes first because without it the refusal
        // below is satisfiable by refusing everything.
        var composition = EnvelopeComposition.Compose(
            [Root("in-scope"), Narrow("flight", "needs-a-person")]);

        await Assert.That(composition.Refused).IsNull();
        await Assert.That(composition.Composed!.Obligations.Select(o => o.Id).Order().ToList())
            .IsEquivalentTo((string[])["in-scope", "needs-a-person"]);
    }

    [Test]
    public async Task A_narrowing_may_not_touch_another_documents_obligation()
    {
        // Redeclaring an id is the only way a document can reach another layer's
        // obligation at all - there is no edit operation to secure, which is a
        // better outcome than securing one.
        var composition = EnvelopeComposition.Compose(
            [Root("in-scope"), Narrow("flight", "in-scope")]);

        await Assert.That(composition.Composed).IsNull();
        await Assert.That(composition.Refused!).Contains("'in-scope'");
        await Assert.That(composition.Refused!).Contains("root");
        await Assert.That(composition.Refused!).Contains("flight")
            .Because("it names both documents, so somebody reading their own file is not "
                   + "sent looking for a mistake that is in another one's.");
    }

    [Test]
    public async Task Strengthening_is_adding_and_needs_no_primitive()
    {
        // If root says a machine check and a team wants a person as well, the
        // narrowing ADDS its own. Root's stays exactly as root wrote it, both
        // attach, and the stricter one binds because both must hold.
        var narrowing = new EnvelopeLayer
        {
            Role = Roles.Narrowing,
            Name = "flight",
            Parent = "root",
            Version = "flight-1",
            Narrowing = new EnvelopeNarrowing
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

        var composition = EnvelopeComposition.Compose([Root("contracts-intact"), narrowing]);

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
        // "two is not twenty" for the Engine on exactly this reasoning - and
        // ADR-0014 makes multiplicity real: any number of narrowings, order-free
        // because every operator below the work kind is a meet.
        var built = new List<EnvelopeLayer> { Root("obligation-0") };
        built.AddRange(Enumerable.Range(1, layers - 1)
            .Select(i => Narrow($"narrowing-{i}", $"obligation-{i}")));

        var forward = EnvelopeComposition.Compose(built);
        var backward = EnvelopeComposition.Compose([.. Enumerable.Reverse(built)]);

        await Assert.That(forward.Refused).IsEqualTo(backward.Refused);
        await Assert.That(forward.Composed!.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList())
            .IsEquivalentTo(backward.Composed!.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList())
            .Because("a caller must not be able to change what governs a flight by shuffling "
                   + "a list.");
    }

    [Test]
    public async Task Two_documents_claiming_one_name_are_refused()
    {
        var composition = EnvelopeComposition.Compose(
            [Root("a"), Narrow("flight", "b"), Narrow("flight", "c")]);

        await Assert.That(composition.Refused!).Contains("'flight'")
            .Because("which one governs would be decided by list order, and a list order is "
                   + "not an ownership model.");
    }

    // ---- provenance is derived ----

    [Test]
    public async Task Provenance_comes_from_where_the_document_sat()
    {
        var composition = EnvelopeComposition.Compose(
            [Root("in-scope"), Narrow("flight", "needs-a-person")]);

        var byId = composition.Composed!.Obligations.ToDictionary(o => o.Id);

        await Assert.That(byId["in-scope"].Provenance)
            .IsEqualTo(new ObligationProvenance { Role = Roles.Root, Name = "root" });
        await Assert.That(byId["needs-a-person"].Provenance)
            .IsEqualTo(new ObligationProvenance { Role = Roles.Narrowing, Name = "flight" });
    }

    [Test]
    public async Task A_document_claiming_a_provenance_it_is_not_at_does_not_get_it()
    {
        // UNFORGEABLE. The obligation says root; the document sat at a
        // narrowing; the composed obligation says (narrowing, flight). The
        // parser refuses the line outright, and this is the second lock: even
        // a document assembled in code cannot lie about where it came from,
        // because the composer never reads it.
        var lying = new EnvelopeLayer
        {
            Role = Roles.Narrowing,
            Name = "flight",
            Parent = "root",
            Version = "flight-1",
            Narrowing = new EnvelopeNarrowing
            {
                Obligations =
                [
                    new Obligation
                    {
                        Id = "mine",
                        Check = ObligationChecks.Machine,
                        Rule = ObligationPredicates.NoFileOutsideScope,
                        Provenance = ObligationProvenance.AtRoot,
                    },
                ],
            },
        };

        var composed = EnvelopeComposition.Compose([Root("in-scope"), lying]).Composed!;

        await Assert.That(composed.Obligations.Single(o => o.Id == "mine").Provenance)
            .IsEqualTo(new ObligationProvenance { Role = Roles.Narrowing, Name = "flight" })
            .Because("the thing being governed does not get to describe its own authority.");
    }

    // ---- what a pin has to be ----

    [Test]
    public async Task A_pinned_set_composes_the_same_way_after_both_layers_move()
    {
        // A PIN THAT RESOLVES TO "WHATEVER ROOT IS TODAY" IS NOT A PIN. What the
        // flight pins is a SET of (name, version) pairs, and the composed result
        // has to be reproducible from that set alone.
        var pinned = new[] { Root("in-scope"), Narrow("flight", "needs-a-person") };

        var before = EnvelopeComposition.Compose(pinned).Composed!;

        // Both documents move on. Nothing about the pinned set changed.
        _ = EnvelopeComposition.Compose(
            [Root("in-scope", "and-another"),
             Narrow("flight", "needs-a-person", "and-a-third")]);

        var after = EnvelopeComposition.Compose(pinned).Composed!;

        await Assert.That(after.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList())
            .IsEquivalentTo(before.Obligations.Select(o => $"{o.Provenance}:{o.Id}").ToList());
        await Assert.That(pinned.Select(p => p.Version).ToList())
            .IsEquivalentTo((string[])["root-1", "flight-1"])
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
