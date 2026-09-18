namespace Gg.Contracts;

/// <summary>
/// What performs a sweep. Closed at one, and the second is slice forty's.
/// </summary>
/// <remarks>
/// <b>ADR-0023 names two, and only one exists.</b> A sweep begins as
/// instructions — an agent following the watch's skill — and may later be
/// performed by a script a <c>crystalize</c> flight wrote. That flight does not
/// exist yet, so neither does the word: a declared executor nothing performs
/// is a value a decider could choose and no runner could honour.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class WatchExecutors
{
    /// <summary>An agent, launched with the skill and the watch's servers attached.</summary>
    public const string Instructions = "instructions";

    public static IReadOnlyList<string> All { get; } = [Instructions];
}

/// <summary>What a sweep may conclude.</summary>
/// <remarks>
/// <b>Two, and "found nothing" is not one of them.</b> An empty sweep is
/// <see cref="Swept"/> with nothing seen — a result, not a third state —
/// because the difference that matters is whether the sweep reached its system
/// of record at all. <see cref="Unreachable"/> is the one a person is asked
/// about.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class WatchOutcomes
{
    /// <summary>The sweep reached its system of record and reports what it saw.</summary>
    public const string Swept = "swept";

    /// <summary>The sweep could not do its job, and says why.</summary>
    public const string Unreachable = "unreachable";

    public static IReadOnlyList<string> All { get; } = [Swept, Unreachable];
}

/// <summary>The shapes a git object id takes.</summary>
/// <remarks>
/// Forty hex digits in a SHA-1 repository and sixty-four in a SHA-256 one.
/// Anything else - a branch, a tag, <c>HEAD</c> - is a name that moves, and a
/// pin that moves pins nothing.
/// </remarks>
internal static class GitObjectIds
{
    public static bool IsOne(string? value) =>
        value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit);
}

/// <summary>One decided sweep, served to the pull point.</summary>
/// <remarks>
/// <para>
/// <b>Serving is the claim</b>, control-plane-side, as it is for a pool
/// action: a decided sweep appears in exactly one answer, so two runners
/// polling one watch at one tick get disjoint sets and one sweeper runs.
/// </para>
/// <para>
/// <b>The watch travels whole, as it stands when the sweep is served.</b> The
/// runner needs its host, its credential locator, its filter and its mapping to
/// run the sweep, and reading them from a second route would be a second
/// version of the watch that could disagree with this one. <b>In force at
/// serve time rather than at decision</b>, for the reason a pool action carries
/// the strategy's current image: a sweep decided under v3 and served after v4
/// was approved must not run the filter somebody replaced.
/// </para>
/// <para>
/// <b>The runner reads the skill; this says where, and at which commit.</b> Decided
/// 2026-09-16 by the owner, amending 0.182.0: the control plane reads exactly
/// one thing from a customer's repository and no code, so it resolves the
/// watch's ref to a commit - a metadata call - and the runner reads
/// <see cref="WatchDocument.Skill"/> in <see cref="WatchDocument.Repository"/>
/// there, with the customer's credential. There is no member the skill's words
/// could travel in.
/// </para>
/// <para>
/// <b>A pin or a diagnosis, never both and never neither.</b> A sweep whose ref
/// could not be resolved is served anyway, with the reason, so the runner
/// attests <see cref="WatchOutcomes.Unreachable"/> and a person hears about it -
/// rather than the watch going quiet with nobody told why.
/// </para>
/// </remarks>
[PinnedId("33ede0fe-aebb-40ac-bdd4-c79c01230be6")]
public sealed record WatchAction
{
    public required Guid ActionId { get; init; }

    /// <summary>The watch's name.</summary>
    public required string Watch { get; init; }

    /// <summary>The version this sweep runs under - the one in force when it was served, e.g. nightly-triage@v4.</summary>
    public required string WatchVersion { get; init; }

    /// <summary>The watch at <see cref="WatchVersion"/>.</summary>
    public required WatchDocument Document { get; init; }

    /// <summary>One of <see cref="WatchExecutors"/>.</summary>
    public required string Executor { get; init; }

    /// <summary>
    /// The moves the executor is granted: the shipped <c>sweep</c> kind's, as
    /// the tenant's envelopes compose them.
    /// </summary>
    /// <remarks>
    /// <b>Composed control-plane-side and handed over</b>, because a sweep has no
    /// lease to carry an envelope and a watch has no moves of its own - the owner
    /// accepted that every sweep's tools are one kind's, so a tenant tightens
    /// them the way it tightens any kind and no watch adds one. Empty is legal:
    /// a tenant that tightened the kind to nothing has sweeps that can do
    /// nothing, and they say so.
    /// </remarks>
    public required IReadOnlyList<string> Moves { get; init; }

