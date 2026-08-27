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

    /// <summary>
    /// A person does the work, and nothing automated runs this loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Named rather than borrowed.</b> The alternative was <c>frontier</c>
    /// with nobody listening, and a rung says what discharges the loop: recording
    /// an agent rung for work no agent does makes every later count of <i>how
    /// much did the machine do</i> wrong in the flattering direction, on the one
    /// measurement this product exists to be honest about.
    /// </para>
    /// <para>
    /// <b>It is the bottom of the ladder, not a rung outside it.</b>
    /// <c>on-failure: escalate</c> needed somewhere to escalate TO; this is
    /// somewhere to escalate FROM, and the two arrived at opposite ends of the
    /// same list.
    /// </para>
    /// <para>
    /// <b>Why it costs a version.</b> A value in a closed enumeration, so the
    /// only safe response to it in a prior reader is to halt. Existing envelopes
    /// are unchanged in meaning - none of them named this - and every reader that
    /// meets it for the first time stops rather than guessing which rung it is.
    /// </para>
    /// </remarks>
    public const string Human = "human";

    /// <summary>
    /// Both rungs. A SET today, and deliberately not declared ordered.
    /// </summary>
    /// <remarks>
    /// The type above calls this a ladder, and nothing reads a position on it -
    /// no <c>Outranks</c>, no escalation, no comparison. <c>ObligationProvenances</c>
    /// is declared <c>Ordered</c> because something DOES read its position, and
    /// copying that here would protect an ordering nothing depends on while
    /// implying one that is not enforced. The day <c>on-failure: escalate</c>
    /// reads which rung is higher, this needs <c>Ordered = true</c> in the same
    /// commit - otherwise swapping these two changes which way escalation goes and
    /// moves no fingerprint, which is the defect this codebase has now found
    /// twice.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } = [Frontier, Human];
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
/// Declared, recorded, and <b>partly enforced - measured per session, not
/// assumed</b>. The write-shaped tools are refused at the call when not
/// granted; Read and Bash are measured unbound; and the runner's probe proves
/// the bound before every invocation, so what a fact set claims about
/// enforcement is what a probe demonstrated on that machine at that moment
/// (environment.identity: moveEnforcement, movesProbed, probedAt).
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
    /// <summary>A person is asked to pick the work up.</summary>
    public const string HandoffToHuman = "handoff-to-human";

    /// <summary>
    /// The loop runs again, with what the last one tried and ruled out as its
    /// declared context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No new machinery, because <c>executor</c> is a parameter of a loop.</b>
    /// An agent resuming another agent's work is the same loop starting again on a
    /// rung, and the thing that was missing was somewhere for this field to point -
    /// plus a way for the seed to reach a runner, which is
    /// <see cref="LeaseLoop.ResumesFrom"/>.
    /// </para>
    /// <para>
    /// <b>It is bounded by the loop's attempt budget and by nothing else.</b>
    /// <c>LoopBudget.Attempts</c> already counts how many times a flight's loop may
    /// run; without that bound this value is an instruction to retry for ever, which
    /// is a termination condition nobody agreed to.
    /// </para>
    /// <para>
    /// <b>Why it costs a version.</b> A value in a closed enumeration, so the only
    /// safe response to it in a prior reader is to halt. Existing envelopes are
    /// unchanged in meaning - none of them named this - and every reader meeting it
    /// for the first time stops rather than guessing what to do when a budget runs
    /// out.
    /// </para>
    /// </remarks>
    public const string HandoffToAgent = "handoff-to-agent";

    public static IReadOnlyList<string> All { get; } = [HandoffToHuman, HandoffToAgent];
}

