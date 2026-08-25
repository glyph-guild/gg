namespace Gg.Contracts;

/// <summary>
/// Marks a type as the payload of one fact kind.
/// </summary>
/// <remarks>
/// The anchor the build-time manifest guard walks. A fact has to be registered
/// four ways to cross the boundary - pinned id, entry in
/// <see cref="FactKinds"/>, declared JSON members, and a slot on
/// <see cref="FactEnvelope"/> to arrive in - and three out of four is a fact
/// that serializes to a digest and nothing.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FactKindAttribute(string kind) : Attribute
{
    public string Kind { get; } = kind;
}

/// <summary>
/// The pinned fact vocabulary.
/// </summary>
/// <remarks>
/// A fact this list does not contain is rejected loudly rather than
/// accepted-and-ignored. Silently absent is indistinguishable from satisfied,
/// which is this system's most dangerous failure mode: governance that reports
/// success while enforcing nothing.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class FactKinds
{
    /// <summary>What ran, and where. The first real fact.</summary>
    public const string EnvironmentIdentity = "environment.identity";

    /// <summary>Which commit was examined, and whether it came from a fork.</summary>
    /// <remarks>
    /// Deliberately NOT <c>change.manifest</c>, which is step 7: there is no
    /// file list here and no diff. It exists because materializing a fork's
    /// head is only trustworthy if the fact set says that is what happened -
    /// a run that examined a fork and recorded the base is a false fact, which
    /// this design treats as unrecoverable.
    /// </remarks>
    public const string SourceProvenance = "source.provenance";

    /// <summary>What changed between the base and the head that was examined.</summary>
    public const string ChangeManifest = "change.manifest";

    /// <summary>
    /// What a loop did: attempts, moves used, how it ended and why.
    /// </summary>
    /// <remarks>
    /// Everything in it is MEASURED - the executor's own result and the tool
    /// calls it made - never anything the agent said about its work. That is
    /// what keeps an injected instruction out of a machine-checked verdict.
    /// </remarks>
    public const string LoopOutcome = "loop.outcome";

    /// <summary>
    /// Where the loop's transcript is, without carrying it.
    /// </summary>
    /// <remarks>
    /// Hash, size, content type and a locator. The bytes are customer-adjacent
    /// and enormous; the reference proves what they were and crosses in a few
    /// hundred bytes.
    /// </remarks>
    public const string LoopTranscript = "loop.transcript";

    /// <summary>
    /// Where a flight's work landed, once a destination admitted it.
    /// </summary>
    /// <remarks>
    /// The first fact reporting something the runner WROTE rather than
    /// something it observed. Recorded because it happened; the decision that
    /// allowed it was made in the control plane before anything was pushed.
    /// </remarks>
    public const string DestinationLanded = "destination.landed";

    /// <summary>
    /// A branch was pushed, and nothing was proposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A ninth kind rather than a member on <c>destination.landed</c>, and the
    /// reason is the name.</b> Under the two-gate split the push happens BEFORE
    /// admission, so a member on <c>destination.landed</c> populated at push time
    /// would make that fact fire when nothing had landed - a name true of one of its
    /// two uses. The third option, one kind whose name describes one of the two
    /// events it reports, is the one to avoid.
    /// </para>
    /// <para>
    /// <b>And <c>destination.landed</c> keeps its exact meaning and shape</b>, so
    /// nothing already recorded is reinterpreted. That was the argument against
    /// reusing <c>change.manifest.HeadCommit</c> for this, and it applies here too.
    /// </para>
    /// <para>
    /// <b>Two events, neither overwriting the other.</b> A flight with full
    /// admission produces both: this one when the branch reaches the remote, and
    /// <c>destination.landed</c> when the proposal opens. A gated flight produces
    /// only this one, which is what a pending decision is about.
    /// </para>
    /// </remarks>
    public const string DestinationPushed = "destination.pushed";

    /// <summary>
    /// What a loop did, extracted so a person can pick the work up without the
    /// transcript.
    /// </summary>
    /// <remarks>
    /// The transcript is a machine-local reference and does not cross. This is
    /// what crosses in its place, and it is mechanically extracted rather than
    /// summarised - a model's account would be a claim rather than a fact, it
    /// would not be comparable across flights, and it would carry whatever the
    /// transcript told it to.
    /// </remarks>
    public const string LoopDigest = "loop.digest";

    /// <summary>
    /// What a person says they did, confirmed in their own name.
    /// </summary>
    /// <remarks>
    /// The only fact in the vocabulary that is a human assertion rather than a
    /// measurement or an agent's claim. Inline, because it is the thing somebody
    /// reads first when they pick the work up next.
    /// </remarks>
    public const string HumanAccount = "handoff.account";

    /// <summary>Every kind that validates.</summary>
    /// <remarks>
    /// <c>check.verdict</c> is deliberately NOT here. It is a fact a
    /// runner-side check reports, and the only obligation this slice has is
    /// evaluated control-plane-side - there is no check to report one. A kind
    /// the pinned runner cannot produce would arrive absent rather than
    /// loudly, which is the failure this list exists to prevent.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } =
        [EnvironmentIdentity, SourceProvenance, ChangeManifest, LoopOutcome, LoopTranscript,
         DestinationLanded,
         DestinationPushed,
         LoopDigest,
         HumanAccount];
}

