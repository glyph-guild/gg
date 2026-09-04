namespace Gg.Contracts;

/// <summary>
/// The first field that could not be shown to tighten, and why.
/// </summary>
/// <remarks>
/// A library answer, like a <c>Validate</c> diagnosis: the field is data so a
/// refusal can carry it as a parameter rather than re-parsing a sentence, and
/// the sentence names both values so the author knows what moved.
/// </remarks>
[PinnedId("7c1f4b02-9a3e-4d15-b8c6-2e50a1f7d9b4")]
public sealed record EnvelopeWidening
{
    /// <summary>The canonical text-form path, e.g. <c>context.scope</c>.</summary>
    public required string Field { get; init; }

    /// <summary>The sentence, naming both values.</summary>
    public required string Because { get; init; }
}

/// <summary>
/// Direction of change, computed from the merge operators. Two answers, total.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0016 § 6: every field carries a merge operator and every operator is a
/// meet, so documents sit in a partial order — a proposed version either sits
/// <b>at or below</b> the applied one, or it does not. Null means shown to
/// tighten (or identical); anything else is a widening naming the first field
/// that could not be shown to tighten. There is no third answer, because a
/// caller handed <i>incomparable</i> will argue it into <i>unchanged</i>,
/// which is how a constitution bump walks through the gate a floor exists to
/// hold. Article XI, in the shape it always takes here: undecidable halts
/// with a diagnosis, never evaluates to the permissive value.
/// </para>
/// <para>
/// <b>This does not reopen 0.31.0's undecidability ruling.</b> The comparator
/// never reads a predicate's meaning; it reads the operator table — finite,
/// per-field data — and wherever the table declares no order (a root-only
/// scalar, an obligation's body, an id-set change) it does not decide, it
/// answers widening. The undecidable region is mapped to the refusal-shaped
/// constant, not solved.
/// </para>
/// <para>
/// <b>One document against its own predecessor, never the chain.</b> Every
/// declared operator is a meet, a union guard or an equality guard, and meets
/// are monotone: if one layer's successor sits pointwise at-or-below its
/// predecessor, the composed meet cannot rise. The moves meets do not cover
/// are exactly the moves this comparator answers widening for, and
/// composition already refuses cross-layer violations. A first version has no
/// predecessor and therefore no direction — whether it needs a gate is the
/// caller's policy, and a comparator that invented an empty predecessor would
/// be answering a policy question in a math costume.
/// </para>
/// </remarks>
public static class EnvelopeDirection
{
    /// <summary>
    /// Field path to direction rule — the operator, drift-guarded both ways.
    /// </summary>
    /// <remarks>
    /// The same keys as <see cref="EnvelopeComposition.Operators"/>, checked
    /// in the static constructor: a new composed field fails the build until
    /// somebody decides its direction, and a new operator value fails until
    /// this class learns to order it. The drift guard, a third time.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Rules { get; }

    private static readonly IReadOnlySet<string> Ordered = new HashSet<string>(StringComparer.Ordinal)
    {
        MergeOperators.RootOnly,
        MergeOperators.WorkKindOnly,
        MergeOperators.Intersect,
        MergeOperators.Min,
        MergeOperators.Union,
        MergeOperators.And,
    };

    static EnvelopeDirection()
    {
        foreach (var (field, op) in EnvelopeComposition.Operators)
        {
            if (!Ordered.Contains(op))
            {
                throw new InvalidOperationException(
                    $"{field} composes by '{op}', and this comparator declares no ordering for "
                  + "it. A direction nobody decided would compose by accident - teach the "
                  + "comparator the operator's order, or the answer for the field is widening.");
            }
        }

        Rules = EnvelopeComposition.Operators;
    }

    /// <summary>Null when proposed is shown to sit at-or-below applied; otherwise the first widening.</summary>
    public static EnvelopeWidening? Widening(Envelope applied, Envelope proposed)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(proposed);

        // context.scope: intersect - equal, or contained by what was allowed.
        if (!string.Equals(applied.Context.Scope, proposed.Context.Scope, StringComparison.Ordinal)
            && !EnvelopeComposition.ScopeContains(applied.Context.Scope, proposed.Context.Scope))
        {
            return Widen("context.scope",
                $"'{proposed.Context.Scope}' is not contained by '{applied.Context.Scope}', and "
              + "scope intersects: it can only ever narrow. What cannot be shown to tighten "
              + "is a widening.");
        }

        // The unordered scalars: equal, or widening. No order exists to consult.
        if (Moved("context.constitution", applied.Context.Constitution, proposed.Context.Constitution) is { } constitution)
        {
            return constitution;
        }

