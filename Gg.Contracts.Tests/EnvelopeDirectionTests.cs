namespace Gg.Contracts.Tests;

/// <summary>
/// Direction is a comparison, it is total, and it has two answers.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0016 § 6: every envelope field carries a merge operator and every
/// operator is a meet, so documents sit in a partial order — a proposed
/// version either sits <b>at or below</b> the applied one, or it does not.
/// A comparator returning three answers — tighter, looser, incomparable —
/// invites a caller to treat <i>incomparable</i> as <i>nothing changed</i>,
/// which is how a constitution bump walks through the gate a floor exists to
/// hold. So this one returns two: null, or a widening naming the first field
/// that could not be shown to tighten.
/// </para>
/// <para>
/// <b>This does not reopen 0.31.0.</b> General widening detection over
/// predicates stays undecidable and undecided: the comparator never reads a
/// predicate's meaning, it reads the operator table — finite, per-field data
/// — and wherever the table declares no order, it does not decide; it
/// answers widening. The undecidable region is mapped to the refusal-shaped
/// constant, not solved.
/// </para>
/// </remarks>
public class EnvelopeDirectionTests
{
    private static Envelope Doc(
        string scope = "src/**",
        string constitution = "1.0.0",
        string? environment = null,
        string[]? moves = null,
        string wallClock = "30m",
        int? attempts = null,
        string? when = null,
        string[]? evidence = null,
        string[]? requires = null,
        bool? preserveUnadmitted = null,
        string destinationKind = DestinationKinds.PullRequest,
        string executor = ExecutorRungs.Frontier,
        string[]? extraObligations = null)
    {
        string[] ids = ["in-scope", .. extraObligations ?? []];

        return new Envelope
        {
            Context = new ContextBinding { Scope = scope, Constitution = constitution },
            Environments = environment is null ? null : [environment],
            Obligations =
            [
                new Obligation
                {
                    Id = "in-scope",
                    Check = ObligationChecks.Machine,
                    Rule = ObligationPredicates.NoFileOutsideScope,
                    When = when,
                    Evidence = evidence ?? [],
                },
                .. (extraObligations ?? []).Select(id => new Obligation
                {
                    Id = id,
                    Check = ObligationChecks.Human,
                    Approver = "lead",
                }),
            ],
            Loops =
            [
                new Loop
                {
                    Id = "implement",
                    Executor = executor,
                    Discharges = ["in-scope"],
                    Moves = moves ?? [LoopMoves.Edit, LoopMoves.Read],
                    Budget = new LoopBudget { WallClock = wallClock, Attempts = attempts },
                    OnExhaustion = ExhaustionPolicies.HandoffToHuman,
                },
            ],
            Destinations =
            [
                new Destination
                {
                    Id = "pull-request",
                    Kind = destinationKind,
                    Requires = requires ?? ["in-scope"],
                    PreserveUnadmitted = preserveUnadmitted,
                },
            ],
        };
    }

    // ---- the two answers ----

    [Test]
    public async Task Two_identical_documents_are_tighter_or_equal()
    {
        // The twin that keeps the widening answer honest: a comparator that
        // widens on any inequality it cannot read would fail here.
        await Assert.That(EnvelopeDirection.Widening(Doc(), Doc())).IsNull();
    }

    [Test]
    public async Task A_document_that_only_constrains_is_shown_to_sit_at_or_below()
    {
        await Assert.That(EnvelopeDirection.Widening(
            Doc(scope: "src/**"), Doc(scope: "src/payments/**"))).IsNull();
        await Assert.That(EnvelopeDirection.Widening(
            Doc(moves: [LoopMoves.Edit, LoopMoves.Read]), Doc(moves: [LoopMoves.Read]))).IsNull();
        await Assert.That(EnvelopeDirection.Widening(
            Doc(wallClock: "30m"), Doc(wallClock: "10m"))).IsNull();
        await Assert.That(EnvelopeDirection.Widening(
            Doc(attempts: null), Doc(attempts: 3))).IsNull()
            .Because("bounding the unbounded is the tightest kind of tightening.");
        await Assert.That(EnvelopeDirection.Widening(
            Doc(), Doc(extraObligations: ["pci-review"]))).IsNull()
            .Because("adding an obligation constrains you further, and is safe for anyone "
                   + "responsible for the work - 0.31.0's add-only rule restated as direction.");
        await Assert.That(EnvelopeDirection.Widening(
            Doc(preserveUnadmitted: true), Doc(preserveUnadmitted: false))).IsNull();
        await Assert.That(EnvelopeDirection.Widening(
            Doc(preserveUnadmitted: true), Doc(preserveUnadmitted: null))).IsNull()
            .Because("null and false are one answer on this member, and both are the tight end.");
    }