/// <summary>
/// The version of the pinned fact vocabulary, declared once.
/// </summary>
/// <remarks>
/// <para>
/// It travels on every request as <c>GG-Fact-Vocabulary</c> and it answers one
/// question: whether the facts a runner can produce are the ones this control
/// plane evaluates obligations against. A runner evaluating against a
/// vocabulary the control plane has moved past gives a silently wrong answer,
/// which is why the header exists at all.
/// </para>
/// <para>
/// <b>Held to account by a ledger</b>, in <c>fact-vocabulary.json</c>: a
/// fingerprint of every registered fact type is recorded against each version,
/// and moving the surface without moving this number fails the build. It was
/// three hand-typed constants until step 7, and it stayed at 0.1.0 through the
/// addition of a whole fact type - which is what a number nothing holds to
/// account does.
/// </para>
/// <para>
/// Declared HERE rather than in each half, so the client, the runner and the
/// control plane read one value. Three copies of a number that must agree is
/// how one of them stops agreeing.
/// </para>
/// </remarks>
public static class FactVocabulary
{
    /// <summary>
    /// 0.5.0: environment.identity, source.provenance, change.manifest with
    /// a diff basis. Same three kinds as 0.4.0 - the version moves because the
    /// FINGERPRINT'S NAMING changed, not the vocabulary.
    /// </summary>
    /// <remarks>
    /// 0.2.0 is recorded in the ledger and was never emitted by a released
    /// binary - it is the version source.provenance should have carried, and
    /// recording it is how the omission stays visible rather than being
    /// renumbered away.
    ///
    /// 0.4.0 adds no fact type. It adds a required member to change.manifest,
    /// which is exactly as much of a vocabulary change: a control plane
    /// reading 0.3.0 manifests cannot tell which diff they described, and the
    /// version is how it knows to stop guessing.
    /// </remarks>
    /// 0.6.0 adds loop.outcome and loop.transcript: the first facts produced by
    /// something other than inspection. A control plane reading 0.5.0 cannot
    /// know a flight ran an executor at all, which is exactly the kind of
    /// silence this version exists to break.
    /// 0.7.0 adds destination.landed: the first fact about something the runner
    /// WROTE. A control plane reading 0.6.0 cannot tell a flight pushed
    /// anything, and "did this flight change a repository" is not a question to
    /// leave to inference.
    /// 0.10.0 adds destination.pushed: a branch reached the remote and nothing was
    /// proposed. A control plane reading 0.9.0 cannot tell that a flight preserved
    /// its work while a person was asked, and it cannot name the commit the decision
    /// is about - "what is this decision about" is not a question to leave to
    /// inference.
    ///
    /// WHY A NINTH KIND rather than a member on destination.landed. The push happens
    /// BEFORE admission now, so a member on destination.landed populated at push
    /// time would make that fact fire when nothing had landed: a name true of one of
    /// its two uses. destination.landed is untouched by this version, so nothing
    /// already recorded is reinterpreted.
    ///
    /// AND WHY THE VERSION HAD TO MOVE AT ALL. The step that added this was guarded
    /// to leave the vocabulary alone, on the reasoning that a gate is evaluated over
    /// facts that already cross. That is true about EVALUATION and false about
    /// PRESENTATION: ADR-0006 makes evidence cross by reference, and a reference is
    /// a commit. The pushed commit does not cross today and nothing else that
    /// crosses is it - source.provenance carries what was cloned, and the manifest
    /// carries the tree's head before the agent's edits were committed.
    /// 0.11.0 adds a THIRD DiffBasis value, prior-attempt, and adds the closed
    /// vocabularies to what this version fingerprints.
    ///
    /// The value first: an attempt-two manifest is measured from the previous attempt's
    /// head, so it describes what one attempt added rather than what the flight did. A
    /// control plane reading it as the flight's total would be wrong by everything the
    /// earlier attempt touched.
    ///
    /// AND THE REASON THIS VERSION MOVED AT ALL, which is the more important half. The
    /// rule has always been that a member may be added freely and a value may not,
    /// because the only safe response to an unknown value is to halt - so an added value
    /// breaks every prior reader by design. The fingerprint could not see values: it
    /// hashed pinned types and their property names, and a third DiffBasis value moved
    /// nothing. The guard that exists to force this conversation was blind to the change
    /// that most needs one. It now hashes every closed vocabulary's values as well.
    ///
    /// 0.12.0 CHANGES NO FACT. The kinds list is byte-identical and no fact type gained,
    /// lost or reshaped a member. What changed is the INSTRUMENT: the closed-vocabulary
    /// scan was attributing by shape, so a gate payload's three vocabularies moved this
    /// number while nothing about facts moved. Membership is now declared and the scan
    /// verifies that everybody declared - and in scoping it, three vocabularies that had
    /// been invisible were found, one of which (Classifications) travels inside every
    /// change manifest. Its levels are a RANKING and are now hashed in their own order,
    /// because sorting them would let a reordering change what may leave a customer's
    /// network without moving any ledger.
    ///
    /// A version computed by a corrected instrument cannot reproduce a reading taken by
    /// the broken one. That is what this bump records.
    /// 0.13.0 ADDS A FIFTH LoopMoves VALUE, `write`, and two members to
    /// environment.identity. The value is why this bumps: a member may be added
    /// freely and a value may not, because the only safe response to an unknown
    /// value is to halt.
    ///
    /// THE MOVE. Nothing in the vocabulary granted putting bytes at a path, so no
    /// flight could create a file - and `when: change.manifest touches
    /// migrations/**`, the gate this design is built around, most often fires on a
    /// NEW migration. It is spelled `write` and not `create` because the tool
    /// overwrites, and `create` would be a name true of one of its two uses. It is
    /// a new value rather than a widening of `edit`, because widening `edit` would
    /// change what an already-declared move permits for every envelope in force,
    /// retroactively, with nothing in the record marking the day.
    ///
    /// Existing envelopes are unchanged in meaning. They cannot create files,
    /// which they could not before either.
    ///
    /// THE MEMBERS. environment.identity gains what this machine's executor
    /// actually bounds - the declared enforcement level, from the new
    /// MoveEnforcements vocabulary, and the tools a startup probe PROVED were
    /// withheld. Deliberately not a boolean saying the bound held: the runner
    /// refuses to take work when it does not, so that member would be constant
    /// across every flight that exists and would record nothing.
    ///
    /// ONE BUMP FOR BOTH, because two would mean two ledger entries, two release
    /// assets and two re-pins - and the second re-pin conflicts with the first in
    /// the two files every step already touches.
    /// 0.16.0 ADDS probedAt TO environment.identity, AND TWO MEMBERS CHANGE
    /// MEANING WITHOUT MOVING. A member add is the free kind; the ledger entry
    /// is the record of the day the semantics moved, which is the expensive
    /// part nobody can diff. moveEnforcement was the executor's compile-time
    /// capability; it is now DERIVED from the session's own probe, so a value
    /// on the wire is something a probe proved rather than something an
    /// adapter declared - slice two's moves row taking its third correction,
    /// upward, on a measurement rather than a flag. movesProbed was a
    /// hardcoded [Write] from one startup run; it is now the set the session's
    /// probe actually held (Edit and Write, each against its own artifact).
    /// probedAt is what makes "a measurement of THIS session" auditable:
    /// the probe runs before every invocation now, because ambient settings
    /// act on the session and the family has five members (acceptEdits
    /// defeats the bound even with setting sources cleared, measured at slice
    /// eleven's step 0). No VALUE moved: per-tool still means what it meant,
    /// none still never crosses from a working runner - a broken bound
    /// releases the lease with the diagnosis instead of shipping anything.
    public const string Version = "0.16.0";
}

