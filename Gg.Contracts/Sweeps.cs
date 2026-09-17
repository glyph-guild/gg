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

/// <summary>
/// A watch's skill as the control plane read it, handed to the runner.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only member on this surface that carries a customer's words, and it
/// carries them one way.</b> ADR-0023 § 1 has the control plane fetch the
/// skill — ADR-0018 § 5's rule, one noun over — and hand the content to the
/// runner. It is read when the action is served and exists in the answer only:
/// the control plane keeps <see cref="Commit"/> and <see cref="Sha"/>, which
/// say what ran, and never <see cref="Content"/>.
/// </para>
/// </remarks>
[PinnedId("fe31444a-696b-4eb7-8111-6798f696cfdb")]
public sealed record WatchSkill
{
    /// <summary>The repository-relative path the watch names.</summary>
    public required string Path { get; init; }

    /// <summary>The commit the watch's ref resolved to when this was read.</summary>
    public required string Commit { get; init; }

    /// <summary>The blob's own digest, as the forge reported it.</summary>
    public required string Sha { get; init; }

    /// <summary>The skill's text.</summary>
    public required string Content { get; init; }
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
/// <b>A skill or a diagnosis, never both and never neither.</b> A skill the
/// control plane could not read is served anyway, with the reason, so the
/// sweep attests <see cref="WatchOutcomes.Unreachable"/> and a person hears
/// about it — rather than the row going unserved and the watch reading as
/// missed with nobody told why.
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

    /// <summary>The skill, when the control plane could read it.</summary>
    public WatchSkill? Skill { get; init; }

    /// <summary>Why there is no skill. Present exactly when <see cref="Skill"/> is not.</summary>
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

        if (action.Skill is null == string.IsNullOrWhiteSpace(action.Diagnosis))
        {
            return action.Skill is null
                ? "This sweep carries no skill and no reason there is none. An instructions "
                  + "executor handed nothing would run an agent on nothing, or do nothing and "
                  + "say nothing."
                : "This sweep carries a skill AND a reason it could not be read. That is two "
                  + "answers to one question, and a runner would have to pick one.";
        }

        if (action.Skill is { } skill
            && (string.IsNullOrWhiteSpace(skill.Path)
                || string.IsNullOrWhiteSpace(skill.Commit)
                || string.IsNullOrWhiteSpace(skill.Sha)))
        {
            return "This sweep's skill has no path, commit or digest. Those are what the "
                 + "control plane keeps instead of the words, so a skill without them runs and "
                 + "leaves no record of what it said.";
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

/// <summary>One thing a sweep saw: an identity and its version.</summary>
/// <remarks>
/// <b>What a nomination would be keyed by, and nothing else.</b> The watch's
/// mapping says which of the shape's fields fill these. A work item's title,
/// its description and its comments have no member here, so the report cannot
/// carry them into a store.
/// </remarks>
[PinnedId("763495ae-bbe3-4f0e-b9b2-e991fa9e4b3d")]
public sealed record WatchSighting
{
    /// <summary>What names the thing, from the mapping's <c>subject</c>.</summary>
    public required string Subject { get; init; }

    /// <summary>Which version of it was seen, from the mapping's <c>version</c>.</summary>
    public required string Version { get; init; }

    /// <summary>What names it outside gg, from the mapping's <c>intent-key</c>, when it has one.</summary>
    public string? IntentKey { get; init; }

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
}

/// <summary>
/// What a sweep saw, attested by the runner that performed it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a fact, for the pool attestation's reason.</b> The fact plumbing is
/// lease-welded, and a sweep has no flight. It gets its own record and its own
/// prefix, idempotent on <see cref="AttestationId"/>, so a retried POST is the
/// ordinary case rather than a duplicate.
/// </para>
/// <para>
/// <b>The runner reports; the control plane nominates.</b> Article IX admits
/// no exception, so nothing here says what should be opened. Which sightings
/// become nominations, and whether the board admits them, is decided where
/// the runner cannot reach.
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

    /// <summary>What the sweep saw. Empty is a result.</summary>
    /// <remarks>
    /// The accessor delivers non-null and the initializer does not: this member
    /// is init-only, so a body that omits the key is built through a creator
    /// that assigns null. <c>AbsentCollectionsSurviveTheWireTests</c> holds it
    /// for the whole contract.
    /// </remarks>
    public IReadOnlyList<WatchSighting> Saw
    {
        get => field ?? [];
        init;
    } = [];

    /// <summary>The runner's clock when the sweep finished.</summary>
    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>Why the sweep could not do its job. Present exactly when it could not.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>The most one sweep may report.</summary>
    /// <remarks>
    /// A watch's own cap per pass bounds what is nominated, and is the control
    /// plane's to apply. This bounds what a runner may put on the wire at all,
    /// whatever a watch declared.
    /// </remarks>
    public const int MaxSightings = 1000;

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

        if (unreachable && attestation.Saw.Count > 0)
        {
            return "An unreachable sweep reports things it saw. A sweep that saw something "
                 + "reached something, and half a report under this outcome is two answers.";
        }

        if (!unreachable && attestation.Diagnosis is not null)
        {
            return "A sweep that reached its system of record carries a diagnosis. The "
                 + "diagnosis is what a person's escalation reads, and one on a good pass is a "
                 + "sentence nobody is asked to read.";
        }

        if (attestation.Saw.Count > MaxSightings)
        {
            return $"This sweep reports {attestation.Saw.Count} things, and one report may "
                 + $"carry at most {MaxSightings}. A watch's cap per pass bounds what is "
                 + "nominated; this bounds what a runner may send at all.";
        }

        foreach (var sighting in attestation.Saw)
        {
            if (string.IsNullOrWhiteSpace(sighting.Subject)
                || string.IsNullOrWhiteSpace(sighting.Version))
            {
                return "A sighting has no subject or no version. Without both it is not a "
                     + "thing anybody could nominate, or know they had already seen.";
            }

            if (sighting.Subject.Length > WatchSighting.MaxSubject
                || sighting.Version.Length > WatchSighting.MaxVersion
                || sighting.IntentKey?.Length > WatchSighting.MaxIntentKey)
            {
                return $"A sighting is longer than an identity is: a subject is at most "
                     + $"{WatchSighting.MaxSubject} characters, a version "
                     + $"{WatchSighting.MaxVersion} and an intent key "
                     + $"{WatchSighting.MaxIntentKey}. Past those it is text arriving under "
                     + "an identity's name.";
            }
        }

        return null;
    }
}