/// <summary>What a destination is.</summary>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class DestinationKinds
{
    public const string PullRequest = "pull-request";

    /// <summary>
    /// The tenant's own envelope. Not a repository, and that is the point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first destination that is not somewhere code goes.</b> Everything
    /// the model claims - a destination is a target plus its admission
    /// conditions, and admission is decided control-plane-side from recorded
    /// verdicts - was only ever exercised against a git remote, so it was
    /// impossible to tell the general claim from a repository-shaped one written
    /// generally.
    /// </para>
    /// <para>
    /// <b>What lands here is a proposal, and landing it is what makes it
    /// effective.</b> The proposal is durable and held outside the envelope
    /// stream, so every version in that stream stays in force by construction:
    /// there is no proposed-but-effective state for a reader to have to tell
    /// apart from an applied one.
    /// </para>
    /// <para>
    /// <b>It cannot relax the gate that governs it</b>, and that falls out of a
    /// rule that already existed rather than a new check. A flight pins the
    /// envelope in force when it opened, so a proposal is evaluated against what
    /// governs now and never against what it is asking for.
    /// </para>
    /// </remarks>
    public const string EnvelopeChange = "envelope-change";

    /// <summary>
    /// A tenant's airspace registries: the chart, the topology, the
    /// repository list. What lands here is a registration, and landing it is
    /// what makes the name reachable.
    /// </summary>
    /// <remarks>
    /// The <see cref="EnvelopeChange"/> shape again, because a registration
    /// IS an envelope-adjacent widening: reach that did not exist before.
    /// Admission blocks on the widens-designated obligations of the envelope
    /// in force, so a registration cannot land past its own open gate - and
    /// absence means no: a root that declares no such gate refuses every
    /// registration naming the tightening that adds one.
    /// </remarks>
    public const string AirspaceRegistration = "airspace-registration";

    /// <summary>
    /// A check run on the commit a flight is working from: a title, a summary
    /// and a conclusion, visible on the pull request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first destination whose act leaves this system.</b>
    /// <see cref="EnvelopeChange"/> and <see cref="AirspaceRegistration"/> are
    /// already performed by admission itself, and both land inside the tenant's
    /// own stream and registries. <see cref="PullRequest"/> leaves, and is
    /// performed by the runner on the customer's credential. This one leaves
    /// AND is performed by the control plane - which is what makes ADR-0020
    /// § 3's containment argument true rather than nearly true, because the
    /// agent that drafted the text never holds a credential that could post it.
    /// </para>
    /// <para>
    /// <b>§ 4 names <c>comment</c>, and this is the correction.</b> § 4 puts
    /// its three kinds on the Notify and Intent ports, on the grounds that both
    /// already exist. Neither does: there is no notification port in any form,
    /// and Intent is a URI rendered into a prompt that nothing resolves in
    /// either direction. A check run is the one outward write this system can
    /// already perform - and the installation holds <c>checks</c> and no write
    /// on issues or pull requests, so <c>comment</c> would cost a re-grant
    /// every installation must approve and nobody can un-ask.
    /// </para>
    /// <para>
    /// <b>It is not a comment wearing a different name.</b> A check run does
    /// not thread and carries no approve/reject verdict on a forge object. What
    /// it does carry is a governed, attributed, durable statement on the commit
    /// under review, posted only after admission. When a re-grant is being
    /// asked for anyway, <c>comment</c> joins on its own merits.
    /// </para>
    /// </remarks>
    public const string CheckRun = "check-run";

    // Slice twelve's step 0 found AirspaceRegistration declared but absent
    // here, so an envelope declaring it was refused by the very vocabulary
    // that declared it. Repaired riding 0.59.0.
    public static IReadOnlyList<string> All { get; } =
        [PullRequest, EnvelopeChange, AirspaceRegistration, CheckRun];
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

    /// <summary>
    /// The change this document is asked to approve widens what is permitted.
    /// </summary>
    /// <remarks>
    /// ADR-0016 § 6's designation, with no new primitive: an ordinary
    /// <c>check: human</c> obligation carrying this condition is the widening
    /// gate, attached from the RECORDED direction of a proposed change rather
    /// than from a fact about the work. Registrations ride the same form -
    /// a new name is reach that did not exist, a widening of the envelope's
    /// reachable estate. A <c>check: machine</c> obligation may not carry it:
    /// a machine predicate reads facts, an envelope-change flight ships none,
    /// and the pair would be a gate no evaluation can ever open.
    /// </remarks>
    public const string Widens = "envelope widens";

    /// <summary>
    /// A recorded loop used the move. Written
    /// <c>moves used include &lt;move&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first condition whose subject is an act.</b> It reads the
    /// flight-wide union of every <c>loop.outcome</c>'s recorded moves -
    /// monotone by construction, because an act cannot be un-happened - so a
    /// gate attached by it does not detach when the evidence of the act is
    /// deleted. ADR-0014's authoring rule in a form: if the concern is that
    /// something happened, the condition reads this rather than the
    /// manifest's residue.
    /// </para>
    /// <para>
    /// Unlike the glob, the move value is the closed <see cref="LoopMoves"/>
    /// vocabulary, so Validate refuses a typo where an author can still act
    /// instead of shipping a gate that silently never attaches.
    /// </para>
    /// </remarks>
    public const string MovesUsedPrefix = "moves used include ";

    /// <summary>Whether this is a condition this version can evaluate.</summary>
    /// <remarks>
    /// <b>Shape, not a list of values.</b> The glob varies, so an allow-list of
    /// whole strings is impossible; what is closed is the FORM. Anything that is
    /// not this form halts rather than attaching or not attaching.
    /// </remarks>
    public static bool IsKnown(string condition) =>
        string.Equals(condition, Widens, StringComparison.Ordinal)
        || (condition.StartsWith(TouchesPrefix, StringComparison.Ordinal)
            && condition.Length > TouchesPrefix.Length)
        || (condition.StartsWith(MovesUsedPrefix, StringComparison.Ordinal)
            && condition.Length > MovesUsedPrefix.Length);

    /// <summary>The glob a touches-condition names, or null when it is not one.</summary>
    public static string? GlobOf(string condition) =>
        condition.StartsWith(TouchesPrefix, StringComparison.Ordinal)
        && condition.Length > TouchesPrefix.Length
            ? condition[TouchesPrefix.Length..].Trim()
            : null;

    /// <summary>The move a moves-condition names, or null when it is not one.</summary>
    public static string? MoveOf(string condition) =>
        condition.StartsWith(MovesUsedPrefix, StringComparison.Ordinal)
        && condition.Length > MovesUsedPrefix.Length
            ? condition[MovesUsedPrefix.Length..].Trim()
            : null;

    /// <summary>Every form this version understands, for a diagnosis to list.</summary>
    public static IReadOnlyList<string> Forms { get; } =
        [TouchesPrefix + "<glob>", Widens, MovesUsedPrefix + "<move>"];
}