    /// <summary>
    /// Where the runner reads the skill: the repository, and the commit the
    /// watch's ref resolved to as its <see cref="LeaseRepoRef.PinnedRef"/>.
    /// </summary>
    /// <remarks>
    /// <b>A lease's repository reference</b>, because that is what the runner's
    /// fetch is keyed by. 0.184.0 handed over only the commit, and the watch
    /// names its repository by this tenant's registry name, which no runner can
    /// reach - so the control plane resolves the name to a provider and a slug
    /// and hands those over with the pin.
    /// </remarks>
    public LeaseRepoRef? Skill { get; init; }

    /// <summary>Why there is no pin. Present exactly when <see cref="Skill"/> is not.</summary>
    public string? Diagnosis { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }

    /// <summary>The diagnosis, or null when the action is one a runner can act on.</summary>
    public static string? Validate(WatchAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (string.IsNullOrWhiteSpace(action.Watch)
            || string.IsNullOrWhiteSpace(action.WatchVersion))
        {
            return "A sweep names its watch and the version it runs under. Without them its "
                 + "report answers for nothing anybody can find.";
        }

        if (!WatchExecutors.All.Contains(action.Executor, StringComparer.Ordinal))
        {
            return $"'{action.Executor}' is not an executor this version knows. Expected one "
                 + $"of: {string.Join(", ", WatchExecutors.All)}.";
        }

        if (action.Moves.FirstOrDefault(m => !LoopMoves.All.Contains(m, StringComparer.Ordinal))
            is { } unknown)
        {
            return $"'{unknown}' is not a move this version knows. Expected any of: "
                 + $"{string.Join(", ", LoopMoves.All)}.";
        }

        // NOT THROUGH THIS DOOR. A loop may declare `anything` because an
        // envelope declares it, a person reads it on the flight, and the
        // runner's facts are made to say the bound was declined. A sweep's
        // moves arrive from a watch the control plane resolved and a schedule
        // performs - nobody is looking at the moment it runs - and none of
        // that machinery has been built for the unbounded case. A value that
        // also worked here would be a bound lost where nobody was watching
        // for it, which is the thing the value exists to stop.
        if (LoopMoves.Unbounded(action.Moves))
        {
            return $"A sweep may not declare '{LoopMoves.Anything}'. That value is an "
                 + "envelope declining to bound an agent, read by a person on the flight it "
                 + "governs; a sweep is decided by a schedule and performed with nobody "
                 + "looking. Name the moves this sweep needs.";
        }

        if (action.Skill is null == string.IsNullOrWhiteSpace(action.Diagnosis))
        {
            return action.Skill is null
                ? "This sweep carries no pin and no reason there is none. A runner handed no "
                  + "pin would read the skill at whatever the ref says now, which is the review "
                  + "the pin exists to hold."
                : "This sweep carries a pin AND a reason it could not be pinned. That is two "
                  + "answers to one question, and a runner would have to pick one.";
        }

        if (action.Skill is { } skill)
        {
            if (string.IsNullOrWhiteSpace(skill.Provider) || string.IsNullOrWhiteSpace(skill.Slug))
            {
                return "This sweep's skill names no provider or no repository. Those are what "
                     + "the runner's fetch is keyed by, and a blank one is a fetch of nothing.";
            }

            // A REF, AND IT MAY MOVE - rule 16 as the owner amended it on
            // 2026-09-17. This demanded a commit, and the sentence it refused
            // with is still true: "a branch or a tag moves, and a pin that
            // moves pins nothing". What replaces the guarantee is a RECORD -
            // the runner resolves this ref and reports the commit it landed on
            // in `WatchAttestation.SkillCommit` - so what ran is answerable
            // afterwards rather than fixed beforehand. A commit is still
            // accepted, because a caller holding one may still hand it over.
            if (string.IsNullOrWhiteSpace(skill.PinnedRef))
            {
                return "This sweep's skill names no ref. That is what the runner's fetch is "
                     + "keyed by, and a blank one is a fetch of nothing.";
            }
        }

        return WatchDocument.Validate(action.Document);
    }
}

/// <summary>The decided sweeps a pull answered with.</summary>
[PinnedId("ee4c2740-aadb-4eb9-aed4-0bc793b24116")]
public sealed record WatchActionList
{
    public required IReadOnlyList<WatchAction> Actions { get; init; }
}

