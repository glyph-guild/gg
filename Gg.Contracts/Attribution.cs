namespace Gg.Contracts;

/// <summary>
/// Whether an obligation applied to a flight, and how that was decided.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three states, because a condition evaluating false is invisible.</b> An
/// obligation that evaluated <c>violated</c> leaves a verdict somebody can be
/// suspicious of. An obligation that never attached leaves nothing - which is
/// governance reporting success while enforcing nothing, with the additional
/// property that nothing was reported at all.
/// </para>
/// <para>
/// <b>Absence is none of these.</b> "This obligation is not in the envelope",
/// "its condition was false" and "nothing ever evaluated it" have to be three
/// different readings, or the most repeated defect in this project - a
/// well-formed wrong value - arrives in its purest form.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class Attachments
{
    /// <summary>The condition was evaluated and held, or there was none.</summary>
    public const string Attached = "attached";

    /// <summary>
    /// The condition was evaluated and did not hold.
    /// </summary>
    /// <remarks>
    /// Recorded, not merely acted on. This is the state that would otherwise be
    /// indistinguishable from an obligation nobody wrote.
    /// </remarks>
    public const string NotAttached = "not-attached";

    /// <summary>
    /// The condition could not be answered.
    /// </summary>
    /// <remarks>
    /// Halts the flight. Article XI, on the field where getting it wrong leaves
    /// no trace: an unrecognised condition must never be read as false, because
    /// false is the answer that removes the obligation.
    /// </remarks>
    public const string Unevaluable = "unevaluable";

    public static IReadOnlyList<string> All { get; } = [Attached, NotAttached, Unevaluable];
}

/// <summary>
/// What an obligation's verdict may be, as it crosses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed, and fingerprinted with <see cref="Attachments"/> beside it.</b> The
/// two are read off one endpoint by one client, so a reader current for one is
/// current for the other - and the verdict was an unconstrained string while the
/// attachment next to it was closed, which meant a value added to it moved no
/// ledger and asked nobody to think about it.
/// </para>
/// <para>
/// <b>It shares <c>unevaluable</c> with <see cref="Attachments"/> and means
/// something else by it.</b> There, the CONDITION could not be read and the
/// obligation's applicability is unknown. Here, the obligation applied and could
/// not be MEASURED. Two vocabularies rather than one, because merging them would
/// make "this rule did not apply" and "this rule could not be checked" the same
/// value - and the second is a halt.
/// </para>
/// <para>
/// <b>The control plane holds its own copy and always will</b>, because the two
/// repositories cannot reference each other and its Engine deliberately holds a
/// third. What this closes is the one that crosses; a test on the other side ties
/// the spellings together, which is the same trade the runner's outcome spellings
/// already make.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class ObligationOutcomes
{
    /// <summary>It held, for the fact set it was measured against.</summary>
    public const string Satisfied = "satisfied";

    /// <summary>It did not hold.</summary>
    public const string Violated = "violated";

    /// <summary>
    /// It applied and could not be measured. Article XI: it halts.
    /// </summary>
    /// <remarks>
    /// <b>Declared, and nothing writes it today.</b> A halted flight records no
    /// verdict at all - a verdict set with a hole in it reads as a complete
    /// answer - so this value is unreachable through the current writer. It is
    /// here because the store can hold it, and a wire vocabulary missing a value
    /// the writer can emit is the permissive silence one spelling away.
    /// </remarks>
    public const string Unevaluable = "unevaluable";

    public static IReadOnlyList<string> All { get; } = [Satisfied, Violated, Unevaluable];
}

/// <summary>
/// One change in whether an obligation applied, with when it happened.
/// </summary>
/// <remarks>
/// <para>
/// <b>State changes only, derived rather than stored.</b> The control plane
/// appends an attribution per evaluation pass; this is the fold's answer to
/// "when did the answer CHANGE", so the common case is one entry and the
/// interesting flight - attached, detached, attached again - is readable with
/// its times. A gate that appeared and vanished is exactly what a reviewer
/// needs to see (ADR-0014), and a log that serves only its latest row files
/// that under never-asked.
/// </para>
/// <para>
/// Derived server-side from a stream that predates this type, so history is
/// retroactive: a flight that flew before transitions existed reads its own.
/// </para>
/// </remarks>
[PinnedId("7ce6248b-a26c-4470-a2cd-725d52f0d41c")]
public sealed record AttachmentTransition
{
    /// <summary>The state it changed to, from <see cref="Attachments"/>.</summary>
    public required string To { get; init; }

    /// <summary>When the evaluation that changed it ran.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The engine's reason for the new answer, when it gave one.</summary>
    public string? Because { get; init; }
}