/// <summary>What a piece of work can be ABOUT.</summary>
/// <remarks>
/// <para>
/// <b>It did not exist, and its absence is why ADR-0020's open question could
/// not be answered.</b> What this codebase called a <i>subject</i> was a
/// repository registry name; a launch request carries a work kind, an
/// environment and a repository as three separate members with no kind between
/// them. <see cref="Envelope.Accepts"/> has to range over something, and there
/// was nothing.
/// </para>
/// <para>
/// <b>Whether a kind has a TREE is the property the schema computes from</b>,
/// which is why it is a member here rather than a rule elsewhere. ADR-0020
/// § 1's table reads <c>[repo]</c> → a path glob, <c>[]</c> → <c>none</c>,
/// <c>[envelope]</c> → <c>none</c>, <i>the subject is the bound</i>. So
/// <c>none</c> is not the value for <i>no subject</i>: it is the value for
/// <b>no tree</b>, and having no subject is one way of having no tree. A
/// fourth kind then costs one classification rather than an edit to a
/// refusal.
/// </para>
/// <para>
/// <b>Closed for the reason every vocabulary here is closed.</b> An unknown
/// subject kind halts. The alternative reading - treat what we do not
/// recognise as no constraint - turns a typo in <c>accepts:</c> into a kind
/// that accepts everything, which is the permissive direction.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class SubjectKinds
{
    /// <summary>A repository the flight is working over: a tree, and a diff.</summary>
    public const string Repository = "repository";

    /// <summary>
    /// A named envelope document the flight is about. The subject is the
    /// bound, so there is nothing left for a path to select.
    /// </summary>
    /// <remarks>
    /// Not hypothetical: an envelope-change flight is a work kind that already
    /// ships, and it had no honest declaration available to it - <c>[]</c>
    /// would say it is about nothing and <c>[repository]</c> would say it is
    /// about a tree.
    /// </remarks>
    public const string Envelope = "envelope";

    public static IReadOnlyList<string> All { get; } = [Repository, Envelope];

    /// <summary>Whether this is a subject kind this version understands.</summary>
    public static bool IsKnown(string? kind) =>
        kind is { Length: > 0 } && All.Contains(kind, StringComparer.Ordinal);

    /// <summary>
    /// Whether work over this subject has paths for a glob to select.
    /// </summary>
    /// <remarks>
    /// <b>Total, and it throws rather than defaulting.</b> A subject kind
    /// nobody classified would fall through to whichever answer the default
    /// happened to be, and that answer silently decides what scope its work
    /// kinds may write - permissively in one direction and impossibly in the
    /// other.
    /// </remarks>
    public static bool HasTree(string kind) => kind switch
    {
        Repository => true,
        Envelope => false,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind,
            "No subject kind by this name is classified, so nothing can say whether work over "
          + "it has paths to bound. Expected one of: " + string.Join(", ", All) + "."),
    };
}

/// <summary>The <c>scope</c> values that are not globs.</summary>
/// <remarks>
/// <para>
/// <b><c>none</c> is a value and <c>scope</c> stays required.</b> The
/// alternative - making the field optional - fails the way <c>evidence:</c>
/// did: a line that can be dropped is a constraint that can be dropped
/// silently, and <i>nothing was written</i> would render identically to
/// <i>nothing is bounded</i>.
/// </para>
/// <para>
/// <b>And it is a tightening.</b> Before it, <c>"**"</c> was the only way to
/// write unbounded and was indistinguishable from a bound somebody meant.
/// After it, subjectless work says <c>none</c> and <c>"**"</c> goes back to
/// meaning every path.
/// </para>
/// </remarks>
public static class EnvelopeScopes
{
    /// <summary>There is no tree, so there is no path to bound.</summary>
    public const string None = "none";
}

/// <summary>
/// The roles a named envelope document can play.
/// </summary>
/// <remarks>
/// <para>
/// <b>A set, deliberately NOT a ranking.</b> The predecessor vocabulary
/// (<c>ObligationProvenances</c>) did two jobs - the list of layers and the
/// ranking - and the ranking is what ADR-0014 recorded as wrongly reasoned:
/// it was an artifact of replacement semantics. Once every lower-layer
/// operation is a meet, order cannot matter, so the roles are unordered and
/// which role may move which field is per-field data (<see cref="ComposesAttribute"/>),
/// never list position.
/// </para>
/// <para>
/// <b>Derived, never declared.</b> A document does not say which role it
/// plays - the role comes from the topology entry of the name it was applied
/// to, and the parser refuses an obligation that tries to say. The same rule
/// as <i>no envelope arrives from a runner</i>, one level up.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class Roles
{
    /// <summary>The tenant floor. Exactly one, named root, always applies.</summary>
    public const string Root = "root";

    /// <summary>What a flight is for: the layer that supplies the sets.</summary>
    public const string WorkKind = "work-kind";

    /// <summary>Constraints only: obligations and narrower values, any number.</summary>
    public const string Narrowing = "narrowing";

    /// <summary>
    /// A management document: the pool its name governs is kept warm, reset
    /// and healthy under it. Never composes - exactly one strategy governs
    /// one name - and a flight cannot be FOR one.
    /// </summary>
    public const string Strategy = "strategy";

    public static IReadOnlyList<string> All { get; } = [Root, WorkKind, Narrowing, Strategy];
}