/// <summary>
/// A resident runner asking for any sweep it can serve, naming no watch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it names no watch.</b> The pull before this was scoped to one watch,
/// so a watch was swept only while somebody at a shell named it to
/// <c>gg runner sweep</c> - the owner's words, running the walk: "today a
/// watch sweeps only while someone keeps that process running". A flight claim
/// names no flight; the control plane matches. This is that shape for a sweep.
/// </para>
/// <para>
/// <b>It says what this runner can reach, so it is handed only what it can
/// do.</b> Rule 18 presents a credential only where the operator declared the
/// pair, and a runner that claimed a sweep it could not serve would attest a
/// FALSE unreachable. So the claim carries the declared pairs and the control
/// plane serves only a watch naming one of them.
/// </para>
/// <para>
/// <b>On the claim rather than the heartbeat.</b> Labels ride the heartbeat
/// because flights are routed before anybody asks; a sweep is pulled, so the
/// pairs travel with the pull that uses them - never stored, never stale. A
/// locator is a reference, not a secret, and the watch document already carries
/// the same one.
/// </para>
/// </remarks>
[PinnedId("4f2b8e61-9c3a-4d7e-b5a1-2e8f6c0d9a37")]
public sealed record SweepClaim
{
    /// <summary>The (host, credential locator) pairs this runner can sweep.</summary>
    public required IReadOnlyList<SweepServes> Serves { get; init; }

    /// <summary>Why this claim cannot be served, or null when it can.</summary>
    public static string? Validate(SweepClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        // NOT ANSWERED EMPTY. A runner that can reach nothing asking is a
        // mistake in the runner, and "nothing decided" would make it
        // indistinguishable from a quiet period.
        if (claim.Serves is not { Count: > 0 })
        {
            return "This claim names no tracker this runner can reach, so no sweep could ever be "
                 + "served to it. A runner with nothing declared should not ask.";
        }

        foreach (var serves in claim.Serves)
        {
            if (string.IsNullOrWhiteSpace(serves.Host)
                || string.IsNullOrWhiteSpace(serves.Credential))
            {
                return "A pair in this claim has no host or no credential. Rule 18 compares the "
                     + "two together, so half a pair matches nothing it would accept.";
            }
        }

        return null;
    }
}

/// <summary>One tracker a runner can sweep: where it is, and how it is reached.</summary>
/// <remarks>
/// <b>The pair, never the host alone.</b> A watch names both, and rule 18
/// compares both - a runner holding a DIFFERENT credential for the same tracker
/// cannot serve that watch, and matching on the host would hand it one anyway.
/// </remarks>
[PinnedId("b83d5f14-6e2c-4a90-8f7d-1c9e3b5a6d02")]
public sealed record SweepServes
{
    /// <summary>The tracker's root, as the runner's operator declared it.</summary>
    public required string Host { get; init; }

    /// <summary>The credential locator paired with it - a reference, never the secret.</summary>
    public required string Credential { get; init; }
}

/// <summary>One thing a sweep's executor nominated, and why.</summary>
/// <remarks>
/// <para>
/// <b>The executor's choice, decided 2026-09-16 by the owner</b>: <i>"the
/// agent and/or script should be doing that. that way it can be dynamic if
/// necessary."</i> The agent - or later the script - decides which subject is
/// worth a flight, which kind from the bound's menu, and why. The board still
/// decides whether it stands, opens or is refused, rule by rule.
/// </para>
/// <para>
/// <b>Was <c>WatchSighting</c>, and keeps its pinned id.</b> 0.182.0 had the
/// runner report what it saw for the control plane to nominate from; the
/// record grew the three members that make a nomination and kept its wire
/// identity, because a rename must not change it.
/// </para>
/// <para>
/// <b>A work item's title, description and comments have no member here</b>,
/// and the reason and note are bounded at <see cref="FlightNomination"/>'s own
/// measured lengths, so the report cannot carry an item's text into a store.
/// </para>
/// </remarks>
[PinnedId("763495ae-bbe3-4f0e-b9b2-e991fa9e4b3d")]
public sealed record SweepNomination
{
    /// <summary>What names the thing, from the mapping's <c>subject</c>.</summary>
    public required string Subject { get; init; }

    /// <summary>Which version of it was nominated, from the mapping's <c>version</c>.</summary>
    public required string Version { get; init; }

    /// <summary>What names it outside gg, from the mapping's <c>intent-key</c>, when it has one.</summary>
    public string? IntentKey { get; init; }

    /// <summary>
    /// The kind the executor chose from the bound's menu, or null to leave it to
    /// the bound.
    /// </summary>
    /// <remarks>
    /// Null is legal on the wire: a menu of one names the kind, and a menu of
    /// more with no choice made is a refusal the board writes with its own
    /// sentence, not a malformed report.
    /// </remarks>
    public string? WorkKind { get; init; }

