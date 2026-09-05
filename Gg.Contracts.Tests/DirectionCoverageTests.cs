namespace Gg.Contracts.Tests;

/// <summary>
/// Every composed field is actually read by the direction comparator, proved by
/// moving it and watching the comparator notice.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guard this replaces asserts a dictionary contains its own keys.</b>
/// <c>EnvelopeDirection.Rules</c> is assigned <i>from</i>
/// <c>EnvelopeComposition.Operators</c> in the static constructor, so
/// <c>Every_composed_field_has_a_direction_rule</c> compares that table against
/// itself. It passes for a field the comparator never looks at. What it does
/// check is real and narrower than it reads: that every operator VALUE has a
/// declared ordering.
/// </para>
/// <para>
/// <b>And a field slipping through is not hypothetical.</b>
/// <c>EnvelopeDirection</c> records it in its own source: <i>"`accepts:` was in
/// the operator table from the day it shipped and was never in this comparison,
/// so narrowing it computed tighter-or-equal and took no gate."</i> Found by a
/// step 0, in shipped code, while adding the second field that would have had
/// the same hole. The arms here are hand-written per field, so the next one is
/// one omission away.
/// </para>
/// <para>
/// <b>Why the table records the direction rather than deriving it.</b> The
/// operator does not settle which way widens: <c>work-kind-only</c> is
/// containment for <c>accepts:</c> and <c>produces:</c> - dropping is the
/// widening - and an unordered set move for <c>loops:</c> and
/// <c>destinations:</c>, where both directions widen because no order exists to
/// consult. Writing the expectation down per field is the decision this file
/// exists to force somebody to make.
/// </para>
/// </remarks>
public class DirectionCoverageTests
{
    /// <summary>
    /// One composed field, and the two documents that move it.
    /// </summary>
    /// <param name="Field">
    /// The <c>Type.Member</c> key, as <c>EnvelopeComposition.Operators</c>
    /// spells it.
    /// </param>
    /// <param name="Path">
    /// The text-form path fragment the comparator must name, so a field that is
    /// noticed for the wrong reason still fails.
    /// </param>
    /// <param name="Tighter">The document at or below the other.</param>
    /// <param name="Looser">The document that reaches further.</param>
    /// <param name="ReverseAlsoWidens">
    /// True where no order exists, so the move is a widening read either way.
    /// </param>
    private sealed record Move(
        string Field, string Path, Envelope Tighter, Envelope Looser, bool ReverseAlsoWidens);

