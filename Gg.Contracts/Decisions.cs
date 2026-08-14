namespace Gg.Contracts;

/// <summary>
/// What a person decided about an obligation.
/// </summary>
/// <remarks>
/// <para>
/// <b>One value, and reject is absent rather than unimplemented.</b> A verb that
/// accepts <c>reject</c> and returns success without doing anything is worse than one
/// that says the word means nothing yet: the first records a decision nobody acted on,
/// and the flight looks answered.
/// </para>
/// <para>
/// A closed list rather than free text, for the same reason obligation rules are: an
/// outcome nothing recognises cannot be acted on, and treating it as approval is the
/// direction that lets work land.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Contract)]
public static class DecisionOutcomes
{
    /// <summary>The obligation is satisfied, for the fact set this was decided against.</summary>
    public const string Approved = "approved";

    /// <summary>
    /// The work is not acceptable, and the loop runs again with the reason.
    /// </summary>
    /// <remarks>
    /// <b>The reason is advice and never authority.</b> It reaches the loop's context and
    /// can change nothing: not moves, not scope, not obligations, not the destination,
    /// not the budget. A reason that could widen any of those would be unreviewable
    /// configuration arriving one sentence at a time - an envelope by accretion, made of
    /// rejection comments.
    /// </remarks>
    public const string Rejected = "rejected";

    public static IReadOnlyList<string> All { get; } = [Approved, Rejected];
}

/// <summary>
/// What gg observed about how a decision was made.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observations, never a conclusion.</b> There is no <c>attended</c> field here and
/// there must not be one: connection is a transport fact and attendance is a decision
/// record, and gg is not in a position to tell them apart. It reports what it saw and
/// the control plane decides what that means.
/// </para>
/// <para>
/// <b>Recorded now because a decision made before this existed is unclassifiable
/// afterwards.</b> Nothing in this version reads these fields. The first thing anybody
/// does with <c>gg decide --json</c> is script it, and a delegated gate is a different
/// thing from an attended one - but which is which is a policy question, and policy
/// questions are answered on the other side of the wire.
/// </para>
/// </remarks>
[PinnedId("5e7b2d94-3c81-4f60-a7d5-9182ce46b03f")]
public sealed record DecisionObservations
{
    /// <summary>
    /// Whether gg was attached to a terminal on both ends.
    /// </summary>
    /// <remarks>
    /// A fact about file descriptors. It is evidence about attendance and it is not
    /// attendance: a person can pipe input, and a script can allocate a tty.
    /// </remarks>
    public required bool Interactive { get; init; }

    /// <summary>Whether gg rendered the evidence before the decision was given.</summary>
    /// <remarks>
    /// False for a decision passed on the command line, which is the honest answer:
    /// nothing was shown, so nothing was read.
    /// </remarks>
    public required bool EvidenceRendered { get; init; }

    /// <summary>
    /// Seconds between rendering the evidence and the decision arriving, when both
    /// happened.
    /// </summary>
    /// <remarks>
    /// Null when nothing was rendered. A number nobody interprets in this version -
    /// and the reason to record it is that "approved 200ms after being shown four
    /// hundred lines" is a fact somebody will want later and cannot reconstruct.
    /// </remarks>
    public int? SecondsToDecide { get; init; }
}

/// <summary>
/// A decision, as gg posts it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It carries the evidence manifest hash gg was shown.</b> Not for the record - the
/// control plane records its own - but so a decision made against work that has since
/// moved can be refused rather than recorded. A person who approved one migration and
/// whose approval lands against four is the escalation this field exists to prevent,
/// and it is the same escalation with the human's signature on it.
/// </para>
/// <para>
/// <b>The client computes nothing else.</b> No obligation is marked satisfied here, no
/// admission is inferred; this is a claim, and it is a claim until the control plane
/// says otherwise.
/// </para>
/// </remarks>
[PinnedId("a1c94f27-6b0d-4e83-95a1-2f7086bd35ce")]
public sealed record DecisionRequest
{
    /// <summary>Which obligation is being decided, by its id in the envelope.</summary>
    public required string ObligationId { get; init; }

    /// <summary>One of <see cref="DecisionOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>The evidence manifest hash this decision was made against.</summary>
    public required string ManifestHash { get; init; }

    /// <summary>What gg observed about how the decision was made.</summary>
    public required DecisionObservations Observations { get; init; }

    /// <summary>
    /// Why, in the deciding person's own words. Required when rejecting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Untrusted prose, treated as such.</b> Trusting the author does not make the
    /// bytes clean: control sequences are stripped and the length is bounded, at the
    /// same boundary every other inline item is. A rejection reason reaching a
    /// terminal is a rejection reason that can move a cursor.
    /// </para>
    /// <para>
    /// <b>Advice, never authority.</b> It enters the loop's context and decides nothing -
    /// no verdict consumes it, and nothing derived from it can widen what the flight may
    /// do. The Flight Envelope says context is declared rather than prompted, and this is
    /// prose entering an agent's context; it is justified on the same terms as the
    /// agent's own account travelling the other way - attributed, recorded, decides
    /// nothing.
    /// </para>
    /// </remarks>
    public string? Reason { get; init; }
}

/// <summary>
/// How much of a person's reason may cross.
/// </summary>
/// <remarks>
/// Bounded where it is produced and again where it arrives, because a reason cut in
/// half is a different reason rather than a shorter one - so it is refused rather than
/// truncated, which is the rule every other inline item follows.
/// </remarks>
public static class DecisionReasons
{
    /// <summary>Enough for a paragraph of feedback, and not enough for a document.</summary>
    public const int MaxLength = 4000;
}

/// <summary>
/// What the control plane did with a decision.
/// </summary>
/// <remarks>
/// <b>Carries the admission, because that is the answer the caller actually wants.</b>
/// A decision that satisfied the last outstanding obligation lets the work land, and
/// the client renders what came back rather than working it out - which is the
/// difference between a client that displays a decision and one that makes one.
/// </remarks>
[PinnedId("d38065af-91e4-4b27-a5c0-71e9f2d648b3")]
public sealed record DecisionRecorded
{
    public required string FlightNumber { get; init; }

    public required string ObligationId { get; init; }

    /// <summary>What was recorded. One of <see cref="DecisionOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>Who it was attributed to.</summary>
    public required string DecidedBy { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }

    /// <summary>
    /// Whether the work may now land, and where. Null means it may not.
    /// </summary>
    /// <remarks>
    /// Re-evaluated by the Engine after the decision was recorded, never derived from
    /// the decision. An approval is an input to evaluation, not a substitute for
    /// admission - ADR-0011.
    /// </remarks>
    public DestinationAdmission? Admission { get; init; }
}