    /// <summary>Why this subject is worth a flight, in the executor's words.</summary>
    public required string Reason { get; init; }

    /// <summary>Anything else the person deciding should know.</summary>
    public string? Note { get; init; }

    /// <summary>The most a subject may be.</summary>
    /// <remarks>
    /// An identity. Past this a subject is a sentence, and a sentence here is a
    /// work item's text arriving in a store under an identity's name.
    /// </remarks>
    public const int MaxSubject = 256;

    /// <summary>The most a version may be.</summary>
    public const int MaxVersion = 128;

    /// <summary>The most an intent key may be — a uri, at the length a browser keeps one.</summary>
    public const int MaxIntentKey = 2048;

    /// <summary>What is wrong with one nomination, or null.</summary>
    public static string? Invalid(SweepNomination nomination)
    {
        ArgumentNullException.ThrowIfNull(nomination);

        if (string.IsNullOrWhiteSpace(nomination.Subject)
            || string.IsNullOrWhiteSpace(nomination.Version))
        {
            return "A nomination has no subject or no version. Without both it is not about "
                 + "anything the board could open, or know it had already seen.";
        }

        if (nomination.Subject.Length > MaxSubject
            || nomination.Version.Length > MaxVersion
            || nomination.IntentKey?.Length > MaxIntentKey)
        {
            return $"A nomination names its subject at more length than an identity takes: a "
                 + $"subject is at most {MaxSubject} characters, a version {MaxVersion} and an "
                 + $"intent key {MaxIntentKey}. Past those it is text arriving under an "
                 + "identity's name.";
        }

        if (string.IsNullOrWhiteSpace(nomination.Reason))
        {
            return $"The nomination of '{nomination.Subject}' gives no reason. The executor's "
                 + "judgment is why it chooses, and a choice with no reason is one the person "
                 + "deciding cannot weigh.";
        }

        if (nomination.Reason.Length > FlightNomination.MaxReason
            || nomination.Note?.Length > FlightNomination.MaxNote
            || nomination.WorkKind?.Length > FlightNomination.MaxWorkKind)
        {
            return $"The nomination of '{nomination.Subject}' runs past a nomination's bounds: a "
                 + $"reason is at most {FlightNomination.MaxReason} characters, a note "
                 + $"{FlightNomination.MaxNote} and a kind {FlightNomination.MaxWorkKind}. Past "
                 + "those it is an analysis, not a nomination.";
        }

        return nomination.WorkKind is { } kind && string.IsNullOrWhiteSpace(kind)
            ? "A nomination names a blank kind. Leave it out to let the bound choose."
            : null;
    }
}

/// <summary>
/// What a sweep nominated, attested by the runner that performed it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a fact, for the pool attestation's reason.</b> The fact plumbing is
/// lease-welded, and a sweep has no flight. It gets its own record and its own
/// prefix, idempotent on <see cref="AttestationId"/>, so a retried POST is the
/// ordinary case rather than a duplicate.
/// </para>
/// <para>
/// <b>The executor nominates; the board decides.</b> What the agent or
/// script nominated through <c>gg</c> arrives here, because a sweep has no
/// flight and the fact pipeline that carries an agent's nomination is
/// lease-welded. Whether each one stands, opens or is refused is the board's,
/// applied exactly as to any other nominator's - so Article IX holds there.
/// </para>
/// </remarks>
[PinnedId("84048973-7115-4597-bcab-c13b0c8f574a")]
public sealed record WatchAttestation
{
    /// <summary>The runner's own id for this attestation — the idempotency key (UUIDv7).</summary>
    public required Guid AttestationId { get; init; }

    /// <summary>The watch this sweep was for.</summary>
    public required string Watch { get; init; }

    /// <summary>The decided sweep this answers. Every sweep was decided.</summary>
    public required Guid ActionId { get; init; }

    /// <summary>One of <see cref="WatchOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>What the sweep's executor nominated. Empty is a result.</summary>
    /// <remarks>
    /// The accessor delivers non-null and the initializer does not: this member
    /// is init-only, so a body that omits the key is built through a creator
    /// that assigns null. <c>AbsentCollectionsSurviveTheWireTests</c> holds it
    /// for the whole contract.
    /// </remarks>
    public IReadOnlyList<SweepNomination> Nominated
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>The runner's clock when the sweep finished.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>Why the sweep could not do its job. Present exactly when it could not.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// The digest of the skill the sweep followed, read at the action's commit.
    /// </summary>
    /// <remarks>
    /// <b>The record of what ran, on the runner's word.</b> The control plane
    /// pins the commit and does not read the file, so the blob's digest can only
    /// come from the machine that read it. Required on a good pass; optional on
    /// an unreachable one, which may never have read the skill at all.
    /// </remarks>
    public string? SkillSha { get; init; }