        if (Moved("environment", applied.Environment, proposed.Environment) is { } environment)
        {
            return environment;
        }

        if (Moved("repository", applied.Repository, proposed.Repository) is { } repository)
        {
            return repository;
        }

        // WHAT THE KIND TAKES AND WHAT IT YIELDS. Both are work-kind-only sets
        // whose REDUCTION removes obligations, so both compare by containment
        // rather than by equality: keeping a subject kind or a fact family is
        // the tightening direction, dropping one is not.
        //
        // `accepts:` was in the operator table from the day it shipped and was
        // never in this comparison, so narrowing it computed tighter-or-equal
        // and took no gate. Found by slice seventeen's step 0, in shipped code,
        // while adding the second field that would have had the same hole.
        if (Declared("accepts", applied.Accepts, proposed.Accepts,
                "a subject kind this work no longer takes is every fact about that subject "
              + "becoming unproducible, which removes every rule that reads one") is { } accepts)
        {
            return accepts;
        }

        if (Declared("produces", applied.Produces, proposed.Produces,
                "a fact family this kind no longer claims to produce makes every rule reading "
              + "it structurally inapplicable, for every flight of this kind, for ever") is { } produces)
        {
            return produces;
        }

        if (Obligations("obligations", applied.Obligations, proposed.Obligations) is { } obligations)
        {
            return obligations;
        }

        var newObligations = proposed.Obligations.Select(o => o.Id)
            .Except(applied.Obligations.Select(o => o.Id), StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (Loops(applied.Loops, proposed.Loops, newObligations) is { } loops)
        {
            return loops;
        }

        return Destinations(applied.Destinations, proposed.Destinations);
    }

    /// <summary>The narrowing shape carries only obligations, and they compare the same way.</summary>
    public static EnvelopeWidening? Widening(EnvelopeNarrowing applied, EnvelopeNarrowing proposed)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(proposed);