/// <summary>How much evidence one fact may be.</summary>
/// <remarks>
/// <para>
/// Declared once and read by both sides. gg refuses an over-budget item where
/// it was produced, and ingress refuses it again - and neither truncates,
/// because a fact cut in half is a false fact rather than a small one.
/// </para>
/// <para>
/// <b>This budget is not the gate budget, and conflating them was a mistake.</b>
/// The gate numbers - 8 KiB an item, 32 KiB a page - were sized for WHAT A
/// PERSON READS while deciding something. A digest is machine-comparable
/// history: nobody reads a hundred of them, something diffs them.
/// </para>
/// <para>
/// Sized at 16 KiB, a manifest of about 150 bytes a file ran out at roughly a
/// hundred files, so an ordinary pull request nearly filled it and the
/// per-directory rollup became the common case. That quietly breaks the claim
/// the hardening rests on - "enough to compare thirty flights" - because thirty
/// rollups cannot be compared with each other at all.
/// </para>
/// </remarks>
public static class FactBudget
{
    /// <summary>
    /// The digest budget, per item. Derived from the measurement.
    /// </summary>
    /// <remarks>
    /// <c>ManifestSizeTests</c> measures about 150 bytes a changed file at
    /// realistic path lengths. A thousand files is a very large pull request
    /// and a real one - a dependency bump, a formatter run, a rename - so the
    /// budget is sized to hold one at full resolution and the rollup goes back
    /// to being the exceptional case it was described as.
    ///
    /// The number is the measurement rounded up to a power of two, not a
    /// number somebody liked: 1000 files x 150 bytes is 150 KiB, and 192 KiB
    /// leaves the same proportional headroom 16 KiB left for a hundred.
    /// </remarks>
    public const int MaxItemBytes = 192 * 1024;

    /// <summary>
    /// Roughly what one changed file costs in a change manifest.
    /// </summary>
    /// <remarks>
    /// Recorded so the budget above can be re-derived rather than
    /// re-guessed, and asserted against a real manifest by
    /// <c>ManifestSizeTests</c> - a constant nothing checks is a comment.
    /// </remarks>
    public const int ManifestBytesPerFile = 150;

    /// <summary>How many files the digest budget holds at full resolution.</summary>
    public static int ManifestFilesWithinBudget => MaxItemBytes / ManifestBytesPerFile;
}

/// <summary>Whether this environment was made for this flight or found.</summary>
/// <remarks>
/// A field that became a feature: it shipped years ahead of warm pools ("a
/// field, not a feature" was its whole argument), and slice twelve's strategy
/// arrived to consume it - the pool attests this value on every routine
/// action, and the next flight's identity carries it as the audit trail.
/// Recording which of the two happened cost nothing then and is what makes
/// fresh-or-reused answerable now.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class EnvironmentProvenance
{
    public const string Fresh = "fresh";

    public const string Reused = "reused";

    public static IReadOnlyList<string> All { get; } = [Fresh, Reused];
}

