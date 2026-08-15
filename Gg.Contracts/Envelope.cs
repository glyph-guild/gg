namespace Gg.Contracts;

/// <summary>Which executor rung a loop runs on.</summary>
/// <remarks>
/// One value, and the field exists so the ladder has somewhere to grow.
/// <c>on-failure: escalate</c> needs somewhere to escalate TO, and naming the
/// rung is free now and a migration later.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
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
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ObligationChecks
{
    /// <summary>A predicate the Engine evaluates against facts.</summary>
    public const string Machine = "machine";

    /// <summary>
    /// A person decides, and nothing else can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The Engine cannot evaluate this, and that is not a failure.</b> It
    /// returns no verdict at all rather than an <c>unevaluable</c> one - those are
    /// both "no answer" and they mean opposite things: <c>unevaluable</c> is the
    /// system having failed, and a pending decision is the system working.
    /// </para>
    /// <para>
    /// <b>It carries an approver and no rule.</b> A human check with a predicate
    /// would be two answers to one question, and one with nobody named to answer
    /// it is a gate with no route out.
    /// </para>
    /// </remarks>
    public const string Human = "human";

    public static IReadOnlyList<string> All { get; } = [Machine, Human];
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
[VocabularyOf(VocabularyFingerprints.Contract)]
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
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class LoopMoves
{
    public const string Read = "read";
    public const string Edit = "edit";
    public const string RunTests = "run-tests";
    public const string Search = "search";

    /// <summary>Putting bytes at a path, including one that did not exist.</summary>
    /// <remarks>
    /// <para>
    /// <b>`write` and not `create`, because the tool overwrites.</b> Naming it
    /// `create` would be a name true of one of its two uses - the argument that
    /// ruled out reusing <c>destination.landed</c> for the pushed commit.
    /// </para>
    /// <para>
    /// <b>And not by making `edit` grant it.</b> That would retroactively widen
    /// what an already-declared move permits, for every envelope in force, with
    /// nothing in the record marking the day it changed. A move whose meaning
    /// moves is the thing a governance product cannot have.
    /// </para>
    /// <para>
    /// <b>Why it costs a version.</b> Adding a value to a closed enumeration is
    /// not additive: the only safe response to an unknown value is to halt, so an
    /// added value breaks every prior reader by design. Existing envelopes are
    /// unchanged in meaning - they cannot create files, which they could not
    /// before either.
    /// </para>
    /// </remarks>
    public const string Write = "write";

    public static IReadOnlyList<string> All { get; } = [Read, Edit, RunTests, Search, Write];
}

/// <summary>What happens when a loop runs out of budget.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ExhaustionPolicies
{
    public const string HandoffToHuman = "handoff-to-human";

    public static IReadOnlyList<string> All { get; } = [HandoffToHuman];
}

/// <summary>What a destination is.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class DestinationKinds
{
    public const string PullRequest = "pull-request";

    public static IReadOnlyList<string> All { get; } = [PullRequest];
}

/// <summary>
/// The conditions under which an obligation attaches to a flight.
/// </summary>
/// <remarks>
/// <para>
/// A closed vocabulary, for the reason the predicates are: a condition nothing
/// recognises must never be read as false. False is the answer that makes the
/// obligation disappear.
/// </para>
/// <para>
/// One condition, over one fact, at this cardinality. It reads
/// <c>change.manifest</c> - the same fact the first predicate reads and the same
/// path-matching underneath, which is the elegant part and also the risk: one
/// evaluator now serves two positions.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class AttachmentConditions
{
    /// <summary>The change manifest touches something under a path.</summary>
    /// <remarks>
    /// Written <c>change.manifest touches &lt;glob&gt;</c>. The glob is part of
    /// the value rather than a second field, because a condition is one thing a
    /// person reads in one line.
    /// </remarks>
    public const string TouchesPrefix = "change.manifest touches ";

    /// <summary>Whether this is a condition this version can evaluate.</summary>
    /// <remarks>
    /// <b>Shape, not a list of values.</b> The glob varies, so an allow-list of
    /// whole strings is impossible; what is closed is the FORM. Anything that is
    /// not this form halts rather than attaching or not attaching.
    /// </remarks>
    public static bool IsKnown(string condition) =>
        condition.StartsWith(TouchesPrefix, StringComparison.Ordinal)
        && condition.Length > TouchesPrefix.Length;

    /// <summary>The glob a touches-condition names, or null when it is not one.</summary>
    public static string? GlobOf(string condition) =>
        IsKnown(condition) ? condition[TouchesPrefix.Length..].Trim() : null;

    /// <summary>Every form this version understands, for a diagnosis to list.</summary>
    public static IReadOnlyList<string> Forms { get; } = [TouchesPrefix + "<glob>"];
}

/// <summary>Which layer an obligation came from.</summary>
/// <remarks>
/// One value, on a real obligation for the first time - the column was carried
/// against nothing until now. Layering is a later slice; the field is what
/// makes "lower layers may only narrow" expressible when it arrives.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
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

    /// <summary>
    /// A predicate from <see cref="ObligationPredicates"/>, or null for a human check.
    /// </summary>
    /// <remarks>
    /// <b>Optional only because <c>check: human</c> exists.</b> A machine check with
    /// no rule is refused at ingress - an obligation nothing can evaluate reports
    /// satisfied by never running - and a human check carrying one is refused too,
    /// because the Engine would then have to choose which answer counts.
    /// </remarks>
    public string? Rule { get; init; }

    /// <summary>
    /// Who may decide, for a human check. Null for a machine one.
    /// </summary>
    /// <remarks>
    /// <b>Required for <c>check: human</c>.</b> A gate nobody was named to answer
    /// is a flight that waits forever, which is the halt-with-no-exit arriving
    /// through the schema rather than through the state machine.
    /// </remarks>
    public string? Approver { get; init; }

    /// <summary>
    /// What this obligation's gate needs before anybody can answer it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Declared, so what a decision requires is reviewed configuration</b> rather than
    /// whatever the payload assembler happened to have. Entries come from
    /// <see cref="EvidenceItems"/>.
    /// </para>
    /// <para>
    /// <b>An entry the flight cannot produce halts the flight.</b> It is never rendered as
    /// an empty section and the gate is never presented with the item missing - a gate
    /// answered on less than was specified is a decision made by somebody with no way to
    /// know what is absent. Article XI, one layer out from the fact vocabulary that
    /// already holds the same rule.
    /// </para>
    /// <para>
    /// Empty is "nothing declared", which is what every envelope written before this
    /// existed means.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>
    /// When this obligation applies at all, or null when it always does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Self-attachment: the obligation finds the work rather than somebody
    /// classifying it.</b> A condition over facts, from
    /// <see cref="AttachmentConditions"/> - so an obligation about migrations
    /// attaches to the flights that touch migrations, and nobody has to remember
    /// to tag them.
    /// </para>
    /// <para>
    /// <b>Null is "always", and it is the only thing null may mean here.</b> An
    /// obligation whose condition was evaluated and did not hold is a different
    /// state, recorded as such - because a condition evaluating false is
    /// INVISIBLE otherwise, and an obligation that never attached leaves no
    /// verdict for anybody to be suspicious of.
    /// </para>
    /// <para>
    /// <b>Facts only.</b> A condition that read another obligation's verdict
    /// would make attachment depend on evaluation, which turns the verdict set
    /// into a fixed-point computation and reintroduces the ordering dependence
    /// this Engine was proven free of.
    /// </para>
    /// </remarks>
    public string? When { get; init; }

    /// <summary>Which layer it came from.</summary>
    public string Provenance { get; init; } = ObligationProvenances.Org;
}