    // ---- widenings, each naming the field and both values ----

    [Test]
    public async Task A_widened_scope_is_refused_naming_the_field_and_both_values()
    {
        var widening = EnvelopeDirection.Widening(
            Doc(scope: "src/payments/**"), Doc(scope: "src/**"));

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("context.scope");
        await Assert.That(widening.Because).Contains("src/payments/**");
        await Assert.That(widening.Because).Contains("src/**");
    }

    [Test]
    public async Task Incomparable_scopes_are_a_widening_because_nothing_shows_them_tighter()
    {
        var widening = EnvelopeDirection.Widening(
            Doc(scope: "src/**"), Doc(scope: "docs/**"));

        await Assert.That(widening).IsNotNull()
            .Because("neither contains the other, so at-or-below cannot be shown - and what "
                   + "cannot be shown to tighten is a widening, never a shrug.");
        await Assert.That(widening!.Field).IsEqualTo("context.scope");
    }

    [Test]
    public async Task A_constitution_change_is_widening_not_unchanged()
    {
        // THE POISON TWIN. context.constitution is root-only - unordered by
        // strictness - and the first time somebody bumps it and gets a gate,
        // the request will be to treat unordered fields as neutral. This test
        // is what makes that fail a build rather than a review.
        var widening = EnvelopeDirection.Widening(
            Doc(constitution: "1.0.0"), Doc(constitution: "2.0.0"));

        await Assert.That(widening).IsNotNull()
            .Because("frontier-to-human and 1.0.0-to-2.0.0 are not ordered by strictness; an "
                   + "unordered move must take the widening path, not fall through as unchanged.");
        await Assert.That(widening!.Field).IsEqualTo("context.constitution");
        await Assert.That(widening.Because).Contains("1.0.0");
        await Assert.That(widening.Because).Contains("2.0.0");
    }

    [Test]
    public async Task The_unordered_family_all_widens()
    {
        await Assert.That(EnvelopeDirection.Widening(
                Doc(executor: ExecutorRungs.Frontier), Doc(executor: ExecutorRungs.Human))!.Field)
            .IsEqualTo("loops.implement.executor");
        // THE ENVIRONMENT CASE MOVED OUT OF THIS FAMILY, and the reason is a
        // semantic change rather than a correction. It used to hold one name and
        // be a SELECTION - what the flights are about - so a name where there
        // was none had no declared order and the conservative answer was
        // widening. It is now a BOUND: a set of environments flights may be
        // about, checked by membership. Writing a bound down where there was
        // none is strictly more restrictive, so it narrows, and calling it a
        // widening would demand a gate for the act of restricting something.
        // Asserted in A_bound_written_down_narrows_and_a_bound_withdrawn_widens.
        await Assert.That(EnvelopeDirection.Widening(
                Doc(destinationKind: DestinationKinds.PullRequest),
                Doc(destinationKind: DestinationKinds.EnvelopeChange))!.Field)
            .IsEqualTo("destinations.pull-request.kind");
    }

    [Test]
    public async Task Loosened_budgets_and_added_moves_widen()
    {
        await Assert.That(EnvelopeDirection.Widening(
                Doc(wallClock: "10m"), Doc(wallClock: "30m"))!.Field)
            .IsEqualTo("loops.implement.budget.wall-clock");
        await Assert.That(EnvelopeDirection.Widening(
                Doc(attempts: 3), Doc(attempts: null))!.Field)
            .IsEqualTo("loops.implement.budget.attempts")
            .Because("removing a bound is the loosening that looks like tidying up.");
        await Assert.That(EnvelopeDirection.Widening(
                Doc(moves: [LoopMoves.Read]), Doc(moves: [LoopMoves.Read, LoopMoves.Edit]))!.Field)
            .IsEqualTo("loops.implement.moves");
    }

    [Test]
    public async Task A_removed_obligation_is_a_widening_naming_it()
    {
        var widening = EnvelopeDirection.Widening(
            Doc(extraObligations: ["pci-review"]), Doc());

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("obligations");
        await Assert.That(widening.Because).Contains("pci-review")
            .Because("the beneficiary owns removal, and the refusal names what was removed.");
    }