/// <summary>One dependency lock file, as a path and a hash.</summary>
/// <remarks>
/// Never the file. The question a lock hash answers is whether two runs
/// resolved the same dependencies, and a hash answers it without carrying a
/// customer's dependency graph off their machine.
/// </remarks>
[PinnedId("3f8a1c05-6d92-4b47-8e13-9c5b0a7d2e64")]
public sealed record LockHash
{
    /// <summary>Relative to the materialized tree.</summary>
    public required string Path { get; init; }

    public required string Sha256 { get; init; }
}

/// <summary>One tool the runner used, and which version of it.</summary>
[PinnedId("c62e4b18-05a7-4d3f-91b6-8e2a7c0d5f39")]
public sealed record ToolVersion
{
    public required string Name { get; init; }

    public required string Version { get; init; }
}

/// <summary>
/// What ran, and where.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tests passed is not a fact without the environment they passed in.</b> A
/// laptop is the least reproducible environment in the fleet, which makes this
/// more important locally rather than less - and it is the reason this belongs
/// in slice one despite warm pools being years away.
/// </para>
/// <para>
/// Paths, counts and hashes. There is no member here that could carry a file.
/// </para>
/// </remarks>
/// <summary>How completely an executor bounds the moves an envelope declares.</summary>
/// <remarks>
/// <b>Three states, because a boolean could not hold the answer.</b> On the one
/// executor this product has, <c>Edit</c> and <c>Write</c> are refused at the
/// call, <c>Grep</c> is removed from the tool list, and <c>Read</c> and
/// <c>Bash</c> are not bound at all - so neither "enforces" nor "does not" is
/// true of it.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class MoveEnforcements
{
    /// <summary>Nothing declared is withheld. A move is an observation only.</summary>
    public const string None = "none";

    /// <summary>Some tools are withheld and some are not.</summary>
    public const string PerTool = "per-tool";

    /// <summary>Every declared move bounds what may happen. Nothing declares this yet.</summary>
    public const string Full = "full";

    public static IReadOnlyList<string> All { get; } = [None, PerTool, Full];
}

[PinnedId("9d1b7e34-2a80-4c56-b7f9-06e3a8d4c150")]
[FactKind(FactKinds.EnvironmentIdentity)]
public sealed record EnvironmentIdentity
{
    /// <summary>
    /// A hash of the stable facts about this machine.
    /// </summary>
    /// <remarks>
    /// Of the ENVIRONMENT rather than of the machine: operating system,
    /// architecture, processor count, and the image when there is one. The
    /// runner's label already identifies the box, and hashing a hostname would
    /// carry an identifier nobody needs into a fact about reproducibility.
    /// </remarks>
    public required string HostFingerprint { get; init; }

    /// <summary>
    /// The container image, when there is one.
    /// </summary>
    /// <remarks>
    /// Null means "not running in an image", which is a different fact from
    /// "running in an image nobody recorded" - and only one of them is true on
    /// a laptop.
    /// </remarks>
    public string? ImageDigest { get; init; }

    /// <summary>Dependency lock files found in the tree, as paths and hashes.</summary>
    public required IReadOnlyList<LockHash> Locks { get; init; }

    /// <summary>The tools that did the work, and their versions.</summary>
    public required IReadOnlyList<ToolVersion> Tools { get; init; }

    /// <summary>One of <see cref="EnvironmentProvenance"/>.</summary>
    public required string Provenance { get; init; }

    /// <summary>
    /// How completely this machine's executor bounds a loop's declared moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One of <see cref="MoveEnforcements"/>, or null when this runner has no
    /// executor - which is a real state and not a degraded one: a runner that
    /// cannot invoke an agent cannot break a move bound, and has nothing to
    /// declare.
    /// </para>
    /// <para>
    /// <b>Not a boolean saying the bound held.</b> The runner refuses to take work
    /// when it does not, so a "bound: true" member would be constant across every
    /// flight that exists and would record nothing. What varies by machine and by
    /// executor version is HOW MUCH is bound, and that is what a comparison across
    /// flights needs.
    /// </para>
    /// </remarks>
    public string? MoveEnforcement { get; init; }

    /// <summary>
    /// The tools this runner PROVED were withheld, before it took any work.
    /// </summary>
    /// <remarks>
    /// Measured on this machine at startup rather than declared, because the
    /// bound rests on a flag whose mechanism is not characterised and whose
    /// failure is silent. Empty when nothing was probed.
    /// </remarks>
    public IReadOnlyList<string> MovesProbed { get; init; } = [];

    /// <summary>
    /// When this session's probe measured the bound, or null when this runner
    /// has no executor.
    /// </summary>
    /// <remarks>
    /// The member that makes "a measurement of this session" auditable rather
    /// than asserted: the probe runs before every invocation (ambient settings
    /// act on the session), and a fact whose probedAt sits outside its own
    /// lease's window is a claim about some other session.
    /// </remarks>
    public DateTimeOffset? ProbedAt { get; init; }
}

