using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>Something this executor cannot do, and what that costs.</summary>
/// <remarks>
/// A gap named without a consequence is a footnote. These are read by somebody
/// deciding whether this executor can do their job, so each one says what
/// happens because of it.
/// </remarks>
public sealed record ExecutorGap
{
    public required string Name { get; init; }

    public required string Consequence { get; init; }
}

/// <summary>
/// What an executor can report, what it cannot, and what degrades.
/// </summary>
/// <remarks>
/// <para>
/// <b>One adapter is not an abstraction</b> - learned twice here, on VCS and
/// on identity. The mitigation is not a second adapter; it is declaring
/// capabilities from the first, which is what the provider adapter
/// established.
/// </para>
/// <para>
/// Every value below was measured against the real binary before it was
/// written down. A declaration assembled from what the interface made
/// convenient would describe an executor nobody has run.
/// </para>
/// </remarks>
public sealed record ExecutorCapabilities
{
    /// <summary>Which rung this is, from the envelope's vocabulary.</summary>
    public required string Rung { get; init; }

    /// <summary>Turns taken. Reported.</summary>
    public required bool ReportsAttempts { get; init; }

    /// <summary>Wall clock. Reported.</summary>
    public required bool ReportsDuration { get; init; }

    /// <summary>Which tools it called. Reported.</summary>
    public required bool ReportsMovesUsed { get; init; }

    /// <summary>
    /// Token usage. Reported, which the slice note assumed it would not be.
    /// </summary>
    /// <remarks>
    /// Seeing a number and stopping on one are different decisions. This slice
    /// enforces wall-clock only, because stopping on tokens needs an answer to
    /// what a half-finished attempt means and this slice does not have one.
    /// </remarks>
    public required bool ReportsTokens { get; init; }

    /// <summary>
    /// Whether the envelope's <c>moves</c> bound what it may do.
    /// </summary>
    /// <remarks>
    /// False, and measured: passing the allowed set does not shorten the tool
    /// list the session advertises. So moves are an OBSERVATION here rather
    /// than a guarantee.
    /// </remarks>
    public required bool EnforcesMoves { get; init; }

    /// <summary>
    /// Whether it can say which tool call produced which file change.
    /// </summary>
    /// <remarks>
    /// False, and that turns out to be the right shape: what a flight touched
    /// is read from the TREE rather than from what the agent said it did,
    /// which is the property that keeps an injected instruction out of a
    /// machine-checked verdict.
    /// </remarks>
    public required bool AttributesEditsToTools { get; init; }

    public required IReadOnlyList<ExecutorGap> Gaps { get; init; }
}

/// <summary>What a loop is asked to do.</summary>
public sealed record ExecutorRequest
{
    /// <summary>The materialized tree. The agent works here and nowhere else.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// What a person said when they sent the previous attempt back, if they did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Advice, never authority.</b> It reaches the agent's context and changes nothing
    /// the flight is permitted to do: the moves, the tree, the budget and the scope all
    /// come from the envelope, and this record carries none of them. A reason able to
    /// widen any of it would be unreviewable configuration arriving one sentence at a
    /// time.
    /// </para>
    /// <para>
    /// <b>Null on a first attempt</b>, and null again on the attempt after next: a
    /// rejection is in the record permanently and in the context once.
    /// </para>
    /// </remarks>
    public LeaseFeedback? Feedback { get; init; }

    /// <summary>
    /// Where to append the live view, when one is wanted.
    /// </summary>
    /// <remarks>
    /// Null is the ordinary state: the pane is off by default and a run nobody
    /// is watching writes nothing. Not evidence - ephemeral, local, and it
    /// crosses nothing.
    /// </remarks>
    public LiveStream? Live { get; init; }

    /// <summary>Which loop, by its id in the envelope.</summary>
    public required string LoopId { get; init; }

    /// <summary>
    /// What the flight is for, as an addressable reference.
    /// </summary>
    /// <remarks>
    /// The URI. The body is resolved here, on this machine, with the
    /// customer's own credential - and it never travels back.
    /// </remarks>
    public required string IntentUri { get; init; }

    /// <summary>What the envelope permits. Passed through, and not enforced.</summary>
    public required IReadOnlyList<string> Moves { get; init; }

    /// <summary>The one budget this slice enforces.</summary>
    public required TimeSpan WallClock { get; init; }

    /// <summary>
    /// Where the transcript is written: durable, and OUTSIDE the ephemeral
    /// tree.
    /// </summary>
    /// <remarks>
    /// Outside deliberately. The tree is deleted when the flight ends - that is
    /// the whole point of the observational tier - and a transcript inside it
    /// would be a reference to something that no longer exists by the time
    /// anybody follows it.
    /// </remarks>
    public required string TranscriptPath { get; init; }
}

/// <summary>What a loop did.</summary>
public sealed record ExecutorRun
{
    public required string LoopId { get; init; }

    /// <summary>One of <see cref="LoopOutcomes"/>.</summary>
    public required string Outcome { get; init; }

    /// <summary>Short, decidable, stripped, and the first thing a person reads.</summary>
    public required string Reason { get; init; }

