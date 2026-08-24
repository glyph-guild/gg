namespace Gg.Contracts;

/// <summary>How a loop ended.</summary>
/// <remarks>
/// <para>
/// Three, and the third is not a failure. <c>exhausted</c> means the loop ran
/// out of budget and is <b>waiting for whoever the envelope's
/// <c>on-exhaustion</c> names</b> - a person, or another agent - a real state,
/// not an error, and the one a console queue should show somebody. Calling it
/// failed would put it in the same bucket as a crash, and those need
/// different people.
/// </para>
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class LoopOutcomes
{
    /// <summary>The loop finished on its own terms.</summary>
    public const string Completed = "completed";

    /// <summary>It stopped because something went wrong, and said what.</summary>
    public const string Failed = "failed";

    /// <summary>
    /// It ran out of budget. Waiting, not broken - and for whom is the
    /// envelope's <c>on-exhaustion</c> to say.
    /// </summary>
    public const string Exhausted = "exhausted";

    public static IReadOnlyList<string> All { get; } = [Completed, Failed, Exhausted];
}

/// <summary>
/// Where an artifact is, for artifacts too large or too sensitive to carry.
/// </summary>
/// <remarks>
/// <para>
/// Hash, size, content type and a locator. Enough to prove what a thing was
/// without holding it - which is the whole of ADR-0006's reference
/// disposition, and the first time the platform uses it.
/// </para>
/// <para>
/// <b><see cref="Scope"/> is a declared capability gap, not a flag.</b> A
/// transcript today is written to a durable path on the runner's own machine,
/// because there is no Storage port yet. That is a real reference - the hash
/// proves what it was - and it only resolves on that machine. Saying so here
/// means a gate that cannot follow it finds out from the artifact rather than
/// from an empty fetch.
/// </para>
/// </remarks>
[FactKind(FactKinds.LoopTranscript)]
[PinnedId("f0b6a1d3-95c4-4e27-8b31-7a92e05c4d68")]
public sealed record ArtifactReference
{
    /// <summary>Where it is, in whatever form that scope understands.</summary>
    public required string Locator { get; init; }

    /// <summary>SHA-256 of the bytes, lowercase hex. What makes this a reference rather than a rumour.</summary>
    public required string Sha256 { get; init; }

    public required long Bytes { get; init; }

    /// <summary>
    /// What kind of thing it is - <c>application/x-ndjson</c> for a transcript.
    /// </summary>
    /// <remarks>
    /// Called MediaType rather than ContentType deliberately. The fact-surface
    /// scan refuses any member whose name contains "content", on the grounds
    /// that a member named for content is one that will eventually hold it -
    /// and it was right to flag this one. Renaming is the honest fix;
    /// exempting it would have taught the next person that the scan is
    /// negotiable.
    /// </remarks>
    public required string MediaType { get; init; }

    /// <summary>Who can follow this locator. One of <see cref="ArtifactScopes"/>.</summary>
    public required string Scope { get; init; }
}

/// <summary>How far a locator reaches.</summary>
/// <remarks>
/// Named rather than implied, because the answer today is the narrow one and a
/// consumer must be able to tell without trying. When a Storage port ships,
/// <c>tenant</c> joins this list and the transcripts move; nothing else has to
/// change shape.
/// </remarks>
[VocabularyOf(VocabularyFingerprints.Fact)]
public static class ArtifactScopes
{
    /// <summary>
    /// Resolvable only on the machine that produced it.
    /// </summary>
    /// <remarks>
    /// The declared gap. A cross-team gate cannot follow this, and that is a
    /// capability this platform does not have yet rather than a defect in the
    /// artifact.
    /// </remarks>
    public const string RunnerLocal = "runner-local";

    public static IReadOnlyList<string> All { get; } = [RunnerLocal];
}

/// <summary>
/// What a loop did, as the executor reported it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is measured, not asserted by the agent.</b> Attempts and
/// duration come from the executor's own result; the moves come from the tool
/// calls it actually made. What the agent SAID about its work is prose and
/// lives in the transcript; nothing in this fact is taken from it.
/// </para>
/// <para>
/// That distinction is what makes an injected instruction unable to move a
/// machine-checked obligation: the obligation is computed control-plane-side
/// from facts the runner extracted, and this fact reports the loop rather than
/// deciding anything.
/// </para>
/// </remarks>
[FactKind(FactKinds.LoopOutcome)]
[PinnedId("6c8e2f47-b013-4a95-9d6e-3f18c07b5a92")]
public sealed record LoopOutcome
{
    /// <summary>Which loop, by its id in the envelope.</summary>
    public required string LoopId { get; init; }

    /// <summary>One of <see cref="LoopOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Short, decidable, and the first thing a person reads.
    /// </summary>
    /// <remarks>
    /// Stripped of control sequences before it gets here. It comes from a
    /// process this machine started, and stdout is what somebody pastes into a
    /// ticket.
    /// </remarks>
    public required string Reason { get; init; }

    /// <summary>Which rung ran it.</summary>
    public required string Executor { get; init; }

    /// <summary>How many turns the executor took. Measured, not claimed.</summary>
    public required int Attempts { get; init; }

    /// <summary>Wall clock, in milliseconds.</summary>
    public required long DurationMs { get; init; }

    /// <summary>
    /// Which moves were actually used, from the tool calls it made.
    /// </summary>
    /// <remarks>
    /// Recorded, never enforced. Recording which moves a flight used is what
    /// makes bounding them designable later - a bound nobody has measured is a
    /// bound nobody can set. The executor cannot restrict its own tool list, so
    /// this is an observation rather than a guarantee, and the capability
    /// declaration says so.
    /// </remarks>
    public required IReadOnlyList<string> MovesUsed { get; init; }

    /// <summary>The diagnosis, or null when there is nothing wrong.</summary>
    public static string? Validate(LoopOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (string.IsNullOrWhiteSpace(outcome.LoopId))
        {
            return "A loop outcome names the loop it came from.";
        }

        if (!LoopOutcomes.All.Contains(outcome.Outcome))
        {
            return $"Unknown loop outcome '{outcome.Outcome}'. Expected one of: "
                 + string.Join(", ", LoopOutcomes.All) + ".";
        }

        if (string.IsNullOrWhiteSpace(outcome.Reason))
        {
            // Article XI. An outcome with no reason is the row somebody reads
            // first and learns nothing from.
            return "A loop outcome carries a reason. It is the first thing a person reads.";
        }

        return outcome.Attempts < 0
            ? "A loop cannot have taken a negative number of attempts."
            : outcome.DurationMs < 0 ? "A loop cannot have taken negative time." : null;
    }
}