/// <summary>
/// Why one obligation applied to a flight, or did not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Computed by the Engine and rendered by the client.</b> A client that
/// re-evaluated a predicate in order to explain it could explain a verdict it did
/// not produce, and the two would drift - Article IX in miniature, wearing the
/// costume of a rendering concern.
/// </para>
/// <para>
/// So every field here is an answer rather than an input. There is nothing on this
/// type a client could use to recompute anything.
/// </para>
/// </remarks>
[PinnedId("4b7d1e93-2f68-4a05-9c31-d8e06f4a72b5")]
public sealed record ObligationAttribution
{
    /// <summary>Which obligation, by its id in the envelope.</summary>
    public required string ObligationId { get; init; }

    /// <summary>One of <see cref="Attachments"/>.</summary>
    public required string Attachment { get; init; }

    /// <summary>
    /// The condition as the envelope wrote it, or null when it always applies.
    /// </summary>
    /// <remarks>
    /// Null here means the obligation declared no condition. It never means "the
    /// condition is unknown" - that is <see cref="Attachments.Unevaluable"/>, and
    /// the two are separate fields for exactly that reason.
    /// </remarks>
    public string? Condition { get; init; }

    /// <summary>
    /// The fact that answered the condition, and what in it did.
    /// </summary>
    /// <remarks>
    /// The point of the whole verb. "It attached" is a claim; "it attached
    /// because change.manifest touched migrations/schema.sql" is something
    /// somebody can check.
    /// </remarks>
    public string? Because { get; init; }

    /// <summary>
    /// The verdict, from <see cref="ObligationOutcomes"/>, when the obligation
    /// attached and was evaluated.
    /// </summary>
    /// <remarks>
    /// Null when it did not attach, or when nobody has measured it yet - which is
    /// not the same as an outcome saying so. <see cref="Attachment"/> is what
    /// tells those apart.
    /// </remarks>
    public string? Outcome { get; init; }

    /// <summary>Why, in the Engine's words.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>
    /// Every time the attachment answer changed, oldest first - the first
    /// evaluation is the first entry.
    /// </summary>
    /// <remarks>
    /// Defaulted empty rather than required: a control plane from before this
    /// member serves none, and an empty history renders as nothing rather
    /// than as a claim about times nobody recorded.
    /// </remarks>
    public IReadOnlyList<AttachmentTransition> Transitions { get; init; } = [];

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(ObligationAttribution attribution)
    {
        ArgumentNullException.ThrowIfNull(attribution);

        if (string.IsNullOrWhiteSpace(attribution.ObligationId))
        {
            return "An attribution names the obligation it is about.";
        }

        if (!Attachments.All.Contains(attribution.Attachment, StringComparer.Ordinal))
        {
            return $"Unknown attachment state '{attribution.Attachment}'. Expected one of: "
                 + string.Join(", ", Attachments.All) + ".";
        }

        foreach (var transition in attribution.Transitions)
        {
            if (!Attachments.All.Contains(transition.To, StringComparer.Ordinal))
            {
                return $"A transition to '{transition.To}' is not a state this contract "
                     + "declares. Expected one of: " + string.Join(", ", Attachments.All)
                     + ". A fourth state in the history would make it unreadable.";
            }
        }

        // The three states are only distinguishable if each carries what makes it
        // that state. An attachment answer with no reason is the shrug this type
        // exists to prevent.
        return attribution.Attachment switch
        {
            Attachments.NotAttached when attribution.Condition is not { Length: > 0 } =>
                "An obligation that did not attach has a condition that did not hold, and this one "
              + "names none. Without it there is no way to tell a false condition from an "
              + "obligation nobody wrote.",

            Attachments.Unevaluable when attribution.Diagnosis is not { Length: > 0 } =>
                "An obligation that could not be evaluated says why. 'Unevaluable' with no reason "
              + "is the silence this state exists to break.",

            _ => null,
        };
    }
}

/// <summary>
/// Why every obligation on one flight applied, or did not.
/// </summary>
/// <remarks>
/// <b>Every obligation the envelope declares appears here</b>, including the ones
/// that did not attach. A list of the ones that applied would make
/// non-attachment invisible, which is the whole failure this is designed against.
/// </remarks>
[PinnedId("e0c6a24f-8b13-4d79-a5e2-31f7d0894c6b")]
public sealed record FlightAttribution
{
    /// <summary>Which flight, rendered. GG-42.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>Which envelope governed it.</summary>
    public required string EnvelopeVersion { get; init; }

    /// <summary>One entry per obligation the envelope declares, by id, ordinal.</summary>
    public required IReadOnlyList<ObligationAttribution> Obligations { get; init; }

    /// <summary>
    /// Why nothing was decided, when nothing was.
    /// </summary>
    /// <remarks>
    /// A halted flight has attributions - it has to, or "unevaluable" would have
    /// nowhere to be said - and this is the sentence that says the flight stopped
    /// rather than that it passed.
    /// </remarks>
    public string? Halt { get; init; }
}