    private static Envelope Doc() => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Environment = "dev",
        Repository = "payments",
        Accepts = [SubjectKinds.Repository],
        Produces = [FactKinds.ChangeManifest],
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
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m", Attempts = 3 },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "the-branch",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
                PreserveUnadmitted = false,
            },
        ],
    };

    private static Envelope WithLoop(Envelope doc, Func<Loop, Loop> change) =>
        doc with { Loops = [change(doc.Loops[0])] };

    private static Envelope WithDestination(Envelope doc, Func<Destination, Destination> change) =>
        doc with { Destinations = [change(doc.Destinations[0])] };

    /// <summary>A flight destination, because `opens:` is legal on no other kind.</summary>
    private static Envelope Opening(IReadOnlyList<string> opens) => Doc() with
    {
        Context = new ContextBinding { Scope = EnvelopeScopes.None, Constitution = "1.0.0" },
        Accepts = [],
        Produces = [],
        Destinations =
        [
            new Destination
            {
                Id = "open-the-flight",
                Kind = DestinationKinds.Flight,
                Requires = ["in-scope"],
                Opens = opens,
            },
        ],
    };

    private static IReadOnlyList<Move> Moves() =>
    [
        new("ContextBinding.Scope", "context.scope",
            Doc(),
            Doc() with { Context = new ContextBinding { Scope = "**", Constitution = "1.0.0" } },
            ReverseAlsoWidens: false),

        // NO ORDER EXISTS FOR A ROOT-ONLY SCALAR, so a move is a widening read
        // from either side. That is the comparator's own rule and not a gap.
        new("ContextBinding.Constitution", "context.constitution",
            Doc(),
            Doc() with { Context = new ContextBinding { Scope = "src/**", Constitution = "1.1.0" } },
            ReverseAlsoWidens: true),

        new("Envelope.Environment", "environment",
            Doc(), Doc() with { Environment = "prod" }, ReverseAlsoWidens: true),

        new("Envelope.Repository", "repository",
            Doc(), Doc() with { Repository = "ledger" }, ReverseAlsoWidens: true),

        // CONTAINMENT, NOT EQUALITY, and dropping is the widening: a subject
        // kind this work no longer takes makes every fact about it unproducible.
        new("Envelope.Accepts", "accepts",
            Doc(), Doc() with { Accepts = [] }, ReverseAlsoWidens: false),

        new("Envelope.Produces", "produces",
            Doc(), Doc() with { Produces = [] }, ReverseAlsoWidens: false),

        // Union: what was required stays required, so losing an obligation is
        // the widening.
        new("Envelope.Obligations", "obligations",
            Doc() with
            {
                Obligations =
                [
                    .. Doc().Obligations,
                    new Obligation
                    {
                        Id = "human-look", Check = ObligationChecks.Human, Approver = "lead",
                    },
                ],
            },
            Doc(),
            ReverseAlsoWidens: false),

        // WORK-KIND-ONLY SETS COMPARED BY ID, where a move is unordered: the
        // loop that governed is gone and a different one is in its place.
        new("Envelope.Loops", "loops",
            Doc(), WithLoop(Doc(), l => l with { Id = "verify" }), ReverseAlsoWidens: true),

        new("Envelope.Destinations", "destinations",
            Doc(), WithDestination(Doc(), d => d with { Id = "the-fork" }),
            ReverseAlsoWidens: true),

        new("Loop.Executor", "executor",
            Doc(), WithLoop(Doc(), l => l with { Executor = ExecutorRungs.Human, Moves = [] }),
            ReverseAlsoWidens: true),

        new("Loop.Moves", "moves",
            Doc(), WithLoop(Doc(), l => l with { Moves = [LoopMoves.Read, LoopMoves.Edit] }),
            ReverseAlsoWidens: false),

        new("Loop.OnExhaustion", "on-exhaustion",
            Doc(),
            WithLoop(Doc(), l => l with { OnExhaustion = ExhaustionPolicies.HandoffToAgent }),
            ReverseAlsoWidens: true),

        new("LoopBudget.WallClock", "wall-clock",
            Doc(),
            WithLoop(Doc(), l => l with { Budget = l.Budget with { WallClock = "60m" } }),
            ReverseAlsoWidens: false),

        new("LoopBudget.Attempts", "attempts",
            Doc(),
            WithLoop(Doc(), l => l with { Budget = l.Budget with { Attempts = 5 } }),
            ReverseAlsoWidens: false),

        new("Destination.Requires", "requires",
            WithDestination(Doc(), d => d with { Requires = ["in-scope", "human-look"] }) with
            {
                Obligations =
                [
                    .. Doc().Obligations,
                    new Obligation
                    {
                        Id = "human-look", Check = ObligationChecks.Human, Approver = "lead",
                    },
                ],
            },
            WithDestination(Doc(), d => d with { Requires = ["in-scope"] }) with
            {
                Obligations =
                [
                    .. Doc().Obligations,
                    new Obligation
                    {
                        Id = "human-look", Check = ObligationChecks.Human, Approver = "lead",
                    },
                ],
            },
            ReverseAlsoWidens: false),

        new("Destination.PreserveUnadmitted", "preserve-unadmitted",
            Doc(), WithDestination(Doc(), d => d with { PreserveUnadmitted = true }),
            ReverseAlsoWidens: false),

        new("Destination.Opens", "opens",
            Opening(["research"]), Opening(["implement", "research"]),
            ReverseAlsoWidens: false),
    ];

    /// <summary>
    /// Fields no <c>Envelope</c> pair can move, with the reason.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Elsewhere =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // A different type and a different overload, exercised by its own
            // test below rather than exempted out of the sweep.
            ["EnvelopeNarrowing.Obligations"] =
                "on the narrowing overload, covered by A_narrowing_obligation_lost_is_a_widening",

            // NOT "nobody can move it" - a pair moves it easily. The comparator
            // deliberately reads it and answers "never a widening", which a
            // Moves row cannot express because every row asserts a widening.
            // Covered by four assertions rather than exempted out of sight.
            ["Envelope.Instructions"] =
                "never a widening in either direction, covered by "
              + "EnvelopeDirectionTests.Adding_an_instruction_is_not_a_widening and its three "
              + "neighbours - text an agent reads moves no bound",
        };

    [Test]
    public async Task Every_composed_field_is_moved_by_this_suite()
    {
        // THE COVERAGE HALF, and unlike the guard it replaces it cannot be
        // satisfied by the table it is checking: a field here must be moved by
        // a real pair of documents below, or named in Elsewhere with a reason.
        var moved = Moves().Select(m => m.Field).ToList();

        var uncovered = EnvelopeComposition.Operators.Keys
            .Where(f => !moved.Contains(f, StringComparer.Ordinal))
            .Where(f => !Elsewhere.ContainsKey(f))
            .ToList();

        await Assert.That(uncovered).IsEmpty()
            .Because("a composed field nobody moves is a field the comparator may not read, "
                   + "which is how `accepts:` shipped able to be narrowed with no gate. "
                   + "Found: " + string.Join(", ", uncovered));
    }

    [Test]
    public async Task Nothing_in_the_table_names_a_field_that_no_longer_composes()
    {
        // THE OTHER DIRECTION. A row for a retired field is a row that passes
        // while covering nothing, and it makes the count above look larger than
        // the schema.
        var schema = EnvelopeComposition.Operators.Keys;

        foreach (var field in Moves().Select(m => m.Field).Concat(Elsewhere.Keys))
        {
            await Assert.That(schema).Contains(field)
                .Because($"'{field}' is not a composed field, so moving it proves nothing.");
        }
    }

    [Test]
    public async Task Moving_a_composed_field_is_noticed_and_named()
    {
        foreach (var move in Moves())
        {
            var widening = EnvelopeDirection.Widening(move.Tighter, move.Looser);

            await Assert.That(widening).IsNotNull()
                .Because($"'{move.Field}' composes by "
                       + $"'{EnvelopeComposition.Operators[move.Field]}' and the looser document "
                       + "reaches further, so the comparator has to say so. Silence here is a "
                       + "governance change that takes no gate.");
            await Assert.That(widening!.Field).Contains(move.Path)
                .Because($"'{move.Field}' must be noticed for its own sake rather than because "
                       + "some other field moved with it - the field names "
                       + $"'{widening.Field}'.");
        }
    }

    [Test]
    public async Task A_tightening_is_not_reported_as_a_widening()
    {
        // THE TWIN. Without it, a comparator that answered "widening" for every
        // inequality would pass the sweep above completely.
        foreach (var move in Moves().Where(m => !m.ReverseAlsoWidens))
        {
            await Assert.That(EnvelopeDirection.Widening(move.Looser, move.Tighter)).IsNull()
                .Because($"'{move.Field}' has an order, and this is the direction that "
                       + "tightens. A comparator that gated it would make every narrowing "
                       + "need an approval.");
        }
    }

    [Test]
    public async Task An_unordered_move_is_a_widening_read_from_either_side()
    {
        // And the rows that say so, asserted rather than left as a comment: for
        // a root-only scalar or an unordered set move there is no order to
        // consult, so both directions answer widening. That is the comparator's
        // rule, and a row claiming it must be true of the actual pair.
        foreach (var move in Moves().Where(m => m.ReverseAlsoWidens))
        {
            await Assert.That(EnvelopeDirection.Widening(move.Looser, move.Tighter)).IsNotNull()
                .Because($"'{move.Field}' declares no order, so a move is reach whichever side "
                       + "it is read from.");
        }
    }

    [Test]
    public async Task A_narrowing_obligation_lost_is_a_widening()
    {
        // The second overload, so the one field that cannot ride an Envelope
        // pair is covered rather than excused.
        var applied = new EnvelopeNarrowing
        {
            Obligations =
            [
                new Obligation
                {
                    Id = "human-look", Check = ObligationChecks.Human, Approver = "lead",
                },
            ],
        };

        var proposed = new EnvelopeNarrowing { Obligations = [] };

        var widening = EnvelopeDirection.Widening(applied, proposed);

        await Assert.That(widening).IsNotNull()
            .Because("obligations union: a gate this narrowing declared and no longer does is "
                   + "a rule removed from every flight it reached.");
        await Assert.That(widening!.Field).Contains("obligations");

        await Assert.That(EnvelopeDirection.Widening(proposed, applied)).IsNull()
            .Because("and adding one is the tightening it is.");
    }

    [Test]
    public async Task Every_field_in_the_table_is_moved_by_a_pair_that_actually_differs()
    {
        // THE POISON TWIN FOR THE FIXTURE RATHER THAN THE CODE. Two identical
        // documents answer null, so a row whose pair was accidentally the same
        // document would fail the sweep for the right reason - but a row whose
        // pair differs in some OTHER field would pass it for the wrong one.
        // This asserts the base is stable, which is what makes the path
        // assertion above meaningful.
        await Assert.That(EnvelopeDirection.Widening(Doc(), Doc())).IsNull();
        await Assert.That(EnvelopeDirection.Widening(
            Opening(["research"]), Opening(["research"]))).IsNull();
    }
}