/// <summary>
/// How one envelope field composes across layers.
/// </summary>
/// <remarks>
/// <b>Order-freedom is a property of the operators, not a claim about the
/// code.</b> <c>intersect</c>, <c>min</c> and <c>union</c> are commutative
/// and associative; the two <c>-only</c> members name the single role that
/// may move a field, so there is nothing to order. Declared per field as
/// data (<see cref="ComposesAttribute"/>) so composition is generic and a
/// new field with no declared operator fails the build.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class MergeOperators
{
    /// <summary>Only root may move it; a lower layer may echo, never move.</summary>
    public const string RootOnly = "root-only";

    /// <summary>The work kind supplies it: the sets are picked, not narrowed into being.</summary>
    public const string WorkKindOnly = "work-kind-only";

    /// <summary>The meet of what every layer allows.</summary>
    public const string Intersect = "intersect";

    /// <summary>The tightest budget wins.</summary>
    public const string Min = "min";

    /// <summary>Everything every layer declared, keyed where the field says.</summary>
    public const string Union = "union";

    /// <summary>
    /// The meet of a two-point order: any layer may force the tight value,
    /// none may force the loose one.
    /// </summary>
    /// <remarks>
    /// ADR-0014's one undecided operator, decided 2026-08-24 by naming the
    /// governed quantity - reach. For <c>preserve-unadmitted</c>, true means
    /// unadmitted work leaves the machine for a fetchable remote and false
    /// means it does not, so <c>and</c> makes composition the meet: a layer
    /// can only ever remove reach. Absence stays what it always meant -
    /// false, the tight end, not "no opinion".
    /// </remarks>
    public const string And = "and";

    public static IReadOnlyList<string> All { get; } =
        [RootOnly, WorkKindOnly, Intersect, Min, Union, And];
}

/// <summary>
/// Which document an obligation came from: a role that is closed and a name
/// that is not.
/// </summary>
/// <remarks>
/// <b>"Why did this gate appear" answers with a word a person recognises.</b>
/// Assigned by the composer from where the document sat - a document that
/// tries to declare it is refused at parse, the rule the string provenance
/// carried before it.
/// </remarks>
[PinnedId("0bc413ce-2103-40b1-8cb7-c53838a56d80")]
public sealed record ObligationProvenance
{
    /// <summary>One of <see cref="Roles"/>.</summary>
    public required string Role { get; init; }

    /// <summary>The document's name in the topology.</summary>
    public required string Name { get; init; }

    /// <summary>The floor's own provenance - the default a parsed document carries.</summary>
    public static ObligationProvenance AtRoot { get; } = new() { Role = Roles.Root, Name = Roles.Root };
}

/// <summary>
/// Declares how a field composes across layers - the operator table, as data
/// on the schema.
/// </summary>
/// <remarks>
/// The fact-vocabulary drift-guard shape, applied to composition: the
/// composer builds its table by reflecting over the schema, so a new field
/// with no declared operator (and no written exemption) throws before
/// anything composes. A table in an ADR cannot force that decision; an
/// attribute the composer reads can.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComposesAttribute(string @operator) : Attribute
{
    /// <summary>One of <see cref="MergeOperators"/>.</summary>
    public string Operator { get; } = @operator;
}

/// <summary>What the flight is bound to.</summary>
[PinnedId("9d4c1e77-3a86-4f02-b95d-2c7e64f8a1b3")]
public sealed record ContextBinding
{
    /// <summary>A glob. Load-bearing: the obligation reads it.</summary>
    [Composes(MergeOperators.Intersect)]
    public required string Scope { get; init; }

    /// <summary>Which constitution governs, by version.</summary>
    [Composes(MergeOperators.RootOnly)]
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

    /// <summary>Which document it came from: (role, name), assigned by the composer.</summary>
    public ObligationProvenance Provenance { get; init; } = ObligationProvenance.AtRoot;
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
    [Composes(MergeOperators.Min)]
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
    [Composes(MergeOperators.Min)]
    public int? Attempts { get; init; }
}

/// <summary>Work that discharges obligations.</summary>
[PinnedId("7c93d0e4-2f61-4a8b-b05c-3e1d7a9f4620")]
public sealed record Loop
{
    public required string Id { get; init; }

    /// <summary>One of <see cref="ExecutorRungs"/>.</summary>
    [Composes(MergeOperators.RootOnly)]
    public required string Executor { get; init; }

    /// <summary>Obligation ids this loop satisfies.</summary>
    public required IReadOnlyList<string> Discharges { get; init; }

    /// <summary>
    /// Moves from <see cref="LoopMoves"/>. Recorded, and partly enforced -
    /// per-tool, proven by the session's own probe rather than assumed.
    /// </summary>
    [Composes(MergeOperators.Intersect)]
    public required IReadOnlyList<string> Moves { get; init; }