/// <summary>
/// Which commit was examined, and where it came from.
/// </summary>
/// <remarks>
/// <para>
/// The fact that makes fork handling trustworthy. A pull request's head is
/// fetched from the BASE repository via <c>refs/pull/&lt;n&gt;/head</c>, which
/// works identically for forks and branches and needs no credential for the
/// fork - and the only way to know that happened correctly is for the fact set
/// to say which commit it actually got, and whose it was.
/// </para>
/// <para>
/// A naive clone of a fork either fails or succeeds against the base and
/// produces a manifest describing the wrong commit. The second is worse, and
/// this record is what makes it detectable.
/// </para>
/// </remarks>
[PinnedId("5e0c9a26-4718-4f83-a2d5-6b91e7c0348f")]
[FactKind(FactKinds.SourceProvenance)]
public sealed record SourceProvenance
{
    public required string Provider { get; init; }

    public required string Slug { get; init; }

    /// <summary>The ref the flight was pinned to.</summary>
    public required string RequestedRef { get; init; }

    /// <summary>What the adapter turned it into. Different for a pull request.</summary>
    public required string ResolvedRef { get; init; }

    /// <summary>The commit that was actually put on disk.</summary>
    public required string HeadCommit { get; init; }

    /// <summary>Whether the head belongs to a fork rather than to the base.</summary>
    public required bool HeadIsFork { get; init; }

    /// <summary>Whose fork, when it is one. Provenance a <c>when:</c> condition will want.</summary>
    public string? ForkSlug { get; init; }

    /// <summary>How many files the tree held. A count, never a list.</summary>
    public required int FileCount { get; init; }

    /// <summary>How much disk it took. The first resource we consume in somebody else's environment.</summary>
    public required long Bytes { get; init; }
}

/// <summary>What happened to one path.</summary>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class ChangeKinds
{
    public const string Added = "added";

    public const string Modified = "modified";

    public const string Deleted = "deleted";

    /// <summary>
    /// Where a flight's work landed, once a destination admitted it.
    /// </summary>
    /// <remarks>
    /// The first fact reporting something the runner WROTE rather than
    /// something it observed. Recorded because it happened; the decision that
    /// allowed it was made in the control plane before anything was pushed.
    /// </remarks>
    public const string DestinationLanded = "destination.landed";

    /// <summary>Every kind that validates.</summary>
    public static IReadOnlyList<string> All { get; } = [Added, Modified, Deleted];
}

/// <summary>At what resolution a manifest describes its change.</summary>
/// <remarks>
/// <b>Degrade resolution. Never degrade completeness, and never silently.</b>
/// A per-directory rollup is a true statement at lower resolution; a truncated
/// file list is a false statement. This field is how a consumer tells which it
/// is holding, and it must never have to guess.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class ChangeResolution
{
    /// <summary>Every changed path, named.</summary>
    public const string Files = "files";

    /// <summary>A per-directory rollup, because the file list would not fit.</summary>
    public const string Directories = "directories";

    public static IReadOnlyList<string> All { get; } = [Files, Directories];
}

/// <summary>
/// Which diff a change manifest measured.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-point over-reports a pull request.</b> It is the difference between
/// two commits; a pull request's change is the difference between the head and
/// the point the branch left the base. They diverge by everything anybody else
/// merged in the meantime, and on a busy repository that is most of it.
/// </para>
/// <para>
/// Recorded rather than corrected, deliberately and for now. A manifest that
/// says which basis it used can be read correctly today and re-read correctly
/// after somebody computes a real merge base; a manifest that stayed silent
/// would have to be re-interpreted retroactively, and there is no way to know
/// which of the old ones were affected.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class DiffBasis
{
    /// <summary>Base commit to head commit. What the runner computes today.</summary>
    public const string TwoPoint = "two-point";

    /// <summary>Merge base to head. What a pull request's change actually is.</summary>
    public const string MergeBase = "merge-base";

    /// <summary>
    /// The previous attempt's head to this one's. What one attempt added.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A third basis, because the base means something different.</b> A manifest on
    /// this basis describes what ONE ATTEMPT did, not what the flight did - so
    /// <c>filesChanged</c> read as the flight's total would be wrong by everything the
    /// previous attempt touched. A reader that cannot tell the two apart has a number
    /// whose meaning depends on history it was not given.
    /// </para>
    /// <para>
    /// <b>Safe because the control plane unions them.</b> Obligations are evaluated over
    /// every manifest a flight has shipped, so an incremental manifest narrows what one
    /// fact says without narrowing what is measured. If they were read one at a time,
    /// this basis would let a violation introduced in attempt one pass unnoticed in
    /// attempt two.
    /// </para>
    /// </remarks>
    public const string PriorAttempt = "prior-attempt";

    public static IReadOnlyList<string> All { get; } = [TwoPoint, MergeBase, PriorAttempt];
}

/// <summary>One changed path: what it is, what happened, how much, how sensitive.</summary>
/// <remarks>
/// A path and three numbers. Reading the file to count its lines happens on the
/// runner and the lines stay there - there is no member here a line could
/// travel in, which is asserted over this type's shape.
/// </remarks>
[PinnedId("8c47b1e0-3d95-4a26-b8f7-1e05c9d3a742")]
public sealed record ChangedPath
{
    public required string Path { get; init; }

    /// <summary>One of <see cref="ChangeKinds"/>.</summary>
    public required string Change { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }

    /// <summary>
    /// What the runner's rules made of this path.
    /// </summary>
    /// <remarks>
    /// Carried so a disagreement with the control plane's own answer is
    /// visible, which is a genuinely interesting signal. It is emphatically
    /// NOT what the control plane checks: a patched runner would label
    /// everything public, and re-validation that read this would pass on every
    /// item it was built to catch.
    /// </remarks>
    public required string Classification { get; init; }
}

/// <summary>One directory's worth of change, when the file list would not fit.</summary>
[PinnedId("2b96d4f8-51a3-4c70-9e18-7f0a6b2c85d3")]
public sealed record DirectoryChange
{
    public required string Directory { get; init; }

    public required int Files { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }
}

/// <summary>How much of the change was in one language.</summary>
[PinnedId("6e83a25c-0f71-4b94-8d36-c15b90e7a4f2")]
public sealed record LanguageChange
{
    public required string Language { get; init; }

    public required int Files { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }
}

/// <summary>
/// What changed between the base and the head that was examined.
/// </summary>
/// <remarks>
/// <para>
/// The first fact that is ABOUT files, and so the first where "paths and counts
/// cross, content does not" is a line of code rather than a slogan.
/// </para>
/// <para>
/// <b>The list and the withheld count account for every file.</b> A manifest
/// whose paths are fewer than its own total, with nothing saying why, is a
/// false statement at full resolution - exactly what a truncation would have
/// been. Validation refuses it, which is what makes "never silently" checkable.
/// </para>
/// </remarks>
[PinnedId("f519c7a3-4e60-4d82-91b5-3a7d0c6e28b4")]
[FactKind(FactKinds.ChangeManifest)]
public sealed record ChangeManifest
{
    /// <summary>The commit the change is measured from.</summary>
    public required string BaseCommit { get; init; }

    /// <summary>The commit that was actually examined.</summary>
    public required string HeadCommit { get; init; }

    /// <summary>One of <see cref="ChangeResolution"/>. Says which list is populated.</summary>
    public required string Resolution { get; init; }

    /// <summary>
    /// One of <see cref="DiffBasis"/>. Says which diff these numbers are.
    /// </summary>
    /// <remarks>
    /// Required, not defaulted. A default would let a producer that has never
    /// heard of this member emit manifests labelled with a basis it did not
    /// use, which is the failure this member exists to end.
    /// </remarks>
    public required string DiffBasis { get; init; }

    /// <summary>Populated at file resolution.</summary>
    public required IReadOnlyList<ChangedPath> Paths { get; init; }

    /// <summary>Populated at directory resolution.</summary>
    public required IReadOnlyList<DirectoryChange> Directories { get; init; }

    public required IReadOnlyList<LanguageChange> Languages { get; init; }

    /// <summary>How many files changed in total, whatever the resolution.</summary>
    /// <remarks>
    /// Always the truth about the change. At directory resolution this is what
    /// the rollup states it summarises; at file resolution it is the list
    /// length plus whatever was withheld.
    /// </remarks>
    public required int FilesChanged { get; init; }

    public required int LinesAdded { get; init; }

    public required int LinesRemoved { get; init; }

    /// <summary>
    /// How many paths the filter withheld for being above the ceiling.
    /// </summary>
    /// <remarks>
    /// Never silently. Without this a filtered manifest is indistinguishable
    /// from a smaller change, and a tenant reading it would draw a conclusion
    /// about a flight that examined more than it said.
    /// </remarks>
    public required int PathsWithheld { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(ChangeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!Gg.Contracts.DiffBasis.All.Contains(manifest.DiffBasis))
        {
            return $"Unknown diff basis '{manifest.DiffBasis}'. Expected one of: "
                 + string.Join(", ", Gg.Contracts.DiffBasis.All)
                 + ". A manifest that cannot say which diff it measured cannot be compared with one "
                 + "that can.";
        }

        if (!ChangeResolution.All.Contains(manifest.Resolution))
        {
            return $"Unknown change resolution '{manifest.Resolution}'. Expected one of: "
                 + string.Join(", ", ChangeResolution.All) + ".";
        }

        var files = manifest.Resolution == ChangeResolution.Files;

        if (files && manifest.Directories.Count > 0)
        {
            return "A manifest at file resolution carries no directory rollup. Two populated lists "
                 + "is a document whose meaning depends on which reader looked first.";
        }

        if (!files && manifest.Paths.Count > 0)
        {
            return "A manifest at directory resolution carries no path list, for the same reason.";
        }

        if (!files && manifest.Directories.Count == 0 && manifest.FilesChanged > 0)
        {
            return "A rollup that summarises nothing is not a rollup.";
        }

        if (files && manifest.Paths.Count + manifest.PathsWithheld != manifest.FilesChanged)
        {
            return $"This manifest counts {manifest.FilesChanged} changed file(s), carries "
                 + $"{manifest.Paths.Count} and admits withholding {manifest.PathsWithheld}. A list "
                 + "shorter than its own total with nothing accounting for the difference is a "
                 + "false statement at full resolution.";
        }

        foreach (var path in manifest.Paths)
        {
            if (!ChangeKinds.All.Contains(path.Change))
            {
                return $"'{path.Path}' says it was '{path.Change}'. Expected one of: "
                     + string.Join(", ", ChangeKinds.All) + ".";
            }

            if (Classifications.RankOf(path.Classification) is null)
            {
                return $"'{path.Path}' is classified '{path.Classification}', which is not a "
                     + "classification.";
            }
        }

        return null;
    }
}