        return Obligations("obligations", applied.Obligations, proposed.Obligations);
    }

    // ---- the per-operator orderings ----

    private static EnvelopeWidening? Obligations(
        string path, IReadOnlyList<Obligation> applied, IReadOnlyList<Obligation> proposed)
    {
        // union: additions tighten (to require more, add your own - both
        // attach, both must hold); a removal is the beneficiary's to ask for,
        // through the gate.
        foreach (var was in applied)
        {
            var now = proposed.FirstOrDefault(o => string.Equals(o.Id, was.Id, StringComparison.Ordinal));
            if (now is null)
            {
                return Widen(path,
                    $"obligation '{was.Id}' was removed, and obligations union: adding "
                  + "constrains anyone, and the beneficiary owns removal.");
            }

            // A changed body is a DIFFERENT gate, not a tighter one: no
            // operator and no order exists over an obligation's members, so
            // equality is the only total answer. Provenance is excluded - the
            // composer assigns it and the parser refuses it authored.
            if (BodyMoved(path, was, now) is { } moved)
            {
                return moved;
            }
        }

        return null;
    }

    private static EnvelopeWidening? BodyMoved(string path, Obligation was, Obligation now)
    {
        if (!string.Equals(was.Check, now.Check, StringComparison.Ordinal))
        {
            return Widen($"{path}.{was.Id}.check", Changed(was.Id, "check", was.Check, now.Check));
        }

        if (!string.Equals(was.Rule, now.Rule, StringComparison.Ordinal))
        {
            return Widen($"{path}.{was.Id}.rule", Changed(was.Id, "rule", was.Rule, now.Rule));
        }

        if (!string.Equals(was.When, now.When, StringComparison.Ordinal))
        {
            return Widen($"{path}.{was.Id}.when", Changed(was.Id, "when", was.When, now.When));
        }

        if (!string.Equals(was.Approver, now.Approver, StringComparison.Ordinal))
        {
            return Widen($"{path}.{was.Id}.approver", Changed(was.Id, "approver", was.Approver, now.Approver));
        }

        if (!was.Evidence.SequenceEqual(now.Evidence, StringComparer.Ordinal))
        {
            return Widen($"{path}.{was.Id}.evidence",
                $"obligation '{was.Id}' changed its evidence from "
              + $"[{string.Join(", ", was.Evidence)}] to [{string.Join(", ", now.Evidence)}] - a "
              + "changed body is a different gate, not a tighter one; equality is the only "
              + "total answer over members no operator orders.");
        }

        return null;
    }

    private static EnvelopeWidening? Loops(
        IReadOnlyList<Loop> applied, IReadOnlyList<Loop> proposed, IReadOnlySet<string> newObligations)
    {
        if (IdSetMoved("loops", applied.Select(l => l.Id), proposed.Select(l => l.Id)) is { } set)
        {
            return set;
        }

        foreach (var was in applied)
        {
            var now = proposed.First(l => string.Equals(l.Id, was.Id, StringComparison.Ordinal));
            var at = $"loops.{was.Id}";

            if (Moved($"{at}.executor", was.Executor, now.Executor) is { } executor)
            {
                return executor;
            }

            if (Moved($"{at}.on-exhaustion", was.OnExhaustion, now.OnExhaustion) is { } exhaustion)
            {
                return exhaustion;
            }

            // moves: intersect - a subset of what was allowed.
            var added = now.Moves.Except(was.Moves, StringComparer.Ordinal).ToList();
            if (added.Count > 0)
            {
                return Widen($"{at}.moves",
                    $"move '{added[0]}' was not allowed before, and moves intersect: they can "
                  + "only ever narrow.");
            }

            // discharges: intra-document wiring. A discharge gained for an
            // obligation NEW in this same document rides its tightening -
            // Validate requires every obligation be discharged, so the pair
            // arrives together or not at all. Anything else - a removal, or a
            // discharge gained for a pre-existing obligation - rewires who
            // answers an existing gate, which has no declared direction.
            var lostDischarges = was.Discharges.Except(now.Discharges, StringComparer.Ordinal).ToList();
            var gainedDischarges = now.Discharges.Except(was.Discharges, StringComparer.Ordinal)
                .Where(id => !newObligations.Contains(id))
                .ToList();
            if (lostDischarges.Count > 0 || gainedDischarges.Count > 0)
            {
                return Widen($"{at}.discharges",
                    $"loop '{was.Id}' rewired its discharges from "
                  + $"[{string.Join(", ", was.Discharges)}] to [{string.Join(", ", now.Discharges)}] "
                  + "beyond the obligations this same change adds, and no order exists over "
                  + "who answers an existing gate.");
            }

            // budget.wall-clock: min. Unparsable is widening - the comparator
            // never guesses, even where Validate should have refused earlier.
            if (!EnvelopeDurations.TryParse(was.Budget.WallClock, out var wasClock)
                || !EnvelopeDurations.TryParse(now.Budget.WallClock, out var nowClock)
                || nowClock > wasClock)
            {
                if (!string.Equals(was.Budget.WallClock, now.Budget.WallClock, StringComparison.Ordinal))
                {
                    return Widen($"{at}.budget.wall-clock",
                        $"'{now.Budget.WallClock}' is not at or below '{was.Budget.WallClock}', "
                      + "and wall-clock is min: the tightest budget wins.");
                }
            }

            // budget.attempts: min, with null the unbounded top.
            var wasAttempts = was.Budget.Attempts;
            var nowAttempts = now.Budget.Attempts;
            if (wasAttempts is { } bound && (nowAttempts is null || nowAttempts > bound))
            {
                return Widen($"{at}.budget.attempts",
                    $"'{Describe(nowAttempts)}' is not at or below '{bound}', and attempts is "
                  + "min - removing a bound is a loosening, however much it looks like tidying.");
            }
        }

        return null;
    }

    private static EnvelopeWidening? Destinations(
        IReadOnlyList<Destination> applied, IReadOnlyList<Destination> proposed)
    {
        if (IdSetMoved("destinations", applied.Select(d => d.Id), proposed.Select(d => d.Id)) is { } set)
        {
            return set;
        }

        foreach (var was in applied)
        {
            var now = proposed.First(d => string.Equals(d.Id, was.Id, StringComparison.Ordinal));
            var at = $"destinations.{was.Id}";

            if (Moved($"{at}.kind", was.Kind, now.Kind) is { } kind)
            {
                return kind;
            }

            // requires: union - what was required stays required.
            var dropped = was.Requires.Except(now.Requires, StringComparer.Ordinal).ToList();
            if (dropped.Count > 0)
            {
                return Widen($"{at}.requires",
                    $"'{dropped[0]}' was required to land here and no longer is, and requires "
                  + "unions: what was demanded stays demanded.");
            }

            // preserve-unadmitted: and - null and false are one answer, the
            // tight end; only false-to-true grants reach.
            var wasEffective = was.PreserveUnadmitted ?? false;
            var nowEffective = now.PreserveUnadmitted ?? false;
            if (nowEffective && !wasEffective)
            {
                return Widen($"{at}.preserve-unadmitted",
                    "preserve-unadmitted moves from off to on, and it composes by and: true "
                  + "means unadmitted work leaves the machine for a fetchable remote, which "
                  + "is reach.");
            }

            // opens: intersect - a subset of the work kinds that could be
            // opened before. Null and empty are the tight end, the same place
            // `moves: []` sits, and this arm does not lean on Validate refusing
            // them: the comparator is also asked about documents that arrived
            // before a rule existed.
            //
            // WRITTEN BY HAND BECAUSE EVERY ARM HERE IS, which is why it is
            // worth a comment. The guard claiming every composed field has a
            // direction rule reads the operator table to check the operator
            // table, so a field can carry an operator and still be invisible to
            // this function - which is what happened to `accepts:` and is
            // recorded above. A work kind gained here is a governance regime an
            // agent can newly nominate, so the omission would be a menu growing
            // with no approver in sight.
            var opened = (now.Opens ?? []).Except(was.Opens ?? [], StringComparer.Ordinal).ToList();
            if (opened.Count > 0)
            {
                return Widen($"{at}.opens",
                    $"work kind '{opened[0]}' could not be opened here before, and opens "
                  + "intersects: it can only ever narrow. A kind gained is a whole governance "
                  + "regime - its loop, its moves, its budget, its destinations and which "
                  + "obligations apply - that a nomination can newly reach.");
            }
        }

        return null;
    }

    // ---- the shared shapes ----

    /// <summary>
    /// A work-kind-only declaration, where dropping a member removes gates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Containment, not equality, and the asymmetry is deliberate.</b>
    /// Declaring a field that was absent is an author making a document legible
    /// for the first time; they are giving nothing up, and sending every
    /// migrating work kind to a gate for that would make the field expensive to
    /// adopt in exactly the estate that most needs it. WITHDRAWING the field is
    /// the opposite: it withdraws every claim in it at once, which is the
    /// maximal reduction rather than a return to innocence.
    /// </para>
    /// <para>
    /// <b>Anything not shown to tighten is a widening</b>, as everywhere else
    /// here — these sets carry no order beyond containment, so a member that
    /// disappeared cannot be argued into <i>unchanged</i>.
    /// </para>
    /// </remarks>
    private static EnvelopeWidening? Declared(
        string field, IReadOnlyList<string>? was, IReadOnlyList<string>? now, string consequence)
    {
        if (was is null)
        {
            return null;
        }

        if (now is null)
        {
            return Widen(field,
                $"'{field}:' declared {Describe(string.Join(", ", was))} and now declares nothing "
              + $"at all, which withdraws every claim in it at once: {consequence}.");
        }

        var dropped = was.Except(now, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        return dropped.Count == 0
            ? null
            : Widen(field,
                $"'{field}:' no longer names {string.Join(", ", dropped)} - {consequence}. "
              + "What cannot be shown to tighten is a widening.");
    }

    private static EnvelopeWidening? Moved(string field, string? was, string? now) =>
        string.Equals(was, now, StringComparison.Ordinal)
            ? null
            : Widen(field,
                $"'{Describe(now)}' is not '{Describe(was)}', and {field} carries no order by "
              + "strictness: an unordered move cannot be shown to tighten, so it takes the "
              + "widening path rather than falling through as unchanged.");

    private static EnvelopeWidening? IdSetMoved(
        string field, IEnumerable<string> was, IEnumerable<string> now)
    {
        var wasSet = was.ToHashSet(StringComparer.Ordinal);
        var nowSet = now.ToHashSet(StringComparer.Ordinal);

        if (wasSet.SetEquals(nowSet))
        {
            return null;
        }

        var moved = nowSet.Except(wasSet).Concat(wasSet.Except(nowSet)).First();
        return Widen(field,
            $"the {field} set moved ('{moved}'), and the sets are picked by one layer: an "
          + "added entry is reach that did not exist, and a removed one strands what was "
          + "wired to it. Neither carries a declared direction.");
    }

    private static bool SetEqual(IReadOnlyList<string> was, IReadOnlyList<string> now) =>
        was.ToHashSet(StringComparer.Ordinal).SetEquals(now);

    private static string Changed(string id, string member, string? was, string? now) =>
        $"obligation '{id}' changed its {member} from '{Describe(was)}' to '{Describe(now)}' - "
      + "a changed body is a different gate, not a tighter one; equality is the only total "
      + "answer over members no operator orders.";

    private static string Describe(object? value) => value?.ToString() ?? "(nothing)";

    private static EnvelopeWidening Widen(string field, string because) =>
        new() { Field = field, Because = because };
}