/// <summary>What a loop may spend.</summary>
/// <remarks>
/// Wall-clock and attempts. Token budgets still need an answer to what a
/// half-finished attempt means, and that is not this step's.
/// </remarks>
[PinnedId("e2a67b5c-118f-4d30-9a4e-7c8b0d1f2a63")]
public sealed record LoopBudget
{
    /// <summary>A duration, as <see cref="EnvelopeDurations"/> reads it.</summary>
    public required string WallClock { get; init; }

    /// <summary>
    /// How many times this flight's loop may run. Null is unbounded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>FLIGHT-LEVEL attempts, and the word is already taken.</b> A loop reports
    /// <c>attempts</c> of its own - the agent's internal turns, and a real run
    /// printed <i>completed after 7 attempt(s)</i> for one invocation. This is the
    /// reject-and-rerun cycle: how many times a person may send the work back and
    /// have it run again. If the two counts ever share a variable, a budget of
    /// five is spent by one loop thinking.
    /// </para>
    /// <para>
    /// Null rather than a default, because a number nobody chose would be a
    /// termination condition nobody agreed to - and the current state is that
    /// there is none, which is worth being able to express.
    /// </para>
    /// <para>
    /// A MEMBER, so this half costs nothing: a member may be added freely and a
    /// value may not. The version this rides on is the <c>write</c> move's.
    /// </para>
    /// </remarks>
    public int? Attempts { get; init; }
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