    public required int Attempts { get; init; }

    public required long DurationMs { get; init; }

    /// <summary>
    /// What the agent REACHED FOR, distinct and ordered.
    /// </summary>
    /// <remarks>
    /// Attempts rather than grants. A real run reaches for tools the envelope
    /// did not name and is refused, and recording the attempt is the more
    /// useful signal: it says what the loop wanted, which is what a bound
    /// would have to be set against. Ordered so two identical flights digest
    /// identically.
    /// </remarks>
    public required IReadOnlyList<string> MovesUsed { get; init; }

    /// <summary>Where the transcript is, when one was written.</summary>
    public ArtifactReference? Transcript { get; init; }

    /// <summary>
    /// What the stream said, extracted so it can cross without the transcript.
    /// </summary>
    /// <remarks>
    /// The transcript is a machine-local reference and does not cross, so this
    /// is what a person gets instead. Extracted mechanically from the same
    /// stream the transcript holds - never summarised, because a summary would
    /// be a claim rather than a fact and would carry whatever the transcript
    /// told it to.
    /// </remarks>
    public LoopDigest? Digest { get; init; }

    public static ExecutorRun Completed(
        string loopId, string reason, int attempts, TimeSpan took, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Completed,
            Reason = Clean(reason),
            Attempts = attempts,
            DurationMs = (long)took.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    public static ExecutorRun Failed(
        string loopId, string reason, int attempts, TimeSpan took, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Failed,
            Reason = Clean(reason),
            Attempts = attempts,
            DurationMs = (long)took.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    /// <summary>
    /// Out of budget, and waiting for a person.
    /// </summary>
    /// <remarks>
    /// A real state rather than an error, and the reason says so in those
    /// words because that is what a console queue shows somebody.
    /// <c>on-exhaustion: handoff-to-human</c> has nowhere to hand off to until
    /// <c>gg take</c> exists, so this is where a flight rests until one does.
    /// </remarks>
    public static ExecutorRun Exhausted(
        string loopId, TimeSpan after, IReadOnlyList<string> movesUsed) =>
        new()
        {
            LoopId = loopId,
            Outcome = LoopOutcomes.Exhausted,
            Reason = $"The loop used its whole wall-clock budget of {Describe(after)} and stopped. "
                   + "This flight is waiting for a person.",
            Attempts = 0,
            DurationMs = (long)after.TotalMilliseconds,
            MovesUsed = Distinct(movesUsed),
        };

    /// <summary>The run, as the fact that crosses.</summary>
    public LoopOutcome ToFact(string executor) => new()
    {
        LoopId = LoopId,
        Outcome = Outcome,
        Reason = Reason,
        Executor = executor,
        Attempts = Attempts,
        DurationMs = DurationMs,
        MovesUsed = MovesUsed,
    };

    private static string Describe(TimeSpan span) =>
        span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes}m"
            : $"{(int)span.TotalSeconds}s";

    /// <summary>The most a reason may be, before it stops being one.</summary>
    /// <remarks>
    /// This value is carried INLINE - it is the row somebody reads first, and
    /// the whole point of that disposition is that it is short and decidable.
    /// A real agent's closing summary runs to paragraphs with code blocks in
    /// it; that is the transcript's job, and putting it here would make the
    /// cheapest thing to read the most expensive.
    /// </remarks>
    public const int MaxReasonLength = 280;

    /// <summary>
    /// Stripped at INGRESS, and cut to something a person reads first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stripped because it came from a process this machine started, and stdout
    /// is what a customer pastes into a ticket. Stripping is not deleting: what
    /// somebody wrote survives and the escape does not.
    /// </para>
    /// <para>
    /// Cut to the first paragraph, because a real run ends with several. The
    /// rest is in the transcript, and the cut is marked rather than silent -
    /// somebody has to be able to tell there was more.
    /// </para>
    /// </remarks>
    private static string Clean(string reason)
    {
        var stripped = (ControlText.Strip(reason, allowLineBreaks: true) ?? "").Trim();

        var firstBreak = stripped.IndexOf("\n\n", StringComparison.Ordinal);
        var head = (firstBreak > 0 ? stripped[..firstBreak] : stripped)
            .ReplaceLineEndings(" ")
            .Trim();

        return head.Length <= MaxReasonLength
            ? head
            : head[..MaxReasonLength].TrimEnd() + "… (the rest is in the transcript)";
    }

    private static IReadOnlyList<string> Distinct(IReadOnlyList<string> moves) =>
        [.. moves.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}

/// <summary>
/// The runner's missing verb.
/// </summary>
/// <remarks>
/// Slice one built <c>lease → resolve → materialize → ~~invoke~~ → extract →
/// digest → filter → emit</c> and omitted exactly one. This is it.
/// </remarks>
public interface IExecutorPort
{
    /// <summary>What this executor can and cannot do.</summary>
    ExecutorCapabilities Capabilities { get; }

    /// <summary>Runs the loop, and never throws for a loop that simply failed.</summary>
    Task<ExecutorRun> ExecuteAsync(ExecutorRequest request, CancellationToken cancellationToken);
}