    public required LoopBudget Budget { get; init; }

    /// <summary>One of <see cref="ExhaustionPolicies"/>.</summary>
    [Composes(MergeOperators.RootOnly)]
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
    [Composes(MergeOperators.Union)]
    public required IReadOnlyList<string> Requires { get; init; }

    /// <summary>
    /// Whether work that was NOT admitted may still be pushed here, so somebody
    /// can take the flight over from another machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A flight that halted, violated or exhausted has no branch anywhere</b>,
    /// and the work exists only in a tree on the machine that ran it - which is
    /// precisely the flight somebody wants to take over, and precisely the one that
    /// cannot be taken over from anywhere else. This is the permission to leave it
    /// on the remote instead.
    /// </para>
    /// <para>
    /// <b>Null and false are one answer, and the absence is load-bearing.</b> The
    /// failure mode is a tenant discovering that every abandoned agent attempt is a
    /// branch on their default remote - so an envelope that does not name this
    /// pushes nothing, which is also what every envelope written before this member
    /// existed continues to mean. Article XI, and the same reason an unknown
    /// predicate halts rather than evaluating false.
    /// </para>
    /// <para>
    /// <b>A push here is not a proposal.</b> Admission is still decided the way it
    /// always was; this separates <i>may the work be kept somewhere fetchable</i>
    /// from <i>may it be offered for merge</i>, which were one permission because
    /// only one of them had ever been asked for.
    /// </para>
    /// <para>
    /// <b>A MEMBER, so this half costs nothing.</b> A member may be added freely and
    /// a value may not - the reasoning written on <see cref="LoopBudget.Attempts"/>.
    /// </para>
    /// </remarks>
    [Composes(MergeOperators.And)]
    public bool? PreserveUnadmitted { get; init; }
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

    [Composes(MergeOperators.Union)]
    public required IReadOnlyList<Obligation> Obligations { get; init; }

    [Composes(MergeOperators.WorkKindOnly)]
    public required IReadOnlyList<Loop> Loops { get; init; }

    [Composes(MergeOperators.WorkKindOnly)]
    public required IReadOnlyList<Destination> Destinations { get; init; }

    /// <summary>
    /// Which subject kinds this work kind takes. <c>[]</c> means none; null
    /// means the document is not a work kind and has nothing to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The floor both halves of ADR-0020's schema compute from.</b> § 1
    /// derives which <c>scope</c> values are legal from it; § 2 derives which
    /// fact families the kind can produce. Neither works if it is not there.
    /// </para>
    /// <para>
    /// <b>Work-kind-only, like the sets it sits beside.</b> The floor governs
    /// every kind at once and so has no single answer to give; a narrowing
    /// tightens whatever it attaches to and does not choose a kind. Both are
    /// refused for declaring it, which keeps the field from parsing somewhere
    /// composition has nowhere to put it.
    /// </para>
    /// <para>
    /// <b>Nullable so it can be ABSENT on the documents that must not carry
    /// it</b>, never so a work kind can forget it. A work kind that omits it is
    /// refused, because the tempting default - <i>no <c>accepts:</c> means a
    /// repository, which is what every kind before this field meant</i> - puts
    /// a subjectless kind one keystroke from a kind that takes a tree, with
    /// nothing on the page saying which was meant.
    /// </para>
    /// </remarks>
    [Composes(MergeOperators.WorkKindOnly)]
    public IReadOnlyList<string>? Accepts { get; init; }

    /// <summary>
    /// The environment this envelope's flights are about: a charted name, or
    /// null when unselected.
    /// </summary>
    /// <remarks>
    /// <b>A selection, not a bound.</b> Every other field composes across
    /// layers through a merge operator; this one gets none, deliberately. Two
    /// layers naming different environments is not an empty intersection - it
    /// is a mistake, and the composer refuses it rather than merging it. A
    /// selection is declared once, validated for MEMBERSHIP at apply against
    /// the tenant's chart, and never merged. What Validate owns here is only
    /// the shape: membership is the control plane's question, because the
    /// control plane has the chart.
    /// </remarks>
    [Composes(MergeOperators.RootOnly)]
    public string? Environment { get; init; }

    /// <summary>
    /// The repository this envelope's flights are about: a slug, or null when
    /// unconstrained.
    /// </summary>
    /// <remarks>
    /// The same selection shape as <see cref="Environment"/>, resolved
    /// differently: a repository was always a subject declared at flight
    /// creation, so this selection is validated against the flight's intent
    /// there - it never compiles to a runner label, because a runner does not
    /// advertise a repository; credentials already carry that.
    /// </remarks>
    [Composes(MergeOperators.RootOnly)]
    public string? Repository { get; init; }

    /// <summary>
    /// The diagnosis, or null when there is nothing wrong.
    /// </summary>
    /// <remarks>
    /// A sentence rather than a bool, and it names the offending value every
    /// time. "Invalid envelope" sends somebody reading their own file to work
    /// out which of nine things went wrong, which is how a schema stops being
    /// adopted.
    /// </remarks>
    public static string? Validate(Envelope envelope) => Validate(envelope, MoveKinds.Of);

