namespace Gg.Contracts;

/// <summary>Which executor rung a loop runs on.</summary>
/// <remarks>
/// One value, and the field exists so the ladder has somewhere to grow.
/// <c>on-failure: escalate</c> needs somewhere to escalate TO, and naming the
/// rung is free now and a migration later.
/// </remarks>
public static class ExecutorRungs
{
    public const string Frontier = "frontier";

    public static IReadOnlyList<string> All { get; } = [Frontier];
}

/// <summary>Who or what discharges an obligation.</summary>
/// <remarks>
/// The ladder an obligation is hardened along - <c>human</c> to <c>agent</c> to
/// <c>machine</c> - with only the end of it implemented. Article XIII says a
/// move along it is a reviewed change on recorded evidence, so the other two
/// arrive with the mechanism that governs them and not before.
/// </remarks>
public static class ObligationChecks
{
    public const string Machine = "machine";

    public static IReadOnlyList<string> All { get; } = [Machine];
}

/// <summary>
/// The predicates an obligation may name.
/// </summary>
/// <remarks>
/// <para>
/// <b>A closed vocabulary, not prose.</b> An obligation whose rule is a
/// sentence is one the Engine cannot evaluate, and an obligation nothing
/// evaluates is worse than no obligation at all: it reports satisfied by
/// never running. Article XI.
/// </para>
/// <para>
/// Each predicate names what it reads, which is what lets the Engine say
/// <i>unevaluable</i> rather than guess. <see cref="NoFileOutsideScope"/> reads
/// <c>context.scope</c> and a <c>change.manifest</c> fact.
/// </para>
/// </remarks>
public static class ObligationPredicates
{
    /// <summary>Nothing was touched outside the context's scope.</summary>
    /// <remarks>Reads <c>context.scope</c> and a <c>change.manifest</c> fact.</remarks>
    public const string NoFileOutsideScope = "no-file-outside-scope";

    /// <summary>
    /// The loop did not run out of time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads a <c>loop.outcome</c> fact - <b>a different fact from the first
    /// predicate</b>, which is the whole reason this is the second one. Two
    /// obligations reading the same fact with similar predicates is the first
    /// obligation twice; two reading different facts is where the interaction
    /// lives, because a flight can carry the facts for one and not the other.
    /// </para>
    /// <para>
    /// Real governance rather than a demonstration: work from a loop that ran out
    /// of time is half-finished by definition, and landing it is how a deadline
    /// becomes a merge.
    /// </para>
    /// <para>
    /// <b>Declares no new fact.</b> <c>loop.outcome</c> has crossed since slice
    /// two step 3 - which is the guard this slice is built under, because a gate
    /// reads facts that already cross.
    /// </para>
    /// </remarks>
    public const string LoopNotExhausted = "loop-not-exhausted";

    public static IReadOnlyList<string> All { get; } = [NoFileOutsideScope, LoopNotExhausted];
}

/// <summary>
/// What a loop is permitted to do.
/// </summary>
/// <remarks>
/// Declared and recorded; <b>not enforced</b>. Recording which moves a flight
/// actually used is what makes enforcement designable later - a bound nobody
/// has measured is a bound nobody can set.
/// </remarks>
public static class LoopMoves
{
    public const string Read = "read";
    public const string Edit = "edit";
    public const string RunTests = "run-tests";
    public const string Search = "search";

    public static IReadOnlyList<string> All { get; } = [Read, Edit, RunTests, Search];
}

/// <summary>What happens when a loop runs out of budget.</summary>
public static class ExhaustionPolicies
{
    public const string HandoffToHuman = "handoff-to-human";

    public static IReadOnlyList<string> All { get; } = [HandoffToHuman];
}

/// <summary>What a destination is.</summary>
public static class DestinationKinds
{
    public const string PullRequest = "pull-request";

    public static IReadOnlyList<string> All { get; } = [PullRequest];
}

/// <summary>Which layer an obligation came from.</summary>
/// <remarks>
/// One value, on a real obligation for the first time - the column was carried
/// against nothing until now. Layering is a later slice; the field is what
/// makes "lower layers may only narrow" expressible when it arrives.
/// </remarks>
public static class ObligationProvenances
{
    public const string Org = "org";

    public static IReadOnlyList<string> All { get; } = [Org];
}

/// <summary>What the flight is bound to.</summary>
[PinnedId("9d4c1e77-3a86-4f02-b95d-2c7e64f8a1b3")]
public sealed record ContextBinding
{
    /// <summary>A glob. Load-bearing: the obligation reads it.</summary>
    public required string Scope { get; init; }

    /// <summary>Which constitution governs, by version.</summary>
    public required string Constitution { get; init; }
}

/// <summary>Something that must hold.</summary>
[PinnedId("4b0f8a21-6d53-4c19-8e77-95a2f0c3d6e8")]
public sealed record Obligation
{
    /// <summary>The name it is referred to by, and its key in the text form.</summary>
    public required string Id { get; init; }