    /// <summary>
    /// The commit the runner resolved this sweep's skill ref to, and read at.
    /// </summary>
    /// <remarks>
    /// <b>What replaces the pin, and the reason this is not a regression on
    /// paper only.</b> Rule 16 had the control plane resolve the ref so that
    /// what ran was fixed before it ran; the owner amended that on 2026-09-17,
    /// so the ref handed over may move and the runner says where it landed.
    /// Required on a good pass for <see cref="SkillSha"/>'s reason and one
    /// more: a digest with no commit beside it is a record nobody can resolve
    /// back to a reviewed version.
    /// </remarks>
    public string? SkillCommit { get; init; }

    /// <summary>The most one sweep may nominate.</summary>
    /// <remarks>
    /// A watch's own cap per pass bounds what stands, and is the control plane's
    /// to apply. This bounds what a runner may put on the wire at all, whatever
    /// a watch declared - <see cref="TrackerAdmission.MaxProposals"/>' number,
    /// for its reason: past it a sweep is rewriting a backlog, not triaging one.
    /// </remarks>
    public const int MaxNominations = TrackerAdmission.MaxProposals;

    /// <summary>
    /// The schema's own rule, shared so the runner and the control plane cannot
    /// disagree about what a valid report is.
    /// </summary>
    public static string? Validate(WatchAttestation attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        if (attestation.AttestationId.Version != 7)
        {
            return $"attestationId '{attestation.AttestationId}' is not a UUIDv7. The id is "
                 + "the idempotency key, and its ordering is the version's.";
        }

        if (string.IsNullOrWhiteSpace(attestation.Watch))
        {
            return "An attestation must name its watch - watch is blank.";
        }

        if (!WatchOutcomes.All.Contains(attestation.Outcome, StringComparer.Ordinal))
        {
            return $"'{attestation.Outcome}' is not an outcome this version knows. Expected "
                 + $"one of: {string.Join(", ", WatchOutcomes.All)}.";
        }

        var unreachable = string.Equals(
            attestation.Outcome, WatchOutcomes.Unreachable, StringComparison.Ordinal);

        if (unreachable && string.IsNullOrWhiteSpace(attestation.Diagnosis))
        {
            return "An unreachable sweep carries no diagnosis. It escalates to a person, and "
                 + "a person handed no reason has nothing to act on.";
        }

        if (unreachable && attestation.Nominated.Count > 0)
        {
            return "An unreachable sweep nominates things. A sweep that nominated something "
                 + "reached something, and half a report under this outcome is two answers.";
        }

        if (!unreachable && attestation.SkillSha is null)
        {
            return "A sweep that reached its system of record does not say which skill it "
                 + "followed. The digest is the record of what ran, and a good pass without one "
                 + "leaves the review with nothing to point at.";
        }

        if (attestation.SkillSha is { } sha && !GitObjectIds.IsOne(sha))
        {
            return $"'{sha}' is not a blob digest. A digest is forty or sixty-four hex digits.";
        }

        if (!unreachable && attestation.SkillCommit is null)
        {
            return "A sweep that reached its system of record does not say which commit it read "
                 + "its skill at. The control plane no longer pins one, so this is the only "
                 + "record of which version ran - and a digest with no commit beside it cannot "
                 + "be resolved back to a reviewed version.";
        }

        if (attestation.SkillCommit is { } read && !GitObjectIds.IsOne(read))
        {
            return $"'{read}' is not a commit. A commit is forty or sixty-four hex digits, and "
                 + "reporting the ref back is the one answer that looks like an answer and is "
                 + "not.";
        }

        if (!unreachable && attestation.Diagnosis is not null)
        {
            return "A sweep that reached its system of record carries a diagnosis. The "
                 + "diagnosis is what a person's escalation reads, and one on a good pass is a "
                 + "sentence nobody is asked to read.";
        }

        if (attestation.Nominated.Count > MaxNominations)
        {
            return $"This sweep nominates {attestation.Nominated.Count} things, and one report "
                 + $"may carry at most {MaxNominations}. A watch's cap per pass bounds what "
                 + "stands; this bounds what a runner may send at all.";
        }

        foreach (var nomination in attestation.Nominated)
        {
            if (SweepNomination.Invalid(nomination) is { } wrong)
            {
                return wrong;
            }
        }

        return null;
    }
}