    /// <summary>
    /// The same validation, plus the questions only the role can answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two overloads because the agreement is role-free and the absence is
    /// not.</b> A document carrying <c>accepts:</c> is claiming to be a work
    /// kind, so its two fields can be checked against each other with no
    /// topology in front of them - which is what makes the check reachable
    /// from a parse of a single file. Whether a work kind was ALLOWED to omit
    /// the field, and whether root was allowed to declare it, are questions
    /// about the role the document was applied to.
    /// </para>
    /// <para>
    /// <b>The role is never read from the document.</b> It comes from the
    /// topology entry of the name it was applied to, or - for a file on
    /// somebody's laptop - from the directory it sits in. A document that
    /// tried to declare its own role would be choosing which rules it is
    /// subject to.
    /// </para>
    /// </remarks>
    public static string? Validate(Envelope envelope, string role)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (Validate(envelope, MoveKinds.Of) is { } refused)
        {
            return refused;
        }

        var isWorkKind = string.Equals(role, Roles.WorkKind, StringComparison.Ordinal);

        if (isWorkKind && envelope.Accepts is null)
        {
            return "A work kind must declare 'accepts:' - the subject kinds it takes, or '[]' "
                 + "for none. It is not defaulted, because 'accepts: []' and a missing line "
                 + "would then mean the same thing, and one of them is a decision.";
        }