    /// <summary>One of <see cref="ObligationChecks"/>.</summary>
    public required string Check { get; init; }

    /// <summary>A predicate from <see cref="ObligationPredicates"/>. Never prose.</summary>
    public required string Rule { get; init; }

    /// <summary>Which layer it came from.</summary>
    public string Provenance { get; init; } = ObligationProvenances.Org;
}

/// <summary>What a loop may spend.</summary>
/// <remarks>
/// Wall-clock only. Token and attempt budgets need an executor that reports
/// them; wall-clock needs a timer that already exists, so it is the one that
/// can be honest today.
/// </remarks>
[PinnedId("e2a67b5c-118f-4d30-9a4e-7c8b0d1f2a63")]
public sealed record LoopBudget
{
    /// <summary>A duration, as <see cref="EnvelopeDurations"/> reads it.</summary>
    public required string WallClock { get; init; }
}

/// <summary>Work that discharges obligations.</summary>
[PinnedId("7c93d0e4-2f61-4a8b-b05c-3e1d7a9f4620")]
public sealed record Loop
{
    public required string Id { get; init; }

    /// <summary>One of <see cref="ExecutorRungs"/>.</summary>
    public required string Executor { get; init; }

    /// <summary>Obligation ids this loop satisfies.</summary>
    public required IReadOnlyList<string> Discharges { get; init; }

    /// <summary>Moves from <see cref="LoopMoves"/>. Recorded, not enforced.</summary>
    public required IReadOnlyList<string> Moves { get; init; }

    public required LoopBudget Budget { get; init; }

    /// <summary>One of <see cref="ExhaustionPolicies"/>.</summary>
    public required string OnExhaustion { get; init; }
}

/// <summary>Where the work is allowed to land.</summary>
/// <remarks>
/// Declared here and acted on nowhere. Write access is a property OF a
/// declared destination - no destination, no write - so the shape has to exist
/// before the escalation it authorises does.
/// </remarks>
[PinnedId("1f5e8c02-9b47-4de6-a13f-6082c5b7e94d")]
public sealed record Destination
{
    public required string Id { get; init; }

    /// <summary>One of <see cref="DestinationKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Obligation ids that must hold before anything is written here.</summary>
    public required IReadOnlyList<string> Requires { get; init; }
}

/// <summary>
/// The rules governing a tenant's flights.
/// </summary>
/// <remarks>
/// <para>
/// <b>Held by the control plane, never read from a customer's repository.</b>
/// A repo file makes every rule relaxable by whoever can merge, which the
/// layering model forbids for org-level rules; and the obvious workaround -
/// letting the runner fetch it - puts policy in the hands of the least-trusted
/// component in the environment, which is Article IX deleted.
/// </para>
/// <para>
/// <b>Cardinality one, and it is checked.</b> One context binding, one
/// obligation, one loop, one destination. At that cardinality the Engine's job
/// is trivial, which is the point: <c>make</c> at cardinality one is still
/// <c>make</c>, and the second obligation is then a change to a working model
/// rather than the arrival of one.
/// </para>
/// </remarks>
[PinnedId("5a2b9f18-7e04-4c63-8d1a-b6f30e97c542")]
public sealed record Envelope
{
    public required ContextBinding Context { get; init; }

    public required IReadOnlyList<Obligation> Obligations { get; init; }

    public required IReadOnlyList<Loop> Loops { get; init; }

    public required IReadOnlyList<Destination> Destinations { get; init; }