/// <summary>
/// One fact, on its way to the control plane.
/// </summary>
/// <remarks>
/// <para>
/// One populated payload, chosen by <see cref="Kind"/> - the same shape
/// <see cref="FlightIntent"/> uses, for the same reason. A kind and a payload
/// that disagree is a document whose meaning depends on which reader saw it
/// first, so validation refuses it.
/// </para>
/// <para>
/// <see cref="Digest"/> is computed BEFORE the filter runs. Computing it after
/// would make every later analysis derive from already-redacted material, and
/// draw conclusions about a document nobody produced.
/// </para>
/// </remarks>
[PinnedId("a47d2f60-8b15-4e93-97c2-0d6a3e8b51c7")]
public sealed record FactEnvelope
{
    /// <summary>
    /// What makes a replay a duplicate rather than a second fact.
    /// </summary>
    /// <remarks>
    /// The runner mints it and the control plane dedupes on it. A retry after a
    /// timeout is the ordinary case, and without this it would append the same
    /// fact twice and make an evidence budget wrong.
    /// </remarks>
    public required string IdempotencyKey { get; init; }

    /// <summary>One of <see cref="FactKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>SHA-256 of the payload, lowercase hex. Computed before the filter.</summary>
    public required string Digest { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.EnvironmentIdentity"/>.</summary>
    public EnvironmentIdentity? Environment { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.SourceProvenance"/>.</summary>
    public SourceProvenance? Source { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.ChangeManifest"/>.</summary>
    public ChangeManifest? Change { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.LoopOutcome"/>.</summary>
    public LoopOutcome? Loop { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.LoopTranscript"/>.</summary>
    public ArtifactReference? Transcript { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.DestinationLanded"/>.</summary>
    public DestinationLanded? Landed { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="FactKinds.DestinationPushed"/>.</summary>
    public DestinationPushed? Pushed { get; init; }

    /// <summary>
    /// Populated when <see cref="Kind"/> is <see cref="FactKinds.LoopDigest"/>.
    /// </summary>
    /// <remarks>
    /// Not <c>Digest</c>: that is this envelope's content hash, and one word
    /// meaning both "the hash proving what this fact was" and "the summary of a
    /// loop" is a confusion that would be read wrong exactly once.
    /// </remarks>
    public LoopDigest? LoopDigest { get; init; }

    /// <summary>
    /// Populated when <see cref="Kind"/> is <see cref="FactKinds.HumanAccount"/>.
    /// </summary>
    /// <remarks>
    /// A person's own statement, kept in its own slot rather than beside the
    /// agent's. A reader who cannot tell which of the two they are looking at
    /// will read a guess as an assertion.
    /// </remarks>
    public HumanAccount? Human { get; init; }


    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    /// <remarks>
    /// A sentence rather than a bool, for the same reason every other Validate
    /// here returns one: Article XI asks for a diagnosis, and "invalid fact"
    /// sends whoever hit it to read their own code.
    /// </remarks>
    public static string? Validate(FactEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
        {
            return "A fact carries an idempotency key. Without one a retry appends the same fact twice.";
        }

        if (!FactKinds.All.Contains(envelope.Kind))
        {
            return $"Unknown fact kind '{envelope.Kind}'. Expected one of: "
                 + string.Join(", ", FactKinds.All) + ".";
        }

        if (!IsSha256(envelope.Digest))
        {
            return $"'{envelope.Digest}' is not a sha256. A digest that cannot be compared is a "
                 + "budget nobody can enforce.";
        }

        // Exactly one payload, and it is the one the kind named. Counting
        // rather than checking the named field alone, so a second payload
        // travelling alongside is refused too.
        var carried = new (string Kind, bool Present)[]
        {
            (FactKinds.EnvironmentIdentity, envelope.Environment is not null),
            (FactKinds.SourceProvenance, envelope.Source is not null),
            (FactKinds.ChangeManifest, envelope.Change is not null),
            (FactKinds.LoopOutcome, envelope.Loop is not null),
            (FactKinds.LoopTranscript, envelope.Transcript is not null),
            (FactKinds.DestinationLanded, envelope.Landed is not null),
            (FactKinds.DestinationPushed, envelope.Pushed is not null),
            (FactKinds.LoopDigest, envelope.LoopDigest is not null),
            (FactKinds.HumanAccount, envelope.Human is not null),
        };

        var present = carried.Where(c => c.Present).ToList();
        if (present.Count != 1)
        {
            return $"A fact carries exactly one payload; this one carries {present.Count}.";
        }

        if (present[0].Kind != envelope.Kind)
        {
            return $"This fact says it is '{envelope.Kind}' and carries a '{present[0].Kind}' payload.";
        }

        if (envelope.Environment is { } environment
            && !EnvironmentProvenance.All.Contains(environment.Provenance))
        {
            return $"Unknown environment provenance '{environment.Provenance}'. Expected one of: "
                 + string.Join(", ", EnvironmentProvenance.All) + ".";
        }

        if (envelope.Loop is { } loop && LoopOutcome.Validate(loop) is { } badLoop)
        {
            return badLoop;
        }

        if (envelope.Human is { } human && HumanAccount.Validate(human) is { } badHuman)
        {
            return badHuman;
        }

        if (envelope.LoopDigest is { } summary && LoopDigest.Validate(summary) is { } badDigest)
        {
            return badDigest;
        }

        if (envelope.Pushed is { } pushed && DestinationPushed.Validate(pushed) is { } badPush)
        {
            return badPush;
        }

        if (envelope.Landed is { } landed && DestinationLanded.Validate(landed) is { } badLanding)
        {
            return badLanding;
        }

        if (envelope.Transcript is { } transcript)
        {
            if (!IsSha256(transcript.Sha256))
            {
                return $"'{transcript.Sha256}' is not a sha256. A reference whose hash cannot be "
                     + "compared proves nothing about what it points at.";
            }

            if (!ArtifactScopes.All.Contains(transcript.Scope))
            {
                return $"Unknown artifact scope '{transcript.Scope}'. Expected one of: "
                     + string.Join(", ", ArtifactScopes.All) + ".";
            }

            if (string.IsNullOrWhiteSpace(transcript.Locator))
            {
                return "A reference carries a locator. Without one it names nothing.";
            }
        }

        return envelope.Change is { } change ? ChangeManifest.Validate(change) : null;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f'));
}

/// <summary>
/// A batch of facts from the runner holding this lease.
/// </summary>
/// <remarks>
/// Against a LEASE rather than a flight: the lease is the authorisation, and
/// the generation is the fence. A runner that lost its flight must not still be
/// able to assert facts about it.
/// </remarks>
[PinnedId("7b3f18d5-0c64-4a29-85e7-3d90a6b2f4e8")]
public sealed record FactBatch
{
    /// <summary>The generation the runner believes it holds.</summary>
    public required int Generation { get; init; }

    public required IReadOnlyList<FactEnvelope> Facts { get; init; }
}

/// <summary>One fact the control plane would not take, and why.</summary>
/// <remarks>
/// Named individually rather than counted. "3 of 5 rejected" sends somebody
/// looking; naming the key and the reason ends the search.
/// </remarks>
[PinnedId("e18c5074-9a3b-4d62-b0f5-72c4e6a91d38")]
public sealed record FactRejection
{
    public required string IdempotencyKey { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// What the control plane refused, out of a batch it has accepted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refusals only, because they are the only part decided synchronously.</b>
/// ADR-0012 makes the write a command: validation, cleanliness, classification
/// and budget are all settled from the request itself and are answered here,
/// while how many items landed and how many were already held are answers only
/// the write has. Reporting a count the control plane has not yet reached would
/// be a number that reads as measured.
/// </para>
/// <para>
/// A rejection is Article XI's diagnosis, so it keeps its place on the wire even
/// though the status is 202: accepting the batch and refusing an item in it are
/// not in tension - the batch was taken, and this says which parts of it will
/// never become evidence.
/// </para>
/// </remarks>
[PinnedId("41a9e7b2-38c0-4f65-9d1a-5e70b8c34962")]
public sealed record FactBatchAccepted
{
    public required IReadOnlyList<FactRejection> Rejected { get; init; }
}

/// <summary>
/// Whether this flight may push, and whether its work may land.
/// </summary>
/// <remarks>
/// <para>
/// <b>It used to ride the facts response, and it cannot any more.</b> The write
/// is a command, so at the moment the batch is accepted neither answer exists
/// yet. A runner asks for this instead, on the lease it already holds.
/// </para>
/// <para>
/// <b><see cref="Settled"/> is what makes absence safe.</b> Both permissions are
/// refused by being absent - deliberately, so a runner that cannot read a field
/// never lands on the strength of it. That rule only works while absence means
/// "the control plane said no"; once the answer is computed asynchronously,
/// absence ALSO means "it has not looked yet", and the two are the same value.
/// A runner reading them as one would stop pushing work that was going to be
/// admitted, silently. So the question "has this been answered" is carried
/// separately from the answer.
/// </para>
/// </remarks>
[PinnedId("33b87adb-e0de-49db-a803-03f4f94ebb10")]
public sealed record LandingDecision
{
    /// <summary>
    /// Whether every fact this flight has shipped has been evaluated.
    /// </summary>
    /// <remarks>
    /// False means ask again, not "no". A runner holds its tree across this, so
    /// the wait is bounded by its own patience rather than by a promise here.
    /// </remarks>
    public required bool Settled { get; init; }

    /// <summary>
    /// Whether the branch may be pushed, and where.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first of two gates.</b> Granted when no machine obligation is
    /// violated, which is a weaker condition than admission: a flight whose human
    /// obligation is pending may preserve its work on the remote so a person has
    /// something to decide about, and may not open a proposal.
    /// </para>
    /// <para>
    /// <b>Absent means no</b> once <see cref="Settled"/> is true, and a runner
    /// must not derive this from <see cref="Admission"/> or the other way round.
    /// Two permissions, two fields, each refused by its own absence.
    /// </para>
    /// </remarks>
    public BranchPush? Push { get; init; }

    /// <summary>
    /// Whether this flight's work may now land, and where.
    /// </summary>
    /// <remarks>
    /// <b>Null means do not push</b> once <see cref="Settled"/> is true, and it
    /// means that for every reason at once: no destination declared, obligations
    /// unmet, or a control plane too old to answer.
    /// </remarks>
    public DestinationAdmission? Admission { get; init; }
}