        return !isWorkKind && envelope.Accepts is not null
            ? $"A '{role}' declares 'accepts:', and only a '{Roles.WorkKind}' may. The floor "
            + "governs every kind at once so it has no single answer to give, and a narrowing "
            + "tightens whatever it attaches to without choosing a kind."
            : null;
    }

    /// <summary>
    /// The same validation with the move classification injectable.
    /// </summary>
    /// <remarks>
    /// <b>Public because the enforced set is correctly empty.</b> The
    /// outward-act refusal below is unreachable with honest inputs - every
    /// real move is record-only, and a fake one dies at the unknown-move
    /// refusal first - so a branch that must still be provable live needs a
    /// seam, and a planted classification through the REAL validate is the
    /// honest alternative to reflection. Production callers use the
    /// one-argument form, which supplies <see cref="MoveKinds.Of"/>.
    /// </remarks>
    public static string? Validate(Envelope envelope, Func<string, string> moveKindOf)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(moveKindOf);

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

        if (Accepting(envelope) is { } accepting)
        {
            return accepting;
        }

        if (Selection(envelope.Environment, "environment") is { } environment)
        {
            return environment;
        }

        if (Selection(envelope.Repository, "repository") is { } repository)
        {
            return repository;
        }

        var obligationIds = envelope.Obligations.Select(o => o.Id).ToList();

        foreach (var obligation in envelope.Obligations)
        {
            if (ValidateObligation(obligation) is { } refused)
            {
                return refused;
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

                // AN OUTWARD ACT NOTHING CAN CONFIRM, refused where an author
                // can still act. The probe confirms withholding of tools that
                // were NOT granted; an outward move's bound would have to hold
                // WHILE GRANTED, which nothing today can measure - so a declared
                // outward act would be granted by the envelope and shown
                // enforceable by nothing. Unreachable with honest inputs while
                // the enforced set is correctly empty; the planted twin in
                // MoveKindsTests keeps it provably alive.
                if (string.Equals(moveKindOf(move), MoveKinds.OutwardAct, StringComparison.Ordinal))
                {
                    return $"Loop '{loop.Id}' declares move '{move}', which is an outward act: "
                         + "using it is itself the act, and nothing can take it back. No "
                         + "executor this product has lets a probe confirm that bound, so the "
                         + "envelope would grant what nothing can be shown to withhold.";
                }
            }

            // A PERMISSION NOTHING ENFORCES, refused where an author can still do
            // something about it. A move is bound by the executor the runner
            // starts - the runner refuses to take work when it cannot bind one -
            // and a human rung starts no executor, so a move declared here would
            // be granted by the envelope, enforced by nothing, and reported on by
            // nothing. That is the shape `write` was added to stop being, arriving
            // through a different door.
            if (string.Equals(loop.Executor, ExecutorRungs.Human, StringComparison.Ordinal)
                && loop.Moves is [var declared, ..])
            {
                return $"Loop '{loop.Id}' runs at the '{ExecutorRungs.Human}' rung and declares "
                     + $"move '{declared}'. Moves are bound by the executor a runner starts, and "
                     + "this rung starts none - so the move would be granted by the envelope and "
                     + "enforced by nothing. A person's permissions are their account's, not this "
                     + "document's.";
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

            // REFUSED RATHER THAN IGNORED. An envelope-change destination has no
            // branch and no repository, so "preserve the work there" names nothing -
            // and a knob that silently does nothing on one kind of destination is a
            // governance permission somebody sets and believes they granted.
            if (destination.PreserveUnadmitted is not null
                && !string.Equals(
                    destination.Kind, DestinationKinds.PullRequest, StringComparison.Ordinal))
            {
                return $"Destination '{destination.Id}' declares preserve-unadmitted and is a "
                     + $"'{destination.Kind}'. There is no branch to preserve work on: that "
                     + $"permission only means anything for a '{DestinationKinds.PullRequest}'.";
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

    /// <summary>One obligation's rules, or why it is refused.</summary>
    /// <remarks>
    /// SHARED with <see cref="EnvelopeNarrowing.Validate"/>, extracted rather
    /// than copied, so the full envelope and the narrowing cannot drift about
    /// what a well-formed obligation is. Everything here is per-obligation on
    /// purpose: the cross-references (a loop discharging an obligation) stay
    /// with the document that has loops.
    /// </remarks>
    internal static string? ValidateObligation(Obligation obligation)
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

        if (string.Equals(obligation.When, AttachmentConditions.Widens, StringComparison.Ordinal)
            && string.Equals(obligation.Check, ObligationChecks.Machine, StringComparison.Ordinal))
        {
            // Refused where the author can still act. A machine predicate
            // reads facts, an envelope-change flight ships none, and the pair
            // would be a widening gate no evaluation can ever open - the
            // permanent deadlock the no-break-glass rule refuses to build.
            return $"Obligation '{obligation.Id}' pairs check: machine with "
                 + $"when: {AttachmentConditions.Widens}. A machine predicate reads facts and "
                 + "an envelope-change flight ships none, so this gate could never be opened "
                 + "and every widening would deadlock behind it. A widening gate is "
                 + "check: human.";
        }

        if (obligation.When is { } moves
            && AttachmentConditions.MoveOf(moves) is { } named
            && Unknown(named, LoopMoves.All) is { } unknownMove)
        {
            // The hazard the glob form cannot avoid, avoided where the value is
            // closed: a condition over a move nobody records would silently
            // never attach - a gate that looks authored and asks nobody, ever.
            return $"'{moves}' names the move '{unknownMove}', at "
                 + $"obligations.{obligation.Id}.when, and that is not a move this contract "
                 + "declares. Expected one of: " + string.Join(", ", LoopMoves.All) + ". A "
                 + "condition over a move nothing records would silently never attach.";
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
            // NAMED SPECIFICALLY, because this one is not a typo. It is THE
            // canonical gate trigger in the design documents, so somebody will
            // type it, and a generic "not understood" would read as a version
            // that has not got round to it yet - which is how an ordering
            // escape hatch ships as unsupported-but-authorable.
            if (condition.StartsWith("obligations.", StringComparison.Ordinal))
            {
                return $"'{condition}' cites a VERDICT, at obligations.{obligation.Id}.when, "
                     + "and this version refuses it rather than leaving it authorable. It "
                     + "makes one obligation's attachment depend on another's outcome, which "
                     + "turns evaluation into a fixed point and reintroduces the ordering "
                     + "dependence the Engine proved absent. The open question it needs an "
                     + "answer to is whether an attribution may cite a verdict: the line is "
                     + "not how many things evaluation may read, it is that none of them may "
                     + "be something evaluation produced. Until that is decided, express the "
                     + "rule as a condition over facts.";
            }

            return $"'{condition}' is not a condition this version understands, at "
                 + $"obligations.{obligation.Id}.when. Expected one of: "
                 + string.Join(", ", AttachmentConditions.Forms)
                 + ". A condition nothing recognises cannot be treated as false - false is the "
                 + "answer that removes the obligation, and nothing would be recorded.";
        }

        if (Unknown(obligation.Provenance.Role, Roles.All) is { } role)
        {
            return $"Unknown provenance role '{role}' on obligation '{obligation.Id}'. "
                 + "Expected one of: " + string.Join(", ", Roles.All) + ".";
        }

        if (string.IsNullOrWhiteSpace(obligation.Provenance.Name))
        {
            return $"Obligation '{obligation.Id}' carries a provenance with no name. Provenance "
                 + "answers 'why did this gate appear' with a name a person recognises, and a "
                 + "blank answers nothing.";
        }

        return null;
    }

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

    /// <summary>The shape a selection must have, or the diagnosis naming the key.</summary>
    /// <remarks>
    /// Null is unselected and valid. Blank is NOT unselected: "environment: "
    /// reads as a selection to the person who typed it, and admitting it as
    /// nothing would make a typo mean the opposite of the line. One line only,
    /// because the name becomes a label the queue matches on, and a newline in
    /// the middle of that is an injection or a paste accident.
    /// </remarks>
    private static string? Selection(string? value, string key) =>
        value is null
            ? null
        : string.IsNullOrWhiteSpace(value)
            ? $"{key} is blank. Select a name, or remove the line - a blank selection is a "
            + "typo wearing a selection's clothes, not an absence."
        : value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal)
            ? $"{key} spans more than one line. A selection is one name; the label the fleet "
            + "matches on cannot carry a line break."
            : null;

    /// <summary>
    /// Whether <c>accepts:</c> and <c>context.scope</c> agree — ADR-0020 § 1,
    /// read in both directions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One rule, two refusals, and neither is the other's contrapositive.</b>
    /// A kind that works over a tree and bounds no path is not a subjectless
    /// kind - it is a kind whose author wrote the wrong word, and the flight it
    /// opens would be unbounded over a repository. A kind with no tree that
    /// bounds a path has written a rule nothing can ever read, which is a gate
    /// that reports satisfied by never running.
    /// </para>
    /// <para>
    /// <b>Role-free, deliberately.</b> Carrying <c>accepts:</c> is itself the
    /// claim to be a work kind, so this half is reachable from a parse of a
    /// single file with no topology in front of it - which is what lets
    /// <c>gg envelope validate</c> catch it before an apply ever happens.
    /// </para>
    /// </remarks>
    private static string? Accepting(Envelope envelope)
    {
        var unbounded = string.Equals(
            envelope.Context.Scope, EnvelopeScopes.None, StringComparison.Ordinal);

        if (envelope.Accepts is not { } accepts)
        {
            // THE TRAP THE MEET RULE OPENS IF THIS IS MISSING. Composition
            // treats `none` as a domain mismatch and answers `none`, so a ROOT
            // that said `scope: none` would bound every path-taking kind in
            // the tenant to nothing - QUIETLY, where before the meet rule it
            // produced a refusal. `none` says there is no tree, and only a
            // document that declared it accepts no subject has standing to
            // say so.
            return unbounded
                ? $"context.scope is '{EnvelopeScopes.None}' and this document declares no "
                + $"'accepts:'. Only a work kind that accepts no subject may say there is no "
                + $"tree - write a path glob, or declare 'accepts: []' and mean it."
                : null;
        }

        if (accepts.FirstOrDefault(k => !SubjectKinds.IsKnown(k)) is { } unknown)
        {
            return $"Unknown subject kind '{unknown}' in accepts:. Expected one of: "
                 + string.Join(", ", SubjectKinds.All) + ".";
        }

        // THE RULE IS ABOUT TREES, NOT ABOUT EMPTINESS, and the difference is
        // ADR-0020 section 1's third row: `[envelope]` accepts a subject and
        // still writes `none`, because the subject IS the bound. Reading the
        // rule as "none if and only if accepts is empty" is right for a
        // one-member vocabulary and wrong the moment there are two.
        //
        // ANY, rather than all: a kind accepting both a repository and an
        // envelope has paths to select from, and refusing it a glob would
        // leave the half that has a tree unbounded.
        var hasTree = accepts.Any(SubjectKinds.HasTree);
        var bounded = !unbounded;

        return hasTree == bounded
            ? null
            : hasTree
                ? $"accepts: names {string.Join(", ", accepts)} and context.scope is "
                + $"'{EnvelopeScopes.None}'. Work over a tree needs a path bound, so "
                + $"'{EnvelopeScopes.None}' would leave it unbounded: write a path glob."
                : $"accepts: names {(accepts.Count == 0 ? "nothing" : string.Join(", ", accepts))} "
                + $"and context.scope is '{envelope.Context.Scope}'. None of those subjects has "
                + $"a tree, so a path bound selects nothing: write '{EnvelopeScopes.None}'.";
    }
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

/// <summary>
/// A narrowing: the role-shaped document for layers that may only add
/// obligations.
/// </summary>
/// <remarks>
/// <para>
/// <b>One member, and the absence of the others is the design.</b> The
/// operator table says a narrowing may not move the context, the loops, the
/// destinations or a selection - and the strongest form of that table is one
/// a document cannot express, rather than one it gets told off for
/// expressing. There is no <c>Validate</c> refusal for a loop set here
/// because there is no loop set here.
/// </para>
/// <para>
/// <b>No role discriminator, no parent, deliberately.</b> Which role a
/// document plays comes from which door it was handed to - the same rule as
/// <c>layer:</c> and <c>provenance:</c> - and its place in the topology is
/// the topology's to say. A parent reference is a free member addition the
/// day narrowings-by-name land; carrying one now would be a claim nothing
/// verifies.
/// </para>
/// <para>
/// <b>Nothing constructs it in production yet.</b> Narrowings by name are
/// the slice's pre-committed cut; until they land this type is kept live by
/// the vocabulary and surface fingerprints and by the round-trip suites,
/// and the 0.44.0 ledger note says so rather than letting the type read as
/// served.
/// </para>
/// </remarks>
[PinnedId("61032bda-f8f9-4216-a1b3-6e7ad55841f5")]
public sealed record EnvelopeNarrowing
{
    /// <summary>What this layer adds. Never what it changes - there is no such member.</summary>
    [Composes(MergeOperators.Union)]
    public required IReadOnlyList<Obligation> Obligations { get; init; }

    /// <summary>Null when valid, or one diagnosis.</summary>
    /// <remarks>
    /// The per-obligation rules are <see cref="Envelope.ValidateObligation"/>,
    /// shared rather than copied, so the two documents cannot disagree about
    /// what a well-formed obligation is.
    /// </remarks>
    public static string? Validate(EnvelopeNarrowing narrowing)
    {
        ArgumentNullException.ThrowIfNull(narrowing);

        if (narrowing.Obligations.Count == 0)
        {
            return "A narrowing declares at least one obligation. One that narrows nothing is "
                 + "a document with no reason to exist, and every version it minted would "
                 + "govern nothing.";
        }

        foreach (var obligation in narrowing.Obligations)
        {
            if (Envelope.ValidateObligation(obligation) is { } refused)
            {
                return refused;
            }
        }

        return null;
    }
}