    /// <summary>
    /// The diagnosis, or null when there is nothing wrong.
    /// </summary>
    /// <remarks>
    /// A sentence rather than a bool, and it names the offending value every
    /// time. "Invalid envelope" sends somebody reading their own file to work
    /// out which of nine things went wrong, which is how a schema stops being
    /// adopted.
    /// </remarks>
    public static string? Validate(Envelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (Cardinality(envelope) is { } slipped)
        {
            return slipped;
        }

        if (string.IsNullOrWhiteSpace(envelope.Context.Scope))
        {
            return "context.scope is empty. The obligation reads it, so an envelope without one "
                 + "governs nothing.";
        }

        if (string.IsNullOrWhiteSpace(envelope.Context.Constitution))
        {
            return "context.constitution is empty. A flight that cannot say which constitution "
                 + "governed it is one nobody can act on later.";
        }

        var obligationIds = envelope.Obligations.Select(o => o.Id).ToList();

        foreach (var obligation in envelope.Obligations)
        {
            if (string.IsNullOrWhiteSpace(obligation.Id))
            {
                return "An obligation must be named: the loop that discharges it refers to it by id.";
            }

            if (Unknown(obligation.Check, ObligationChecks.All) is { } check)
            {
                return $"Unknown check '{check}' on obligation '{obligation.Id}'. Expected one of: "
                     + string.Join(", ", ObligationChecks.All) + ".";
            }

            if (Unknown(obligation.Rule, ObligationPredicates.All) is { } rule)
            {
                // Article XI, at the earliest point it can be caught. A rule
                // nothing can evaluate must never become an obligation that
                // reports satisfied by never running.
                return $"Unknown rule '{rule}' on obligation '{obligation.Id}'. Expected one of: "
                     + string.Join(", ", ObligationPredicates.All) + ".";
            }

            if (Unknown(obligation.Provenance, ObligationProvenances.All) is { } provenance)
            {
                return $"Unknown provenance '{provenance}' on obligation '{obligation.Id}'. "
                     + "Expected one of: " + string.Join(", ", ObligationProvenances.All) + ".";
            }
        }

        foreach (var loop in envelope.Loops)
        {
            if (string.IsNullOrWhiteSpace(loop.Id))
            {
                return "A loop must be named.";
            }

            if (Unknown(loop.Executor, ExecutorRungs.All) is { } executor)
            {
                return $"Unknown executor '{executor}' on loop '{loop.Id}'. Expected one of: "
                     + string.Join(", ", ExecutorRungs.All) + ".";
            }

            if (Unknown(loop.OnExhaustion, ExhaustionPolicies.All) is { } exhaustion)
            {
                return $"Unknown on-exhaustion '{exhaustion}' on loop '{loop.Id}'. Expected one of: "
                     + string.Join(", ", ExhaustionPolicies.All) + ".";
            }

            foreach (var move in loop.Moves)
            {
                if (Unknown(move, LoopMoves.All) is { } unknownMove)
                {
                    return $"Unknown move '{unknownMove}' on loop '{loop.Id}'. Expected one of: "
                         + string.Join(", ", LoopMoves.All) + ".";
                }
            }

            if (!EnvelopeDurations.TryParse(loop.Budget.WallClock, out _))
            {
                return $"'{loop.Budget.WallClock}' is not a wall-clock budget on loop '{loop.Id}'. "
                     + "Expected a whole number followed by s, m or h - for example 30m.";
            }

            foreach (var discharged in loop.Discharges)
            {
                if (!obligationIds.Contains(discharged, StringComparer.Ordinal))
                {
                    return $"Loop '{loop.Id}' discharges '{discharged}', which is not an obligation "
                         + "in this envelope. An obligation nothing discharges is a flight that "
                         + "can never finish.";
                }
            }
        }

        foreach (var destination in envelope.Destinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Id))
            {
                return "A destination must be named.";
            }

            if (Unknown(destination.Kind, DestinationKinds.All) is { } kind)
            {
                return $"Unknown kind '{kind}' on destination '{destination.Id}'. Expected one of: "
                     + string.Join(", ", DestinationKinds.All) + ".";
            }

            foreach (var required in destination.Requires)
            {
                if (!obligationIds.Contains(required, StringComparer.Ordinal))
                {
                    return $"Destination '{destination.Id}' requires '{required}', which is not an "
                         + "obligation in this envelope.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The steel thread is one of each, and this is where that is a rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checked rather than aspired to. Every primitive here is justified and the
    /// pressure to add a second is constant, so the failure mode is a slice that
    /// ships a schema instead of a handoff.
    /// </para>
    /// <para>
    /// <b>Obligations are many; everything else is still one.</b> Two obligations
    /// is where the interaction between obligations first exists - a flight can
    /// carry the facts for one and not the other - and nothing in this slice needs
    /// a second loop or a second destination.
    /// </para>
    /// </remarks>
    private static string? Cardinality(Envelope envelope) =>
        envelope.Obligations.Count < 1
            ? "An envelope carries at least one obligation, and this one has none. An envelope "
            + "that governs nothing is a flight nobody is measuring."
        : envelope.Loops.Count != 1
            ? $"An envelope carries one loop, and this one has {envelope.Loops.Count}. "
            + "A second is the next slice arriving early."
        : envelope.Destinations.Count != 1
            ? $"An envelope carries one destination, and this one has {envelope.Destinations.Count}."
            : null;

    private static string? Unknown(string value, IReadOnlyList<string> known) =>
        known.Contains(value, StringComparer.Ordinal) ? null : value;
}

/// <summary>
/// How a budget is written, read the same way on both sides.
/// </summary>
/// <remarks>
/// Declared once here rather than parsed on each side. Two implementations of
/// one grammar agree until they do not, and the disagreement surfaces as a
/// budget that expired early on the machine that mattered.
/// </remarks>
public static class EnvelopeDurations
{
    /// <summary>A whole number of seconds, minutes or hours. Nothing else.</summary>
    /// <remarks>
    /// Deliberately narrow. Fractional durations, compound forms and negative
    /// values are all readable and all mean something slightly different in
    /// every library that reads them, so none of them is accepted.
    /// </remarks>
    public static bool TryParse(string? text, out TimeSpan duration)
    {
        duration = default;

        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            return false;
        }

        var unit = text[^1];
        var digits = text[..^1];

        foreach (var character in digits)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        if (!int.TryParse(digits, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        duration = unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            _ => default,
        };

        return duration != default || value == 0;
    }
}