            // THE TWO ROUTES, each closed. Which fields an obligation carries is
            // determined by its check, and one carrying the other route's fields is
            // refused rather than half-interpreted.
            if (string.Equals(obligation.Check, ObligationChecks.Human, StringComparison.Ordinal))
            {
                if (obligation.Approver is not { Length: > 0 }
                    || obligation.Approver.All(char.IsWhiteSpace))
                {
                    return $"Obligation '{obligation.Id}' is checked by a human and names no "
                         + "approver. A gate nobody was named to answer is a flight that waits "
                         + "forever - and it would wait looking exactly like a flight that is "
                         + "still working.";
                }

                if (obligation.Rule is not null)
                {
                    return $"Obligation '{obligation.Id}' is checked by a human and also carries "
                         + $"rule '{obligation.Rule}'. That is two answers to one question, and "
                         + "the Engine would have to choose which one counts.";
                }
            }
            else
            {
                if (obligation.Rule is null)
                {
                    return $"Obligation '{obligation.Id}' is checked by a machine and names no "
                         + "rule. An obligation nothing can evaluate reports satisfied by never "
                         + "running, which is worse than no obligation at all.";
                }

                if (obligation.Approver is not null)
                {
                    return $"Obligation '{obligation.Id}' is checked by a machine and names "
                         + $"approver '{obligation.Approver}'. Nobody will ever be asked, so the "
                         + "field records a person as responsible for something that will not "
                         + "reach them - and somebody would read it as a gate.";
                }

                if (Unknown(obligation.Rule, ObligationPredicates.All) is { } rule)
                {
                    // Article XI, at the earliest point it can be caught. A rule
                    // nothing can evaluate must never become an obligation that
                    // reports satisfied by never running.
                    return $"Unknown rule '{rule}' on obligation '{obligation.Id}'. Expected one "
                         + "of: " + string.Join(", ", ObligationPredicates.All) + ".";
                }
            }

            if (obligation.When is { } condition && !AttachmentConditions.IsKnown(condition))
            {
                // Article XI at the earliest point it can be caught, on the field
                // where getting it wrong is invisible: an unrecognised condition
                // must never be read as false, because false is the answer that
                // makes the obligation vanish without a trace.
                // Naming the key as well as the value: a diagnosis quoting only
                // the condition sends somebody looking for it without saying
                // which line of the obligation it came from.
                return $"'{condition}' is not a condition this version understands, at "
                     + $"obligations.{obligation.Id}.when. Expected one of: "
                     + string.Join(", ", AttachmentConditions.Forms)
                     + ". A condition nothing recognises cannot be treated as false - false is the "
                     + "answer that removes the obligation, and nothing would be recorded.";
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
                    // A DANGLING REFERENCE, which is the defect here - not "an
                    // obligation nothing discharges", which is what this message
                    // used to say. An obligation with no loop is a GATE and is
                    // allowed; the only thing refused is a loop naming something
                    // that is not there.
                    return $"Loop '{loop.Id}' discharges '{discharged}', which is not an obligation "
                         + "in this envelope. A loop cannot discharge something nothing declares.";
                }

                if (envelope.Obligations.FirstOrDefault(o =>
                        string.Equals(o.Id, discharged, StringComparison.Ordinal)) is
                    { Check: ObligationChecks.Human } human)
                {
                    // A runner satisfying a gate, which is the escalation this
                    // route exists to prevent. Refused at ingress, because the
                    // alternative is an envelope that reads as though a loop could
                    // answer for a person.
                    return $"Loop '{loop.Id}' discharges '{human.Id}', which is checked by a "
                         + "human. A loop that discharged a human check would be a runner "
                         + "answering for a person.";
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