    [Test]
    public async Task A_changed_obligation_body_is_a_widening_because_no_order_exists_over_it()
    {
        // The Obligation-members hole made explicit: no operator and no order
        // exists over an obligation's members, so a changed rule is a
        // DIFFERENT gate, not a tighter one. Equality is total where ordering
        // is not.
        var widening = EnvelopeDirection.Widening(
            Doc(when: null), Doc(when: "change.manifest touches migrations/**"));

        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("obligations.in-scope.when");

        var evidence = EnvelopeDirection.Widening(
            Doc(evidence: []), Doc(evidence: ["change-manifest"]));
        await Assert.That(evidence).IsNotNull()
            .Because("even an ADDED evidence item is a changed body: relaxing this later is a "
                   + "deliberate contract version, never a fall-through.");
        await Assert.That(evidence!.Field).IsEqualTo("obligations.in-scope.evidence");
    }

    [Test]
    public async Task A_removed_require_and_a_new_loop_widen()
    {
        await Assert.That(EnvelopeDirection.Widening(
                Doc(requires: ["in-scope"]), Doc(requires: []))!.Field)
            .IsEqualTo("destinations.pull-request.requires");

        var applied = Doc();
        var proposed = Doc() with
        {
            Loops =
            [
                .. Doc().Loops,
                new Loop
                {
                    Id = "second",
                    Executor = ExecutorRungs.Frontier,
                    Discharges = [],
                    Moves = [LoopMoves.Read],
                    Budget = new LoopBudget { WallClock = "5m" },
                    OnExhaustion = ExhaustionPolicies.HandoffToHuman,
                },
            ],
        };
        await Assert.That(EnvelopeDirection.Widening(applied, proposed)!.Field)
            .IsEqualTo("loops")
            .Because("a new loop is new machinery on its face - reach that did not exist.");
    }

    [Test]
    public async Task Granting_reach_through_preserve_unadmitted_widens()
    {
        var widening = EnvelopeDirection.Widening(
            Doc(preserveUnadmitted: null), Doc(preserveUnadmitted: true));

        await Assert.That(widening).IsNotNull()
            .Because("true means unadmitted work leaves the machine for a fetchable remote - "
                   + "reach, exactly what and composes away.");
        await Assert.That(widening!.Field)
            .IsEqualTo("destinations.pull-request.preserve-unadmitted");
    }

    [Test]
    public async Task Adding_an_obligation_together_with_its_own_discharge_is_the_tightening_it_is()
    {
        // FOUND BY THE CONSUMER'S SUITE: Validate requires every obligation
        // be discharged, so adding one FORCES adding its discharge - and a
        // rule that widens on any discharges change makes "add an obligation
        // through a full document" impossible, which the flight layer does
        // as its ordinary day.
        var applied = Doc();
        var proposed = Doc(extraObligations: ["and-more"]);
        proposed = proposed with
        {
            Loops = [proposed.Loops[0] with { Discharges = ["in-scope", "and-more"] }],
        };

        await Assert.That(EnvelopeDirection.Widening(applied, proposed)).IsNull()
            .Because("the discharge is part of the obligation it discharges; refusing the "
                   + "pair would refuse the tightening this comparator exists to wave through.");
    }

    [Test]
    public async Task A_discharge_gained_for_a_pre_existing_obligation_still_widens()
    {
        // The twin that keeps the refinement honest: rewiring who answers an
        // obligation that already existed has no declared order.
        var applied = Doc(extraObligations: ["and-more"]);
        var proposed = applied with
        {
            Loops = [applied.Loops[0] with { Discharges = ["in-scope", "and-more"] }],
        };

        var widening = EnvelopeDirection.Widening(applied, proposed);
        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("loops.implement.discharges");
    }

    // ---- the narrowing shape ----

    [Test]
    public async Task A_narrowing_that_adds_holds_and_one_that_removes_widens()
    {
        EnvelopeNarrowing At(params string[] ids) => new()
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
        };

        await Assert.That(EnvelopeDirection.Widening(At("pci"), At("pci", "sox"))).IsNull();
        var widening = EnvelopeDirection.Widening(At("pci", "sox"), At("pci"));
        await Assert.That(widening).IsNotNull();
        await Assert.That(widening!.Field).IsEqualTo("obligations");
        await Assert.That(widening.Because).Contains("sox");
    }

    // ---- totality ----

    [Test]
    public async Task Every_composed_field_has_a_direction_rule()
    {
        // The drift guard, a third time: a new [Composes] field fails here
        // (and the comparator's own constructor) until somebody decides its
        // direction, and a new operator value fails until the comparator
        // learns to order it.
        foreach (var (field, op) in EnvelopeComposition.Operators)
        {
            await Assert.That(EnvelopeDirection.Rules.ContainsKey(field)).IsTrue()
                .Because($"'{field}' composes by '{op}' and no direction rule covers it - a "
                       + "comparison over a table with a hole is a confident answer about a "
                       + "document whose one undecidable field is where somebody would hide "
                       + "a widening.");
        }

        await Assert.That(EnvelopeDirection.Rules.Keys.ToList())
            .IsEquivalentTo(EnvelopeComposition.Operators.Keys.ToList())
            .Because("and nothing beyond the operator table either: the comparator answers "
                   + "for exactly the fields composition owns.");
    }

    /// <summary>
    /// Instructions are never a widening, in either direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>S30.1-06, and it is an explicit answer rather than a default.</b> The
    /// comparator's default for an operator it does not know is <i>widening</i>
    /// — <c>EnvelopeDirection</c>'s static constructor throws rather than
    /// guess. So `append` had to be taught, and this is the assertion that the
    /// answer taught was the intended one.
    /// </para>
    /// <para>
    /// <b>Why it can be "never".</b> Instructions are text an agent READS, and
    /// reading changes no bound: they cannot enlarge a scope, grant a move,
    /// raise a budget or add a destination. Rule 5 makes that a disposition the
    /// prompt states, and the manifest check is what decides. So an envelope
    /// that adds, edits or removes a block sits at-or-below the one before it
    /// on every governed quantity.
    /// </para>
    /// <para>
    /// <b>What it costs, said out loud:</b> standing guidance changes without
    /// the widening gate. That is the latitude a tenant already has over a
    /// flight's intent and a rejection reason — and unlike those two, this text
    /// is reviewed, versioned and attributed.
    /// </para>
    /// </remarks>
    [Test]
    public async Task Adding_an_instruction_is_not_a_widening()
    {
        var without = Doc();
        var with = Doc() with
        {
            Instructions = [new EnvelopeInstruction { Text = "read the ADRs first" }],
        };

        await Assert.That(EnvelopeDirection.Widening(without, with)).IsNull();
    }

    [Test]
    public async Task Removing_an_instruction_is_not_a_widening_either()
    {
        // BOTH DIRECTIONS, because the unordered scalars widen either way and
        // it would be easy to assume text behaves like them. It does not: an
        // instruction removed takes no bound with it.
        var with = Doc() with
        {
            Instructions = [new EnvelopeInstruction { Text = "read the ADRs first" }],
        };

        await Assert.That(EnvelopeDirection.Widening(with, Doc())).IsNull();
    }

    [Test]
    public async Task Rewording_an_instruction_is_not_a_widening()
    {
        var was = Doc() with { Instructions = [new EnvelopeInstruction { Text = "be careful" }] };
        var now = Doc() with { Instructions = [new EnvelopeInstruction { Text = "be bold" }] };

        await Assert.That(EnvelopeDirection.Widening(was, now)).IsNull()
            .Because("a wording change that demanded a widening gate would make the gate "
                   + "meaningless by making it routine.");
    }

    [Test]
    public async Task The_comparator_has_an_ordering_for_append_rather_than_a_default()
    {
        // THE DRIFT GUARD FIRED WHEN THIS FIELD LANDED, which is the mechanism
        // working: EnvelopeDirection's static constructor throws for an
        // operator with no declared ordering. This asserts the answer exists
        // rather than that the type merely loaded.
        await Assert.That(EnvelopeDirection.Rules["Envelope.Instructions"])
            .IsEqualTo(MergeOperators.Append);
    }

    [Test]
    public async Task Declaring_a_bound_and_withdrawing_one_are_both_new_reach()
    {
        // Null is unbounded. So the direction is asymmetric, which is the whole
        // difference between a bound and the single selection this used to be.
        await Assert.That(EnvelopeDirection.Widening(
                Doc(environment: null), Doc(environment: "aspire-payments"))!.Field)
            .IsEqualTo("environments")
            .Because("selecting a name where none was selected is NEW REACH - the tenant's "
                   + "flights can run somewhere they could not and compile a label they did "
                   + "not. I briefly made this a narrowing on the reasoning that a bound "
                   + "restricts; the airspace walk says otherwise in those words, and it is "
                   + "right: what a reviewer needs to see is work reaching a new place.");

        await Assert.That(EnvelopeDirection.Widening(
                Doc(environment: "aspire-payments"), Doc(environment: null))!.Field)
            .IsEqualTo("environments")
            .Because("afterwards nothing is refused, which is more than was permitted before.");
    }

    [Test]
    public async Task Adding_a_permitted_environment_widens_and_removing_one_does_not()
    {
        var one = Doc(environment: "dev");
        var two = one with { Environments = ["dev", "production"] };

        await Assert.That(EnvelopeDirection.Widening(one, two)!.Field)
            .IsEqualTo("environments")
            .Because("somewhere work may newly run is what the bound exists to decide.");
        await Assert.That(EnvelopeDirection.Widening(two, one)).IsNull();
    }
}
